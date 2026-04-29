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
        [SerializeField] private Transform splitCardArea;

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
        [SerializeField] private Button doubleDownButton;

        [Header("Score Labels")]
        [SerializeField] private TextMeshProUGUI playerScoreLabel;
        [SerializeField] private TextMeshProUGUI dealerScoreLabel;

        [Header("Status")]
        [SerializeField] private TextMeshProUGUI statusLabel;

        [Header("Money")]
        [SerializeField] private TextMeshProUGUI playerMoneyLabel;
        [SerializeField] private ChipBetting chipBetting;
        [SerializeField] private int startingMoney = 1000;

        [Header("Effects")]
        [SerializeField] private FireworksEffect fireworks;

        [Header("Audio")] //mark sound
        [SerializeField] private AudioSource audioSource;

    [SerializeField] private SoundEntry cardSlideSound;
    [SerializeField] private SoundEntry cheaterSound;
    [SerializeField] private SoundEntry chipSound;
    [SerializeField] private SoundEntry dealCardSound;
    [SerializeField] private SoundEntry exitSound;
    [SerializeField] private SoundEntry knockSound;
    [SerializeField] private SoundEntry loseSound;
    [SerializeField] private SoundEntry naturalBlackjackSound;
    [SerializeField] private SoundEntry startupSound;
    [SerializeField] private SoundEntry surrenderSound;
    [SerializeField] private SoundEntry tieSound;
    [SerializeField] private SoundEntry winSound;
    [SerializeField] private SoundEntry yuhuSound;
        

        [Header("Timing")]
        [SerializeField] private float dealDelay        = 0.45f;
        [SerializeField] private float dealerPauseDelay = 0.7f;
        [SerializeField] private float endRoundDelay    = 3.0f;
        [SerializeField] private float newRoundPause    = 0.5f;

        // ──────────────────────────────────────────────────────────────────────────
        // Constants
        // ──────────────────────────────────────────────────────────────────────────

        private const int AutoStandHard      = 17;
        private const int AutoStandSoft      = 19;
        private const int AutoHitMaxScore    = 11;
        private const int DealerSoft17       = 17;
        private const int BlackjackValue     = 21;

        private static readonly Color WinStatusColor = new Color(1f, 0f, 0f, 1f);        //gold
        private static readonly Color WinColor = new Color(0f, 1f, 0f, 1f);              //green
        private static readonly Color LoseColor = new Color(1f, 0f, 0f, 1f);             //red
        private static readonly Color PushColor = new Color(0.7f, 0f, 0.7f, 1f);        //magenta
        private static readonly Color SurrenderColor = new Color(0f, 1f, 1f, 1f);       //cyan
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
        private bool _forceBothBlackjack;
        private bool _forceSplitHand;
        private bool _isSplitRound;
        private int  _activeHandIndex; // 0 = player, 1 = split

        private int _doubleDownExtraBet; // extra bet deducted when doubling down

        private decimal _playerMoney; //decimal need for 5 chips / 2 surrendering = 2.5 chips

    private TextMeshProUGUI _splitScoreLabel;
        private Vector2 _defaultPlayerScorePosition;
        private ScoreLabelPulse _playerScorePulse;
        private ScoreLabelPulse _splitScorePulse;
        private Color _defaultStatusColor;

        private Hand           ActiveHand  => _activeHandIndex == 0 ? _playerHand  : _splitHand;
        private List<CardView> ActiveViews => _activeHandIndex == 0 ? _playerCardViews : _splitCardViews;

        private enum GameState { Idle, PlayerTurn, DealerTurn, RoundOver }
        private GameState _state = GameState.Idle;

        /// <summary>True when the player is allowed to place or remove bets (before a round begins).</summary>
        public bool IsBettingAllowed => _state == GameState.Idle;

        /// <summary>True when the current round has ended and the table is showing results.</summary>
        public bool IsRoundOver => _state == GameState.RoundOver;

        /// <summary>
        /// Transitions from RoundOver back to Idle, clearing the table and prompting the player to bet.
        /// Called by ChipBetting when the player clicks a chip after a round ends.
        /// </summary>
        public void PrepareForBetting()
        {
            if (_state != GameState.RoundOver) return;

            foreach (CardView v in _playerCardViews) if (v != null) Destroy(v.gameObject);
            _playerCardViews.Clear();

            foreach (CardView v in _splitCardViews) if (v != null) Destroy(v.gameObject);
            _splitCardViews.Clear();

            foreach (CardView v in _dealerCardViews) if (v != null) Destroy(v.gameObject);
            _dealerCardViews.Clear();

            _playerHand.Clear();
            _splitHand.Clear();
            _dealerHand.Clear();
            _dealerHoleCardView = null;
            _isSplitRound       = false;
            _activeHandIndex    = 0;

            StopAllScorePulses();
            ResetPlayerScoreLabelPosition();
            SetScoreLabelsVisible(false);
            SetStatus("Place your bet");

            _state = GameState.Idle;
        }


        // ──────────────────────────────────────────────────────────────────────────

        private void Start()
        {
            if (startupSound.HasClip && audioSource != null)
                startupSound.Play(audioSource);

            _playerMoney = startingMoney;
            RefreshMoneyLabel();

            if (chipBetting != null)
                chipBetting.OnBetChanged += OnBetChangedHandler;

            _deck.Build();
            _defaultStatusColor = statusLabel.color;
            InitSplitScoreLabel();
            SetScoreLabelsVisible(false);
            SetButtonState(dealEnabled: true, actionEnabled: false, splitEnabled: false);
            SetStatus("Press Deal to start");
        }

        private void OnDestroy()
        {
            if (chipBetting != null)
                chipBetting.OnBetChanged -= OnBetChangedHandler;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Public Audio API
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>Plays the exit sound and returns its length in seconds.</summary>
        public float PlayExitSound()
        {
            exitSound.Play(audioSource);
            return exitSound.Length;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Button Handlers
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>Starts a new round. Ensures a minimum bet of 1 chip and deducts the total bet from the player's balance.</summary>
        public void OnDeal()
        {
            if (_state != GameState.Idle && _state != GameState.RoundOver) return;
            StopBlackjackCelebration();

            if (chipBetting != null && chipBetting.TotalBet <= 0)
                chipBetting.PlaceSmallestChip();

            _playerMoney -= CurrentBet;
            RefreshMoneyLabel();

            _state = GameState.PlayerTurn; // lock betting immediately before coroutine starts
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

            
      surrenderSound.Play(audioSource);
      SetStatus("Surrender returns 1/2 of bet", SurrenderColor);
      ApplyPayout(PayoutResult.Surrender, CurrentBet);

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

        /// <summary>
        /// Doubles down: player receives exactly one more card, then automatically stands.
        /// Only available on the initial two-card hand.
        /// </summary>
        public void OnDoubleDown()
        {
            if (_state != GameState.PlayerTurn) return;
            if (ActiveHand.Cards.Count != 2) return;
            StartCoroutine(PerformDoubleDown());
        }

        /// <summary>Forces the next deal to give the player a natural blackjack, then starts the round.</summary>
        public void OnBlackjackTest()
        {
            if (_state != GameState.Idle && _state != GameState.RoundOver) return;
            StopBlackjackCelebration();
            _state = GameState.Idle;
            _forcePlayerBlackjack = true;
            StartCoroutine(DealRound());
        }

        /// <summary>Forces the next deal to give the player two 5s, enabling the split button.</summary>
        public void OnSplitTest()
        {
            if (_state != GameState.Idle && _state != GameState.RoundOver) return;
            StopBlackjackCelebration();
            _state = GameState.Idle;
            _forceSplitHand = true;
            StartCoroutine(DealRound());
        }

        /// <summary>Forces the next deal to give both player and dealer a natural blackjack, then starts the round.</summary>
        public void OnBothBlackjackTest()
        {
            if (_state != GameState.Idle && _state != GameState.RoundOver) return;
            StopBlackjackCelebration();
            _state = GameState.Idle;
            _forceBothBlackjack = true;
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

            if (_forceBothBlackjack)   { _deck.ForceBothBlackjack();   _forceBothBlackjack   = false; }
            if (_forcePlayerBlackjack) { _deck.ForcePlayerBlackjack(); _forcePlayerBlackjack = false; }
            if (_forceSplitHand)       { _deck.ForceSplitHand();       _forceSplitHand       = false; }

            ClearTable();
            SetStatus("");
            _doubleDownExtraBet = 0;
            yield return new WaitForSeconds(newRoundPause);
            //SetStatus("Dealing...");

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

                if (playerBJ && dealerBJ)  { cheaterSound.Play(audioSource); SetStatus("Push", PushColor); ApplyPayout(PayoutResult.Push, CurrentBet); }
                else if (playerBJ)         { ApplyBlackjackGlow(); fireworks.Play(GetPlayerCardsCenter()); PlayNaturalBlackjackSound(); SetStatus("You win", WinColor); ApplyPayout(PayoutResult.BlackjackWin, CurrentBet); }
                else                       { PlayLoseSound();   SetStatus("You lose", LoseColor); ApplyPayout(PayoutResult.Lose, CurrentBet); }

                yield return StartCoroutine(EndRound());
                yield break;
            }

            // ── Player turn ──
            SetButtonState(dealEnabled: false, actionEnabled: true, splitEnabled: CanSplit(), doubleDownEnabled: CanDoubleDown());
            SetStatus($"Your turn");

            bool hasPair = CanSplit();

            if (!hasPair && _playerHand.BestValue() <= AutoHitMaxScore)
            {
                yield return new WaitForSeconds(0.3f);
                yield return StartCoroutine(AutoHitLoop());
                yield break;
            }

            bool shouldStand = hasPair
                ? _playerHand.BestValue() == 20
                : ShouldAutoStand(_playerHand);

            if (shouldStand)
            {
                knockSound.Play(audioSource);
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

            // Move card[1] to split card area
            movedView.transform.SetParent(splitCardArea, worldPositionStays: false);
            _splitCardViews.Add(movedView);
            _splitHand.AddCard(movedCard);

            cardSlideSound.Play(audioSource);
            yield return new WaitForSeconds(0.5f);

            bool isAces = _playerHand.Cards[0].Rank == Rank.Ace;

            // Deal second card to each hand
            yield return StartCoroutine(DealCardTo(_playerHand,  _playerCardViews, playerCardArea, faceUp: true));
            yield return StartCoroutine(DealCardTo(_splitHand,   _splitCardViews,  splitCardArea,  faceUp: true));

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
            SetStatus($"Players turn Hand 1");

            if (ActiveHand.BestValue() <= AutoHitMaxScore)
            {
                yield return new WaitForSeconds(0.3f);
                yield return StartCoroutine(AutoHitLoop());
                yield break;
            }

            if (ShouldAutoStand(ActiveHand))
            {
                knockSound.Play(audioSource);
                yield return new WaitForSeconds(0.3f);
                yield return StartCoroutine(AdvanceOrDealerTurn());
            }
        }

        // ── Double Down ───────────────────────────────────────────────────────────

        private bool CanDoubleDown() =>
            ActiveHand.Cards.Count == 2 && !_isSplitRound;

        private IEnumerator PerformDoubleDown()
        {
            SetButtonState(dealEnabled: false, actionEnabled: false, splitEnabled: false);
            SetStatus("Double Down!");

            _doubleDownExtraBet = CurrentBet;
            _playerMoney -= _doubleDownExtraBet;
            RefreshMoneyLabel();

            yield return StartCoroutine(
                DealCardTo(ActiveHand, ActiveViews,
                           _activeHandIndex == 0 ? playerCardArea : splitCardArea,
                           faceUp: true));

            UpdateScoreLabels(revealDealer: false);

            if (ActiveHand.IsBust())
            {
                yield return StartCoroutine(RevealHoleCard());
                UpdateScoreLabels(revealDealer: true);
                PlayLoseSound();
                SetStatus($"Busted, LoseColor");
                yield return StartCoroutine(EndRound());
                yield break;
            }

            SetStatus($"Double Down stands at {ActiveHand.BestValue()}.");
            yield return new WaitForSeconds(dealerPauseDelay);
            yield return StartCoroutine(AdvanceOrDealerTurn());
        }

        private IEnumerator AdvanceOrDealerTurn()
        {
            if (_isSplitRound && _activeHandIndex == 0)
            {
                _activeHandIndex = 1;
                UpdateScoreLabels(revealDealer: false);
                SetStatus($"Players turn Hand 2");
                SetButtonState(dealEnabled: false, actionEnabled: true, splitEnabled: false);

                if (ActiveHand.BestValue() <= AutoHitMaxScore)
                {
                    yield return new WaitForSeconds(0.3f);
                    yield return StartCoroutine(AutoHitLoop());
                }
                else if (ShouldAutoStand(ActiveHand))
                {
                    knockSound.Play(audioSource);
                    yield return new WaitForSeconds(0.3f);
                    yield return StartCoroutine(DealerTurn());
                }
            }
            else
            {
                yield return StartCoroutine(DealerTurn());
            }
        }

        /// <summary>Automatically hits until the score exceeds AutoHitMaxScore, then returns control to the player or proceeds with auto-stand logic.</summary>
        private IEnumerator AutoHitLoop()
        {
            while (ActiveHand.BestValue() <= AutoHitMaxScore)
            {
                yield return StartCoroutine(PlayerHit());

                int score = ActiveHand.BestValue();
                if (score > BlackjackValue || score == BlackjackValue || ShouldAutoStand(ActiveHand))
                    yield break;
            }
        }

        private IEnumerator PlayerHit()
        {
            SetButtonState(dealEnabled: false, actionEnabled: false, splitEnabled: false);

            Transform area = (_isSplitRound && _activeHandIndex == 1) ? splitCardArea : playerCardArea;
            yield return StartCoroutine(DealCardTo(ActiveHand, ActiveViews, area, faceUp: true));
            UpdateScoreLabels(revealDealer: false);

            int score = ActiveHand.BestValue();

            if (score > BlackjackValue)
            {
                string label = _isSplitRound ? $"Hand {_activeHandIndex + 1} busts" : "Bust";
                SetStatus($"{label}");
                PlayLoseSound();

                if (_isSplitRound)
                {
                    // Always advance to next hand or dealer turn so both hands get resolved.
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

            if (score == BlackjackValue || ShouldAutoStand(ActiveHand))
            {
                if (score != BlackjackValue)
                    knockSound.Play(audioSource);
                yield return new WaitForSeconds(0.25f);
                yield return StartCoroutine(AdvanceOrDealerTurn());
                yield break;
            }

            if (score <= AutoHitMaxScore)
            {
                yield return new WaitForSeconds(0.3f);
                yield return StartCoroutine(PlayerHit());
                yield break;
            }

            SetButtonState(dealEnabled: false, actionEnabled: true, splitEnabled: false);
            SetStatus(_isSplitRound
                ? $"Players turn Hand 1"
                : $"Your turn");
        }

        private IEnumerator DealerTurn()
        {
            _state = GameState.DealerTurn;
            SetButtonState(dealEnabled: false, actionEnabled: false, splitEnabled: false);
            StopAllScorePulses();

            yield return StartCoroutine(RevealHoleCard());
            UpdateScoreLabels(revealDealer: true);

            // If both split hands busted, skip dealer drawing.
            bool allPlayerHandsBusted = _isSplitRound
                ? _playerHand.IsBust() && _splitHand.IsBust()
                : _playerHand.IsBust();

            if (!allPlayerHandsBusted)
            {
                SetStatus("Dealer's turn");
                yield return new WaitForSeconds(dealerPauseDelay);

                while (ShouldDealerHit())
                {
                    yield return StartCoroutine(DealCardTo(_dealerHand, _dealerCardViews, dealerCardArea, faceUp: true));
                    UpdateScoreLabels(revealDealer: true);
                    yield return new WaitForSeconds(dealerPauseDelay);
                }
            }

            yield return StartCoroutine(ResolveRound());
        }

        /// <summary>
        /// Dealer hits below 17, and also hits on soft 17 (a 17 with an Ace counted as 11).
        /// </summary>
        private bool ShouldDealerHit()
        {
            int value = _dealerHand.BestValue();
            if (value < DealerSoft17) return true;
            if (value == DealerSoft17 && _dealerHand.IsSoft()) return true;
            return false;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Money / Payout
        // ──────────────────────────────────────────────────────────────────────────

        private static readonly System.Globalization.CultureInfo GermanCulture =
            System.Globalization.CultureInfo.GetCultureInfo("de-DE");

        private enum PayoutResult { Win, BlackjackWin, Lose, Push, Surrender }

        /// <summary>
        /// Receives live bet delta from ChipBetting. Money is deducted at deal time,
        /// so this handler only refreshes the label to reflect any UI-only changes.
        /// </summary>
        private void OnBetChangedHandler(int delta)
        {
            RefreshMoneyLabel();
        }

        /// <summary>
        /// Applies end-of-round payout. The bet was deducted at deal time,
        /// so payouts return the appropriate amount to the balance.
        /// Win → bet×2 | BJ → bet×2.5 | Push → bet | Surrender → bet×0.5 | Lose → 0
        /// </summary>
        private void ApplyPayout(PayoutResult result, int bet)
        {
            _playerMoney += result switch
            {
                PayoutResult.Win          => bet * 2m,
                PayoutResult.BlackjackWin => bet * 2.5m,
                PayoutResult.Push         => bet,
                PayoutResult.Surrender    => bet * 0.5m,
                _                         => 0,                   // Lose — bet already gone
            };
            RefreshMoneyLabel();
        }

        private void RefreshMoneyLabel()
        {
            if (playerMoneyLabel == null) return;
            playerMoneyLabel.text = $"€ {((decimal)_playerMoney).ToString("N2", GermanCulture)}";
        }

        private int CurrentBet => chipBetting != null ? chipBetting.TotalBet : 0;

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
                    int handBet = CurrentBet / 2;

          if (s > BlackjackValue)
                    {
                        results.Add(ColorizeText($"{labels[i]}: Bust", LoseColor));
                        anyLoss = true;
                        ApplyPayout(PayoutResult.Lose, handBet);
                    }
                    else if (dealerBust || s > dealerScore)
                    {
                        results.Add(ColorizeText($"{labels[i]}: Win", WinColor));
                        anyWin = true;
                        ApplyPayout(PayoutResult.Win, handBet);
                    }
                    else if (s < dealerScore)
                    {
                        results.Add(ColorizeText($"{labels[i]}: Lose", LoseColor));
                        anyLoss = true;
                        ApplyPayout(PayoutResult.Lose, handBet);
                    }
                    else
                    {
                        results.Add(ColorizeText($"{labels[i]}: Push", PushColor));
                        ApplyPayout(PayoutResult.Push, handBet);
                    }
                }

                if (anyWin)       PlayWinSound();
                else if (anyLoss) PlayLoseSound();
                else              PlayTieSound();

                SetStatus(string.Join("  |  ", results));
            }
            else
            {
                int p = _playerHand.BestValue();
                int totalBet = CurrentBet + _doubleDownExtraBet;
                if      (dealerBust)         { PlayWinSound();  SetStatus($"You win", WinColor);  ApplyPayout(PayoutResult.Win,  totalBet); }
                else if (p > dealerScore)    { PlayWinSound();  SetStatus($"You win", WinColor);  ApplyPayout(PayoutResult.Win,  totalBet); }
                else if (dealerScore > p)    { PlayLoseSound(); SetStatus($"You lose",LoseColor); ApplyPayout(PayoutResult.Lose, totalBet); }
                else                         { PlayTieSound();  SetStatus($"Push",PushColor);     ApplyPayout(PayoutResult.Push, totalBet); }
            }

            yield return StartCoroutine(EndRound());
        }

        private IEnumerator EndRound()
        {
            _state = GameState.RoundOver;
            SetButtonState(dealEnabled: false, actionEnabled: false, splitEnabled: false);
            yield return new WaitForSeconds(endRoundDelay);
            SetButtonState(dealEnabled: true, actionEnabled: false, splitEnabled: false);
            // State stays RoundOver — chip click or Deal press drives the next transition.
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Card Dealing
        // ──────────────────────────────────────────────────────────────────────────

        private IEnumerator DealCardTo(Hand hand, List<CardView> views, Transform area, bool faceUp)
        {
            yield return new WaitForSeconds(dealDelay);

            CardData card = _deck.Draw();
            hand.AddCard(card);

            if (dealCardSound.HasClip && audioSource != null)
                dealCardSound.Play(audioSource);

            CardView view = SpawnCardView(card, area, faceUp);

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

        /// <summary>Stops fireworks, all audio, and card glow pulses from a blackjack celebration.</summary>
        private void StopBlackjackCelebration()
        {
            fireworks.Stop();

            if (audioSource != null)
                audioSource.Stop();

            foreach (CardView v in _playerCardViews)
                v.StopGlowPulse();
        }

        // ──────────────────────────────────────────────────────────────────────────
        // UI Helpers
        // ──────────────────────────────────────────────────────────────────────────

        private void UpdateScoreLabels(bool revealDealer)
        {
            SetScoreLabelsVisible(true);

            if (_isSplitRound)
            {
                int p1 = _playerHand.BestValue();
                int p2 = _splitHand.BestValue();
                string s1 = p1 > BlackjackValue ? "Bust" : p1.ToString();
                string s2 = p2 > BlackjackValue ? "Bust" : p2.ToString();

                PositionLabelLeftOfArea(playerScoreLabel, playerCardArea);
                playerScoreLabel.text = s1;

                PositionLabelLeftOfArea(_splitScoreLabel, splitCardArea);
                _splitScoreLabel.text = s2;

                if (_state == GameState.PlayerTurn)
                    UpdateSplitScorePulse();
                else
                    StopAllScorePulses();
            }
            else
            {
                ResetPlayerScoreLabelPosition();
                StopAllScorePulses();
                playerScoreLabel.text = $"{_playerHand.BestValue()}";
            }

            if (revealDealer)
                dealerScoreLabel.text = $"{_dealerHand.BestValue()}";
            else
            {
                int visibleValue = _dealerHand.Cards.Count > 0
                    ? _dealerHand.Cards[0].BlackjackValue
                    : 0;
                dealerScoreLabel.text = $"{visibleValue}";
            }
        }

        /// <summary>Sets the status label text and resets its color to the default.</summary>
        private void SetStatus(string message)
        {
            statusLabel.text = message;
            statusLabel.color = _defaultStatusColor;
        }

        /// <summary>Sets the status label text with a specific color.</summary>
        private void SetStatus(string message, Color color)
        {
            statusLabel.text = message;
            statusLabel.color = color;
        }

        /// <summary>Wraps text in TMP rich text color tags.</summary>
        private static string ColorizeText(string text, Color color)
        {
            string hex = ColorUtility.ToHtmlStringRGBA(color);
            return $"<color=#{hex}>{text}</color>";
        }

        /// <summary>Shows or hides all score labels including the split label.</summary>
        private void SetScoreLabelsVisible(bool visible)
        {
            playerScoreLabel.gameObject.SetActive(visible);
            dealerScoreLabel.gameObject.SetActive(visible);

            if (_splitScoreLabel != null)
                _splitScoreLabel.gameObject.SetActive(visible && _isSplitRound);
        }

        /// <summary>Returns true when the given hand should automatically stand (hard 17+ or soft 19+).</summary>
        private bool ShouldAutoStand(Hand hand)
        {
            int score = hand.BestValue();
            if (hand.IsSoft())
                return score >= AutoStandSoft;
            return score >= AutoStandHard;
        }

        /// <summary>Creates the split score label by cloning the player score label and adds pulse components.</summary>
        private void InitSplitScoreLabel()
        {
            RectTransform playerScoreRT = playerScoreLabel.GetComponent<RectTransform>();
            _defaultPlayerScorePosition = playerScoreRT.anchoredPosition;

            GameObject splitLabelObj = Instantiate(playerScoreLabel.gameObject, playerScoreLabel.transform.parent);
            splitLabelObj.name = "SplitScoreLabel";
            _splitScoreLabel = splitLabelObj.GetComponent<TextMeshProUGUI>();
            _splitScoreLabel.text = "";
            splitLabelObj.SetActive(false);

            _playerScorePulse = playerScoreLabel.gameObject.AddComponent<ScoreLabelPulse>();
            _splitScorePulse  = splitLabelObj.AddComponent<ScoreLabelPulse>();
        }

        /// <summary>Positions a score label to the left of the given card area, vertically centered.</summary>
        private void PositionLabelLeftOfArea(TextMeshProUGUI label, Transform cardArea)
        {
            RectTransform labelRT = label.GetComponent<RectTransform>();
            RectTransform areaRT  = cardArea.GetComponent<RectTransform>();

            float areaCenterY = areaRT.anchoredPosition.y + areaRT.sizeDelta.y * 0.5f;
            float labelHalfHeight = labelRT.sizeDelta.y * 0.5f;

            labelRT.anchorMin = areaRT.anchorMin;
            labelRT.anchorMax = areaRT.anchorMax;
            labelRT.anchoredPosition = new Vector2(
                _defaultPlayerScorePosition.x,
                areaCenterY - labelHalfHeight
            );
        }

        /// <summary>Resets the player score label to its original position.</summary>
        private void ResetPlayerScoreLabelPosition()
        {
            RectTransform playerScoreRT = playerScoreLabel.GetComponent<RectTransform>();
            playerScoreRT.anchoredPosition = _defaultPlayerScorePosition;
        }

        /// <summary>Pulses the active hand's score label and dims the inactive one.</summary>
        private void UpdateSplitScorePulse()
        {
            if (_activeHandIndex == 0)
            {
                _playerScorePulse.StartPulse();
                _splitScorePulse.StopPulse();
            }
            else
            {
                _playerScorePulse.StopPulse();
                _splitScorePulse.StartPulse();
            }
        }

        /// <summary>Stops all score label pulses and resets their alpha.</summary>
        private void StopAllScorePulses()
        {
            _playerScorePulse.StopPulse();
            _splitScorePulse.StopPulse();
        }

        /// <summary>Returns the center of the first two player cards in the fireworks RectTransform's local space.</summary>
        private Vector2 GetPlayerCardsCenter()
        {
            if (_playerCardViews.Count < 2 || fireworks == null)
                return Vector2.zero;

            RectTransform fireworksRt = fireworks.GetComponent<RectTransform>();
            Vector3 worldPos0 = _playerCardViews[0].transform.position;
            Vector3 worldPos1 = _playerCardViews[1].transform.position;
            Vector3 worldCenter = (worldPos0 + worldPos1) * 0.5f;

            Vector2 localCenter;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                fireworksRt,
                RectTransformUtility.WorldToScreenPoint(null, worldCenter),
                null,
                out localCenter
            );

            return localCenter;
        }

        /// <summary>Plays the win sound if both clip and source are assigned.</summary>
        private void PlayWinSound()
        {
            winSound.Play(audioSource);
        }

        /// <summary>Plays the natural blackjack sound if assigned, otherwise falls back to win sound.
        /// Also plays the yuhu sound simultaneously. Stops all player card glow pulses once the longest clip finishes.</summary>
        private void PlayNaturalBlackjackSound()
        {
            SoundEntry primary = naturalBlackjackSound.HasClip ? naturalBlackjackSound : winSound;
            float longestDuration = 0f;

            if (primary.HasClip && audioSource != null)
            {
                primary.Play(audioSource);
                longestDuration = primary.Length;
            }

            if (yuhuSound.HasClip && audioSource != null)
            {
                yuhuSound.Play(audioSource);
                if (yuhuSound.Length > longestDuration)
                    longestDuration = yuhuSound.Length;
            }

            if (longestDuration > 0f)
                StartCoroutine(StopGlowAfterClip(longestDuration));
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
            loseSound.Play(audioSource);
        }

        /// <summary>Plays the tie sound if both clip and source are assigned.</summary>
        private void PlayTieSound()
        {
            tieSound.Play(audioSource);
        }

        private void SetButtonState(bool dealEnabled, bool actionEnabled, bool splitEnabled, bool doubleDownEnabled = false)
        {
            dealButton.interactable      = dealEnabled;
            hitButton.interactable       = actionEnabled;
            standButton.interactable     = actionEnabled;
            surrenderButton.interactable = actionEnabled;
            if (splitButton != null)
                splitButton.interactable = splitEnabled;
            if (doubleDownButton != null)
                doubleDownButton.interactable = doubleDownEnabled;
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
            StopAllScorePulses();
            ResetPlayerScoreLabelPosition();
            SetScoreLabelsVisible(false);

            foreach (CardView v in _playerCardViews) if (v != null) Destroy(v.gameObject);
            _playerCardViews.Clear();

            foreach (CardView v in _splitCardViews)  if (v != null) Destroy(v.gameObject);
            _splitCardViews.Clear();

            foreach (CardView v in _dealerCardViews) if (v != null) Destroy(v.gameObject);
            _dealerCardViews.Clear();
        }
    }
}
