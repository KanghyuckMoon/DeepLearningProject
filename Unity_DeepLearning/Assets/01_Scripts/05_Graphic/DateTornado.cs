using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 현재 날짜의 월/일 네 글자를 소용돌이치게 한 뒤 MM / DD 형태로 정렬합니다.
/// Canvas 아래의 빈 UI 오브젝트에 붙이면 숫자 TextMeshProUGUI를 자동으로 생성합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[ExecuteAlways]
public sealed class DateTornado : MonoBehaviour
{
    [Min(0f)] public float delay = 3f;

    private const int DigitCount = 4;

    [Header("Date UI")]
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField, Min(1f)] private float fontSize = 120f;
    [SerializeField] private Color digitColor = Color.white;
    [SerializeField, Min(0f)] private float digitSpacing = 78f;
    [SerializeField, Min(0f)] private float rowSpacing = 122f;

    [Header("Tornado Animation")]
    [SerializeField, Min(0.1f)] private float duration = 2.8f;
    [SerializeField, Min(0f)] private float startRadius = 330f;
    [SerializeField, Min(0f)] private float startHeight = 260f;
    [SerializeField, Min(0f)] private float turns = 3.25f;
    [SerializeField, Range(0f, 0.2f)] private float digitStagger = 0.055f;
    [SerializeField] private AnimationCurve settleCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool replayWhenDateChanges = true;

    private readonly TextMeshProUGUI[] _digits = new TextMeshProUGUI[DigitCount];
    private readonly Vector2[] _targets = new Vector2[DigitCount];

    private Coroutine _animation;
    private DateTime _displayedDate;
    private bool _hasFinished;

    private void Awake()
    {
        CreateDigitsIfNeeded();
        CalculateTargets();
        SetDate(DateTime.Now);

        if (Application.isPlaying)
        {
            HideDigits();
        }
        else
        {
            SnapToTargets();
        }
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            ShowEditorPreview();
            return;
        }

        HideDigits();

        if (playOnEnable)
        {
            Invoke(nameof(Replay), delay);
        }
    }

    private void Update()
    {
        if (!replayWhenDateChanges || !_hasFinished)
        {
            return;
        }

        DateTime now = DateTime.Now;
        if (now.Date != _displayedDate.Date)
        {
            Replay();
        }
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(Replay));

        if (_animation != null)
        {
            StopCoroutine(_animation);
            _animation = null;
        }
    }

    /// <summary>현재 날짜를 다시 읽고 토네이도 애니메이션을 처음부터 재생합니다.</summary>
    [ContextMenu("Replay Date Tornado")]
    public void Replay()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        CancelInvoke(nameof(Replay));
        CreateDigitsIfNeeded();
        CalculateTargets();
        SetDate(DateTime.Now);

        if (!Application.isPlaying)
        {
            SnapToTargets();
            return;
        }

        if (_animation != null)
        {
            StopCoroutine(_animation);
        }

        _animation = StartCoroutine(AnimateDigits());
    }

    private void CreateDigitsIfNeeded()
    {
        for (int i = 0; i < DigitCount; i++)
        {
            if (_digits[i] != null)
            {
                ApplyTextStyle(_digits[i]);
                continue;
            }

            Transform existingChild = transform.Find($"Date Digit {i + 1}");
            if (existingChild != null &&
                existingChild.TryGetComponent(out TextMeshProUGUI existingDigit))
            {
                _digits[i] = existingDigit;
                ApplyTextStyle(_digits[i]);
                continue;
            }

            var digitObject = new GameObject(
                $"Date Digit {i + 1}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

            RectTransform digitRect = digitObject.GetComponent<RectTransform>();
            digitRect.SetParent(transform, false);
            digitRect.anchorMin = new Vector2(0.5f, 0.5f);
            digitRect.anchorMax = new Vector2(0.5f, 0.5f);
            digitRect.pivot = new Vector2(0.5f, 0.5f);
            digitRect.sizeDelta = new Vector2(fontSize * 1.15f, fontSize * 1.25f);

            _digits[i] = digitObject.GetComponent<TextMeshProUGUI>();
            ApplyTextStyle(_digits[i]);
        }
    }

    private void ApplyTextStyle(TextMeshProUGUI digit)
    {
        if (fontAsset != null)
        {
            digit.font = fontAsset;
        }

        digit.fontSize = fontSize;
        digit.color = digitColor;
        digit.alignment = TextAlignmentOptions.Center;
        digit.enableWordWrapping = false;
        digit.raycastTarget = false;
        digit.rectTransform.sizeDelta = new Vector2(fontSize * 1.15f, fontSize * 1.25f);
    }

    private void CalculateTargets()
    {
        float halfDigitGap = digitSpacing * 0.5f;
        float halfRowGap = rowSpacing * 0.5f;

        _targets[0] = new Vector2(-halfDigitGap, halfRowGap);
        _targets[1] = new Vector2(halfDigitGap, halfRowGap);
        _targets[2] = new Vector2(-halfDigitGap, -halfRowGap);
        _targets[3] = new Vector2(halfDigitGap, -halfRowGap);
    }

    private void SetDate(DateTime date)
    {
        _displayedDate = date;
        string dateDigits = date.ToString("MMdd");

        for (int i = 0; i < DigitCount; i++)
        {
            _digits[i].text = dateDigits[i].ToString();
        }
    }

    private IEnumerator AnimateDigits()
    {
        _hasFinished = false;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsed += deltaTime;
            float time = Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < DigitCount; i++)
            {
                float staggerStart = i * digitStagger;
                float localTime = Mathf.InverseLerp(staggerStart, 1f, time);
                AnimateDigit(i, localTime);
            }

            yield return null;
        }

        SnapToTargets();
        _hasFinished = true;
        _animation = null;
    }

    private void AnimateDigit(int index, float time)
    {
        float curveProgress = settleCurve == null
            ? Mathf.SmoothStep(0f, 1f, time)
            : settleCurve.Evaluate(time);
        float settled = SmootherStep(Mathf.Clamp01(curveProgress));
        float remaining = 1f - settled;

        float angle = (time * turns * Mathf.PI * 2f) +
                      (index * Mathf.PI * 2f / DigitCount);
        float radius = startRadius * remaining;
        float fallingHeight = Mathf.Lerp(startHeight, 0f, settled);

        // 타원형 궤도와 아래로 좁아지는 움직임을 합쳐 토네이도 실루엣을 만듭니다.
        var vortexPosition = new Vector2(
            Mathf.Cos(angle) * radius,
            Mathf.Sin(angle) * radius * 0.42f + fallingHeight);

        RectTransform rect = _digits[index].rectTransform;
        rect.anchoredPosition = Vector2.Lerp(vortexPosition, _targets[index], settled);

        // 남아 있는 전체 회전각도 함께 감쇠시켜 마지막 프레임의 각도 스냅을 없앱니다.
        float rawRotation = -angle * Mathf.Rad2Deg - (remaining * 540f);
        float rotation = rawRotation * remaining * remaining;
        rect.localRotation = Quaternion.Euler(0f, 0f, rotation);

        float pulse = 1f + Mathf.Sin(angle * 1.4f) * 0.14f * remaining;
        float scale = Mathf.Lerp(0.58f, 1f, settled) * pulse;
        rect.localScale = Vector3.one * scale;

        Color color = digitColor;
        color.a *= Mathf.Lerp(0.18f, 1f, settled);
        _digits[index].color = color;
    }

    private static float SmootherStep(float value)
    {
        // 시작과 끝에서 속도와 가속도가 모두 0이 되어 부드럽게 이어집니다.
        return value * value * value * (value * (value * 6f - 15f) + 10f);
    }

    private void SnapToTargets()
    {
        for (int i = 0; i < DigitCount; i++)
        {
            if (_digits[i] == null)
            {
                continue;
            }

            RectTransform rect = _digits[i].rectTransform;
            rect.anchoredPosition = _targets[i];
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            _digits[i].color = digitColor;
        }
    }

    private void HideDigits()
    {
        _hasFinished = false;

        for (int i = 0; i < DigitCount; i++)
        {
            if (_digits[i] == null)
            {
                continue;
            }

            Color hiddenColor = digitColor;
            hiddenColor.a = 0f;
            _digits[i].color = hiddenColor;
        }
    }

    private void ShowEditorPreview()
    {
        if (Application.isPlaying)
        {
            return;
        }

        CreateDigitsIfNeeded();
        CalculateTargets();
        SetDate(DateTime.Now);
        SnapToTargets();
    }

    private void OnValidate()
    {
        delay = Mathf.Max(0f, delay);
        fontSize = Mathf.Max(1f, fontSize);
        duration = Mathf.Max(0.1f, duration);
        startRadius = Mathf.Max(0f, startRadius);
        startHeight = Mathf.Max(0f, startHeight);
        turns = Mathf.Max(0f, turns);
        digitSpacing = Mathf.Max(0f, digitSpacing);
        rowSpacing = Mathf.Max(0f, rowSpacing);

        if (!Application.isPlaying && isActiveAndEnabled)
        {
            ShowEditorPreview();
        }
    }
}
