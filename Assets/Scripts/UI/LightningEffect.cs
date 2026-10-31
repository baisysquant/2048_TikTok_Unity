using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 糖果紫"颗粒气雾"特效：从起点（魔法少女）喷出柔和半透明紫色颗粒流，
/// 沿路径飘向终点（MOVES 按钮）。视觉柔和不生硬：无锐利线段，颗粒径向渐变、
/// 低透明度、带横向摆动与雾感膨胀，起终点光晕 + 终点光爆脉冲。
/// 纯 UI 层，不拦截点击。
/// </summary>
public class LightningEffect : MonoBehaviour
{
    // ===== 糖果系紫色调 =====
    private static readonly Color CorePurple = new Color(195f / 255f, 155f / 255f, 255f / 255f);   // 亮紫
    private static readonly Color DeepPurple = new Color(168f / 255f, 126f / 255f, 240f / 255f);  // 深紫
    private static readonly Color WhiteCore = new Color(1f, 0.98f, 1f);

    private static Sprite _dotSprite;
    private GameObject _fromGlow, _hitGlow, _hitBurst, _hitBurst2;

    public static LightningEffect Play(Transform canvas, Vector2 from, Vector2 to)
    {
        var go = new GameObject("LightningFx");
        go.transform.SetParent(canvas, false);
        var fx = go.AddComponent<LightningEffect>();
        fx.StartCoroutine(fx.Run(from, to));
        return fx;
    }

    private IEnumerator Run(Vector2 from, Vector2 to)
    {
        _fromGlow = CreateGlow(from, CorePurple, 80f, 130f);        // 起手柔光球（魔法少女处，深紫可辨）
        _hitGlow = CreateGlow(to, CorePurple, 130f, 190f);          // 终点常驻光晕（MOVES 处，更亮更大）

        // ===== 阶段一：颗粒气雾喷涌（无锐线，持续发射柔和圆点颗粒） =====
        float mist = 0f;
        while (mist < 0.5f)
        {
            mist += Time.deltaTime;
            SpawnMist(from, to, mist < 0.12f ? 5 : 3);   // 起始略密集，营造喷发感
            yield return null;
        }

        // ===== 阶段二：终点光爆（双层叠加更明显）+ 更多扩散微粒 + MOVES 框闪烁 =====
        SpawnParticles(to, 40);
        _hitBurst = CreateGlow(to, WhiteCore, 26f, 430f);       // 白芯光爆
        _hitBurst2 = CreateGlow(to, CorePurple, 60f, 320f);     // 第二层紫色光爆，叠加更醒目
        if (_moveCountText != null)
        {
            var targetRT = _moveCountText;
            var targetParent = targetRT.parent;
            var targetImg = targetParent != null ? targetParent.GetComponent<Image>() : null;
            if (targetImg != null) FlashTarget(targetImg, targetRT);   // MOVES 框亮闪，明显可辨
            StartCoroutine(PulseUi(targetRT));
        }

        // ===== 阶段三：起终点光晕淡出 =====
        float fade = 0f;
        while (fade < 0.55f)
        {
            fade += Time.deltaTime;
            if (_fromGlow != null && _fromGlow.activeSelf)
                SetAlpha(_fromGlow, Mathf.Lerp(0.9f, 0f, fade / 0.5f));
            if (_hitGlow != null)
                SetAlpha(_hitGlow, Mathf.Lerp(0.95f, 0f, fade / 0.55f));
            if (_hitBurst != null && _hitBurst.activeSelf)
            {
                var rt = _hitBurst.GetComponent<RectTransform>();
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 2.4f, fade / 0.5f);
                SetAlpha(_hitBurst, Mathf.Lerp(0.95f, 0f, fade / 0.5f));
            }
            if (_hitBurst2 != null && _hitBurst2.activeSelf)
            {
                var rt2 = _hitBurst2.GetComponent<RectTransform>();
                rt2.localScale = Vector3.one * Mathf.Lerp(1f, 2.2f, fade / 0.5f);
                SetAlpha(_hitBurst2, Mathf.Lerp(0.9f, 0f, fade / 0.5f));
            }
            yield return null;
        }

