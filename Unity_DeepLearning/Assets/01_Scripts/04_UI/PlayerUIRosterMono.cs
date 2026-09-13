using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeepLearning.GameClient
{
    /// <summary>
    /// 접속한 다른 플레이어 수만큼 PlayerUIElement를 만들고 스프링 물리로 정렬합니다.
    /// </summary>
    public sealed class PlayerUIRosterMono : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameClientMono gameClient;
        [SerializeField] private GameObject playerUIElementPrefab;
        [SerializeField] private RectTransform container;

        [Header("Layout")]
        [SerializeField] private Vector2 layoutCenter = new Vector2(0f, -332f);
        [SerializeField, Min(1f)] private float horizontalSpacing = 245f;
        [SerializeField, Min(1f)] private float verticalSpacing = 235f;
        [SerializeField, Min(1)] private int maxElementsPerRow = 4;

        [Header("Soft Motion")]
        [SerializeField, Min(1f)] private float springStrength = 72f;
        [SerializeField, Min(0.1f)] private float damping = 10.5f;
        [SerializeField, Min(1f)] private float collisionDiameter = 205f;
        [SerializeField, Min(0f)] private float collisionStrength = 34f;
        [SerializeField, Min(0f)] private float enterImpulse = 1150f;
        [SerializeField, Min(0.1f)] private float exitDuration = 1.1f;
        [SerializeField, Range(0f, 0.5f)] private float squashAmount = 0.18f;

        private readonly Dictionary<int, PlayerElement> _activeElements =
            new Dictionary<int, PlayerElement>();
        private readonly List<PlayerElement> _elements = new List<PlayerElement>();

        public void ConfigureHudContainer(RectTransform hudContainer)
        {
            if (hudContainer != null)
            {
                container = hudContainer;
                layoutCenter = Vector2.zero;
                horizontalSpacing = 224f;
                verticalSpacing = 220f;
                collisionDiameter = 188f;
                maxElementsPerRow = 4;
            }
        }

        private void Awake()
        {
            if (gameClient == null)
            {
                gameClient = FindFirstObjectByType<GameClientMono>();
            }

            if (container == null)
            {
                Canvas canvas = FindFirstObjectByType<Canvas>();
                container = canvas != null ? canvas.transform as RectTransform : null;
            }

            HideLegacyPlayerSlots();
        }

        private void OnEnable()
        {
            if (gameClient != null)
            {
                gameClient.ClientEvent += HandleClientEvent;
                SyncPlayers(gameClient.Snapshot);
            }
        }

        private void OnDisable()
        {
            if (gameClient != null)
            {
                gameClient.ClientEvent -= HandleClientEvent;
            }

            for (int i = _elements.Count - 1; i >= 0; i--)
            {
                DestroyElementImmediately(_elements[i]);
            }

            _elements.Clear();
            _activeElements.Clear();
        }

        private void Update()
        {
            float deltaTime = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            if (deltaTime <= 0f)
            {
                return;
            }

            SimulateSprings(deltaTime);
            ResolveSoftCollisions(deltaTime);
            ApplyVisualDeformation(deltaTime);
            RemoveFinishedExits();
        }

        private void HandleClientEvent(GameClientEvent clientEvent)
        {
            if (clientEvent.Type == GameClientEventType.CameraFrame)
            {
                ApplyCameraFrame(clientEvent.CameraFrame);
                return;
            }

            SyncPlayers(clientEvent.Snapshot);
        }

        private void SyncPlayers(GameClientSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            var expectedPlayers = new HashSet<int>();
            if (snapshot.Connected)
            {
                foreach (int playerId in snapshot.Scores.Keys)
                {
                    // 자신의 카메라는 중앙의 기존 WebCam UI로 표시합니다.
                    if (playerId != snapshot.PlayerId)
                    {
                        expectedPlayers.Add(playerId);
                    }
                }
            }

            var removedPlayers = new List<int>();
            foreach (int playerId in _activeElements.Keys)
            {
                if (!expectedPlayers.Contains(playerId))
                {
                    removedPlayers.Add(playerId);
                }
            }

            foreach (int playerId in removedPlayers)
            {
                BeginExit(playerId);
            }

            var addedPlayers = new List<int>();
            foreach (int playerId in expectedPlayers)
            {
                if (!_activeElements.ContainsKey(playerId))
                {
                    addedPlayers.Add(playerId);
                }
            }

            addedPlayers.Sort();
            foreach (int playerId in addedPlayers)
            {
                AddPlayer(playerId);
            }

            foreach (KeyValuePair<int, PlayerElement> pair in _activeElements)
            {
                if (pair.Value.Label == null)
                {
                    continue;
                }

                string nickname = snapshot.Nicknames != null &&
                                  snapshot.Nicknames.TryGetValue(pair.Key, out string foundNickname)
                    ? foundNickname
                    : $"Player {pair.Key}";
                int score = snapshot.Scores.TryGetValue(pair.Key, out int foundScore)
                    ? foundScore
                    : 0;
                pair.Value.Label.text = $"{nickname}\n<b>{score}</b>점";

                bool succeeded = snapshot.RoundWinner == pair.Key;
                if (pair.Value.Outline != null)
                {
                    pair.Value.Outline.color = succeeded
                        ? InGameHudViewMono.SuccessColor
                        : Color.white;
                }

                if (pair.Value.Checkmark != null)
                {
                    pair.Value.Checkmark.gameObject.SetActive(succeeded);
                }
            }

            RecalculateTargets();
        }

        private void AddPlayer(int playerId)
        {
            if (playerUIElementPrefab == null || container == null)
            {
                Debug.LogWarning("PlayerUIElement 프리팹 또는 UI Container가 지정되지 않았습니다.", this);
                return;
            }

            GameObject instance = Instantiate(playerUIElementPrefab, container, false);
            instance.name = $"PlayerUIElement (Player {playerId})";
            instance.SetActive(true);

            RectTransform rect = instance.GetComponent<RectTransform>();
            CanvasGroup canvasGroup = instance.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = instance.AddComponent<CanvasGroup>();
            }

            Image cameraImage = null;
            Transform imageTransform = rect.Find("TargetImage");
            if (imageTransform != null)
            {
                imageTransform.TryGetComponent(out cameraImage);
            }

            TMP_Text label = rect.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = $"Player {playerId}";
            }

            PrepareCasualThumbnail(rect, cameraImage, label, out Image outline, out TMP_Text checkmark);

            float side = playerId % 2 == 0 ? -1f : 1f;
            float halfWidth = Mathf.Max(500f, container.rect.width * 0.5f);
            rect.anchoredPosition = new Vector2(
                side * (halfWidth + rect.rect.width + 160f),
                layoutCenter.y + Mathf.Sin(playerId * 2.17f) * 110f);
            rect.localScale = Vector3.one * 0.72f;
            canvasGroup.alpha = 0f;

            var element = new PlayerElement(
                playerId,
                instance,
                rect,
                canvasGroup,
                cameraImage,
                label,
                outline,
                checkmark)
            {
                Velocity = new Vector2(-side * enterImpulse, 0f),
                Scale = 0.72f,
                Squash = squashAmount
            };

            _activeElements.Add(playerId, element);
            _elements.Add(element);
        }

        private static void PrepareCasualThumbnail(
            RectTransform root,
            Image cameraImage,
            TMP_Text label,
            out Image outline,
            out TMP_Text checkmark)
        {
            outline = null;
            checkmark = null;
            if (root == null)
            {
                return;
            }

            Image rootImage = root.GetComponent<Image>();
            Sprite circleSprite = rootImage != null ? rootImage.sprite : null;
            Mask rootMask = root.GetComponent<Mask>();
            if (rootMask != null)
            {
                rootMask.enabled = false;
            }

            if (rootImage != null)
            {
                rootImage.enabled = false;
            }

            Transform legacyOutline = root.Find("TargetOutline");
            if (legacyOutline != null)
            {
                legacyOutline.gameObject.SetActive(false);
            }

            root.sizeDelta = new Vector2(196f, 248f);

            var outlineObject = new GameObject("StateOutline", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            outlineObject.transform.SetParent(root, false);
            outlineObject.transform.SetAsFirstSibling();
            RectTransform outlineRect = (RectTransform)outlineObject.transform;
            outlineRect.anchorMin = outlineRect.anchorMax = new Vector2(0.5f, 0.5f);
            outlineRect.anchoredPosition = new Vector2(0f, 26f);
            outlineRect.sizeDelta = new Vector2(180f, 180f);
            outline = outlineObject.GetComponent<Image>();
            outline.sprite = circleSprite;
            outline.color = Color.white;
            outline.raycastTarget = false;

            var maskObject = new GameObject("CameraMask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            maskObject.transform.SetParent(root, false);
            RectTransform maskRect = (RectTransform)maskObject.transform;
            maskRect.anchorMin = maskRect.anchorMax = new Vector2(0.5f, 0.5f);
            maskRect.anchoredPosition = new Vector2(0f, 26f);
            maskRect.sizeDelta = new Vector2(162f, 162f);
            Image maskImage = maskObject.GetComponent<Image>();
            maskImage.sprite = circleSprite;
            maskImage.color = InGameHudViewMono.DarkColor;
            maskImage.raycastTarget = false;
            maskObject.GetComponent<Mask>().showMaskGraphic = true;

            if (cameraImage != null)
            {
                cameraImage.transform.SetParent(maskRect, false);
                RectTransform cameraRect = cameraImage.rectTransform;
                cameraRect.anchorMin = Vector2.zero;
                cameraRect.anchorMax = Vector2.one;
                cameraRect.offsetMin = Vector2.zero;
                cameraRect.offsetMax = Vector2.zero;
            }

            if (label != null)
            {
                label.transform.SetParent(root, false);
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
                labelRect.anchoredPosition = new Vector2(0f, -92f);
                labelRect.sizeDelta = new Vector2(220f, 70f);
                label.alignment = TextAlignmentOptions.Center;
                label.enableAutoSizing = true;
                label.fontSizeMin = 18f;
                label.fontSizeMax = 26f;
                label.color = InGameHudViewMono.DarkColor;
                label.raycastTarget = false;
            }

            GameObject checkObject = new GameObject("SuccessCheck", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            checkObject.transform.SetParent(root, false);
            RectTransform checkRect = (RectTransform)checkObject.transform;
            checkRect.anchorMin = checkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkRect.anchoredPosition = new Vector2(62f, 88f);
            checkRect.sizeDelta = new Vector2(54f, 54f);
            checkmark = checkObject.GetComponent<TextMeshProUGUI>();
            checkmark.font = label != null ? label.font : null;
            checkmark.text = "✓";
            checkmark.fontSize = 42f;
            checkmark.fontStyle = FontStyles.Bold;
            checkmark.alignment = TextAlignmentOptions.Center;
            checkmark.color = InGameHudViewMono.DarkColor;
            checkmark.raycastTarget = false;
            checkObject.SetActive(false);
        }

        private void BeginExit(int playerId)
        {
            if (!_activeElements.TryGetValue(playerId, out PlayerElement element))
            {
                return;
            }

            _activeElements.Remove(playerId);
            element.IsExiting = true;
            element.ExitAge = 0f;

            float halfWidth = Mathf.Max(500f, container.rect.width * 0.5f);
            float side = element.Rect.anchoredPosition.x <= layoutCenter.x ? -1f : 1f;
            element.Target = new Vector2(
                side * (halfWidth + element.Rect.rect.width + 260f),
                element.Rect.anchoredPosition.y + (playerId % 2 == 0 ? 90f : -90f));
            element.Velocity += new Vector2(side * enterImpulse * 0.55f, 0f);
            element.Squash = -squashAmount;
        }

        private void RecalculateTargets()
        {
            var ordered = new List<PlayerElement>(_activeElements.Values);
            ordered.Sort((left, right) => left.PlayerId.CompareTo(right.PlayerId));

            int count = ordered.Count;
            int columns = Mathf.Max(1, Mathf.Min(maxElementsPerRow, count));
            int rows = Mathf.CeilToInt((float)count / columns);

            for (int index = 0; index < count; index++)
            {
                int row = index / columns;
                int column = index % columns;
                int elementsInRow = Mathf.Min(columns, count - row * columns);

                float x = layoutCenter.x +
                          (column - (elementsInRow - 1) * 0.5f) * horizontalSpacing;
                float y = layoutCenter.y +
                          ((rows - 1) * 0.5f - row) * verticalSpacing;
                ordered[index].Target = new Vector2(x, y);
            }
        }

        private void SimulateSprings(float deltaTime)
        {
            float velocityDecay = Mathf.Exp(-damping * deltaTime);

            foreach (PlayerElement element in _elements)
            {
                Vector2 displacement = element.Target - element.Rect.anchoredPosition;
                element.Velocity += displacement * springStrength * deltaTime;
                element.Velocity *= velocityDecay;
                element.Rect.anchoredPosition += element.Velocity * deltaTime;

                if (element.IsExiting)
                {
                    element.ExitAge += deltaTime;
                }
            }
        }

        private void ResolveSoftCollisions(float deltaTime)
        {
            float minimumDistance = Mathf.Max(1f, collisionDiameter);

            for (int i = 0; i < _elements.Count; i++)
            {
                PlayerElement first = _elements[i];
                for (int j = i + 1; j < _elements.Count; j++)
                {
                    PlayerElement second = _elements[j];
                    Vector2 difference = second.Rect.anchoredPosition - first.Rect.anchoredPosition;
                    float distance = difference.magnitude;
                    if (distance >= minimumDistance)
                    {
                        continue;
                    }

                    Vector2 normal = distance > 0.01f
                        ? difference / distance
                        : (first.PlayerId < second.PlayerId ? Vector2.right : Vector2.left);
                    float overlap = minimumDistance - distance;
                    Vector2 separation = normal * overlap * 0.5f;

                    first.Rect.anchoredPosition -= separation;
                    second.Rect.anchoredPosition += separation;

                    Vector2 impulse = normal * overlap * collisionStrength * deltaTime;
                    first.Velocity -= impulse;
                    second.Velocity += impulse;

                    float impact = Mathf.Clamp01(overlap / minimumDistance) * squashAmount;
                    first.Squash = Mathf.Max(first.Squash, impact);
                    second.Squash = Mathf.Max(second.Squash, impact);
                }
            }
        }

        private void ApplyVisualDeformation(float deltaTime)
        {
            foreach (PlayerElement element in _elements)
            {
                float targetAlpha = element.IsExiting
                    ? Mathf.Clamp01(1f - element.ExitAge / exitDuration)
                    : 1f;
                element.CanvasGroup.alpha = Mathf.MoveTowards(
                    element.CanvasGroup.alpha,
                    targetAlpha,
                    deltaTime * 4.5f);

                float scaleTarget = element.IsExiting ? 0.78f : 1f;
                float scaleVelocity = element.ScaleVelocity;
                element.Scale = Mathf.SmoothDamp(
                    element.Scale,
                    scaleTarget,
                    ref scaleVelocity,
                    element.IsExiting ? 0.22f : 0.16f,
                    Mathf.Infinity,
                    deltaTime);
                element.ScaleVelocity = scaleVelocity;

                float squashVelocity = element.SquashVelocity;
                element.Squash = Mathf.SmoothDamp(
                    element.Squash,
                    0f,
                    ref squashVelocity,
                    0.2f,
                    Mathf.Infinity,
                    deltaTime);
                element.SquashVelocity = squashVelocity;

                float speedStretch = Mathf.Clamp(element.Velocity.magnitude / 4500f, 0f, 0.09f);
                float horizontalScale = element.Scale + element.Squash + speedStretch;
                float verticalScale = element.Scale - element.Squash * 0.65f - speedStretch * 0.55f;
                element.Rect.localScale = new Vector3(horizontalScale, verticalScale, 1f);

                float targetRotation = Mathf.Clamp(-element.Velocity.x * 0.006f, -7f, 7f);
                element.Rotation = Mathf.Lerp(
                    element.Rotation,
                    targetRotation,
                    1f - Mathf.Exp(-9f * deltaTime));
                element.Rect.localRotation = Quaternion.Euler(0f, 0f, element.Rotation);
            }
        }

        private void RemoveFinishedExits()
        {
            for (int i = _elements.Count - 1; i >= 0; i--)
            {
                PlayerElement element = _elements[i];
                if (!element.IsExiting || element.ExitAge < exitDuration)
                {
                    continue;
                }

                _elements.RemoveAt(i);
                DestroyElementImmediately(element);
            }
        }

        private void ApplyCameraFrame(RemoteCameraFrame frame)
        {
            if (frame == null || frame.JpegData == null ||
                !_activeElements.TryGetValue(frame.PlayerId, out PlayerElement element) ||
                element.CameraImage == null)
            {
                return;
            }

            if (element.Texture == null)
            {
                element.Texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            }

            if (!element.Texture.LoadImage(frame.JpegData, false))
            {
                return;
            }

            if (element.Sprite == null ||
                element.Sprite.rect.width != element.Texture.width ||
                element.Sprite.rect.height != element.Texture.height)
            {
                if (element.Sprite != null)
                {
                    Destroy(element.Sprite);
                }

                element.Sprite = Sprite.Create(
                    element.Texture,
                    new Rect(0f, 0f, element.Texture.width, element.Texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }

            element.CameraImage.sprite = element.Sprite;
            element.CameraImage.preserveAspect = true;
            element.CameraImage.rectTransform.localEulerAngles =
                new Vector3(0f, 0f, -frame.Rotation);
            element.CameraImage.rectTransform.localScale = new Vector3(
                frame.MirrorHorizontally ? -1f : 1f,
                frame.FlipVertically ? -1f : 1f,
                1f);
        }

        private void DestroyElementImmediately(PlayerElement element)
        {
            if (element.Sprite != null)
            {
                Destroy(element.Sprite);
            }

            if (element.Texture != null)
            {
                Destroy(element.Texture);
            }

            if (element.Instance != null)
            {
                Destroy(element.Instance);
            }
        }

        private void HideLegacyPlayerSlots()
        {
            if (container == null)
            {
                return;
            }

            Mask[] masks = container.GetComponentsInChildren<Mask>(true);
            foreach (Mask mask in masks)
            {
                if (mask.gameObject.name.StartsWith("Player", StringComparison.Ordinal))
                {
                    mask.gameObject.SetActive(false);
                }
            }
        }

        private void OnValidate()
        {
            horizontalSpacing = Mathf.Max(1f, horizontalSpacing);
            verticalSpacing = Mathf.Max(1f, verticalSpacing);
            maxElementsPerRow = Mathf.Max(1, maxElementsPerRow);
            springStrength = Mathf.Max(1f, springStrength);
            damping = Mathf.Max(0.1f, damping);
            collisionDiameter = Mathf.Max(1f, collisionDiameter);
            collisionStrength = Mathf.Max(0f, collisionStrength);
            enterImpulse = Mathf.Max(0f, enterImpulse);
            exitDuration = Mathf.Max(0.1f, exitDuration);
        }

        private sealed class PlayerElement
        {
            public PlayerElement(
                int playerId,
                GameObject instance,
                RectTransform rect,
                CanvasGroup canvasGroup,
                Image cameraImage,
                TMP_Text label,
                Image outline,
                TMP_Text checkmark)
            {
                PlayerId = playerId;
                Instance = instance;
                Rect = rect;
                CanvasGroup = canvasGroup;
                CameraImage = cameraImage;
                Label = label;
                Outline = outline;
                Checkmark = checkmark;
                Target = rect.anchoredPosition;
            }

            public int PlayerId { get; }
            public GameObject Instance { get; }
            public RectTransform Rect { get; }
            public CanvasGroup CanvasGroup { get; }
            public Image CameraImage { get; }
            public TMP_Text Label { get; }
            public Image Outline { get; }
            public TMP_Text Checkmark { get; }
            public Vector2 Target { get; set; }
            public Vector2 Velocity { get; set; }
            public float Scale { get; set; }
            public float ScaleVelocity { get; set; }
            public float Squash { get; set; }
            public float SquashVelocity { get; set; }
            public float Rotation { get; set; }
            public bool IsExiting { get; set; }
            public float ExitAge { get; set; }
            public Texture2D Texture { get; set; }
            public Sprite Sprite { get; set; }
        }
    }
}
