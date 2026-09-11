using UnityEngine;

namespace DeepLearning.GameClient
{
    public readonly struct ClassificationResult
    {
        public ClassificationResult(int classIndex, string label, float confidence)
        {
            ClassIndex = classIndex;
            Label = label;
            Confidence = confidence;
        }

        public int ClassIndex { get; }
        public string Label { get; }
        public float Confidence { get; }
    }

    public interface IImageClassifier
    {
        bool TryClassify(Texture source, out ClassificationResult result);
    }
}
