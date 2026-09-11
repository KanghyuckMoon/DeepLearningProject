using DeepLearning.GameData;
using Unity.InferenceEngine;
using UnityEngine;

namespace DeepLearning.GameClient
{
    public sealed class OnnxImageClassifierMono : MonoBehaviour, IImageClassifier
    {
        [SerializeField] private DataBaseSO itemDatabase;
        [SerializeField] private BackendType backendType = BackendType.GPUCompute;
        [SerializeField, Min(1)] private int inputWidth = 128;
        [SerializeField, Min(1)] private int inputHeight = 128;

        private ModelAsset _activeModelAsset;
        private Worker _worker;
        private Tensor<float> _inputTensor;

        private void OnDestroy()
        {
            ReleaseResources();
        }

        public bool TryClassify(
            Texture source,
            int targetItemIndex,
            out ClassificationResult result)
        {
            result = default;

            if (source == null || itemDatabase == null ||
                !itemDatabase.TryGetAIModel(targetItemIndex, out ModelAsset modelAsset))
            {
                return false;
            }

            if (!EnsureWorker(modelAsset))
            {
                return false;
            }

            try
            {
                TextureConverter.ToTensor(source, _inputTensor, new TextureTransform());
                _worker.Schedule(_inputTensor);

                Tensor<float> output = _worker.PeekOutput() as Tensor<float>;

                if (output == null)
                {
                    return false;
                }

                using Tensor<float> cpuOutput = output.ReadbackAndClone();
                float[] probabilities = cpuOutput.DownloadToArray();
                if (probabilities == null || probabilities.Length == 0)
                {
                    return false;
                }

                int predictedIndex;
                float confidence;

                if (probabilities.Length == 1)
                {
                    predictedIndex = targetItemIndex;
                    confidence = probabilities[0];
                }
                else
                {
                    predictedIndex = 0;
                    confidence = probabilities[0];

                    for (int index = 1; index < probabilities.Length; index++)
                    {
                        if (probabilities[index] > confidence)
                        {
                            predictedIndex = index;
                            confidence = probabilities[index];
                        }
                    }
                }

                string label = itemDatabase.TryGetItem(predictedIndex, out ItemData item)
                    ? item.Name
                    : $"Class {predictedIndex}";

                result = new ClassificationResult(predictedIndex, label, confidence);
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"ONNX 이미지 추론 실패: {exception.Message}", this);
                return false;
            }
        }

        private bool EnsureWorker(ModelAsset modelAsset)
        {
            if (_worker != null && _activeModelAsset == modelAsset)
            {
                return true;
            }

            ReleaseResources();

            try
            {
                Model runtimeModel = ModelLoader.Load(modelAsset);
                _worker = new Worker(runtimeModel, backendType);
                _inputTensor = new Tensor<float>(new TensorShape(1, 3, inputHeight, inputWidth));
                _activeModelAsset = modelAsset;
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"ONNX 모델 초기화 실패: {exception.Message}", this);
                ReleaseResources();
                return false;
            }
        }

        private void ReleaseResources()
        {
            _worker?.Dispose();
            _inputTensor?.Dispose();
            _worker = null;
            _inputTensor = null;
            _activeModelAsset = null;
        }
    }
}
