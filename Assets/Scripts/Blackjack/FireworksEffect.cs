using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Blackjack
{
    /// <summary>
    /// Spawns simple procedural particle bursts on the canvas for a blackjack win celebration.
    /// Fully self-contained — no particle system assets required.
    /// </summary>
    public class FireworksEffect : MonoBehaviour
    {
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private int    burstCount     = 6;
        [SerializeField] private int    particlesPerBurst = 18;
        [SerializeField] private float  burstInterval  = 0.22f;
        [SerializeField] private float  particleLifetime = 1.1f;
        [SerializeField] private float  spread         = 220f;

        private static readonly Color[] ParticleColors =
        {
            new Color(1f,  0.9f, 0.1f),
            new Color(1f,  0.3f, 0.3f),
            new Color(0.3f,1f,  0.3f),
            new Color(0.3f,0.6f,1f),
            new Color(1f,  0.6f,0.1f),
            new Color(0.9f,0.3f,1f),
        };

        /// <summary>Plays a multi-burst firework sequence at the given canvas position.</summary>
        public void Play(Vector2 anchoredCenter)
        {
            StartCoroutine(FireworksSequence(anchoredCenter));
        }

        private IEnumerator FireworksSequence(Vector2 center)
        {
            for (int b = 0; b < burstCount; b++)
            {
                Vector2 offset = new Vector2(
                    Random.Range(-spread * 0.5f, spread * 0.5f),
                    Random.Range(-spread * 0.25f, spread * 0.25f));
                SpawnBurst(center + offset);
                yield return new WaitForSeconds(burstInterval);
            }
        }

        private void SpawnBurst(Vector2 position)
        {
            Color burstColor = ParticleColors[Random.Range(0, ParticleColors.Length)];

            for (int i = 0; i < particlesPerBurst; i++)
            {
                GameObject pGO = new GameObject("FW_Particle");
                pGO.transform.SetParent(targetCanvas.transform, false);

                RectTransform rt = pGO.AddComponent<RectTransform>();
                rt.sizeDelta        = new Vector2(8f, 8f);
                rt.anchoredPosition = position;

                Image img = pGO.AddComponent<Image>();
                img.color = burstColor;

                float angle    = (i / (float)particlesPerBurst) * 360f * Mathf.Deg2Rad;
                float speed    = Random.Range(80f, 200f);
                Vector2 dir    = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                StartCoroutine(AnimateParticle(rt, img, dir * speed));
            }
        }

        private IEnumerator AnimateParticle(RectTransform rt, Image img, Vector2 velocity)
        {
            float elapsed  = 0f;
            Color startCol = img.color;

            while (elapsed < particleLifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / particleLifetime;

                rt.anchoredPosition += velocity * Time.deltaTime;
                velocity.y          -= 180f * Time.deltaTime; // gravity

                img.color = new Color(startCol.r, startCol.g, startCol.b, 1f - t);
                rt.localScale = Vector3.one * (1f - t * 0.5f);

                yield return null;
            }

            Destroy(rt.gameObject);
        }
    }
}
