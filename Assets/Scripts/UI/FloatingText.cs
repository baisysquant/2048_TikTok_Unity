using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 飘字提示系统（对齐微信版 showHint / pushFloatText）：
/// 提示在棋盘中心行出现 → 上浮 → 在棋盘顶部淡出；同时显示多条时自动错开高度。
/// </summary>
public class FloatingText : MonoBehaviour
{
    private Canvas _canvas;
    private float _startY;
    private float _rise;
    private readonly List<TMPro.TextMeshProUGUI> _active = new List<TMPro.TextMeshProUGUI>();

    public void Initialize(Canvas canvas, float startY, float rise)
    {
        _canvas = canvas;
        _startY = startY;
        _rise = rise;
    }

    public void Show(string text, float duration = 1.5f, int fontSize = 34)
    {
        if (_canvas == null || string.IsNullOrEmpty(text)) return;

        // 同时显示多条时，起始 y 向上错开，避免重叠
        int offset = Mathf.Min(3, _active.Count) * 36;
        float y = _startY + offset;

        var go = new GameObject("FloatText", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        go.transform.SetParent(_canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(_canvas.GetComponent<RectTransform>().rect.width - 40, 80);

        var tmp = go.GetComponent<TMPro.TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = new Color(0.85f, 0.30f, 0.22f);   // 红字（微信版统一红色提示）
        tmp.horizontalAlignment = TMPro.HorizontalAlignmentOptions.Center;
        tmp.verticalAlignment = TMPro.VerticalAlignmentOptions.Middle;
        tmp.raycastTarget = false;

        rt.anchoredPosition = new Vector2(0, y);
        _active.Add(tmp);
        StartCoroutine(FloatCoroutine(rt, tmp, y, duration));
    }

    private IEnumerator FloatCoroutine(RectTransform rt, TMPro.TextMeshProUGUI tmp, float startY, float duration)
    {
        float elapsed = 0;
        var cg = tmp.canvasRenderer;
        // 淡入
        while (elapsed < 0.18f)
        {
            elapsed += Time.unscaledDeltaTime;
            tmp.color = SetAlpha(tmp.color, Mathf.Clamp01(elapsed / 0.18f));
            yield return null;
        }
        // 上浮 + 尾段淡出
        float riseStart = elapsed;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01((elapsed - riseStart) / (duration - riseStart));
            float e = 1 - Mathf.Pow(1 - t, 3);   // easeOutCubic
            rt.anchoredPosition = new Vector2(0, startY + _rise * e);
            // 最后 22% 淡出
            float alpha = 1;
            if (elapsed > duration * 0.78f)
                alpha = 1 - Mathf.Clamp01((elapsed - duration * 0.78f) / (duration * 0.22f));
            tmp.color = SetAlpha(tmp.color, alpha);
            yield return null;
        }
        _active.Remove(tmp);
        if (rt != null) Destroy(rt.gameObject);
    }

    private static Color SetAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }

    public void ClearAll()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var t = _active[i];
            if (t != null && t.gameObject != null)
                Destroy(t.gameObject);
        }
        _active.Clear();
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}
