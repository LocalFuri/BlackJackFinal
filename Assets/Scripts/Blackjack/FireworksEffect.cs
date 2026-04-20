using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Blackjack
{
    /// <summary>
    /// Realistic procedural fireworks on a UI Canvas.
    /// Each firework has three phases: shell rise, burst flash, and particle decay with trailing sparks.
    /// Fully self-contained — no particle system assets required.
    /// </summary>
    public class FireworksEffect : MonoBehaviour
    {
        [Header("Sequence")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private int   burstCount    = 7;
        [SerializeField] private float burstInterval = 0.28f;
        [SerializeField] private float spread        = 260f;

        [Header("Shell")]
        [SerializeField] private float shellRiseDuration = 0.35f;
        [SerializeField] private float shellRiseHeight   = 120f;

        [Header("Burst")]
        [SerializeField] private int   particlesPerBurst = 28;
        [SerializeField] private float particleLifetime  = 1.4f;
        [SerializeField] private float minSpeed          = 120f;
        [SerializeField] private float maxSpeed          = 300f;
        [SerializeField] private float drag              = 2.8f;   // exponential velocity decay
        [SerializeField] private float gravity           = 240f;
        [SerializeField] private float angleJitter       = 14f;    // degrees — breaks perfect radial symmetry

        [Header("Sparks")]
        [SerializeField] private int   sparksPerParticle = 3;
        [SerializeField] private float sparkInterval     = 0.18f;
        [SerializeField] private float sparkLifetime     = 0.35f;
        [SerializeField] private float sparkSize         = 4f;

        // Primary burst colors (paired as primary + secondary for each burst)
        private static readonly Color[] BurstColors =
        {
            new Color(1.00f, 0.90f, 0.10f),  // gold
            new Color(1.00f, 0.28f, 0.28f),  // red
            new Color(0.25f, 1.00f, 0.35f),  // green
            new Color(0.25f, 0.55f, 1.00f),  // blue
            new Color(1.00f, 0.55f, 0.05f),  // orange
            new Color(0.90f, 0.25f, 1.00f),  // purple
            new Color(0.20f, 1.00f, 0.95f),  // cyan
            new Color(1.00f, 1.00f, 1.00f),  // white
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
                Vector2 launchPos = new Vector2(
                    center.x + Random.Range(-spread * 0.5f, spread * 0.5f),
                    center.y + Random.Range(-spread * 0.3f, -spread * 0.05f));
                StartCoroutine(FireShell(launchPos));
                yield return new WaitForSeconds(burstInterval);
            }
        }

        // ── Shell rise ────────────────────────────────────────────────────────────

        private IEnumerator FireShell(Vector2 launchPos)
        {
            // Spawn small shell dot that rises to the burst point
            GameObject shell = CreateParticle("FW_Shell", launchPos, new Vector2(6f, 6f));
            Image shellImg   = shell.GetComponent<Image>();
            shellImg.color   = Color.white;

            Vector2 burstPos = launchPos + new Vector2(0f, shellRiseHeight);
            float elapsed    = 0f;

            while (elapsed < shellRiseDuration)
            {
                elapsed += Time.deltaTime;
                float t  = elapsed / shellRiseDuration;

                shell.GetComponent<RectTransform>().anchoredPosition =
                    Vector2.Lerp(launchPos, burstPos, Mathf.SmoothStep(0f, 1f, t));

                // Flicker trail
                shellImg.color = new Color(1f, 0.85f, 0.4f, Mathf.Lerp(0.9f, 0.3f, t));
                yield return null;
            }

            Destroy(shell);
            SpawnBurst(burstPos);
        }

        // ── Burst ─────────────────────────────────────────────────────────────────

        private void SpawnBurst(Vector2 position)
        {
            // Pick two complementary colors for this burst
            int    colorIndexA = Random.Range(0, BurstColors.Length);
            int    colorIndexB = (colorIndexA + Random.Range(1, 3)) % BurstColors.Length;
            Color  colorA      = BurstColors[colorIndexA];
            Color  colorB      = BurstColors[colorIndexB];

            // Flash — bright white circle that fades instantly
            StartCoroutine(BurstFlash(position, colorA));

            float angleStep = 360f / particlesPerBurst;

            for (int i = 0; i < particlesPerBurst; i++)
            {
                // Jitter the angle so burst isn't perfectly uniform
                float angle  = (angleStep * i + Random.Range(-angleJitter, angleJitter)) * Mathf.Deg2Rad;
                float speed  = Random.Range(minSpeed, maxSpeed);
                Vector2 dir  = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                // Alternate colors between primary and secondary
                Color  col  = (i % 2 == 0) ? colorA : colorB;
                float  size = Random.Range(6f, 11f);

                StartCoroutine(AnimateParticle(position, dir * speed, col, size));
            }
        }

        private IEnumerator BurstFlash(Vector2 position, Color color)
        {
            GameObject flash    = CreateParticle("FW_Flash", position, new Vector2(60f, 60f));
            Image      flashImg = flash.GetComponent<Image>();

            float elapsed = 0f;
            float duration = 0.12f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t  = elapsed / duration;
                flashImg.color = new Color(color.r, color.g, color.b, Mathf.Lerp(0.9f, 0f, t));
                flash.GetComponent<RectTransform>().localScale = Vector3.one * Mathf.Lerp(0.4f, 1.6f, t);
                yield return null;
            }

            Destroy(flash);
        }

        // ── Particle ──────────────────────────────────────────────────────────────

        private IEnumerator AnimateParticle(Vector2 startPos, Vector2 velocity, Color color, float size)
        {
            GameObject  pGO = CreateParticle("FW_Particle", startPos, new Vector2(size, size));
            RectTransform rt  = pGO.GetComponent<RectTransform>();
            Image         img = pGO.GetComponent<Image>();

            float elapsed      = 0f;
            float sparkTimer   = 0f;
            int   sparksSpawned = 0;

            // Initial flash size — briefly larger then shrinks
            float flashScale = Random.Range(1.6f, 2.2f);

            while (elapsed < particleLifetime)
            {
                float dt = Time.deltaTime;
                elapsed     += dt;
                sparkTimer  += dt;
                float t      = elapsed / particleLifetime;

                // Physics: drag + gravity
                velocity   *= Mathf.Exp(-drag * dt);
                velocity.y -= gravity * dt;
                rt.anchoredPosition += velocity * dt;

                // Scale: flash large at start, shrink smoothly
                float scale = Mathf.Lerp(flashScale, 0.2f, Mathf.Pow(t, 0.6f));
                rt.localScale = Vector3.one * scale;

                // Alpha: hold bright for first half, then drop sharply
                float alpha = t < 0.5f
                    ? 1f
                    : Mathf.Pow(1f - ((t - 0.5f) / 0.5f), 1.8f);

                img.color = new Color(color.r, color.g, color.b, alpha);

                // Spawn trailing sparks at intervals
                if (sparkTimer >= sparkInterval && sparksSpawned < sparksPerParticle)
                {
                    sparkTimer = 0f;
                    sparksSpawned++;
                    StartCoroutine(AnimateSpark(rt.anchoredPosition, color));
                }

                yield return null;
            }

            Destroy(pGO);
        }

        // ── Spark ─────────────────────────────────────────────────────────────────

        private IEnumerator AnimateSpark(Vector2 startPos, Color color)
        {
            GameObject    sGO = CreateParticle("FW_Spark", startPos, new Vector2(sparkSize, sparkSize));
            RectTransform rt  = sGO.GetComponent<RectTransform>();
            Image         img = sGO.GetComponent<Image>();

            // Sparks shoot off at a small random angle
            float angle    = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector2 vel    = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(20f, 60f);
            float elapsed  = 0f;

            while (elapsed < sparkLifetime)
            {
                float dt   = Time.deltaTime;
                elapsed   += dt;
                float t    = elapsed / sparkLifetime;

                vel.y -= gravity * 0.5f * dt;
                rt.anchoredPosition += vel * dt;

                img.color     = new Color(1f, Mathf.Lerp(0.9f, 0.3f, t), 0f, 1f - t);
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 0f, t);
                yield return null;
            }

            Destroy(sGO);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private GameObject CreateParticle(string goName, Vector2 anchoredPos, Vector2 size)
        {
            GameObject go = new GameObject(goName);
            go.transform.SetParent(targetCanvas.transform, false);

            RectTransform rt   = go.AddComponent<RectTransform>();
            rt.sizeDelta        = size;
            rt.anchoredPosition = anchoredPos;

            Image img  = go.AddComponent<Image>();
            img.color  = Color.white;
            img.raycastTarget = false;

            return go;
        }
    }
}
