using UnityEngine;
using TMPro;

namespace Blackjack
{
    /// <summary>Pulses a TextMeshProUGUI label color between its original color and white to indicate the active hand.</summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class ScoreLabelPulse : MonoBehaviour
    {
        private const float PulseSpeed = 1.5f; //2f orig

        private TextMeshProUGUI _label;
        private Color _originalColor;
        private bool _isPulsing;
        private float _time;

        private void Awake()
        {
            _label = GetComponent<TextMeshProUGUI>();
            _originalColor = _label.color;
        }

        private void Update()
        {
            if (!_isPulsing) return;

            _time += Time.deltaTime;
            float t = (Mathf.Sin(_time * PulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            _label.color = Color.Lerp(_originalColor, Color.white, t);
        }

        /// <summary>Starts pulsing the label color between original and white.</summary>
        public void StartPulse()
        {
            _isPulsing = true;
            _time = 0f;
        }

        /// <summary>Stops pulsing and resets to the original color.</summary>
        public void StopPulse()
        {
            _isPulsing = false;
            _time = 0f;

            if (_label != null)
                _label.color = _originalColor;
        }
    }
}
