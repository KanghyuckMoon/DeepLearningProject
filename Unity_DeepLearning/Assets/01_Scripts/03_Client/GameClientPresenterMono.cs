using System;
using System.Collections.Generic;
using DeepLearning.GameData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeepLearning.GameClient
{
    public sealed class GameClientPresenterMono : MonoBehaviour
    {
        [SerializeField] private GameClientMono gameClient;
        [SerializeField] private ImageRecognitionControllerMono recognition;
        [SerializeField] private DataBaseSO itemDatabase;

        [Header("UI")]
        [SerializeField] private TMP_Text gameInfoText;
        [SerializeField] private TMP_Text centerText;
        [SerializeField] private TMP_Text aiInfoText;
        [SerializeField] private TMP_Text targetNameText;
        [SerializeField] private Image targetImage;

        private InGameHudViewMono _hud;

        [Header("Remote Cameras")]
        [Tooltip("비워 두면 Player로 시작하는 Mask의 TargetImage를 자동으로 사용합니다.")]
        [SerializeField] private Image[] remoteCameraImages;

        [Header("Notice")]
        [SerializeField] private GameObject noticePanel;
        [SerializeField] private TMP_Text noticeTitleText;
        [SerializeField] private TMP_Text noticeSubtitleText;
        [SerializeField, Min(0f)] private float noticeDuration = 2f;

        private float _noticeEndTime;
        private readonly List<RemoteCameraSlot> _remoteCameraSlots =
            new List<RemoteCameraSlot>();

        private void Awake()
        {
            _hud = GetComponent<InGameHudViewMono>();
            if (_hud == null)
            {
                _hud = gameObject.AddComponent<InGameHudViewMono>();
            }

            _hud.Initialize(gameInfoText, centerText, aiInfoText, targetNameText, targetImage);

            if (noticePanel != null)
            {
                noticePanel.transform.SetAsLastSibling();
            }

            PlayerUIRosterMono roster = GetComponent<PlayerUIRosterMono>();
            if (roster != null)
            {
                roster.ConfigureHudContainer(_hud.RemotePlayerContainer);
            }
        }

        public void InitializeHudPreview(InGameHudViewMono hud)
        {
            if (hud != null)
            {
                hud.Initialize(gameInfoText, centerText, aiInfoText, targetNameText, targetImage);
            }
        }

        private void OnEnable()
        {
            if (gameClient != null)
            {
                gameClient.ClientEvent += HandleClientEvent;
            }

            if (centerText != null)
            {
                centerText.text = "물건을 찾아라!";
            }

            Refresh(gameClient != null ? gameClient.Snapshot : null);
        }

        private void OnDisable()
        {
            if (gameClient != null)
            {
                gameClient.ClientEvent -= HandleClientEvent;
            }

            ClearRemoteCameras();
        }

        private void Update()
        {
            if (noticePanel != null && noticePanel.activeSelf && Time.unscaledTime >= _noticeEndTime)
            {
                noticePanel.SetActive(false);
            }

            if (_hud != null)
            {
                _hud.RefreshRecognition(recognition, gameClient != null ? gameClient.Snapshot : null);
            }
        }

        private void HandleClientEvent(GameClientEvent clientEvent)
        {
            Refresh(clientEvent.Snapshot);

            switch (clientEvent.Type)
            {
                case GameClientEventType.Connected:
                    ShowNotice(
                        $"{GetNickname(clientEvent.Snapshot, clientEvent.Snapshot.PlayerId)} 접속",
                        "게임 서버 연결 성공");
                    break;
                case GameClientEventType.RoundStarted:
                    ShowNotice($"Round {clientEvent.Snapshot.Round}", "새 라운드 시작");
                    break;
                case GameClientEventType.RoundResult:
                    ShowNotice(
                        $"Round {clientEvent.Snapshot.Round} 종료",
                        $"{GetNickname(clientEvent.Snapshot, clientEvent.SubjectPlayerId)} 승리");
                    break;
                case GameClientEventType.GameOver:
                    ShowNotice(
                        "게임 종료",
                        $"{GetNickname(clientEvent.Snapshot, clientEvent.SubjectPlayerId)} 최종 승리");
                    break;
                case GameClientEventType.PlayerJoined:
                    ShowNotice(
                        $"{GetNickname(clientEvent.Snapshot, clientEvent.SubjectPlayerId)} 접속",
                        "");
                    break;
                case GameClientEventType.PlayerLeft:
                    RemoveRemoteCamera(clientEvent.SubjectPlayerId);
                    ShowNotice($"Player {clientEvent.SubjectPlayerId} 연결 종료", "");
                    break;
                case GameClientEventType.PlayerUpdated:
                    break;
                case GameClientEventType.CameraFrame:
                    UpdateRemoteCamera(clientEvent.CameraFrame);
                    break;
                case GameClientEventType.Disconnected:
                    ClearRemoteCameras();
                    break;
                case GameClientEventType.Error:
                    ShowNotice("네트워크 오류", clientEvent.Message);
                    break;
            }
        }

        private void Refresh(GameClientSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            ItemData targetItem = null;
            bool hasTarget = itemDatabase != null &&
                             itemDatabase.TryGetItem(snapshot.AnswerIndex, out targetItem);

            if (targetImage != null)
            {
                targetImage.enabled = hasTarget;
                targetImage.sprite = hasTarget ? targetItem.Sprite : null;
            }

            if (targetNameText != null)
            {
                targetNameText.text = hasTarget ? targetItem.Name : string.Empty;
            }

            if (centerText != null)
            {
                centerText.text = "물건을 찾아라!";
            }

            if (_hud != null)
            {
                _hud.RefreshSnapshot(
                    snapshot,
                    hasTarget ? targetItem.Sprite : null,
                    hasTarget ? targetItem.Name : string.Empty);
            }
        }

        private void ShowNotice(string title, string subtitle)
        {
            if (noticeTitleText != null)
            {
                noticeTitleText.text = title;
            }

            if (noticeSubtitleText != null)
            {
                noticeSubtitleText.text = subtitle;
            }

            if (noticePanel != null)
            {
                noticePanel.SetActive(true);
                _noticeEndTime = Time.unscaledTime + noticeDuration;
            }
        }

        private static string GetNickname(GameClientSnapshot snapshot, int playerId)
        {
            return snapshot != null && snapshot.Nicknames != null &&
                   snapshot.Nicknames.TryGetValue(playerId, out string nickname)
                ? nickname
                : $"Player {playerId}";
        }

        private void DiscoverRemoteCameraSlots()
        {
            if (_remoteCameraSlots.Count > 0)
            {
                return;
            }

            var images = new List<Image>();
            if (remoteCameraImages != null && remoteCameraImages.Length > 0)
            {
                foreach (Image image in remoteCameraImages)
                {
                    if (image != null)
                    {
                        images.Add(image);
                    }
                }
            }
            else
            {
                Mask[] masks = FindObjectsByType<Mask>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                Array.Sort(
                    masks,
                    (left, right) => string.Compare(
                        left.gameObject.name,
                        right.gameObject.name,
                        StringComparison.Ordinal));

                foreach (Mask mask in masks)
                {
                    if (!mask.gameObject.name.StartsWith("Player", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Transform imageTransform = mask.transform.Find("TargetImage");
                    if (imageTransform != null && imageTransform.TryGetComponent(out Image image))
                    {
                        images.Add(image);
                    }
                }
            }

            foreach (Image image in images)
            {
                Transform root = image.transform.parent;
                TMP_Text label = root != null
                    ? root.GetComponentInChildren<TMP_Text>(true)
                    : null;
                _remoteCameraSlots.Add(new RemoteCameraSlot(image, label, root?.gameObject));

                if (root != null)
                {
                    root.gameObject.SetActive(false);
                }
            }
        }

        private void UpdateRemoteCamera(RemoteCameraFrame frame)
        {
            if (frame == null || frame.JpegData == null || frame.JpegData.Length == 0)
            {
                return;
            }

            RemoteCameraSlot slot = _remoteCameraSlots.Find(item => item.PlayerId == frame.PlayerId);
            if (slot == null)
            {
                slot = _remoteCameraSlots.Find(item => item.PlayerId < 0);
            }

            if (slot == null)
            {
                return;
            }

            slot.PlayerId = frame.PlayerId;
            if (slot.Texture == null)
            {
                slot.Texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            }

            if (!slot.Texture.LoadImage(frame.JpegData, false))
            {
                return;
            }

            if (slot.Sprite == null ||
                slot.Sprite.rect.width != slot.Texture.width ||
                slot.Sprite.rect.height != slot.Texture.height)
            {
                if (slot.Sprite != null)
                {
                    Destroy(slot.Sprite);
                }

                slot.Sprite = Sprite.Create(
                    slot.Texture,
                    new Rect(0f, 0f, slot.Texture.width, slot.Texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }

            slot.Image.sprite = slot.Sprite;
            slot.Image.preserveAspect = true;
            slot.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, -frame.Rotation);
            slot.Image.rectTransform.localScale = new Vector3(
                frame.MirrorHorizontally ? -1f : 1f,
                frame.FlipVertically ? -1f : 1f,
                1f);

            if (slot.Label != null)
            {
                slot.Label.text = $"Player {frame.PlayerId}";
            }

            if (slot.Root != null && !slot.Root.activeSelf)
            {
                slot.Root.SetActive(true);
            }
        }

        private void RemoveRemoteCamera(int playerId)
        {
            RemoteCameraSlot slot = _remoteCameraSlots.Find(item => item.PlayerId == playerId);
            if (slot != null)
            {
                ClearRemoteCameraSlot(slot);
            }
        }

        private void ClearRemoteCameras()
        {
            foreach (RemoteCameraSlot slot in _remoteCameraSlots)
            {
                ClearRemoteCameraSlot(slot);
            }
        }

        private void ClearRemoteCameraSlot(RemoteCameraSlot slot)
        {
            slot.PlayerId = -1;
            slot.Image.sprite = null;
            slot.Image.rectTransform.localRotation = Quaternion.identity;
            slot.Image.rectTransform.localScale = Vector3.one;

            if (slot.Sprite != null)
            {
                Destroy(slot.Sprite);
                slot.Sprite = null;
            }

            if (slot.Texture != null)
            {
                Destroy(slot.Texture);
                slot.Texture = null;
            }

            if (slot.Root != null)
            {
                slot.Root.SetActive(false);
            }
        }

        private sealed class RemoteCameraSlot
        {
            public RemoteCameraSlot(Image image, TMP_Text label, GameObject root)
            {
                Image = image;
                Label = label;
                Root = root;
                PlayerId = -1;
            }

            public Image Image { get; }
            public TMP_Text Label { get; }
            public GameObject Root { get; }
            public int PlayerId { get; set; }
            public Texture2D Texture { get; set; }
            public Sprite Sprite { get; set; }
        }
    }
}
