using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeepLearning.GameClient
{
    /// <summary>
    /// Builds the portrait casual-game HUD around the existing camera and gameplay bindings.
    /// The view is available in Edit Mode while keeping the original gameplay references intact.
    /// </summary>
    [ExecuteAlways]
    public sealed class InGameHudViewMono : MonoBehaviour
    {
        public static readonly Color DarkColor = new Color32(55, 65, 70, 255);
        public static readonly Color TealColor = new Color32(29, 174, 157, 255);
        public static readonly Color SuccessColor = new Color32(104, 225, 190, 255);
        public static readonly Color DetectingColor = new Color32(255, 225, 139, 255);

        private static Sprite _roundedSprite;
        private Canvas _canvas;
        private RectTransform _safeRoot;
        private RectTransform _remotePlayerContainer;
        private TMP_Text _roundText;
        private TMP_Text _targetText;
        private TMP_Text _confidenceText;
        private TMP_Text _scoreText;
        private TMP_Text _statusText;
        private Image _targetPreview;
        private Image _confidenceFill;
        private Image _mainCameraRing;
        private TMP_Text _mainCheckmark;
        private RectTransform _mainCameraMask;
        private bool _initialized;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        [SerializeField, HideInInspector] private TMP_Text legacyGameInfo;
        [SerializeField, HideInInspector] private TMP_Text legacyCenter;
        [SerializeField, HideInInspector] private TMP_Text legacyAiInfo;
        [SerializeField, HideInInspector] private TMP_Text legacyTargetName;
        [SerializeField, HideInInspector] private Image legacyTargetImage;
        [SerializeField] private bool showEditModePreview = true;

        public RectTransform RemotePlayerContainer => _remotePlayerContainer;

        private void OnEnable()
        {
            if (!Application.isPlaying && showEditModePreview && legacyGameInfo != null)
            {
                Initialize(
                    legacyGameInfo,
                    legacyCenter,
                    legacyAiInfo,
                    legacyTargetName,
                    legacyTargetImage);
            }
        }

        private void Update()
        {
            ApplySafeArea();
        }

        public void Initialize(
            TMP_Text legacyGameInfo,
            TMP_Text legacyCenter,
            TMP_Text legacyAiInfo,
            TMP_Text legacyTargetName,
            Image legacyTargetImage)
        {
            if (legacyGameInfo != null) this.legacyGameInfo = legacyGameInfo;
            if (legacyCenter != null) this.legacyCenter = legacyCenter;
            if (legacyAiInfo != null) this.legacyAiInfo = legacyAiInfo;
            if (legacyTargetName != null) this.legacyTargetName = legacyTargetName;
            if (legacyTargetImage != null) this.legacyTargetImage = legacyTargetImage;

            if (_initialized)
            {
                return;
            }

            _canvas = legacyGameInfo != null ? legacyGameInfo.canvas : FindFirstObjectByType<Canvas>();
            if (_canvas == null)
            {
                Debug.LogWarning("HUD를 구성할 Canvas를 찾지 못했습니다.", this);
                return;
            }

            _initialized = true;
            ConfigureCanvas();
            HideLegacy(legacyGameInfo, legacyCenter, legacyAiInfo, legacyTargetName, legacyTargetImage);

            TMP_FontAsset font = legacyGameInfo != null ? legacyGameInfo.font : null;
            if (!TryBindExistingHud())
            {
                BuildHud(font);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
                }
#endif
            }

            ApplySafeArea();
        }

        public void RefreshSnapshot(GameClientSnapshot snapshot, Sprite targetSprite, string targetName)
        {
            if (!_initialized || snapshot == null)
            {
                return;
            }

            _roundText.text = $"ROUND {Mathf.Max(1, snapshot.Round):00}";
            int score = snapshot.Scores != null &&
                        snapshot.Scores.TryGetValue(snapshot.PlayerId, out int value)
                ? value
                : 0;
            _scoreText.text = $"<size=25>내 점수</size>\n<b>{score}</b>";
            _targetText.text = string.IsNullOrWhiteSpace(targetName) ? "찾아야 할 물건" : targetName;
            _targetPreview.sprite = targetSprite;
            _targetPreview.enabled = targetSprite != null;
        }

        public void RefreshRecognition(
            ImageRecognitionControllerMono recognition,
            GameClientSnapshot snapshot)
        {
            if (!_initialized)
            {
                return;
            }

            float confidence = recognition != null
                ? recognition.TargetConfidence
                : 0f;
            _confidenceFill.fillAmount = confidence;
            float holdProgress = recognition != null ? recognition.HoldProgress : 0f;
            _confidenceFill.color = Color.Lerp(TealColor, DetectingColor, holdProgress);
            _confidenceText.text = $"<size=25>인식 정확도</size>\n<b>{confidence * 100f:0}%</b>";

            bool succeeded = snapshot != null && snapshot.RoundWinner == snapshot.PlayerId;
            bool detecting = !succeeded && holdProgress > 0f;

            if (succeeded)
            {
                SetCameraState(SuccessColor, true);
            }
            else if (detecting)
            {
                SetCameraState(DetectingColor, false);
            }
            else
            {
                SetCameraState(Color.white, false);
            }
        }

        private void SetCameraState(Color ringColor, bool showCheckmark)
        {
            _mainCameraRing.color = ringColor;
            _mainCheckmark.gameObject.SetActive(showCheckmark);
        }

        private void ConfigureCanvas()
        {
            CanvasScaler scaler = _canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
        }

        private static void HideLegacy(
            TMP_Text gameInfo,
            TMP_Text center,
            TMP_Text aiInfo,
            TMP_Text targetName,
            Image targetImage)
        {
            if (gameInfo != null) gameInfo.gameObject.SetActive(false);
            if (center != null) center.gameObject.SetActive(false);
            if (aiInfo != null) aiInfo.gameObject.SetActive(false);
            if (targetName != null) targetName.gameObject.SetActive(false);
            if (targetImage != null && targetImage.transform.parent != null)
            {
                targetImage.transform.parent.gameObject.SetActive(false);
            }
        }

        private void BuildHud(TMP_FontAsset font)
        {
            GameObject rootObject = CreateUiObject("CasualHUD", _canvas.transform);
            RectTransform root = (RectTransform)rootObject.transform;
            Stretch(root);

            root.SetAsLastSibling();

            GameObject safeObject = CreateUiObject("SafeArea", root);
            _safeRoot = (RectTransform)safeObject.transform;
            Stretch(_safeRoot);

            _roundText = CreateText("Round", _safeRoot, font, 38f, FontStyles.Bold);
            SetRect(_roundText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -68f), new Vector2(310f, 76f));
            AddCard(_roundText.transform, new Color32(255, 255, 255, 238), 14f);
            _roundText.text = "ROUND 01";
            _roundText.characterSpacing = 4f;

            BuildMainCamera(font);
            BuildTargetCard(font);
            BuildStats(font);

            _statusText = CreateText("StatusMessage", _safeRoot, font, 38f, FontStyles.Bold);
            SetRect(_statusText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -1285f), new Vector2(920f, 88f));
            _statusText.text = "물건을 찾아라!";

            BuildRemotePlayers(font);
        }

        private bool TryBindExistingHud()
        {
            Transform existingRoot = FindDeepChild(_canvas.transform, "CasualHUD");
            if (existingRoot == null)
            {
                return false;
            }

            _safeRoot = FindDeepChild(existingRoot, "SafeArea") as RectTransform;
            _roundText = GetComponentFromChild<TMP_Text>(existingRoot, "Round");
            _targetText = GetComponentFromChild<TMP_Text>(existingRoot, "TargetName");
            _confidenceText = GetComponentFromChild<TMP_Text>(existingRoot, "Confidence");
            _scoreText = GetComponentFromChild<TMP_Text>(existingRoot, "MyScore");
            _statusText = GetComponentFromChild<TMP_Text>(existingRoot, "StatusMessage");
            _targetPreview = GetComponentFromChild<Image>(existingRoot, "TargetPreview");
            _confidenceFill = GetComponentFromChild<Image>(existingRoot, "GaugeFill");
            _mainCameraRing = GetComponentFromChild<Image>(existingRoot, "MainCameraStateRing");
            _mainCheckmark = GetComponentFromChild<TMP_Text>(existingRoot, "MainSuccessCheck");
            _mainCameraMask = FindDeepChild(existingRoot, "CircleMask") as RectTransform;
            _remotePlayerContainer = FindDeepChild(existingRoot, "RemotePlayerContainer") as RectTransform;

            RestoreRoundedSprites(existingRoot);

            return _safeRoot != null && _roundText != null && _targetText != null &&
                   _confidenceText != null && _scoreText != null && _statusText != null &&
                   _targetPreview != null && _confidenceFill != null &&
                   _mainCameraRing != null && _mainCheckmark != null &&
                   _remotePlayerContainer != null;
        }

        private static void RestoreRoundedSprites(Transform root)
        {
            string[] roundedImageNames =
            {
                "Card",
                "TargetCard",
                "ConfidenceCard",
                "ScoreCard",
                "OtherPlayersPanel",
                "GaugeBackground",
                "GaugeFill"
            };

            foreach (string imageName in roundedImageNames)
            {
                Image image = GetComponentFromChild<Image>(root, imageName);
                if (image != null && image.sprite == null)
                {
                    image.sprite = RoundedSprite;
                }
            }
        }

        private void ApplySafeArea()
        {
            if (_safeRoot == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Rect safeArea = Screen.safeArea;
            Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
            if (_lastSafeArea == safeArea && _lastScreenSize == screenSize)
            {
                return;
            }

            _lastSafeArea = safeArea;
            _lastScreenSize = screenSize;
            _safeRoot.anchorMin = new Vector2(safeArea.xMin / Screen.width, safeArea.yMin / Screen.height);
            _safeRoot.anchorMax = new Vector2(safeArea.xMax / Screen.width, safeArea.yMax / Screen.height);
            _safeRoot.offsetMin = Vector2.zero;
            _safeRoot.offsetMax = Vector2.zero;
        }

        private void BuildMainCamera(TMP_FontAsset font)
        {
            Transform existing = FindDeepChild(_canvas.transform, "CircleMask");
            Sprite circleSprite = null;
            if (existing != null)
            {
                _mainCameraMask = existing as RectTransform;
                Image maskImage = existing.GetComponent<Image>();
                circleSprite = maskImage != null ? maskImage.sprite : null;
                existing.SetParent(_safeRoot, false);
                SetRect(_mainCameraMask, new Vector2(0.5f, 1f), new Vector2(0f, -438f), new Vector2(640f, 640f));
                if (maskImage != null)
                {
                    maskImage.color = DarkColor;
                    maskImage.raycastTarget = false;
                }
            }

            GameObject ringObject = CreateUiObject("MainCameraStateRing", _safeRoot);
            ringObject.transform.SetSiblingIndex(Mathf.Max(0, _mainCameraMask != null ? _mainCameraMask.GetSiblingIndex() : 1));
            RectTransform ringRect = (RectTransform)ringObject.transform;
            SetRect(ringRect, new Vector2(0.5f, 1f), new Vector2(0f, -438f), new Vector2(676f, 676f));
            _mainCameraRing = ringObject.AddComponent<Image>();
            _mainCameraRing.sprite = circleSprite;
            _mainCameraRing.color = Color.white;
            _mainCameraRing.raycastTarget = false;

            _mainCheckmark = CreateText("MainSuccessCheck", _safeRoot, font, 66f, FontStyles.Bold);
            SetRect(_mainCheckmark.rectTransform, new Vector2(0.5f, 1f), new Vector2(248f, -218f), new Vector2(100f, 100f));
            _mainCheckmark.text = "✓";
            _mainCheckmark.color = DarkColor;
            _mainCheckmark.gameObject.SetActive(false);
        }

        private void BuildTargetCard(TMP_FontAsset font)
        {
            GameObject card = CreateCard("TargetCard", _safeRoot, new Vector2(0.5f, 1f), new Vector2(0f, -850f), new Vector2(650f, 168f));
            TMP_Text hint = CreateText("Hint", card.transform, font, 21f, FontStyles.Normal);
            SetRect(hint.rectTransform, new Vector2(0f, 0.5f), new Vector2(184f, 35f), new Vector2(380f, 42f));
            hint.alignment = TextAlignmentOptions.Left;
            hint.color = TealColor;
            hint.text = "FIND THIS";

            _targetText = CreateText("TargetName", card.transform, font, 34f, FontStyles.Bold);
            SetRect(_targetText.rectTransform, new Vector2(0f, 0.5f), new Vector2(184f, -15f), new Vector2(410f, 70f));
            _targetText.alignment = TextAlignmentOptions.Left;
            _targetText.text = "찾아야 할 물건";

            GameObject previewObject = CreateUiObject("TargetPreview", card.transform);
            RectTransform previewRect = (RectTransform)previewObject.transform;
            SetRect(previewRect, new Vector2(0f, 0.5f), new Vector2(91f, 0f), new Vector2(126f, 126f));
            _targetPreview = previewObject.AddComponent<Image>();
            _targetPreview.preserveAspect = true;
            _targetPreview.raycastTarget = false;
        }

        private void BuildStats(TMP_FontAsset font)
        {
            GameObject confidenceCard = CreateCard("ConfidenceCard", _safeRoot, new Vector2(0.5f, 1f), new Vector2(-237f, -1055f), new Vector2(436f, 168f));
            _confidenceText = CreateText("Confidence", confidenceCard.transform, font, 39f, FontStyles.Bold);
            SetRect(_confidenceText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(380f, 98f));
            _confidenceText.alignment = TextAlignmentOptions.Left;
            _confidenceText.text = "<size=25>인식 정확도</size>\n<b>0%</b>";

            GameObject gaugeBackground = CreateUiObject("GaugeBackground", confidenceCard.transform);
            RectTransform gaugeBackgroundRect = (RectTransform)gaugeBackground.transform;
            SetRect(gaugeBackgroundRect, new Vector2(0.5f, 0.5f), new Vector2(0f, -55f), new Vector2(374f, 18f));
            Image gaugeBgImage = gaugeBackground.AddComponent<Image>();
            gaugeBgImage.sprite = RoundedSprite;
            gaugeBgImage.type = Image.Type.Sliced;
            gaugeBgImage.color = new Color32(225, 235, 231, 255);

            GameObject gaugeFill = CreateUiObject("GaugeFill", gaugeBackground.transform);
            RectTransform gaugeFillRect = (RectTransform)gaugeFill.transform;
            Stretch(gaugeFillRect);
            _confidenceFill = gaugeFill.AddComponent<Image>();
            _confidenceFill.sprite = RoundedSprite;
            _confidenceFill.type = Image.Type.Filled;
            _confidenceFill.fillMethod = Image.FillMethod.Horizontal;
            _confidenceFill.fillOrigin = 0;
            _confidenceFill.fillAmount = 0f;
            _confidenceFill.color = TealColor;

            GameObject scoreCard = CreateCard("ScoreCard", _safeRoot, new Vector2(0.5f, 1f), new Vector2(237f, -1055f), new Vector2(436f, 168f));
            _scoreText = CreateText("MyScore", scoreCard.transform, font, 52f, FontStyles.Bold);
            Stretch(_scoreText.rectTransform, 28f);
            _scoreText.alignment = TextAlignmentOptions.Center;
            _scoreText.text = "<size=25>내 점수</size>\n<b>0</b>";
            _scoreText.color = TealColor;
        }

        private void BuildRemotePlayers(TMP_FontAsset font)
        {
            GameObject panel = CreateCard("OtherPlayersPanel", _safeRoot, new Vector2(0.5f, 0f), new Vector2(0f, 235f), new Vector2(1000f, 430f));
            Image panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color32(255, 255, 255, 205);

            TMP_Text title = CreateText("OtherPlayersTitle", panel.transform, font, 25f, FontStyles.Bold);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(900f, 50f));
            title.text = "다른 플레이어";
            title.color = TealColor;

            GameObject containerObject = CreateUiObject("RemotePlayerContainer", panel.transform);
            _remotePlayerContainer = (RectTransform)containerObject.transform;
            _remotePlayerContainer.anchorMin = new Vector2(0f, 0f);
            _remotePlayerContainer.anchorMax = new Vector2(1f, 1f);
            _remotePlayerContainer.offsetMin = new Vector2(35f, 20f);
            _remotePlayerContainer.offsetMax = new Vector2(-35f, -78f);
        }

        private static GameObject CreateCard(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            GameObject card = CreateUiObject(name, parent);
            RectTransform rect = (RectTransform)card.transform;
            SetRect(rect, anchor, position, size);
            Image image = card.AddComponent<Image>();
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            image.color = new Color32(255, 255, 255, 242);
            image.raycastTarget = false;
            return card;
        }

        private static void AddCard(Transform target, Color color, float padding)
        {
            GameObject card = CreateUiObject("Card", target.parent);
            card.transform.SetSiblingIndex(target.GetSiblingIndex());
            RectTransform rect = (RectTransform)card.transform;
            RectTransform targetRect = (RectTransform)target;
            rect.anchorMin = targetRect.anchorMin;
            rect.anchorMax = targetRect.anchorMax;
            rect.anchoredPosition = targetRect.anchoredPosition;
            rect.sizeDelta = targetRect.sizeDelta + Vector2.one * padding;
            Image image = card.AddComponent<Image>();
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
        }

        private static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, float size, FontStyles style)
        {
            GameObject textObject = CreateUiObject(name, parent);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                text.font = font;
            }
            text.fontSize = size;
            text.fontStyle = style;
            text.color = DarkColor;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var result = new GameObject(name, typeof(RectTransform));
            result.layer = 5;
            result.transform.SetParent(parent, false);
            return result;
        }

        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * padding;
            rect.offsetMax = Vector2.one * -padding;
        }

        private static Transform FindDeepChild(Transform root, string objectName)
        {
            foreach (Transform child in root)
            {
                if (child.name == objectName)
                {
                    return child;
                }

                Transform found = FindDeepChild(child, objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static T GetComponentFromChild<T>(Transform root, string objectName)
            where T : Component
        {
            Transform child = FindDeepChild(root, objectName);
            return child != null ? child.GetComponent<T>() : null;
        }

        private static Sprite RoundedSprite
        {
            get
            {
                if (_roundedSprite != null)
                {
                    return _roundedSprite;
                }

                const int size = 64;
                const float radius = 20f;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "HUD Rounded Rectangle",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = Mathf.Max(radius - x, 0f, x - (size - 1 - radius));
                        float dy = Mathf.Max(radius - y, 0f, y - (size - 1 - radius));
                        float alpha = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                _roundedSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect, Vector4.one * radius);
                _roundedSprite.name = "HUD Rounded Rectangle";
                _roundedSprite.hideFlags = HideFlags.HideAndDontSave;
                return _roundedSprite;
            }
        }
    }

}
