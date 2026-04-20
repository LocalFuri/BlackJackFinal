using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Blackjack
{
    /// <summary>
    /// Central game controller. Manages game state, deal flow, dealer AI, and UI updates.
    /// </summary>
    public class BlackjackGame : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────────
        // Inspector References
        // ──────────────────────────────────────────────────────────────────────────

        [Header("Registry")]
        [SerializeField] private CardSpriteRegistry spriteRegistry;

        [Header("Layout - Player")]
        [SerializeField] private Transform playerCardArea;

        [Header("Layout - Dealer")]
        [SerializeField] private Transform dealerCardArea;

        [Header("Card Prefab")]
        [SerializeField] private GameObject cardViewPrefab;

        [Header("Buttons")]
        [SerializeField] private Button dealButton;
        [SerializeField] private Button hitButton;
        [SerializeField] private Button standButton;
        [SerializeField] private Button surrenderButton;
        [SerializeField] private Button splitButton;

        [Header("Score Labels")]
        [SerializeField] private TextMeshProUGUI playerScoreLabel;
        [SerializeField] private TextMeshProUGUI dealerScoreLabel;

        [Header("Status")]
        [SerializeField] private TextMeshProUGUI statusLabel;

        [Header("Effects")]
        [SerializeField] private FireworksEffect fireworks;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip startupSound;
        [SerializeField] private AudioClip winSound;
        [SerializeField] private AudioClip naturalBlackjackSound;
        [SerializeField] private AudioClip loseSound;
        [SerializeField] private AudioClip tieSound;
        [SerializeField] private AudioClip dealCardSound;

        [Header("Timing")]
        [SerializeField] private float dealDelay        = 0.45f;
        [SerializeField] private float dealerPauseDelay = 0.7f;

        // ──────────────────────────────────────────────────────────────────────────
        // Constants
        // ──────────────────────────────────────────────────────────────────────────

        private const int AutoStandThreshold = 16;
        private const int BlackjackValue     = 21;

        // ──────────────────────────────────────────────────────────────────────────
        // State
        // ──────────────────────────────────────────────────────────────────────────

        private readonly Deck _deck          = new();
        private readonly Hand _playerHand    = new();
        private readonly Hand _dealerHand    = new();
        private readonly Hand _splitHand     = new();

        private readonly List<CardView> _playerCardViews = new();
        private readonly List<CardView> _splitCardViews  = new();
        private readonly List<CardView> _dealerCardViews = new();

        private CardView _dealerHoleCardView;

        private bool _forcePlayerBlackjack;
        private bool _forceSplitHand;
        private bool _isSplitRound;
        private int  _activeHandIndex; // 0 = player, 1 = split

        private Hand           ActiveHand  => _activeHandIndex == 0 ? _playerHand  : _splitHand;
        private List<CardView> ActiveViews => _activeHandIndex == 0 ? _playerCardViews : _splitCardViews;

        private enum GameState { Idle, PlayerTurn, DealerTurn, RoundOver }
        private GameState _state = GameState.Idle;

        // ──────────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ──────────────────────────────────────────────────────────────────────────

        private void Start()
        {
            if (startupSound != null && audioSource != null)
                audioSource.PlayOneShot(startupSound);

            _deck.Build();
            SetButtonState(dealEnabled: true, actionEnabled: false, splitEnabled: false);
            SetStatus("Press Deal to start.");
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Button Handlers
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>Starts a new round.</summary>
        public void OnDeal()
        {
            if (_state != GameState.Idle) return;
            StartCoroutine(DealRound());
        }

        /// <summary>Player draws another card.</summary>
        public void OnHit()
        {
            if (_state != GameState.PlayerTurn) return;
            StartCoroutine(PlayerHit());
        }

        /// <summary>Player ends their turn; advances to split hand or dealer turn.</summary>
        public void OnStand()
        {
            if (_state != GameState.PlayerTurn) return;
            StartCoroutine(AdvanceOrDealerTurn());
        }

        /// <summary>Player surrenders — forfeits half their bet and ends the round immediately.</summary>
        public void OnSurrender()
        {
            if (_state != GameState.PlayerTurn) return;
            StartCoroutine(PlayerSurrender());
        }

        private IEnumerator PlayerSurrender()
        {
            _state = GameState.RoundOver;
            SetButtonState(dealEnabled: false, actionEnabled: false, splitEnabled: false);

            yield return StartCoroutine(RevealHoleCard());
            UpdateScoreLabels(revealDealer: true);

            PlayLoseSound();
            SetStatus("Surrendered. You recover half your bet.");

            yield return StartCoroutine(EndRound());
        }

        /// <summary>
        /// Splits the current two-card hand. Only available when both initial cards share the same rank.
        /// The split card moves to splitCardArea; each hand then receives one additional card stacked on top.
        /// </summary>
        public void OnSplit()
        {
            if (_state != GameState.PlayerTurn) return;
            if (!CanSplit()) return;
            StartCoroutine(PerformSplit());
        }

        /// <summary>Forces the next deal to give the player a natural blackjack, then starts the round.</summary>
        public void OnBlackjackTest()
        {
            if (_state != GameState.Idle) return;
            _forcePlayerBlackjack = true;
            StartCoroutine(DealRound());
        }

        /// <summary>Forces the next deal to give the player two 5s, enabling the split button.</summary>
        public void OnSplitTest()
        {
            if (_state != GameState.Idle) return;
            _forceSplitHand = true;
            StartCoroutine(DealRound());
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Round Flow
        // ──────────────────────────────────────────────────────────────────────────

        private IEnumerator DealRound()
        {
            _state = GameState.PlayerTurn;
            SetButtonState(dealEnabled: false, actionEnabled: false, splitEnabled: false);

            _deck.Build();

            if (_forcePlayerBlackjack) { _deck.ForcePlayerBlackjack(); _forcePlayerBlackjack = false; }
            if (_forceSplitHand)       { _deck.ForceSplitHand();       _forceSplitHand       = false; }

            ClearTable();
            SetStatus("Dealing...");

            yield return StartCoroutine(DealCardTo(_playerHand, _playerCardViews, playerCardArea, faceUp: true));
            yield return StartCoroutine(DealCardTo(_dealerHand, _dealerCardViews, dealerCardArea, faceUp: true));
            yield return StartCoroutine(DealCardTo(_playerHand, _playerCardViews, playerCardArea, faceUp: true));
            yield return StartCoroutine(DealCardTo(_dealerHand, _dealerCardViews, dealerCardArea, faceUp: false));

            _dealerHoleCardView = _dealerCardViews[^1];
            UpdateScoreLabels(revealDealer: false);

            // ── Natural blackjack check ──
            bool playerBJ = _playerHand.IsBlackjack();
            bool dealerBJ = _dealerHand.IsBlackjack();

            if (playerBJ || dealerBJ)
            {
                yield return StartCoroutine(RevealHoleCard());
                UpdateScoreLabels(revealDealer: true);

                if (playerBJ && dealerBJ)  { PlayTieSound();   SetStatus("Both Blackjack! Push."); }
                else if (playerBJ)         { ApplyBlackjackGlow(); fireworks.Play(Vector2.zero); PlayNaturalBlackjackSound(); SetStatus("Blackjack! Player wins!"); }
                else                       { PlayLoseSound();   SetStatus("Dealer Blackjack! Dealer wins."); }

                yield return StartCoroutine(EndRound());
                yield break;
            }

            // ── Player turn ──
            SetButtonState(dealEnabled: false, actionEnabled: true, splitEnabled: CanSplit());
            SetStatus($"Your turn. Score: {_playerHand.BestValue()}");

            if (_playerHand.BestValue() >= AutoStandThreshold)
            {
                yield return new WaitForSeconds(0.3f);
                yield return StartCoroutine(DealerTurn());
            }
        }

        // ── Split ─────────────────────────────────────────────────────────────────

        private bool CanSplit() =>
            !_isSplitRound
            && _playerHand.Count == 2
            && _playerHand.Cards[0].Rank == _playerHand.Cards[1].Rank;

        private IEnumerator PerformSplit()
        {
            _isSplitRound = true;
            SetButtonState(dealEnabled: false, actionEnabled: false, splitEnabled: false);

            // Move card[1] from player hand to split hand
            CardData movedCard = _playerHand.Cards[1];
            _playerHand.RemoveAt(1);

            CardView movedView = _playerCardViews[1];
            _playerCardViews.RemoveAt(1);

            // Move card[1] to player area as a split card
            movedView.transform.SetParent(playerCardArea, worldPositionStays: true);
            _splitCardViews.Add(movedView);
            _splitHand.AddCard(movedCard);

            yield return new WaitForSeconds(0.3f);

            bool isAces = _playerHand.Cards[0].Rank == Rank.Ace;

            // Deal second card to each hand — stacked on top of the existing card
            yield return StartCoroutine(DealCardTo(_playerHand,  _playerCardViews, playerCardArea, faceUp: true));
            yield return StartCoroutine(DealCardTo(_splitHand,   _splitCardViews,  playerCardArea,  faceUp: true));

            UpdateScoreLabels(revealDealer: false);
            _activeHandIndex = 0;

            if (isAces)
            {
                SetStatus("Split Aces — one card each. Standing.");
                yield return new WaitForSeconds(0.5f);
                yield return StartCoroutine(DealerTurn());
                yield break;
            }

            SetButtonState(dealEnabled: false, actionEnabled: true, splitEnabled: false);
            SetStatus($"Hand 1. Score: {ActiveHand.BestValue()}");

            if (ActiveHand.BestValue() >= AutoStandThreshold)
            {
                yield return new WaitForSeconds(0.3f);
                yield return StartCoroutine(AdvanceOrDealerTurn());
            }
        }

        private IEnumerator AdvanceOrDealerTurn()
        {
            if (_isSplitRound && _activeHandIndex == 0)
            {
                _activeHandIndex = 1;
                UpdateScoreLabels(revealDealer: false);
                SetStatus($"Hand 2. Score: {ActiveHand.BestValue()}");
                SetButtonState(dealEnabled: false, actionEnabled: true, splitEnabled: false);

                if (ActiveHand.BestValue() >= AutoStandThreshold)
                {
                    yield return new WaitForSeconds(0.3f);
                    yield return StartCoroutine(DealerTurn());
                }
            }
            else
            {
                yield return StartCoroutine(DealerTurn());
            }
        }

        private IEnumerator PlayerHit()
        {
            SetButtonState(dealEnabled: false, actionEnabled: false, splitEnabled: false);

            yield return StartCoroutine(DealCardTo(ActiveHand, ActiveViews, playerCardArea, faceUp: true));
            UpdateScoreLabels(revealDealer: false);

            int score = ActiveHand.BestValue();

            if (score > BlackjackValue)
            {
                string label = _isSplitRound ? $"Hand {_activeHandIndex + 1} busts" : "Bust";
                SetStatus($"{label}! You scored {score}.");
                PlayLoseSound();

                if (_isSplitRound && _activeHandIndex == 0)
                {
                    yield return new WaitForSeconds(0.5f);
                    yield return StartCoroutine(AdvanceOrDealerTurn());
                }
                else
                {
                    yield return StartCoroutine(RevealHoleCard());
                    yield return StartCoroutine(EndRound());
                }
                yield break;
            }

            if (score == BlackjackValue || score >= AutoStandThreshold)
            {
                yield return new WaitForSeconds(0.25f);
                yield return StartCoroutine(AdvanceOrDealerTurn());
                yield break;
            }

            SetButtonState(dealEnabled: false, actionEnabled: true, splitEnabled: false);
            SetStatus(_isSplitRound
                ? $"Hand {_activeHandIndex + 1}. Score: {score}"
                : $"Your turn. Score: {score}");
        }

        private IEnumerator DealerTurn()
        {
            _state = GameState.DealerTurn;
            SetButtonState(dealEnabled: false, actionEnabled: false, splitEnabled: false);

            yield return StartCoroutine(RevealHoleCard());
            UpdateScoreLabels(revealDealer: true);

            SetStatus("Dealer's turn...");
            yield return new WaitForSeconds(dealerPauseDelay);

            while (_dealerHand.BestValue() < AutoStandThreshold)
            {
                yield return StartCoroutine(DealCardTo(_dealerHand, _dealerCardViews, dealerCardArea, faceUp: true));
                UpdateScoreLabels(revealDealer: true);
                yield return new WaitForSeconds(dealerPauseDelay);
            }

            yield return StartCoroutine(ResolveRound());
        }

        private IEnumerator ResolveRound()
        {
            int dealerScore = _dealerHand.BestValue();
            bool dealerBust = dealerScore > BlackjackValue;

            if (_isSplitRound)
            {
                var    results = new List<string>();
                bool   anyWin  = false;
                bool   anyLoss = false;

                Hand[]   hands  = { _playerHand, _splitHand };
                string[] labels = { "Hand 1", "Hand 2" };

                for (int i = 0; i < hands.Length; i++)
                {
                    int s = hands[i].BestValue();
                    if      (s > BlackjackValue)            { results.Add($"{labels[i]}: Bust"); anyLoss = true; }
                    else if (dealerBust || s > dealerScore) { results.Add($"{labels[i]}: Win");  anyWin  = true; }
                    else if (s < dealerScore)               { results.Add($"{labels[i]}: Loss"); anyLoss = true; }
                    else                                    { results.Add($"{labels[i]}: Push"); }
                }

                if (anyWin)       PlayWinSound();
                else if (anyLoss) PlayLoseSound();
                else              PlayTieSound();

                SetStatus(string.Join("  |  ", results));
            }
            else
            {
                int p = _playerHand.BestValue();
                if      (dealerBust)         { PlayWinSound();  SetStatus($"Dealer busts at {dealerScore}! You win!"); }
                else if (p > dealerScore)    { PlayWinSound();  SetStatus($"You win! {p} vs {dealerScore}."); }
                else if (dealerScore > p)    { PlayLoseSound(); SetStatus($"Dealer wins. {dealerScore} vs {p}."); }
                else                         { PlayTieSound();  SetStatus($"Push! Both scored {p}."); }
            }

            yield return StartCoroutine(EndRound());
        }

        private IEnumerator EndRound()
        {
            _state = GameState.RoundOver;
            yield return new WaitForSeconds(1.5f);
            _state = GameState.Idle;
            SetButtonState(dealEnabled: true, actionEnabled: false, splitEnabled: false);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Card Dealing
        // ──────────────────────────────────────────────────────────────────────────

        private IEnumerator DealCardTo(Hand hand, List<CardView> views, Transform area, bool faceUp)
        {
            yield return new WaitForSeconds(dealDelay);

            CardData card = _deck.Draw();
            hand.AddCard(card);

            if (dealCardSound != null && audioSource != null)
                audioSource.PlayOneShot(dealCardSound);

            CardView view = SpawnCardView(card, area, faceUp);

            // Split hand: place each new card exactly below the first split card.
            if (views == _splitCardViews && views.Count > 0)
            {
                RectTransform firstRt = views[0].GetComponent<RectTransform>();
                RectTransform rt      = view.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(
                    firstRt.anchoredPosition.x,
                    firstRt.anchoredPosition.y - firstRt.rect.height * (float)views.Count
                );
            }

            views.Add(view);
        }

        private CardView SpawnCardView(CardData card, Transform parent, bool faceUp)
        {
            GameObject go   = Instantiate(cardViewPrefab, parent);
            CardView   view = go.GetComponent<CardView>();
            view.Setup(
                spriteRegistry.GetSprite(card),
                spriteRegistry.GetBackSprite(),
                faceUp
            );
            return view;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Hole Card
        // ──────────────────────────────────────────────────────────────────────────

        private IEnumerator RevealHoleCard()
        {
            if (_dealerHoleCardView == null || _dealerHoleCardView.IsFaceUp)
                yield break;

            bool done = false;
            _dealerHoleCardView.Flip(toFaceUp: true, () => done = true);
            yield return new WaitUntil(() => done);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Glow Effect
        // ──────────────────────────────────────────────────────────────────────────

        private void ApplyBlackjackGlow()
        {
            foreach (CardView v in _playerCardViews)
                v.StartGlowPulse();
        }

        // ──────────────────────────────────────────────────────────────────────────
        // UI Helpers
        // ──────────────────────────────────────────────────────────────────────────

        private void UpdateScoreLabels(bool revealDealer)
        {
            if (_isSplitRound)
            {
                int p1 = _playerHand.BestValue();
                int p2 = _splitHand.BestValue();
                string s1 = p1 > BlackjackValue ? "Bust" : p1.ToString();
                string s2 = p2 > BlackjackValue ? "Bust" : p2.ToString();
                playerScoreLabel.text = $"Player: {s1} / {s2}";
            }
            else
            {
                playerScoreLabel.text = $"Player: {_playerHand.BestValue()}";
            }

            if (revealDealer)
                dealerScoreLabel.text = $"Dealer: {_dealerHand.BestValue()}";
            else
            {
                int visibleValue = _dealerHand.Cards.Count > 0
                    ? _dealerHand.Cards[0].BlackjackValue
                    : 0;
                dealerScoreLabel.text = $"Dealer: {visibleValue} + ?";
            }
        }

        private void SetStatus(string message) => statusLabel.text = message;

        /// <summary>Plays the win sound if both clip and source are assigned.</summary>
        private void PlayWinSound()
        {
            if (winSound != null && audioSource != null)
                audioSource.PlayOneShot(winSound);
        }

        /// <summary>Plays the natural blackjack sound if assigned, otherwise falls back to win sound.
        /// Stops all player card glow pulses once the clip finishes.</summary>
        private void PlayNaturalBlackjackSound()
        {
            AudioClip clip = naturalBlackjackSound != null ? naturalBlackjackSound : winSound;
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
                StartCoroutine(StopGlowAfterClip(clip.length));
            }
        }

        private IEnumerator StopGlowAfterClip(float duration)
        {
            yield return new WaitForSeconds(duration);
            foreach (CardView v in _playerCardViews) v.StopGlowPulse();
            foreach (CardView v in _splitCardViews)  v.StopGlowPulse();
        }

        /// <summary>Plays the lose sound if both clip and source are assigned.</summary>
        private void PlayLoseSound()
        {
            if (loseSound != null && audioSource != null)
                audioSource.PlayOneShot(loseSound);
        }

        /// <summary>Plays the tie sound if both clip and source are assigned.</summary>
        private void PlayTieSound()
        {
            if (tieSound != null && audioSource != null)
                audioSource.PlayOneShot(tieSound);
        }

        private void SetButtonState(bool dealEnabled, bool actionEnabled, bool splitEnabled)
        {
            dealButton.interactable      = dealEnabled;
            hitButton.interactable       = actionEnabled;
            standButton.interactable     = actionEnabled;
            surrenderButton.interactable = actionEnabled;
            if (splitButton != null)
                splitButton.interactable = splitEnabled;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Table Clear
        // ──────────────────────────────────────────────────────────────────────────

        private void ClearTable()
        {
            _playerHand.Clear();
            _splitHand.Clear();
            _dealerHand.Clear();
            _dealerHoleCardView = null;
            _isSplitRound    = false;
            _activeHandIndex = 0;

            foreach (CardView v in _playerCardViews) if (v != null) Destroy(v.gameObject);
            _playerCardViews.Clear();

            foreach (CardView v in _splitCardViews)  if (v != null) Destroy(v.gameObject);
            _splitCardViews.Clear();

            foreach (CardView v in _dealerCardViews) if (v != null) Destroy(v.gameObject);
            _dealerCardViews.Clear();
        }
    }
}
