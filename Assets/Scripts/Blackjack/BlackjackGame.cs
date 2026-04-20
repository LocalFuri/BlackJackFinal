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
        [SerializeField] private float dealDelay          = 0.45f;
        [SerializeField] private float dealerPauseDelay   = 0.7f;

        // ──────────────────────────────────────────────────────────────────────────
        // Constants
        // ──────────────────────────────────────────────────────────────────────────

        private const int AutoStandThreshold   = 17;
        private const int BlackjackValue        = 21;

        // ──────────────────────────────────────────────────────────────────────────
        // State
        // ──────────────────────────────────────────────────────────────────────────

        private readonly Deck _deck          = new();
        private readonly Hand _playerHand    = new();
        private readonly Hand _dealerHand    = new();

        private readonly List<CardView> _playerCardViews = new();
        private readonly List<CardView> _dealerCardViews = new();

        private CardView _dealerHoleCardView; // reference to the hidden card

        private bool _forcePlayerBlackjack;

        private enum GameState { Idle, PlayerTurn, DealerTurn, RoundOver }
        private GameState _state = GameState.Idle;

        // ──────────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ──────────────────────────────────────────────────────────────────────────

        private void Start()
        {
            Screen.SetResolution(1920, 1080, FullScreenMode.FullScreenWindow);

            if (startupSound != null && audioSource != null)
                audioSource.PlayOneShot(startupSound);

            _deck.Build();
            SetButtonState(dealEnabled: true, actionEnabled: false);
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

        /// <summary>Player ends their turn; dealer plays.</summary>
        public void OnStand()
        {
            if (_state != GameState.PlayerTurn) return;
            StartCoroutine(DealerTurn());
        }

        /// <summary>Forces the next deal to give the player a natural blackjack, then starts the round.</summary>
        public void OnBlackjackTest()
        {
            if (_state != GameState.Idle) return;
            _forcePlayerBlackjack = true;
            StartCoroutine(DealRound());
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Round Flow
        // ──────────────────────────────────────────────────────────────────────────

        private IEnumerator DealRound()
        {
            _state = GameState.PlayerTurn;
            SetButtonState(dealEnabled: false, actionEnabled: false);

            // Rebuild & shuffle deck for each round
            _deck.Build();

            if (_forcePlayerBlackjack)
            {
                _deck.ForcePlayerBlackjack();
                _forcePlayerBlackjack = false;
            }

            ClearTable();

            SetStatus("Dealing...");

            // Deal: player, dealer, player, dealer (dealer 2nd card is hole card)
            yield return StartCoroutine(DealCardTo(_playerHand, _playerCardViews, faceUp: true));
            yield return StartCoroutine(DealCardTo(_dealerHand, _dealerCardViews, faceUp: true));
            yield return StartCoroutine(DealCardTo(_playerHand, _playerCardViews, faceUp: true));
            yield return StartCoroutine(DealCardTo(_dealerHand, _dealerCardViews, faceUp: false)); // hole card

            _dealerHoleCardView = _dealerCardViews[^1];

            UpdateScoreLabels(revealDealer: false);

            // ── Check natural blackjack ──
            bool playerBJ = _playerHand.IsBlackjack();
            bool dealerBJ = _dealerHand.IsBlackjack();

            if (playerBJ || dealerBJ)
            {
                yield return StartCoroutine(RevealHoleCard());
                UpdateScoreLabels(revealDealer: true);

                if (playerBJ && dealerBJ)
                {
                    PlayTieSound();
                    SetStatus("Both Blackjack! Push.");
                }
                else if (playerBJ)
                {
                    ApplyBlackjackGlow();
                    fireworks.Play(Vector2.zero);
                    PlayNaturalBlackjackSound();
                    SetStatus("Blackjack! Player wins!");
                }
                else
                {
                    SetStatus("Dealer Blackjack! Dealer wins.");
                    PlayLoseSound();
                }

                yield return StartCoroutine(EndRound());
                yield break;
            }

            // ── Player turn ──
            SetButtonState(dealEnabled: false, actionEnabled: true);
            SetStatus($"Your turn. Score: {_playerHand.BestValue()}");

            // Auto-stand if player already at 17+
            if (_playerHand.BestValue() >= AutoStandThreshold)
            {
                yield return new WaitForSeconds(0.3f);
                yield return StartCoroutine(DealerTurn());
            }
        }

        private IEnumerator PlayerHit()
        {
            SetButtonState(dealEnabled: false, actionEnabled: false);
            yield return StartCoroutine(DealCardTo(_playerHand, _playerCardViews, faceUp: true));
            UpdateScoreLabels(revealDealer: false);

            int score = _playerHand.BestValue();

            if (score > BlackjackValue)
            {
                SetStatus($"Bust! You scored {score}. Dealer wins.");
                PlayLoseSound();
                yield return StartCoroutine(RevealHoleCard());
                yield return StartCoroutine(EndRound());
                yield break;
            }

            if (score == BlackjackValue || score >= AutoStandThreshold)
            {
                yield return new WaitForSeconds(0.25f);
                yield return StartCoroutine(DealerTurn());
                yield break;
            }

            SetButtonState(dealEnabled: false, actionEnabled: true);
            SetStatus($"Your turn. Score: {score}");
        }

        private IEnumerator DealerTurn()
        {
            _state = GameState.DealerTurn;
            SetButtonState(dealEnabled: false, actionEnabled: false);

            yield return StartCoroutine(RevealHoleCard());
            UpdateScoreLabels(revealDealer: true);

            SetStatus("Dealer's turn...");

            yield return new WaitForSeconds(dealerPauseDelay);

            // Dealer draws until 17
            while (_dealerHand.BestValue() < AutoStandThreshold)
            {
                yield return StartCoroutine(DealCardTo(_dealerHand, _dealerCardViews, faceUp: true));
                UpdateScoreLabels(revealDealer: true);
                yield return new WaitForSeconds(dealerPauseDelay);
            }

            yield return StartCoroutine(ResolveRound());
        }

        private IEnumerator ResolveRound()
        {
            int playerScore = _playerHand.BestValue();
            int dealerScore = _dealerHand.BestValue();

            if (dealerScore > BlackjackValue)
            {
                PlayWinSound();
                SetStatus($"Dealer busts at {dealerScore}! You win!");
            }
            else if (playerScore > dealerScore)
            {
                PlayWinSound();
                SetStatus($"You win! {playerScore} vs {dealerScore}.");
            }
            else if (dealerScore > playerScore)
            {
                PlayLoseSound();
                SetStatus($"Dealer wins. {dealerScore} vs {playerScore}.");
            }
            else
            {
                PlayTieSound();
                SetStatus($"Push! Both scored {playerScore}.");
            }

            yield return StartCoroutine(EndRound());
        }

        private IEnumerator EndRound()
        {
            _state = GameState.RoundOver;
            yield return new WaitForSeconds(1.5f);
            _state = GameState.Idle;
            SetButtonState(dealEnabled: true, actionEnabled: false);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Card Dealing
        // ──────────────────────────────────────────────────────────────────────────

        private IEnumerator DealCardTo(Hand hand, List<CardView> views, bool faceUp)
        {
            yield return new WaitForSeconds(dealDelay);

            CardData card = _deck.Draw();
            hand.AddCard(card);

            if (dealCardSound != null && audioSource != null)
                audioSource.PlayOneShot(dealCardSound);

      Transform parent = (hand == _playerHand) ? playerCardArea : dealerCardArea;
            CardView  view   = SpawnCardView(card, parent, faceUp);
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
            playerScoreLabel.text = $"Player: {_playerHand.BestValue()}";

            if (revealDealer)
                dealerScoreLabel.text = $"Dealer: {_dealerHand.BestValue()}";
            else
            {
                // Only show value of the visible (first) dealer card
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

        /// <summary>Plays the natural blackjack sound (fireworks) if assigned, otherwise falls back to win sound.
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
            foreach (CardView v in _playerCardViews)
                v.StopGlowPulse();
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

        private void SetButtonState(bool dealEnabled, bool actionEnabled)
        {
            dealButton.interactable  = dealEnabled;
            hitButton.interactable   = actionEnabled;
            standButton.interactable = actionEnabled;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Table Clear
        // ──────────────────────────────────────────────────────────────────────────

        private void ClearTable()
        {
            _playerHand.Clear();
            _dealerHand.Clear();
            _dealerHoleCardView = null;

            foreach (CardView v in _playerCardViews)
                if (v != null) Destroy(v.gameObject);
            _playerCardViews.Clear();

            foreach (CardView v in _dealerCardViews)
                if (v != null) Destroy(v.gameObject);
            _dealerCardViews.Clear();
        }
    }
}
