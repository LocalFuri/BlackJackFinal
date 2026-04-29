using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blackjack
{
    /// <summary>
    /// Manages chip selection from the chip tray and placement into the bet area.
    /// Same-kind chips stack vertically; different kinds sit in separate columns.
    /// When a stack's total value reaches the next chip's denomination the stack
    /// is automatically converted to one chip of that higher value (cascading).
    /// </summary>
    public class ChipBetting : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // Nested types
        // ──────────────────────────────────────────────────────────────────────

        [System.Serializable]
        public class ChipType
        {
            [Tooltip("Monetary value of this chip.")]
            public int value;

            [Tooltip("Sprite used when placing this chip in the bet area.")]
            public Sprite sprite;

            [Tooltip("Tray button the player clicks to add this chip.")]
            public Button sourceButton;

            [Tooltip("How many chips of this type are needed to auto-upgrade to the next. -1 = no upgrade.")]
            public int upgradeAt = -1;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────────────────────────────────

        [Header("Chip Types (lowest → highest value)")]
        [SerializeField] private List<ChipType> chipTypes = new();

        [Header("References")]
        [SerializeField] private Transform betArea;
        [SerializeField] private BlackjackGame blackjackGame;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private SoundEntry chipSound;
        [SerializeField] private TextMeshProUGUI betSumLabel;

        [Header("Layout")]
        [Tooltip("Horizontal spacing between chip type columns.")]
        [SerializeField] private float columnSpacing = 70f;

        [Tooltip("Vertical pixel offset per stacked chip within a column.")]
        [SerializeField] private float stackOffsetY = 6f;

        [Tooltip("Uniform scale applied to each bet chip image.")]
        [SerializeField] private float betChipScale = 1.2f;

        [Tooltip("Size of each chip image rect in the bet area.")]
        [SerializeField] private Vector2 chipSize = new(60f, 60f);

        // ──────────────────────────────────────────────────────────────────────
        // Events
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fired whenever the bet amount changes.
        /// The argument is the signed delta (positive = chip added, negative = chip removed).
        /// </summary>
        public event Action<int> OnBetChanged;

        // ──────────────────────────────────────────────────────────────────────
        // State
        // ──────────────────────────────────────────────────────────────────────

        // Column order — chip type indices in the order they first appeared
        private readonly List<int> _columnOrder = new();

        // Placed GameObjects per chip type index
        private readonly Dictionary<int, List<GameObject>> _stacks = new();

        // Chip count per chip type index
        private readonly Dictionary<int, int> _chipCounts = new();

        // ──────────────────────────────────────────────────────────────────────
        // Unity lifecycle
        // ──────────────────────────────────────────────────────────────────────

        private void Start()
        {
            for (int i = 0; i < chipTypes.Count; i++)
            {
                int index = i;
                if (chipTypes[i].sourceButton == null) continue;

                // Left click — add chip
                chipTypes[i].sourceButton.onClick.AddListener(() => OnChipClicked(index));

                // Right click — remove top chip of this type from the bet area
                EventTrigger trigger = chipTypes[i].sourceButton.gameObject
                    .GetComponent<EventTrigger>() ?? chipTypes[i].sourceButton.gameObject
                    .AddComponent<EventTrigger>();

                var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                entry.callback.AddListener(data =>
                {
                    var pointerData = (PointerEventData)data;
                    if (pointerData.button == PointerEventData.InputButton.Right)
                        OnChipRightClicked(index);
                });
                trigger.triggers.Add(entry);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Public API
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Total monetary value of all chips currently in the bet area.</summary>
        public int TotalBet
        {
            get
            {
                int total = 0;
                foreach (KeyValuePair<int, int> kvp in _chipCounts)
                {
                    if (kvp.Key >= 0 && kvp.Key < chipTypes.Count)
                        total += chipTypes[kvp.Key].value * kvp.Value;
                }
                return total;
            }
        }

        /// <summary>
        /// Places one chip of the lowest available denomination into the bet area
        /// and fires <see cref="OnBetChanged"/>. Used as a minimum-bet fallback.
        /// </summary>
        public void PlaceSmallestChip()
        {
            if (chipTypes.Count == 0) return;

            int typeIndex = 0;
            chipSound.Play(audioSource);
            PlaceChip(typeIndex);
            CheckUpgrade(typeIndex);
            OnBetChanged?.Invoke(chipTypes[typeIndex].value);
            RefreshBetLabel();
        }

        /// <summary>Removes all chips from the bet area and resets state.</summary>
        public void ClearBetArea()
        {
            int refund = TotalBet;

            foreach (KeyValuePair<int, List<GameObject>> kvp in _stacks)
                foreach (GameObject go in kvp.Value)
                    if (go != null) Destroy(go);

            _stacks.Clear();
            _columnOrder.Clear();
            _chipCounts.Clear();

            if (refund != 0)
                OnBetChanged?.Invoke(-refund);

            RefreshBetLabel();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Chip placement
        // ──────────────────────────────────────────────────────────────────────

        private void OnChipClicked(int typeIndex)
        {
            if (typeIndex < 0 || typeIndex >= chipTypes.Count) return;

            if (blackjackGame != null)
            {
                if (blackjackGame.IsRoundOver)
                    blackjackGame.PrepareForBetting();
                else if (!blackjackGame.IsBettingAllowed)
                    return;
            }

            int chipValue = chipTypes[typeIndex].value;
            chipSound.Play(audioSource);
            PlaceChip(typeIndex);
            CheckUpgrade(typeIndex);
            OnBetChanged?.Invoke(chipValue);
            RefreshBetLabel();
        }

        private void OnChipRightClicked(int typeIndex)
        {
            if (typeIndex < 0 || typeIndex >= chipTypes.Count) return;

            if (blackjackGame != null)
            {
                if (blackjackGame.IsRoundOver)
                    blackjackGame.PrepareForBetting();
                else if (!blackjackGame.IsBettingAllowed)
                    return;
            }

            if (!_stacks.ContainsKey(typeIndex) || _stacks[typeIndex].Count == 0) return;

            int chipValue = chipTypes[typeIndex].value;
            chipSound.Play(audioSource);
            RemoveTopChips(typeIndex, 1);
            OnBetChanged?.Invoke(-chipValue);
            RefreshBetLabel();
        }

        /// <summary>Places one chip of the given type into the bet area.</summary>
        private void PlaceChip(int typeIndex)
        {
            bool isNewColumn = !_columnOrder.Contains(typeIndex);
            EnsureTracking(typeIndex);

            List<GameObject> stack = _stacks[typeIndex];
            int col = _columnOrder.IndexOf(typeIndex);

            GameObject chipGO = CreateChipGO(typeIndex, col, stack.Count);
            stack.Add(chipGO);
            _chipCounts[typeIndex]++;

            // Always recentre when a new column is added (sort may have shifted others)
            if (isNewColumn)
                RecenterAllColumns();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Auto-upgrade logic
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Checks whether the stack for the given chip type has reached its upgrade
        /// threshold. If so, removes those chips and places one of the next type,
        /// then recursively checks the next type for further upgrades.
        /// </summary>
        private void CheckUpgrade(int typeIndex)
        {
            if (typeIndex < 0 || typeIndex >= chipTypes.Count) return;

            ChipType ct = chipTypes[typeIndex];
            if (ct.upgradeAt <= 0) return;

            int nextIndex = typeIndex + 1;
            if (nextIndex >= chipTypes.Count) return;

            if (!_chipCounts.TryGetValue(typeIndex, out int count)) return;
            if (count < ct.upgradeAt) return;

            // Remove the required number of lower chips
            RemoveTopChips(typeIndex, ct.upgradeAt);

            // Place one chip of the next denomination
            PlaceChip(nextIndex);

            // Cascade: the next type might also now be eligible for an upgrade
            CheckUpgrade(nextIndex);
        }

        /// <summary>Destroys the top <paramref name="count"/> chips of a stack and updates tracking.</summary>
        private void RemoveTopChips(int typeIndex, int count)
        {
            if (!_stacks.TryGetValue(typeIndex, out List<GameObject> stack)) return;

            int removeCount = Mathf.Min(count, stack.Count);
            for (int i = 0; i < removeCount; i++)
            {
                int last = stack.Count - 1;
                if (stack[last] != null) Destroy(stack[last]);
                stack.RemoveAt(last);
            }

            _chipCounts[typeIndex] = Mathf.Max(0, _chipCounts[typeIndex] - removeCount);

            // Remove the column entirely if the stack is now empty
            if (stack.Count == 0)
            {
                _columnOrder.Remove(typeIndex);
                _stacks.Remove(typeIndex);
                _chipCounts.Remove(typeIndex);
                RecenterAllColumns();
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────

        private void EnsureTracking(int typeIndex)
        {
            if (_columnOrder.Contains(typeIndex)) return;

            _columnOrder.Add(typeIndex);
            _columnOrder.Sort(); // chipTypes is ordered low→high, so index order = value order
            _stacks[typeIndex] = new List<GameObject>();
            _chipCounts[typeIndex] = 0;
        }

        private GameObject CreateChipGO(int typeIndex, int col, int stackHeight)
        {
            float x = chipSize.x * 0.5f + col * columnSpacing;
            float y = stackHeight * stackOffsetY;

            GameObject go = new($"BetChip_{chipTypes[typeIndex].value}_{stackHeight}");
            go.transform.SetParent(betArea, worldPositionStays: false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.sizeDelta = chipSize;
            rt.localScale = Vector3.one * betChipScale;
            rt.anchoredPosition = new Vector2(x, y);

            Image img = go.AddComponent<Image>();
            img.sprite = chipTypes[typeIndex].sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;

            return go;
        }

        private void RecenterAllColumns()
        {
            for (int col = 0; col < _columnOrder.Count; col++)
            {
                int typeIndex = _columnOrder[col];
                if (!_stacks.TryGetValue(typeIndex, out List<GameObject> stack)) continue;

                float x = chipSize.x * 0.5f + col * columnSpacing;
                for (int s = 0; s < stack.Count; s++)
                {
                    if (stack[s] == null) continue;
                    stack[s].GetComponent<RectTransform>().anchoredPosition = new Vector2(x, s * stackOffsetY);
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Label
        // ──────────────────────────────────────────────────────────────────────

        private static readonly System.Globalization.CultureInfo GermanCulture =
            System.Globalization.CultureInfo.GetCultureInfo("de-DE");

        /// <summary>Updates the bet sum label to reflect the current total bet value.</summary>
        private void RefreshBetLabel()
        {
            if (betSumLabel == null) return;
            //betSumLabel.text = $"Bet: € {((decimal)TotalBet).ToString("N2", GermanCulture)}";
            betSumLabel.text = $"Bet: € {TotalBet}";
    }
    }
}
