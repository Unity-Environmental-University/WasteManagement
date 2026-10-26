using System.Collections.Generic;
using _project.Scripts.Object_Scripts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace _project.Scripts.Core
{
    /// <summary>
    ///     Runs the debris-sorting minigame. The pieces on screen always match the sifter's debris:
    ///     each stands for an even share of a full screen, and sorting one takes that share away, so
    ///     sorting the last one clears the sifter. The X button, Escape, or a click on the backdrop
    ///     walks away mid-round: each sifter remembers which pieces are sorted and how the rest
    ///     looked, and junk caught in the meantime (or while the window is open) brings pieces back.
    /// </summary>
    public class SifterMiniGameController : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private GameObject minigamePanel;
        [SerializeField] private Button closeButton;

        [Header("Debris Sprites")]
        [SerializeField] private Sprite[] incinerateSprites;
        [SerializeField] private Sprite[] landfillSprites;
        [SerializeField] private Sprite[] catchAndReleaseSprites;

        private readonly List<DebrisMiniBucket> _buckets = new();
        private readonly List<DebrisHandler> _debris = new();

        // Each sifter's unfinished round, kept until that sifter is fully cleared.
        private readonly Dictionary<WasteSifter, SortProgress> _progress = new();
        private DebrisHandler[] _roster = System.Array.Empty<DebrisHandler>();
        private SortProgress _activeProgress;
        private WasteSifter _activeSifter;
        private bool _isRunning;
        private bool _endQueued;

        /// <summary>True, only between StartMiniGame and the round ending, while a roster is being sorted.</summary>
        public bool IsRunning => _isRunning;

        private void Awake()
        {
            // The panel is authored active, so its layout stays visible while editing, but leaving it
            // live at runtime hands the player a minigame with no roster and no sifter behind it:
            // every piece can be sorted before StartMiniGame runs, and nothing would end the round.
            if (minigamePanel) minigamePanel.SetActive(false);
            if (closeButton) closeButton.onClick.AddListener(CloseMiniGame);
        }

        private void Update()
        {
            if (!_isRunning) return;

            if (Keyboard.current != null && Keyboard.current[Key.Escape].wasPressedThisFrame)
            {
                CloseMiniGame();
                return;
            }

            // Junk keeps landing on the sifter while the window is open; show it as it arrives.
            if (_activeSifter && _activeProgress != null)
                while (_debris.Count < _activeSifter.DebrisPieces(_roster.Length) && ReturnSortedPiece()) { }
        }

        // Clicks anywhere in the panel bubble up here, since nothing beneath it handles them. Only a
        // click that both starts and ends on the backdrop itself closes the round; one on the window,
        // a bucket, or a piece is part of playing, and a press that slips off the window shouldn't count.
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.pointerPressRaycast.gameObject == minigamePanel &&
                eventData.pointerCurrentRaycast.gameObject == minigamePanel)
                CloseMiniGame();
        }

        /// <summary>A random sprite for a piece of this type, or null when none are assigned.</summary>
        public Sprite GetDebrisSprite(DebrisHandler.DebrisType type)
        {
            var sprites = type switch
            {
                DebrisHandler.DebrisType.Incinerate => incinerateSprites,
                DebrisHandler.DebrisType.Landfill => landfillSprites,
                DebrisHandler.DebrisType.CatchAndRelease => catchAndReleaseSprites,
                _ => null
            };

            return sprites is { Length: > 0 } ? sprites[Random.Range(0, sprites.Length)] : null;
        }

        public void RegisterBucket(DebrisMiniBucket bucket)
        {
            if (!_buckets.Contains(bucket)) _buckets.Add(bucket);
        }

        public void UnregisterBucket(DebrisMiniBucket bucket)
        {
            _buckets.Remove(bucket);
        }

        public void RegisterHandler(DebrisHandler handler)
        {
            if (_isRunning && !_debris.Contains(handler)) _debris.Add(handler);
        }

        public void UnregisterHandler(DebrisHandler handler)
        {
            if (!_debris.Remove(handler)) return;

            // Something outside the round switched the panel or a parent off. Walk away instead of
            // counting every piece that went with it as sorted, and leave the panel alone: it can't
            // change state while Unity is still deactivating it.
            if (!minigamePanel || !minigamePanel.activeInHierarchy)
            {
                AbandonRound();
                return;
            }

            // Otherwise only sorting disables a piece mid-round: the round's end drops the roster first.
            var index = System.Array.IndexOf(_roster, handler);
            if (index >= 0 && _activeProgress != null) _activeProgress.Sorted[index] = true;
            if (_activeSifter) _activeSifter.SortDebrisPiece(_roster.Length);

            // The last piece unregisters from its own OnDisable, while Unity is still deactivating it, and
            // the panel above it can't be deactivated from inside that; finish the round in LateUpdate.
            if (_isRunning && _debris.Count is 0)
                _endQueued = true;
        }

        private void LateUpdate()
        {
            if (!_endQueued) return;

            // Junk that landed after the last sort is still waiting on the panel: keep playing.
            if (_debris.Count is 0) EndMiniGame(true);
            else _endQueued = false;
        }

        public void StartMiniGame(WasteSifter sifter)
        {
            if (_isRunning || !minigamePanel) return;

            _activeSifter = sifter;
            _debris.Clear();
            minigamePanel.SetActive(true);

            // Take the roster from the hierarchy rather than from OnEnable registration: a piece that
            // was already enabled has nothing left to fire, and one that woke before GameMaster
            // existed never registered at all. Re-activating also restores pieces the last round sorted.
            _roster = minigamePanel.GetComponentsInChildren<DebrisHandler>(true);
            ForgetRemovedSifters();
            if (!sifter || !_progress.TryGetValue(sifter, out _activeProgress) ||
                _activeProgress.Sorted.Length != _roster.Length)
                _activeProgress = new SortProgress(_roster.Length);
            if (sifter) _progress[sifter] = _activeProgress;
            MatchPiecesToDebris(sifter);

            for (var i = 0; i < _roster.Length; i++)
            {
                var handler = _roster[i];
                if (!handler) continue;

                // A piece this sifter already had sorted stays gone; the rest come back as they were.
                if (_activeProgress.Sorted[i])
                {
                    handler.gameObject.SetActive(false);
                    continue;
                }

                handler.gameObject.SetActive(true);
                if (_activeProgress.Seen[i]) handler.Restore(_activeProgress.Types[i], _activeProgress.Sprites[i]);
                else _activeProgress.Remember(i, handler);
                if (!_debris.Contains(handler)) _debris.Add(handler);
            }

            if (_debris.Count is 0)
            {
                Debug.LogWarning($"{name}: no debris under {minigamePanel.name}; nothing to sort.", this);
                EndMiniGame(true);
                return;
            }

            _isRunning = true;
        }

        /// <summary>
        ///     Sets which pieces are left to sort so their count matches what the sifter's debris is
        ///     worth. Junk caught since the last visit brings sorted pieces back as new junk; debris
        ///     broken down meanwhile takes pieces away. Without a sifter every piece is in play.
        /// </summary>
        private void MatchPiecesToDebris(WasteSifter sifter)
        {
            var wanted = sifter ? sifter.DebrisPieces(_roster.Length) : _roster.Length;
            var left = 0;
            for (var i = 0; i < _roster.Length; i++)
                if (_roster[i] && !_activeProgress.Sorted[i])
                    left++;

            for (var i = 0; i < _roster.Length && left != wanted; i++)
            {
                if (!_roster[i]) continue;
                if (left < wanted && _activeProgress.Sorted[i])
                {
                    _activeProgress.Sorted[i] = false;
                    _activeProgress.Seen[i] = false;
                    left++;
                }
                else if (left > wanted && !_activeProgress.Sorted[i])
                {
                    _activeProgress.Sorted[i] = true;
                    left--;
                }
            }
        }

        /// <summary>Puts one sorted piece back in play as new junk; false when none is left to return.</summary>
        private bool ReturnSortedPiece()
        {
            for (var i = 0; i < _roster.Length; i++)
            {
                var handler = _roster[i];
                if (!handler || !_activeProgress.Sorted[i]) continue;

                // Re-enabling rolls the piece a new look and registers it with the running round.
                _activeProgress.Sorted[i] = false;
                handler.gameObject.SetActive(true);
                _activeProgress.Remember(i, handler);
                if (!_debris.Contains(handler)) _debris.Add(handler);
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Leaves the round unfinished: the sifter keeps what's left, and the next round resumes it.
        ///     A round whose last piece is already sorted still ends cleared.
        /// </summary>
        public void CloseMiniGame()
        {
            if (_isRunning) EndMiniGame(_endQueued && _debris.Count is 0);
        }

        private void EndMiniGame(bool cleared)
        {
            // Drop the roster first: deactivating the panel disables every remaining piece, and each of
            // those OnDisable calls comes straight back through UnregisterHandler.
            _isRunning = false;
            _endQueued = false;
            _debris.Clear();

            if (minigamePanel) minigamePanel.SetActive(false);

            if (cleared && _activeSifter)
            {
                _activeSifter.ClearDebris();
                _progress.Remove(_activeSifter);
            }

            _activeSifter = null;
            _activeProgress = null;
        }

        private void AbandonRound()
        {
            _isRunning = false;
            _endQueued = false;
            _debris.Clear();
            _activeSifter = null;
            _activeProgress = null;
        }

        private void ForgetRemovedSifters()
        {
            List<WasteSifter> removed = null;
            foreach (var sifter in _progress.Keys)
                if (!sifter)
                    (removed ??= new List<WasteSifter>()).Add(sifter);
            if (removed == null) return;
            foreach (var sifter in removed)
                _progress.Remove(sifter);
        }

        /// <summary>Which of the roster's pieces one sifter has sorted, and how the others looked.</summary>
        private sealed class SortProgress
        {
            public readonly bool[] Sorted;
            public readonly bool[] Seen; // Whether Types/Sprites hold the piece's look yet.
            public readonly DebrisHandler.DebrisType[] Types;
            public readonly Sprite[] Sprites;

            public SortProgress(int pieceCount)
            {
                Sorted = new bool[pieceCount];
                Seen = new bool[pieceCount];
                Types = new DebrisHandler.DebrisType[pieceCount];
                Sprites = new Sprite[pieceCount];
            }

            public void Remember(int index, DebrisHandler handler)
            {
                Seen[index] = true;
                Types[index] = handler.GetDebrisType();
                Sprites[index] = handler.GetSprite();
            }
        }
    }
}