        // ===== 阶段四：等尾部颗粒自然淡出后销毁 =====
        yield return new WaitForSeconds(0.9f);
        Destroy(gameObject);
    }

    // ===== 颗粒气雾 =====

    /// <summary>从起点喷出 count 个柔和圆点颗粒，沿路径飘向终点</summary>
    private void SpawnMist(Vector2 from, Vector2 to, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 start = from + Random.insideUnitCircle * Random.Range(3f, 30f);   // 源头小逸散
            float speed = Random.Range(300f, 560f);          // 沿路径流速
            float swayAmp = Random.Range(16f, 52f);          // 横向摆动幅度
            float swayFreq = Random.Range(2.5f, 5.5f);
            float phase = Random.value * Mathf.PI * 2f;
            float life = Random.Range(0.75f, 1.15f);
            float size = Random.Range(14f, 34f);
            Color col = Color.Lerp(DeepPurple, CorePurple, Random.Range(0f, 0.6f));   // 深紫系，与淡紫棋盘区分
            float alpha = 0.89f;                                   // 高不透明度 → 气雾清晰
            StartCoroutine(MistParticle(start, to, speed, swayAmp, swayFreq, phase, life, size, col, alpha));
        }
    }

    private IEnumerator MistParticle(Vector2 start, Vector2 to, float speed, float swayAmp, float swayFreq,
                                     float phase, float life, float size, Color color, float maxAlpha)
    {
        var go = new GameObject("Mist", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.position = start;
        rt.sizeDelta = new Vector2(size, size);
        var img = go.GetComponent<Image>();
        img.sprite = GetDot();
        img.color = color;
        img.raycastTarget = false;

        Vector2 dir = (to - start).normalized;
        Vector2 norm = new Vector2(-dir.y, dir.x);
        Vector2 p = start;
        float t = 0f;
        float fadeIn = 0.16f;
        while (t < life && rt != null)
        {
            t += Time.deltaTime;
            float dt = Time.deltaTime;
            p += dir * (speed * dt);
            float sway = Mathf.Sin(t * swayFreq + phase) * swayAmp * 0.5f;
            rt.position = p + norm * sway;

            // 透明度：快速淡入 → 缓慢淡出（尾部更柔）
            var c = img.color;
            if (t < fadeIn)
                c.a = maxAlpha * (t / fadeIn);
            else
            {
                float fo = (t - fadeIn) / (life - fadeIn);
                c.a = maxAlpha * Mathf.Clamp01(1f - fo * fo);
            }
            img.color = c;

            // 雾感膨胀（缓慢变大）
            float s = size * Mathf.Lerp(0.8f, 1.4f, Mathf.Clamp01(t / life));
            rt.sizeDelta = new Vector2(s, s);
            yield return null;
        }
        if (rt != null) Destroy(rt.gameObject);
    }

    // ===== 光球 / 扩散微粒 =====

    private GameObject CreateGlow(Vector2 pos, Color color, float startSize, float endSize)
    {
        var go = new GameObject("Glow", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.position = pos;
        rt.sizeDelta = new Vector2(startSize, startSize);
        var img = go.GetComponent<Image>();
        img.sprite = GetDot();
        img.color = color;
        img.raycastTarget = false;
        StartCoroutine(ScaleGlow(rt, startSize, endSize, 0.5f));
        return go;
    }

    private IEnumerator ScaleGlow(RectTransform rt, float from, float to, float dur)
    {
        float t = 0f;
        while (t < dur && rt != null)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(from, to, t / dur);
            rt.sizeDelta = new Vector2(s, s);
            yield return null;
        }
    }

    private void SpawnParticles(Vector2 origin, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("HaloSpark", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.position = origin;
            float size = Random.Range(10f, 26f);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.GetComponent<Image>();
            img.sprite = GetDot();
            img.color = Color.Lerp(DeepPurple, CorePurple, Random.Range(0f, 0.55f));
            img.raycastTarget = false;
            Vector2 vel = Random.insideUnitCircle.normalized * Random.Range(140f, 440f);
            StartCoroutine(FlyParticle(rt, vel, Random.Range(0.35f, 0.55f), Random.Range(0.5f, 1f)));
        }
    }

    private IEnumerator FlyParticle(RectTransform rt, Vector2 vel, float life, float drag)
    {
        float t = 0f;
        while (t < life && rt != null)
        {
            t += Time.deltaTime;
            vel *= drag;
            rt.position += (Vector3)(vel * Time.deltaTime);
            var img = rt.GetComponent<Image>();
            var c = img.color;
            c.a = Mathf.Lerp(1f, 0f, t / life);
            img.color = c;
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, t / life);
            yield return null;
        }
        if (rt != null) Destroy(rt.gameObject);
    }

    private IEnumerator PulseUi(RectTransform target)
    {
        if (target == null) yield break;
        float t = 0f;
        Vector3 baseS = target.localScale;
        while (t < 0.55f)
        {
            t += Time.deltaTime;
            float k = Mathf.Sin(t * 22f) * 0.09f;   // 脉冲幅度加大，更明显
            target.localScale = baseS * (1f + k);
            yield return null;
        }
        target.localScale = baseS;
    }

    /// <summary>覆盖在目标控件上快速白色闪烁，替代原先仅在周围扩散的微弱光晕</summary>
    private void FlashTarget(Image targetImg, RectTransform targetRT)
    {
        var go = new GameObject("FlashOverlay", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(targetImg.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.sprite = targetImg.sprite;   // 复用同款圆角贴图，罩住整个框
        img.color = new Color(1f, 0.97f, 1f, 0f);
        img.raycastTarget = false;
        _moveCountText.SetAsLastSibling();   // 保证数值文字仍显示在闪光层之上
        StartCoroutine(FlashRoutine(img));
    }

    private IEnumerator FlashRoutine(Image img)
    {
        float t = 0f;
        float dur = 0.5f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float a = Mathf.Abs(Mathf.Sin(t * 26f)) * 0.5f;   // 0~0.5 快速闪烁两下
            var c = img.color;
            c.a = a;
            img.color = c;
            yield return null;
        }
        Destroy(img.gameObject);
    }

    private void SetAlpha(GameObject go, float a)
    {
        if (go == null) return;
        var img = go.GetComponent<Image>();
        var c = img.color;
        c.a = Mathf.Clamp01(a);
        img.color = c;
    }

    /// <summary>柔和径向渐变圆点（二次幂衰减，边缘虚化，气雾感）</summary>
    private static Sprite GetDot()
    {
        if (_dotSprite == null)
        {
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    float dx = (x - 7.5f) / 7.5f;
                    float dy = (y - 7.5f) / 7.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;   // 二次衰减：边缘更柔和
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            _dotSprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 100f);
        }
        return _dotSprite;
    }

    // 由 ScoreDisplay 注入，用于击中脉冲目标
    public RectTransform _moveCountText;
}
