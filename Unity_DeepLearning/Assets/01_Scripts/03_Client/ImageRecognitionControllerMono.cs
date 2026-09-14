using DeepLearning.GameData;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeepLearning.GameClient
{
    public sealed class ImageRecognitionControllerMono : MonoBehaviour
    {
        private const float DisplayedFullConfidenceThreshold = 0.995f;

        [SerializeField] private GameClientMono gameClient;
        [SerializeField] private WebCamFeedMono cameraFeed;
        [SerializeField] private DataBaseSO itemDatabase;
        [Tooltip("IImageClassifier를 구현한 MonoBehaviour를 지정하세요.")]
        [SerializeField] private MonoBehaviour classifierSource;

        [Header("CNN Rule")]
        [Tooltip("목표 물건의 인식률 100%가 유지되어야 하는 시간입니다.")]
        [SerializeField, Min(0.1f)] private float fullConfidenceHoldSeconds = 0.5f;
        [SerializeField, Min(0f)] private float classificationInterval;
        [Tooltip("새 라운드가 시작된 뒤 AI 인식을 다시 시작하기까지 기다리는 시간입니다.")]
        [SerializeField, Min(0f)] private float recognitionResumeDelaySeconds = 0.5f;

        [Header("Test")]
        [SerializeField] private bool enableKeyboardTest = true;
        [SerializeField] private Key detectionKey = Key.Digit1;

        private IImageClassifier _classifier;
        private int _correctCount;
        private float _fullConfidenceStartedAt = -1f;
        private float _holdProgress;
        private float _nextClassificationTime;
        private float _resumeRecognitionAt = -1f;
        private bool _recognitionPaused;
        private ClassificationResult _lastResult;

        public int CorrectCount => _correctCount;
        public float HoldProgress => _holdProgress;
        public bool IsRecognitionPaused => _recognitionPaused;
        public ClassificationResult LastResult => _lastResult;
        public float TargetConfidence
        {
            get
            {
                int answerIndex = gameClient != null ? gameClient.Snapshot.AnswerIndex : -1;
                return answerIndex >= 0 && _lastResult.ClassIndex == answerIndex
                    ? Mathf.Clamp01(_lastResult.Confidence)
                    : 0f;
            }
        }
        public string TargetLabel
        {
            get
            {
                int answerIndex = gameClient != null ? gameClient.Snapshot.AnswerIndex : -1;
                return itemDatabase != null && itemDatabase.TryGetItem(answerIndex, out ItemData item)
                    ? item.Name
                    : "";
            }
        }

        private void Awake()
        {
            _classifier = classifierSource as IImageClassifier;
            if (_classifier == null)
            {
                _classifier = GetComponent<IImageClassifier>();
            }

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
            if (_recognitionPaused)
            {
                if (_resumeRecognitionAt < 0f || Time.unscaledTime < _resumeRecognitionAt)
                {
                    return;
                }

                _recognitionPaused = false;
                _resumeRecognitionAt = -1f;
                _nextClassificationTime = Time.unscaledTime;
            }

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

            int answerIndex = gameClient != null ? gameClient.Snapshot.AnswerIndex : -1;
            if (answerIndex < 0 ||
                !_classifier.TryClassify(cameraFeed.Texture, answerIndex, out ClassificationResult prediction))
            {
                ResetRecognition();
                return;
            }

            bool matchesTarget = prediction.ClassIndex == answerIndex;
            _lastResult = matchesTarget
                ? prediction
                : new ClassificationResult(prediction.ClassIndex, prediction.Label, 0f);

            bool displaysOneHundredPercent =
                matchesTarget && prediction.Confidence >= DisplayedFullConfidenceThreshold;
            if (!displaysOneHundredPercent)
            {
                _correctCount = 0;
                _fullConfidenceStartedAt = -1f;
                _holdProgress = 0f;
                return;
            }

            _correctCount++;
            if (_fullConfidenceStartedAt < 0f)
            {
                _fullConfidenceStartedAt = Time.unscaledTime;
            }

            float holdDuration = Mathf.Max(0.1f, fullConfidenceHoldSeconds);
            _holdProgress = Mathf.Clamp01(
                (Time.unscaledTime - _fullConfidenceStartedAt) / holdDuration);

            if (_holdProgress >= 1f)
            {
                SubmitDetection();
                _correctCount = 0;
                _fullConfidenceStartedAt = -1f;
                _holdProgress = 0f;
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
            switch (clientEvent.Type)
            {
                case GameClientEventType.Connected:
                case GameClientEventType.RoundStarted:
                    PauseRecognition(recognitionResumeDelaySeconds);
                    break;

                case GameClientEventType.RoundResult:
                case GameClientEventType.GameOver:
                case GameClientEventType.Disconnected:
                    PauseRecognition(-1f);
                    break;
            }
        }

        private void PauseRecognition(float resumeDelaySeconds)
        {
            ResetRecognition();
            _recognitionPaused = true;
            _resumeRecognitionAt = resumeDelaySeconds >= 0f
                ? Time.unscaledTime + resumeDelaySeconds
                : -1f;
        }

        private void ResetRecognition()
        {
            _correctCount = 0;
            _fullConfidenceStartedAt = -1f;
            _holdProgress = 0f;
            _lastResult = default;
        }
    }
}
