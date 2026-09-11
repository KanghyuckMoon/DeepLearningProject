using UnityEngine;
using UnityEngine.InputSystem;

namespace DeepLearning.GameClient
{
    public sealed class ImageRecognitionController : MonoBehaviour
    {
        [SerializeField] private GameClient gameClient;
        [SerializeField] private WebCamFeed cameraFeed;
        [Tooltip("IImageClassifier를 구현한 MonoBehaviour를 지정하세요.")]
        [SerializeField] private MonoBehaviour classifierSource;

        [Header("CNN Rule")]
        [SerializeField] private string[] classNames = { "nipper", "pen", "wire stripper" };
        [SerializeField, Range(0f, 1f)] private float confidenceThreshold = 0.95f;
        [SerializeField, Min(1)] private int requiredConsecutiveFrames = 20;
        [SerializeField, Min(0f)] private float classificationInterval;

        [Header("Test")]
        [SerializeField] private bool enableKeyboardTest = true;
        [SerializeField] private Key detectionKey = Key.Digit1;

        private IImageClassifier _classifier;
        private int _correctCount;
        private float _nextClassificationTime;
        private ClassificationResult _lastResult;

        public int CorrectCount => _correctCount;
        public int RequiredCount => requiredConsecutiveFrames;
        public ClassificationResult LastResult => _lastResult;
        public string TargetLabel
        {
            get
            {
                int answerIndex = gameClient != null ? gameClient.Snapshot.AnswerIndex : -1;
                return answerIndex >= 0 && answerIndex < classNames.Length ? classNames[answerIndex] : "";
            }
        }

        private void Awake()
        {
            _classifier = classifierSource as IImageClassifier;
            if (classifierSource != null && _classifier == null)
            {
                Debug.LogError("Classifier Source가 IImageClassifier를 구현하지 않았습니다.", this);
            }
        }

        private void OnEnable()
        {
            if (gameClient != null)
            {
                gameClient.ClientEvent += HandleClientEvent;
            }
        }

        private void OnDisable()
        {
            if (gameClient != null)
            {
                gameClient.ClientEvent -= HandleClientEvent;
            }
        }

        private void Update()
        {
            if (enableKeyboardTest &&
                Keyboard.current != null &&
                Keyboard.current[detectionKey].wasPressedThisFrame)
            {
                SubmitDetection();
            }

            if (_classifier == null || cameraFeed == null || !cameraFeed.HasValidFrame ||
                Time.unscaledTime < _nextClassificationTime)
            {
                return;
            }

            _nextClassificationTime = Time.unscaledTime + classificationInterval;

            if (!_classifier.TryClassify(cameraFeed.Texture, out _lastResult))
            {
                _correctCount = 0;
                return;
            }

            int answerIndex = gameClient != null ? gameClient.Snapshot.AnswerIndex : -1;
            bool correct = answerIndex >= 0 &&
                           _lastResult.ClassIndex == answerIndex &&
                           _lastResult.Confidence >= confidenceThreshold;

            _correctCount = correct ? _correctCount + 1 : 0;

            if (_correctCount >= requiredConsecutiveFrames)
            {
                SubmitDetection();
                _correctCount = 0;
            }
        }

        public void SubmitDetection()
        {
            if (gameClient != null)
            {
                gameClient.SendDetection();
            }
        }

        private void HandleClientEvent(GameClientEvent clientEvent)
        {
            if (clientEvent.Type == GameClientEventType.RoundStarted ||
                clientEvent.Type == GameClientEventType.GameOver ||
                clientEvent.Type == GameClientEventType.Disconnected)
            {
                _correctCount = 0;
            }
        }
    }
}
