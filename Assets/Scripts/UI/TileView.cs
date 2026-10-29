using UnityEngine;

/// <summary>
/// 3D 方块视图：立体 Cube + 顶面世界空间 TMP 数字。
/// 与 2D 版保持相同动画接口（Move / Spawn / Merge / SetPosition / UpdateDisplay）。
/// </summary>
public class TileView : MonoBehaviour
{
    private Transform _cube;
    private Renderer _cubeRenderer;
    private TMPro.TextMeshPro _text;
    private BoardRenderer _boardRenderer;

    private Vector3 _targetPosition;
    private bool _isAnimating;
    public bool MergedFromSelf { get; set; }

    public void Initialize(Transform cube, TMPro.TextMeshPro text, BoardRenderer boardRenderer)
    {
        _cube = cube;
        _cubeRenderer = cube.GetComponent<Renderer>();
        _text = text;
        _boardRenderer = boardRenderer;
    }

    /// <summary>直接定位到世界坐标（不含动画）</summary>
    public void SetPosition(Vector3 worldPos)
    {
        transform.position = worldPos;
        _targetPosition = worldPos;
    }

    public void UpdateDisplay(int value)
    {
        _cubeRenderer.sharedMaterial = BoardRenderer.GetTileMaterial(value);
        _text.color = BoardRenderer.GetTextColor(value);
        _text.text = FormatValue(value);
        float s = _boardRenderer.GetTextScaleForValue(value);
        _text.transform.localScale = new Vector3(s, s, 1f);
    }

    private static string FormatValue(int value)
    {
        if (value >= 10000000) return (value / 1000000f).ToString("0.#") + "M";
        if (value >= 10000) return (value / 1000f).ToString("0.#") + "K";
        return value.ToString();
    }

    public void AnimateMove(Vector3 targetPos, float duration)
    {
        _targetPosition = targetPos;
        if (_isAnimating)
            StopCoroutine(nameof(MoveCoroutine));
        StartCoroutine(MoveCoroutine(targetPos, duration));
    }

    private System.Collections.IEnumerator MoveCoroutine(Vector3 targetPos, float duration)
    {
        _isAnimating = true;
        Vector3 startPos = transform.position;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Min(elapsed / duration, 1);
            t = t * t * (3f - 2f * t); // smoothstep
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        transform.position = targetPos;
        _isAnimating = false;
    }

    public void AnimateSpawn(float duration)
    {
        transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        StartCoroutine(SpawnCoroutine(duration));
    }

    private System.Collections.IEnumerator SpawnCoroutine(float duration)
    {
        float elapsed = 0;
        Vector3 basePos = transform.position;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Min(elapsed / duration, 1);
            t = Mathf.SmoothStep(0, 1, t);
            float scale = Mathf.Lerp(0.3f, 1f, t);
            transform.localScale = new Vector3(scale, scale, scale);
            // 从棋盘下方升入棋盘面
            transform.position = basePos + Vector3.up * Mathf.Lerp(-0.35f, 0f, t);
            yield return null;
        }
        transform.localScale = Vector3.one;
        transform.position = basePos;
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
            float s = Mathf.Lerp(1f, 1.22f, t);
            transform.localScale = new Vector3(s, s, s);
            yield return null;
        }

        elapsed = 0;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Min(elapsed / halfDuration, 1);
            float s = Mathf.Lerp(1.22f, 1f, t);
            transform.localScale = new Vector3(s, s, s);
            yield return null;
        }

        transform.localScale = Vector3.one;
    }
}
