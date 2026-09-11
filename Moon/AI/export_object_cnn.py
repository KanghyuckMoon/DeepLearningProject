from __future__ import annotations

import argparse
import json
import shutil
from pathlib import Path

import numpy as np
import onnx
import onnxruntime as ort
import torch
from torch import nn


PROJECT_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_CHECKPOINT = PROJECT_ROOT / "JUHA" / "models" / "object_cnn.pth"
DEFAULT_OUTPUT = Path(__file__).resolve().parent / "models" / "object_cnn.onnx"
DEFAULT_UNITY_DIRECTORY = (
    PROJECT_ROOT / "Unity_DeepLearning" / "Assets" / "03_AI" / "Models"
)


class ObjectCnn(nn.Module):
    def __init__(self, number_of_classes: int) -> None:
        super().__init__()
        self.features = nn.Sequential(
            nn.Conv2d(3, 16, 3, padding=1),
            nn.ReLU(),
            nn.MaxPool2d(2),
            nn.Conv2d(16, 32, 3, padding=1),
            nn.ReLU(),
            nn.MaxPool2d(2),
            nn.Conv2d(32, 64, 3, padding=1),
            nn.ReLU(),
            nn.MaxPool2d(2),
        )
        self.classifier = nn.Sequential(
            nn.Flatten(),
            nn.Linear(64 * 16 * 16, 128),
            nn.ReLU(),
            nn.Linear(128, number_of_classes),
        )

    def forward(self, value: torch.Tensor) -> torch.Tensor:
        return self.classifier(self.features(value))


class ObjectCnnWithProbabilities(nn.Module):
    """Python 게임과 동일하게 softmax 확률을 반환하는 배포 모델."""

    def __init__(self, model: ObjectCnn) -> None:
        super().__init__()
        self.model = model

    def forward(self, value: torch.Tensor) -> torch.Tensor:
        return torch.softmax(self.model(value), dim=1)


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="PyTorch 물체 분류 모델을 ONNX로 변환합니다.")
    parser.add_argument("--checkpoint", type=Path, default=DEFAULT_CHECKPOINT)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--unity-directory", type=Path, default=DEFAULT_UNITY_DIRECTORY)
    parser.add_argument("--opset", type=int, default=17)
    return parser.parse_args()


def load_model(checkpoint_path: Path) -> tuple[ObjectCnnWithProbabilities, list[str]]:
    checkpoint = torch.load(checkpoint_path, map_location="cpu", weights_only=True)
    classes = list(checkpoint["classes"])

    model = ObjectCnn(len(classes))
    model.load_state_dict(checkpoint["model_state"], strict=True)
    model.eval()

    deployment_model = ObjectCnnWithProbabilities(model)
    deployment_model.eval()
    return deployment_model, classes


def export_model(
    model: ObjectCnnWithProbabilities,
    output_path: Path,
    opset: int,
) -> torch.Tensor:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    torch.manual_seed(20260911)
    sample_input = torch.rand(1, 3, 128, 128, dtype=torch.float32)

    torch.onnx.export(
        model,
        sample_input,
        output_path,
        export_params=True,
        opset_version=opset,
        do_constant_folding=True,
        input_names=["input"],
        output_names=["probabilities"],
        dynamo=False,
    )
    return sample_input


def add_model_metadata(onnx_path: Path, classes: list[str], checkpoint_path: Path) -> None:
    model = onnx.load(onnx_path)

    # Unity Inference Engine 2.6은 MaxPool의 기본값과 같은 ceil_mode 속성도
    # 경고로 표시한다. 값이 0인 속성만 제거해 그래프 의미는 유지한다.
    for node in model.graph.node:
        if node.op_type != "MaxPool":
            continue

        for attribute in list(node.attribute):
            if attribute.name == "ceil_mode" and attribute.i == 0:
                node.attribute.remove(attribute)

    metadata = {
        "classes": json.dumps(classes, ensure_ascii=False),
        "input_layout": "NCHW",
        "input_color": "RGB",
        "input_range": "0..1",
        "source_checkpoint": checkpoint_path.name,
    }

    del model.metadata_props[:]
    for key, value in metadata.items():
        property_entry = model.metadata_props.add()
        property_entry.key = key
        property_entry.value = value

    onnx.save(model, onnx_path)


def validate_model(
    model: ObjectCnnWithProbabilities,
    onnx_path: Path,
    sample_input: torch.Tensor,
    number_of_classes: int,
) -> float:
    onnx_model = onnx.load(onnx_path)
    onnx.checker.check_model(onnx_model, full_check=True)

    input_shape = [dimension.dim_value for dimension in onnx_model.graph.input[0].type.tensor_type.shape.dim]
    output_shape = [dimension.dim_value for dimension in onnx_model.graph.output[0].type.tensor_type.shape.dim]
    if input_shape != [1, 3, 128, 128]:
        raise RuntimeError(f"잘못된 ONNX 입력 크기: {input_shape}")
    if output_shape != [1, number_of_classes]:
        raise RuntimeError(f"잘못된 ONNX 출력 크기: {output_shape}")

    with torch.no_grad():
        expected = model(sample_input).cpu().numpy()

    session = ort.InferenceSession(str(onnx_path), providers=["CPUExecutionProvider"])
    actual = session.run(["probabilities"], {"input": sample_input.cpu().numpy()})[0]

    if not np.allclose(actual.sum(axis=1), 1.0, rtol=1e-5, atol=1e-6):
        raise RuntimeError("ONNX 출력 확률의 합이 1이 아닙니다.")

    maximum_error = float(np.max(np.abs(expected - actual)))
    if maximum_error > 1e-5:
        raise RuntimeError(f"PyTorch/ONNX 출력 차이가 너무 큽니다: {maximum_error}")

    return maximum_error


def write_classes(classes: list[str], destination: Path) -> Path:
    classes_path = destination.with_name("object_cnn.classes.json")
    classes_path.write_text(
        json.dumps({"classes": classes}, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    return classes_path


def copy_to_unity(onnx_path: Path, classes_path: Path, unity_directory: Path) -> None:
    unity_directory.mkdir(parents=True, exist_ok=True)
    shutil.copy2(onnx_path, unity_directory / onnx_path.name)
    shutil.copy2(classes_path, unity_directory / classes_path.name)


def main() -> None:
    arguments = parse_arguments()
    checkpoint_path = arguments.checkpoint.resolve()
    output_path = arguments.output.resolve()
    unity_directory = arguments.unity_directory.resolve()

    model, classes = load_model(checkpoint_path)
    sample_input = export_model(model, output_path, arguments.opset)
    add_model_metadata(output_path, classes, checkpoint_path)
    maximum_error = validate_model(model, output_path, sample_input, len(classes))
    classes_path = write_classes(classes, output_path)
    copy_to_unity(output_path, classes_path, unity_directory)

    print(f"Checkpoint : {checkpoint_path}")
    print(f"ONNX       : {output_path}")
    print(f"Unity      : {unity_directory / output_path.name}")
    print(f"Classes    : {classes}")
    print("Input      : input [1, 3, 128, 128] float32 (RGB, 0..1)")
    print(f"Output     : probabilities [1, {len(classes)}] float32")
    print(f"Max error  : {maximum_error:.10f}")


if __name__ == "__main__":
    main()
