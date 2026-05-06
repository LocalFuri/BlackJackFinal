using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Blackjack
{
    /// <summary>
    /// Controls a single card UI element: face/back display, flip animation, and bloom glow effect.
    /// Flip animation animates sizeDelta.x so it works correctly inside a UI Canvas.
    /// Glow uses a UIGlowBloom material (additive, HDR) to feed into URP post-processing Bloom.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class CardView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image cardImage;
        [SerializeField] private Image glowImage;

        [Header("Flip Animation")]
        [SerializeField] private float flipDuration = 3.5f; //default was 0.35f


        [Header("Bloom Pulse")]
        [SerializeField] private float breatheSpeed  = 0.7f;   // slow base rhythm (Hz)
        [SerializeField] private float flickerSpeed  = 4.5f;   // Perlin noise scroll speed
        [SerializeField] private float flickerWeight = 0.35f;  // flicker contribution (0–1)
        [SerializeField] private float pulseMinAlpha = 0.15f;
        [SerializeField] private float pulseMaxAlpha = 0.88f;

        private Sprite    _faceSprite;
        private Sprite    _backSprite;
        private bool      _isFaceUp;

        private Coroutine _flipCoroutine;
        private Coroutine _glowCoroutine;

        // Unique per-instance Perlin offset so cards don't pulse in sync
        private float _glowNoiseOffset;

        private void Awake()
        {
            if (cardImage == null) cardImage = GetComponent<Image>();
            _glowNoiseOffset = Random.Range(0f, 100f);
        }

        /// <summary>Initialises card with face and back sprites.</summary>
        public void Setup(Sprite faceSprite, Sprite backSprite, bool faceUp = true)
        {
            _faceSprite      = faceSprite;
            _backSprite      = backSprite;
            _isFaceUp        = faceUp;
            cardImage.sprite = faceUp ? _faceSprite : _backSprite;
            SetGlow(false);
        }

        // ── Flip ─────────────────────────────────────────────────────────────────

        /// <summary>Plays a squash-and-stretch flip animation, swapping the sprite at the midpoint.</summary>
        public void Flip(bool toFaceUp, System.Action onComplete = null)
        {
            if (_flipCoroutine != null) StopCoroutine(_flipCoroutine);
            _flipCoroutine = StartCoroutine(FlipRoutine(toFaceUp, onComplete));
        }

        private IEnumerator FlipRoutine(bool toFaceUp, System.Action onComplete)
        {
            RectTransform rt      = GetComponent<RectTransform>();
            Vector3       origScale = rt.localScale;
            float         half    = flipDuration * 0.5f;
            float         elapsed = 0f;

            // Phase 1: squash X scale to zero
            while (elapsed < half)
            {
                elapsed      += Time.deltaTime;
                float t       = Mathf.Clamp01(elapsed / half);
                rt.localScale = new Vector3(origScale.x * (1f - t), origScale.y, 1f);
                yield return null;
            }
            rt.localScale = new Vector3(0f, origScale.y, 1f);

            // Swap sprite at mid-point
            _isFaceUp        = toFaceUp;
            cardImage.sprite = toFaceUp ? _faceSprite : _backSprite;

            elapsed = 0f;

            // Phase 2: expand X scale back to original
            while (elapsed < half)
            {
                elapsed      += Time.deltaTime;
                float t       = Mathf.Clamp01(elapsed / half);
                rt.localScale = new Vector3(origScale.x * t, origScale.y, 1f);
                yield return null;
            }
            rt.localScale = origScale;

            onComplete?.Invoke();
        }

        // ── Bloom Glow ────────────────────────────────────────────────────────────

        /// <summary>Enables or disables a static glow. Stops any active pulse.</summary>
        public void SetGlow(bool enabled)
        {
            StopGlowPulse();
            if (glowImage == null) return;
            glowImage.enabled = enabled;
            glowImage.color   = new Color(3f, 2.7f, 0.5f, enabled ? pulseMaxAlpha : 0f);
        }

        /// <summary>Starts the organic HDR bloom pulse coroutine.</summary>
        public void StartGlowPulse()
        {
            if (glowImage == null) return;
            StopGlowPulse();
            glowImage.enabled = true;
            _glowCoroutine    = StartCoroutine(GlowPulseRoutine());
        }

        /// <summary>Stops the bloom pulse and hides the glow image.</summary>
        public void StopGlowPulse()
        {
            if (_glowCoroutine != null)
            {
                StopCoroutine(_glowCoroutine);
                _glowCoroutine = null;
            }
            if (glowImage != null)
                glowImage.enabled = false;
        }

        private IEnumerator GlowPulseRoutine()
        {
            float time = 0f;
            while (true)
            {
                time += Time.deltaTime;

                // Slow sine "breathe" — organic base rhythm
                float breathe = (Mathf.Sin(time * breatheSpeed * Mathf.PI * 2f) + 1f) * 0.5f;

                // Fast Perlin "flicker" — non-repeating irregularity, unique per card
                float flicker = Mathf.PerlinNoise(time * flickerSpeed + _glowNoiseOffset, 0f);

                float t     = Mathf.Lerp(breathe, flicker, flickerWeight);
                float alpha = Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, t);

                // HDR colors: values > 1.0 write into the HDR framebuffer and trigger URP Bloom.
                // Trough = warm orange (~1.5× LDR), Peak = intense bright gold (~4× LDR).
                Color color = Color.Lerp(
                    new Color(1.5f, 0.65f, 0.05f, alpha),
                    new Color(4.0f, 3.5f,  0.6f,  alpha),
                    t);

                glowImage.color = color;
                yield return null;
            }
        }

        public bool IsFaceUp => _isFaceUp;
    }
}

