using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeepLearning.GameClient
{
    public sealed class GameClientPresenter : MonoBehaviour
    {
        [SerializeField] private GameClient gameClient;
        [SerializeField] private ImageRecognitionController recognition;

        [Header("UI")]
        [SerializeField] private TMP_Text gameInfoText;
        [SerializeField] private TMP_Text centerText;
        [SerializeField] private TMP_Text aiInfoText;
        [SerializeField] private Image targetImage;
        [SerializeField] private Sprite[] targetSprites = { };

        [Header("Notice")]
        [SerializeField] private GameObject noticePanel;
        [SerializeField] private TMP_Text noticeTitleText;
        [SerializeField] private TMP_Text noticeSubtitleText;
        [SerializeField, Min(0f)] private float noticeDuration = 2f;

        private float _noticeEndTime;

        private void OnEnable()
        {
            if (gameClient != null)
            {
                gameClient.ClientEvent += HandleClientEvent;
            }

            if (centerText != null)
            {
                centerText.text = "물건을 찾아라";
            }

            Refresh(gameClient != null ? gameClient.Snapshot : null);
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
            if (noticePanel != null && noticePanel.activeSelf && Time.unscaledTime >= _noticeEndTime)
            {
                noticePanel.SetActive(false);
            }

            if (aiInfoText != null && recognition != null)
            {
                ClassificationResult prediction = recognition.LastResult;
                aiInfoText.text =
                    $"Target : {recognition.TargetLabel}\n" +
                    $"AI : {prediction.Label}\n" +
                    $"Confidence : {prediction.Confidence * 100f:F1}%\n" +
                    $"Correct : {recognition.CorrectCount}/{recognition.RequiredCount}";
            }
        }

        private void HandleClientEvent(GameClientEvent clientEvent)
        {
            Refresh(clientEvent.Snapshot);

            switch (clientEvent.Type)
            {
                case GameClientEventType.Connected:
                    ShowNotice($"Player {clientEvent.Snapshot.PlayerId} 접속", "게임 서버 연결 성공");
                    break;
                case GameClientEventType.RoundStarted:
                    ShowNotice($"Round {clientEvent.Snapshot.Round}", "새 라운드 시작");
                    break;
                case GameClientEventType.RoundResult:
                    ShowNotice(
                        $"Round {clientEvent.Snapshot.Round} 종료",
                        $"Player {clientEvent.SubjectPlayerId} 승리");
                    break;
                case GameClientEventType.GameOver:
                    ShowNotice("게임 종료", $"Player {clientEvent.SubjectPlayerId} 최종 승리");
                    break;
                case GameClientEventType.PlayerJoined:
                    ShowNotice($"Player {clientEvent.SubjectPlayerId} 접속", "");
                    break;
                case GameClientEventType.PlayerLeft:
                    ShowNotice($"Player {clientEvent.SubjectPlayerId} 연결 종료", "");
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

            if (gameInfoText != null)
            {
                var builder = new StringBuilder();
                builder.Append("Round : ").Append(snapshot.Round);

                var playerIds = new List<int>(snapshot.Scores.Keys);
                playerIds.Sort();

                foreach (int playerId in playerIds)
                {
                    builder.Append('\n')
                        .Append("Player ")
                        .Append(playerId);

                    if (playerId == snapshot.PlayerId)
                    {
                        builder.Append(" (나)");
                    }

                    builder.Append(" : ").Append(snapshot.Scores[playerId]);
                }

                gameInfoText.text = builder.ToString();
            }

            if (targetImage != null)
            {
                bool hasTarget = snapshot.AnswerIndex >= 0 && snapshot.AnswerIndex < targetSprites.Length;
                targetImage.enabled = hasTarget;
                targetImage.sprite = hasTarget ? targetSprites[snapshot.AnswerIndex] : null;
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
    }
}
