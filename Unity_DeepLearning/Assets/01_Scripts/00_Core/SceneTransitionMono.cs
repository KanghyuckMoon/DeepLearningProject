using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeepLearning.SceneFlow
{
    /// <summary>
    /// 씬이 바뀌어도 유지되는 전체 화면 페이드 전환기입니다.
    /// </summary>
    public sealed class SceneTransitionMono : MonoBehaviour
    {
        private const float DefaultFadeDuration = 0.65f;

        private static SceneTransitionMono _instance;

        private CanvasGroup _fadeGroup;
        private RectTransform _topCurtain;
        private RectTransform _bottomCurtain;
        private RectTransform _leftCurtain;
        private RectTransform _rightCurtain;
        private float _curtainProgress;
        private Coroutine _transitionRoutine;

        public static bool IsTransitioning => _instance != null && _instance._transitionRoutine != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateBeforeFirstScene()
        {
            EnsureInstance();
        }

        public static void LoadScene(string sceneName, float fadeDuration = DefaultFadeDuration)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("전환할 씬 이름이 비어 있습니다.");
                return;
            }

            EnsureInstance().BeginTransition(sceneName, fadeDuration);
        }

        public static void Reveal(float fadeDuration = DefaultFadeDuration)
        {
            SceneTransitionMono instance = EnsureInstance();
            if (instance._transitionRoutine == null)
            {
                instance._transitionRoutine = instance.StartCoroutine(instance.RevealRoutine(fadeDuration));
            }
        }

        public static void SetBlackImmediately()
        {
            SceneTransitionMono instance = EnsureInstance();
            instance.SetCurtainProgress(1f);
            instance._fadeGroup.blocksRaycasts = true;
        }

        private static SceneTransitionMono EnsureInstance()
        {
            if (_instance != null)
            {
                return _instance;
            }

            var transitionObject = new GameObject("SceneTransition");
            DontDestroyOnLoad(transitionObject);
            _instance = transitionObject.AddComponent<SceneTransitionMono>();
            _instance.BuildOverlay();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            BuildOverlay();
        }

        private void BuildOverlay()
        {
            if (_fadeGroup != null)
            {
                return;
            }

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            gameObject.AddComponent<GraphicRaycaster>();
            _fadeGroup = gameObject.AddComponent<CanvasGroup>();
            _fadeGroup.alpha = 1f;
            _fadeGroup.blocksRaycasts = false;
            _fadeGroup.interactable = false;

            RectTransform inputBlocker = CreateCurtain("InputBlocker");
            SetAnchors(inputBlocker, Vector2.zero, Vector2.one);
            Image blockerImage = inputBlocker.GetComponent<Image>();
            blockerImage.color = Color.clear;
            blockerImage.raycastTarget = true;

            _topCurtain = CreateCurtain("TopCurtain");
            _bottomCurtain = CreateCurtain("BottomCurtain");
            _leftCurtain = CreateCurtain("LeftCurtain");
            _rightCurtain = CreateCurtain("RightCurtain");
            SetCurtainProgress(0f);
        }

        private RectTransform CreateCurtain(string objectName)
        {
            var curtainObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = curtainObject.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = curtainObject.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
            return rect;
        }

        private void BeginTransition(string sceneName, float fadeDuration)
        {
            if (_transitionRoutine != null)
            {
                return;
            }

            _transitionRoutine = StartCoroutine(TransitionRoutine(sceneName, Mathf.Max(0.01f, fadeDuration)));
        }

        private IEnumerator TransitionRoutine(string sceneName, float fadeDuration)
        {
            _fadeGroup.blocksRaycasts = true;
            yield return FadeTo(1f, fadeDuration);

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (loadOperation == null)
            {
                Debug.LogError($"씬 '{sceneName}'을 불러올 수 없습니다. Build Settings를 확인해 주세요.");
                yield return FadeTo(0f, fadeDuration);
                _fadeGroup.blocksRaycasts = false;
                _transitionRoutine = null;
                yield break;
            }

            while (!loadOperation.isDone)
            {
                yield return null;
            }

            // 새 씬의 Awake/Start 및 첫 레이아웃 계산이 끝난 다음 화면을 보여줍니다.
            yield return null;
            yield return new WaitForEndOfFrame();
            yield return FadeTo(0f, fadeDuration);

            _fadeGroup.blocksRaycasts = false;
            _transitionRoutine = null;
        }

        private IEnumerator RevealRoutine(float fadeDuration)
        {
            _fadeGroup.blocksRaycasts = true;
            yield return FadeTo(0f, Mathf.Max(0.01f, fadeDuration));
            _fadeGroup.blocksRaycasts = false;
            _transitionRoutine = null;
        }

        private IEnumerator FadeTo(float targetAlpha, float duration)
        {
            float startProgress = _curtainProgress;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = progress * progress * (3f - (2f * progress));
                SetCurtainProgress(Mathf.LerpUnclamped(startProgress, targetAlpha, eased));
                yield return null;
            }

            SetCurtainProgress(targetAlpha);
        }

        private void SetCurtainProgress(float progress)
        {
            _curtainProgress = Mathf.Clamp01(progress);
            float half = _curtainProgress * 0.5f;

            // 위/아래 면이 먼저 전체 폭을 채우고, 좌/우 면은 남은 중앙 영역을 향해 닫힙니다.
            SetAnchors(_topCurtain, new Vector2(0f, 1f - half), Vector2.one);
            SetAnchors(_bottomCurtain, Vector2.zero, new Vector2(1f, half));
            SetAnchors(_leftCurtain, new Vector2(0f, half), new Vector2(half, 1f - half));
            SetAnchors(_rightCurtain, new Vector2(1f - half, half), new Vector2(1f, 1f - half));
        }

        private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
