using _project.Scripts.Core;
using _project.Scripts.Object_Scripts;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace _project.Scripts.Tutorial
{
    /// <summary>
    ///     Interactive tutorial for the pipe-path building loop, shown at the start of
    ///     every run. Waits for the card phase, then walks the player through arming a
    ///     pipe tool, rotating it, placing it, completing a spawner-to-lake route that
    ///     passes through a blue build square, and installing a sifter and a cesspit.
    ///     Steps advance by watching real board state (PathBuildBoard tool/pieces,
    ///     EntitySpawner path validation, placed utilities), so the player learns by
    ///     doing rather than clicking through screens.
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        private const int StepCount = 8;

        [SerializeField] private bool debugging;
        private PathBuildBoard _board;
        private SpecialInteractController[] _buildSquares;
        private int _initialCesspitCount;
        private int _initialPlacedCount;
        private int _initialSifterCount;
        private PlacementInventory _inventory;
        private bool _layoutDirty;

        // The tutorial runs exactly while _panel exists (created in BeginTutorial, destroyed in Finish).
        private TutorialPanel _panel;
        private PathPieceOrientation? _rotateBaseline;
        private bool _started;
        private Step _step;
        private GameObject _toolHighlight;
        private Tween _toolHighlightPulse;
        private bool _utilityDirty;

        private PathBuildBoard Board
        {
            get
            {
                if (_board) return _board;
                _board = GameMaster.Instance ? GameMaster.Instance.pathBuildBoard : null;
                if (_board) _board.PathLayoutChanged += HandlePathLayoutChanged;
                return _board;
            }
        }

        private void Start()
        {
            // The first card phase may have been entered before this component enabled.
            var gm = GameMaster.Instance;
            if (gm && gm.turnController && gm.turnController.currentPhase == GamePhase.Card)
                BeginTutorial();
        }

        private void Update()
        {
            if (!_panel) return;
            var board = Board;
            if (!board) return;

            // The player can outrun the instructions: any placed pipe while we are
            // still explaining tools/rotation jumps straight to route completion.
            if (_step < Step.Connect && board.PlacedPieces.Count > _initialPlacedCount)
                EnterStep(Step.Connect);

            switch (_step)
            {
                case Step.ArmTool:
                    if (board.ActiveTool == PathBuildTool.Place && board.ActivePiece != null)
                        EnterStep(Step.Rotate);
                    break;

                case Step.Rotate:
                    var piece = board.ActivePiece;
                    if (piece == null)
                        _rotateBaseline = null; // tool cleared; wait for a re-arm
                    else if (_rotateBaseline == null)
                        _rotateBaseline = piece.Orientation;
                    else if (piece.Orientation != _rotateBaseline)
                        EnterStep(Step.Place);

                    break;

                case Step.Connect:
                    if (!_layoutDirty) break;
                    _layoutDirty = false;
                    string hint;
                    var turnController = GameMaster.Instance ? GameMaster.Instance.turnController : null;
                    if (turnController && !turnController.CanBeginWave(out var reason))
                    {
                        hint = string.IsNullOrEmpty(reason) ? null : $"Route check: {reason}";
                    }
                    else
                    {
                        var crossed = CountBuildSquaresOnRoute();
                        var required = RequiredBuildSquaresOnRoute();
                        if (crossed >= required)
                        {
                            EnterStep(Step.PlaceSifter);
                            break;
                        }

                        hint = $"Route check: connected, but it only crosses {crossed} blue build " +
                               $"square{(crossed == 1 ? "" : "s")}. Run it through at least {required} — " +
                               "one for each utility you will install.";
                    }

                    _panel.SetHint(hint);
                    break;

                case Step.PlaceSifter:
                    if (!_utilityDirty) break;
                    _utilityDirty = false;
                    if (CountPlaced<WasteSifter>() > _initialSifterCount)
                        EnterStep(Step.PlaceCesspit);
                    break;

                case Step.PlaceCesspit:
                    if (!_utilityDirty) break;
                    _utilityDirty = false;
                    if (CountPlaced<Cesspit>() > _initialCesspitCount)
                        EnterStep(Step.StartWave);
                    break;
            }
        }

        private void OnEnable()
        {
            TurnController.OnCardPhaseEntered += HandleCardPhaseEntered;
            TurnController.OnTowerPhaseEntered += HandleTowerPhaseEntered;
        }

        private void OnDisable()
        {
            TurnController.OnCardPhaseEntered -= HandleCardPhaseEntered;
            TurnController.OnTowerPhaseEntered -= HandleTowerPhaseEntered;
            if (_board) _board.PathLayoutChanged -= HandlePathLayoutChanged;
            UnbindInventory();
        }

        private void BeginTutorial()
        {
            if (_started) return;
            _started = true;

            _panel = TutorialPanel.Create();
            _panel.NextRequested += HandleNextRequested;
            _panel.SkipRequested += HandleSkipRequested;

            // Utility placement consumes from the inventory, so InventoryChanged marks
            // the sifter/cesspit steps dirty instead of recounting the scene every frame.
            _inventory = GameMaster.Instance ? GameMaster.Instance.placementInventory : null;
            if (_inventory) _inventory.InventoryChanged += HandleInventoryChanged;

            _initialPlacedCount = Board ? Board.PlacedPieces.Count : 0;
            // Baselines from run start, so a utility placed ahead of its step still counts.
            _initialSifterCount = CountPlaced<WasteSifter>();
            _initialCesspitCount = CountPlaced<Cesspit>();
            EnterStep(Step.Welcome);
        }

        private void EnterStep(Step step)
        {
            _step = step;
            _rotateBaseline = null;
            ClearToolHighlight();
            if (step == Step.Connect) _layoutDirty = true; // route may already be complete
            if (step is Step.PlaceSifter or Step.PlaceCesspit) _utilityDirty = true; // utility may already be placed

            switch (step)
            {
                case Step.Welcome:
                    _panel.ShowStep(1, StepCount, "BUILD A PIPELINE",
                        "Waste spews from the outfall every wave and flows toward the lake. " +
                        "Before a wave can start, you must lay a connected pipe route between them.");
                    _panel.SetDemoVisible(true);
                    _panel.SetNextButton("NEXT");
                    break;
                case Step.ArmTool:
                    _panel.ShowStep(2, StepCount, "ARM A PIPE TOOL",
                        "In the PIPELINE panel, click PIPE 2-CELL to arm a two-cell pipe segment.");                    ShowPipeToolHighlight();
                    break;
                case Step.Rotate:
                    _panel.ShowStep(3, StepCount, "ROTATE THE SEGMENT",
                        "Press R to flip the armed segment between horizontal and vertical.");                    break;
                case Step.Place:
                    _panel.ShowStep(4, StepCount, "LAY THE PIPE",
                        "Click a board cell to place the segment. Hover the grid first to preview where it will land.");                    break;
                case Step.Connect:
                    _panel.ShowStep(5, StepCount, "COMPLETE THE ROUTE",
                        "Keep laying pipe until one unbroken route links the outfall to the lake — " +
                        "and run it through at least two blue build squares, one for each utility " +
                        "you will install next. The REMOVE tool clears mistakes.");
                    _panel.SetDemoVisible(true);                    break;
                case Step.PlaceSifter:
                    _panel.ShowStep(6, StepCount, "INSTALL A SIFTER",
                        "Buy a WASTE SIFTER from the shop — its box glows orange while it is ready " +
                        "to place. Click a blue square on your pipeline to install it. Sifters " +
                        "shrink waste that flows past.");                    break;
                case Step.PlaceCesspit:
                    _panel.ShowStep(7, StepCount, "ADD A CESSPIT",
                        "Buy a CESSPIT from the shop and install it on another blue square along " +
                        "the route. Cesspits store and process waste — but spawn runaways when they " +
                        "fill up.");                    break;
                case Step.StartWave:
                    _panel.ShowStep(8, StepCount, "READY FOR THE WAVE",
                        "Pipeline connected, defenses installed. Press the next-phase button when you " +
                        "are ready to face the wave!");
                    _panel.SetNextButton("FINISH");
                    break;
            }

            if (debugging) Debug.Log($"Tutorial entered step {step}", this);
        }

        /// <summary>
        ///     Pulsing amber frame around the toolbar's PIPE 2-CELL switch, so the player
        ///     can find the control the ArmTool step is talking about. Parented to the
        ///     button, so it follows the layout; raycasts stay off so clicks pass through.
        /// </summary>
        private void ShowPipeToolHighlight()
        {
            ClearToolHighlight();

            var gm = GameMaster.Instance;
            var toolbar = gm && gm.interfaceManager ? gm.interfaceManager.PathToolBar : null;
            var target = toolbar ? toolbar.ShortPipeButtonRect : null;
            if (!target) return;

            _toolHighlight = new GameObject("TutorialToolHighlight", typeof(RectTransform));
            _toolHighlight.transform.SetParent(target, false);
            var frame = (RectTransform)_toolHighlight.transform;
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.offsetMin = new Vector2(-5f, -5f);
            frame.offsetMax = new Vector2(5f, 5f);

            var accent = TutorialPanel.CautionColor;
            CreateHighlightStrip(frame, "Top", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -3f), Vector2.zero,
                accent);
            CreateHighlightStrip(frame, "Bottom", Vector2.zero, new Vector2(1f, 0f), Vector2.zero,
                new Vector2(0f, 3f), accent);
            CreateHighlightStrip(frame, "Left", Vector2.zero, new Vector2(0f, 1f), Vector2.zero,
                new Vector2(3f, 0f), accent);
            CreateHighlightStrip(frame, "Right", new Vector2(1f, 0f), Vector2.one, new Vector2(-3f, 0f),
                Vector2.zero, accent);

            // One CanvasGroup alpha pulse instead of dirtying four Image geometries per frame.
            var pulseGroup = _toolHighlight.AddComponent<CanvasGroup>();
            // DOTween's CanvasGroup.DOFade lives in the UI module (Assembly-CSharp), which this
            // assembly can't see — tween the alpha through the core API instead.
            _toolHighlightPulse = DOTween.To(() => pulseGroup.alpha, a => pulseGroup.alpha = a, 0.1f, 0.63f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(_toolHighlight);
        }

        private static Image CreateHighlightStrip(RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private void ClearToolHighlight()
        {
            _toolHighlightPulse?.Kill();
            _toolHighlightPulse = null;

            if (_toolHighlight)
            {
                Destroy(_toolHighlight);
                _toolHighlight = null;
            }
        }

        /// <summary>
        ///     Two squares are needed so the sifter and the cesspit each get one on the
        ///     route; clamped to however many squares the board actually has, so a sparse
        ///     board can never soft-lock the step.
        /// </summary>
        private int RequiredBuildSquaresOnRoute()
        {
            EnsureBuildSquares();
            return Mathf.Min(2, _buildSquares.Length);
        }

        /// <summary>Blue build squares (SpecialInteractController slots) whose cell is occupied by a pipe.</summary>
        private int CountBuildSquaresOnRoute()
        {
            var board = Board;
            if (!board) return 0;
            EnsureBuildSquares();

            var count = 0;
            foreach (var square in _buildSquares)
            {
                if (!square) continue;
                if (board.TryWorldToCell(square.transform.position, out var cell) && board.IsOccupied(cell))
                    count++;
            }

            return count;
        }

        private void EnsureBuildSquares()
        {
            _buildSquares ??= FindObjectsByType<SpecialInteractController>(FindObjectsInactive.Exclude);
        }

        private static int CountPlaced<T>() where T : Behaviour
        {
            return FindObjectsByType<T>(FindObjectsInactive.Exclude).Length;
        }

        private void HandleNextRequested()
        {
            switch (_step)
            {
                case Step.Welcome:
                    EnterStep(Step.ArmTool);
                    break;
                case Step.StartWave:
                    Finish(false);
                    break;
            }
        }

        private void HandleSkipRequested()
        {
            Finish(true);
        }

        private void HandlePathLayoutChanged()
        {
            _layoutDirty = true;
        }

        private void HandleInventoryChanged()
        {
            _utilityDirty = true;
        }

        private void HandleCardPhaseEntered()
        {
            if (!_started && enabled) BeginTutorial();
            else if (_panel) _panel.SetVisible(true);
        }

        private void HandleTowerPhaseEntered()
        {
            if (!_panel) return;

            // Starting a wave means the route validated, so the lesson is learned.
            if (_step == Step.StartWave)
                Finish(false);
            else
                _panel.SetVisible(false);
        }

        private void UnbindInventory()
        {
            if (!_inventory) return;
            _inventory.InventoryChanged -= HandleInventoryChanged;
            _inventory = null;
        }

        private void Finish(bool skipped)
        {
            ClearToolHighlight();
            UnbindInventory();

            if (_panel) Destroy(_panel.gameObject);
            _panel = null;

            if (debugging) Debug.Log(skipped ? "Tutorial skipped." : "Tutorial completed.", this);
            enabled = false;
        }

        private enum Step
        {
            Welcome,
            ArmTool,
            Rotate,
            Place,
            Connect,
            PlaceSifter,
            PlaceCesspit,
            StartWave
        }
    }
}