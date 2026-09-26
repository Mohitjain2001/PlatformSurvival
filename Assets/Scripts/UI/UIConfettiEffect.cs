using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIConfettiEffect : MonoBehaviour
{
    private struct ConfettiParticle
    {
        public RectTransform rt;
        public Image img;
        public Vector2 velocity;
        public float rotationSpeed;
        public float swayFrequency;
        public float swayAmplitude;
        public float timeOffset;
    }

    private List<ConfettiParticle> particles = new List<ConfettiParticle>();
    private bool isEmitting = false;

    private readonly Color[] confettiColors = new Color[]
    {
        new Color(0.98f, 0.70f, 0.15f), // Gold
        new Color(0.88f, 0.38f, 0.48f), // Coral/Pink
        new Color(0.20f, 0.75f, 0.95f), // Cyan
        new Color(0.35f, 0.85f, 0.40f), // Lime
        new Color(0.90f, 0.30f, 0.90f)  // Purple
    };

    public void PlayConfetti(Transform parentCanvasTransform, bool loop = false)
    {
        StopAllCoroutines();
        ClearParticles();
        StartCoroutine(ConfettiRoutine(parentCanvasTransform, loop));
    }

    public void ClearParticles()
    {
        isEmitting = false;
        foreach (var p in particles)
        {
            if (p.rt != null && p.rt.gameObject != null)
            {
                Destroy(p.rt.gameObject);
            }
        }
        particles.Clear();
    }

    private IEnumerator ConfettiRoutine(Transform parent, bool loop)
    {
        isEmitting = true;

        // Create 65 colorful confetti particles
        for (int i = 0; i < 65; i++)
        {
            GameObject confettiObj = new GameObject($"Confetti_{i}");
            confettiObj.transform.SetParent(parent, false);

            RectTransform rt = confettiObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1.0f);
            rt.anchorMax = new Vector2(0.5f, 1.0f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            float startX = Random.Range(-480f, 480f);
            float startY = Random.Range(20f, 400f);
            rt.anchoredPosition = new Vector2(startX, startY);

            float width = Random.Range(16f, 30f);
            float height = Random.Range(12f, 22f);
            rt.sizeDelta = new Vector2(width, height);

            Image img = confettiObj.AddComponent<Image>();
            img.color = confettiColors[Random.Range(0, confettiColors.Length)];

            ConfettiParticle p = new ConfettiParticle
            {
                rt = rt,
                img = img,
                velocity = new Vector2(Random.Range(-50f, 50f), Random.Range(-380f, -650f)),
                rotationSpeed = Random.Range(-300f, 300f),
                swayFrequency = Random.Range(2.5f, 5.5f),
                swayAmplitude = Random.Range(40f, 90f),
                timeOffset = Random.Range(0f, 10f)
            };

            particles.Add(p);
        }

        float elapsed = 0f;
        while (isEmitting && (loop || elapsed < 6.0f))
        {
            elapsed += Time.deltaTime;

            for (int i = 0; i < particles.Count; i++)
            {
                var p = particles[i];
                if (p.rt == null) continue;

                Vector2 pos = p.rt.anchoredPosition;
                pos.y += p.velocity.y * Time.deltaTime;
                pos.x += Mathf.Sin((elapsed + p.timeOffset) * p.swayFrequency) * p.swayAmplitude * Time.deltaTime;

                if (pos.y < -1200f)
                {
                    if (loop)
                    {
                        pos.y = Random.Range(20f, 400f);
                        pos.x = Random.Range(-480f, 480f);
                    }
                }

                p.rt.anchoredPosition = pos;
                p.rt.Rotate(0, 0, p.rotationSpeed * Time.deltaTime);

                if (!loop && elapsed > 4.5f)
                {
                    Color col = p.img.color;
                    col.a = Mathf.Lerp(1.0f, 0f, (elapsed - 4.5f) / 1.5f);
                    p.img.color = col;
                }
            }

            yield return null;
        }

        ClearParticles();
    }
}
