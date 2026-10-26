using UnityEngine;
using UnityEngine.UI;

public class TileView : MonoBehaviour
{
    private Image _image;
    private TMPro.TextMeshProUGUI _text;
    private BoardRenderer _boardRenderer;
    private RectTransform _rectTransform;

    private Vector2 _targetPosition;
    private Vector3 _targetScale;
    private bool _isAnimating;
    public bool MergedFromSelf { get; set; }

    public void Initialize(Image image, TMPro.TextMeshProUGUI text, BoardRenderer boardRenderer)
    {
        _image = image;
        _text = text;
        _boardRenderer = boardRenderer;
        _rectTransform = GetComponent<RectTransform>();
        _targetScale = Vector3.one;
        transform.localScale = Vector3.one;
    }

    public void SetPosition(Vector2 pos)
    {
        _rectTransform.anchoredPosition = pos;
        _targetPosition = pos;
    }

    public void UpdateDisplay(int value)
    {
        _image.color = BoardRenderer.GetTileColor(value);
        _text.color = BoardRenderer.GetTextColor(value);
        _text.fontSize = BoardRenderer.GetFontSize(value, _boardRenderer.CellSize);
        _text.text = FormatValue(value);
    }

    private string FormatValue(int value)
    {
        if (value >= 10000000) return (value / 1000000f).ToString("0.#") + "M";
        if (value >= 10000) return (value / 1000f).ToString("0.#") + "K";
        return value.ToString();
    }

    public void AnimateMove(Vector2 targetPos, float duration)
    {
        _targetPosition = targetPos;
        if (_isAnimating)
            StopCoroutine(nameof(MoveCoroutine));
        StartCoroutine(MoveCoroutine(targetPos, duration));
    }

    private System.Collections.IEnumerator MoveCoroutine(Vector2 targetPos, float duration)
    {
        _isAnimating = true;
        Vector2 startPos = _rectTransform.anchoredPosition;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Min(elapsed / duration, 1);
            t = t * t * (3f - 2f * t); // smoothstep
            _rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }

        _rectTransform.anchoredPosition = targetPos;
        _isAnimating = false;
    }

    public void AnimateSpawn(float duration)
    {
        transform.localScale = Vector3.zero;
        StartCoroutine(SpawnCoroutine(duration));
    }

    private System.Collections.IEnumerator SpawnCoroutine(float duration)
    {
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Min(elapsed / duration, 1);
            t = Mathf.SmoothStep(0, 1, t);
            float scale = Mathf.Lerp(0, 1, t);
            transform.localScale = new Vector3(scale, scale, scale);
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    public void AnimateMerge(float duration)
    {
        StartCoroutine(MergeCoroutine(duration));
    }

    private System.Collections.IEnumerator MergeCoroutine(float duration)
    {
        float halfDuration = duration / 2f;
        float elapsed = 0;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Min(elapsed / halfDuration, 1);
            float scale = Mathf.Lerp(1, 1.2f, t);
            transform.localScale = new Vector3(scale, scale, scale);
            yield return null;
        }

        elapsed = 0;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Min(elapsed / halfDuration, 1);
            float scale = Mathf.Lerp(1.2f, 1, t);
            transform.localScale = new Vector3(scale, scale, scale);
            yield return null;
        }

        transform.localScale = Vector3.one;
    }
}