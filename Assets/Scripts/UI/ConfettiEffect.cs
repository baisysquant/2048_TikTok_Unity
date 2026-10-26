using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class ConfettiEffect : MonoBehaviour
{
    private Canvas _canvas;
    private List<RectTransform> _pieces;
    private List<Vector2> _velocities;
    private List<float> _rotations;
    private List<float> _rotationSpeeds;
    private List<RectTransform> _pool;

    private static readonly Color[] Colors = {
        new Color(0.93f, 0.89f, 0.85f),
        new Color(0.95f, 0.69f, 0.47f),
        new Color(0.96f, 0.58f, 0.39f),
        new Color(0.96f, 0.37f, 0.23f),
        new Color(0.93f, 0.81f, 0.45f),
        new Color(0.93f, 0.76f, 0.14f),
        new Color(1f, 0.47f, 0.78f),
        new Color(0.47f, 0.78f, 1f),
    };

    public void Initialize(Canvas canvas)
    {
        _canvas = canvas;
        _pool = new List<RectTransform>();
    }

    public void Play(int count = 50)
    {
        Stop();
        _pieces = new List<RectTransform>();
        _velocities = new List<Vector2>();
        _rotations = new List<float>();
        _rotationSpeeds = new List<float>();

        var canvasRect = _canvas.GetComponent<RectTransform>().rect;
        float width = canvasRect.width;
        float height = canvasRect.height;

        for (int i = 0; i < count; i++)
        {
            var rt = GetFromPool();
            float size = Random.Range(6f, 14f);
            rt.sizeDelta = new Vector2(size, size * Random.Range(0.5f, 1.5f));
            rt.anchoredPosition = new Vector2(
                Random.Range(-width * 0.45f, width * 0.45f),
                Random.Range(-20f, 20f)
            );
            rt.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

            var img = rt.GetComponent<Image>();
            img.color = Colors[Random.Range(0, Colors.Length)];
            img.enabled = true;

            _pieces.Add(rt);
            _velocities.Add(new Vector2(
                Random.Range(-120f, 120f),
                Random.Range(200f, 450f)
            ));
            _rotations.Add(Random.Range(0f, 360f));
            _rotationSpeeds.Add(Random.Range(-360f, 360f));
        }

        StartCoroutine(AnimateConfetti(height));
    }

    public void Stop()
    {
        StopAllCoroutines();
        if (_pieces != null)
        {
            foreach (var p in _pieces)
            {
                if (p != null && p.gameObject != null)
                    ReturnToPool(p);
            }
        }
    }

    private RectTransform GetFromPool()
    {
        for (int i = _pool.Count - 1; i >= 0; i--)
        {
            if (_pool[i] != null && _pool[i].gameObject != null)
            {
                var rt = _pool[i];
                _pool.RemoveAt(i);
                rt.gameObject.SetActive(true);
                return rt;
            }
        }

        var go = new GameObject("Confetti", typeof(Image));
        var newRt = go.GetComponent<RectTransform>();
        newRt.SetParent(_canvas.transform, false);
        newRt.anchorMin = new Vector2(0.5f, 1);
        newRt.anchorMax = new Vector2(0.5f, 1);
        return newRt;
    }

    private void ReturnToPool(RectTransform rt)
    {
        if (rt == null || rt.gameObject == null) return;
        rt.gameObject.SetActive(false);
        var img = rt.GetComponent<Image>();
        if (img != null) img.enabled = false;
        _pool.Add(rt);
    }

    private IEnumerator AnimateConfetti(float screenHeight)
    {
        float gravity = -600f;
        float duration = 3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float dt = Time.deltaTime;
            elapsed += dt;

            for (int i = 0; i < _pieces.Count; i++)
            {
                if (_pieces[i] == null) continue;

                _velocities[i] = new Vector2(
                    _velocities[i].x + Random.Range(-20f, 20f) * dt,
                    _velocities[i].y + gravity * dt
                );

                var pos = _pieces[i].anchoredPosition;
                pos += _velocities[i] * dt;
                _pieces[i].anchoredPosition = pos;

                _rotations[i] += _rotationSpeeds[i] * dt;
                _pieces[i].rotation = Quaternion.Euler(0, 0, _rotations[i]);

                float alpha = Mathf.Clamp01(1f - (elapsed / duration));
                var img = _pieces[i].GetComponent<Image>();
                if (img != null)
                {
                    var c = img.color;
                    img.color = new Color(c.r, c.g, c.b, alpha);
                }
            }

            yield return null;
        }

        Stop();
    }
}
