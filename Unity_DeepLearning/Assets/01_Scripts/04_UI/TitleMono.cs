using DeepLearning.GameClient;
using DeepLearning.SceneFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TitleMono : MonoBehaviour
{
    private const string GameSceneName = "GameScene";
    private const string ServerSceneName = "ServerScene";

    [Header("Nickname")]
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private TMP_Text validationText;

    private void Awake()
    {
        if (nicknameInput == null)
        {
            nicknameInput = FindFirstObjectByType<TMP_InputField>();
        }

        if (nicknameInput == null)
        {
            CreateDefaultNicknameInput();
        }

        if (nicknameInput != null)
        {
            nicknameInput.characterLimit = PlayerProfile.MaxNicknameLength;
            nicknameInput.text = PlayerProfile.SavedNickname;
        }

        SetValidationMessage(string.Empty);
    }

    public void MoveToGameScene()
    {
        string requestedNickname = nicknameInput != null
            ? nicknameInput.text
            : PlayerProfile.SavedNickname;

        if (!PlayerProfile.TrySaveNickname(requestedNickname, out _))
        {
            SetValidationMessage("닉네임을 입력해 주세요.");
            nicknameInput?.Select();
            return;
        }

        SceneTransitionMono.LoadScene(GameSceneName);
    }

    public void SetNickname(string nickname)
    {
        if (PlayerProfile.TrySaveNickname(nickname, out string savedNickname))
        {
            if (nicknameInput != null && nicknameInput.text != savedNickname)
            {
                nicknameInput.SetTextWithoutNotify(savedNickname);
            }

            SetValidationMessage(string.Empty);
        }
        else
        {
            SetValidationMessage("닉네임을 입력해 주세요.");
        }
    }

    public void MoveToServerScene()
    {
        SceneTransitionMono.LoadScene(ServerSceneName);
    }

    private void CreateDefaultNicknameInput()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        var inputObject = new GameObject(
            "NicknameInput",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(TMP_InputField));
        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        inputRect.SetParent(canvas.transform, false);
        inputRect.anchorMin = new Vector2(0.5f, 0f);
        inputRect.anchorMax = new Vector2(0.5f, 0f);
        inputRect.pivot = new Vector2(0.5f, 0.5f);
        inputRect.anchoredPosition = new Vector2(0f, 610f);
        inputRect.sizeDelta = new Vector2(430f, 72f);

        Image background = inputObject.GetComponent<Image>();
        background.color = new Color(0.06f, 0.08f, 0.1f, 0.88f);

        RectTransform textArea = CreateRect("Text Area", inputRect);
        textArea.anchorMin = Vector2.zero;
        textArea.anchorMax = Vector2.one;
        textArea.offsetMin = new Vector2(24f, 8f);
        textArea.offsetMax = new Vector2(-24f, -8f);
        textArea.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI text = CreateText("Text", textArea, Color.white);
        TextMeshProUGUI placeholder = CreateText(
            "Placeholder",
            textArea,
            new Color(1f, 1f, 1f, 0.42f));
        placeholder.text = "닉네임을 입력하세요";
        placeholder.fontStyle = FontStyles.Italic;

        nicknameInput = inputObject.GetComponent<TMP_InputField>();
        nicknameInput.textViewport = textArea;
        nicknameInput.textComponent = text;
        nicknameInput.placeholder = placeholder;
        nicknameInput.characterLimit = PlayerProfile.MaxNicknameLength;
        nicknameInput.lineType = TMP_InputField.LineType.SingleLine;
        nicknameInput.targetGraphic = background;

        RectTransform validationRect = CreateRect("NicknameValidation", inputRect);
        validationRect.anchorMin = new Vector2(0f, 0f);
        validationRect.anchorMax = new Vector2(1f, 0f);
        validationRect.pivot = new Vector2(0.5f, 1f);
        validationRect.anchoredPosition = new Vector2(0f, -10f);
        validationRect.sizeDelta = new Vector2(0f, 38f);
        validationText = CreateText(
            "Text",
            validationRect,
            new Color(1f, 0.42f, 0.42f, 1f));
        validationText.alignment = TextAlignmentOptions.Center;
        validationText.fontSize = 23f;
    }

    private static RectTransform CreateRect(string objectName, RectTransform parent)
    {
        var child = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static TextMeshProUGUI CreateText(
        string objectName,
        RectTransform parent,
        Color color)
    {
        RectTransform rect = CreateRect(objectName, parent);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.color = color;
        text.fontSize = 30f;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        return text;
    }

    private void SetValidationMessage(string message)
    {
        if (validationText != null)
        {
            validationText.text = message;
        }
    }
}
