using UnityEngine;

namespace DeepLearning.GameClient
{
    /// <summary>
    /// ONNX 추론기를 연결하기 전, Inspector 값으로 연속 인식 흐름을 시험하기 위한 분류기입니다.
    /// </summary>
    public sealed class InspectorImageClassifierMono : MonoBehaviour, IImageClassifier
    {
        [SerializeField] private bool emitPrediction;
        [SerializeField, Min(0)] private int classIndex;
        [SerializeField] private string label = "nipper";
        [SerializeField, Range(0f, 1f)] private float confidence = 1f;

        public bool TryClassify(
            Texture source,
            int targetItemIndex,
            out ClassificationResult result)
        {
            result = new ClassificationResult(classIndex, label, confidence);
            return emitPrediction && source != null;
        }
    }
}
