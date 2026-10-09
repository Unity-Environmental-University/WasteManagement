using System;
using System.Collections.Generic;
using _project.Scripts.Core;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>How a <see cref="PathSplitter" /> treats one issue type.</summary>
    public enum SplitterRule
    {
        /// <summary>Shared between both lanes by the splitter's percentage.</summary>
        Split,

        /// <summary>Always kept on the main route.</summary>
        MainOnly,

        /// <summary>Always diverted down the branch.</summary>
        BranchOnly
    }

    /// <summary>
    ///     Divides issues between the normal (main) route and the alternate (branch) route. Each
    ///     issue type is either pinned to one lane or shared by <see cref="MainSharePercent" />;
    ///     shared issues are dealt out in a fixed rotation rather than at random, so the split is
    ///     exact and free of streaks (50% runs 0, 1, 0, 1).
    /// </summary>
    public class PathSplitter : MonoBehaviour, IRemovableUtility
    {
        /// <summary>
        ///     Raised when a splitter enters or leaves the active scene, so live route previews can
        ///     immediately reveal or hide their alternate branch.
        /// </summary>
        public static event Action AvailabilityChanged;

        private static readonly List<PathSplitter> LiveSplitters = new();

        /// <summary>All currently enabled splitters.</summary>
        public static IReadOnlyList<PathSplitter> Live => LiveSplitters;

        [Tooltip("Share of split issues kept on the main route; the rest take the branch.")]
        [SerializeField] [Range(0, 100)] private int mainSharePercent = 50;

        // The model's pipe stubs, one per side, are the child renderers with this in their name.
        private const string PipeStubName = "Pipe";

        // The valve handles that point down the main lane and the branch, found the same way.
        private const string MainHandleName = "ValveBlue";
        private const string BranchHandleName = "ValveOrange";

        private static readonly List<Vector3> FlowOffsets = new();
        private static readonly List<Vector2Int> ConnectedSides = new();

        private readonly HashSet<EntityId> _routedIssueIds = new();

        // Indexed by IssueType.
        private readonly SplitterRule[] _typeRules = new SplitterRule[Enum.GetValues(typeof(IssueType)).Length];

        // Branch share accumulated by split issues; each full 100 sends one down the branch.
        private int _branchCredit;
        private Renderer[] _modelRenderers;
        private Renderer[] _pipeStubs;
        private Renderer _mainHandle;
        private Renderer _branchHandle;

        /// <summary>Share (0–100) of split issues kept on the main route; the rest take the branch.</summary>
        public int MainSharePercent
        {
            get => mainSharePercent;
            set => mainSharePercent = Mathf.Clamp(value, 0, 100);
        }

        private static bool Debugging
        {
            get
            {
                var gm = GameMaster.Instance;
                return gm && gm.debugging;
            }
        }

        private SpecialInteractController _slot;
        private int _infraValue;

        public bool CanRemove => true;

        /// <summary>Tints the model with <paramref name="color" />, or restores it when null.</summary>
        public void SetHighlight(Color? color)
        {
            if (_modelRenderers == null) return;
            MaterialPropertyBlock block = null;
            foreach (var modelRenderer in _modelRenderers)
            {
                if (!modelRenderer) continue;
                if (color.HasValue) RendererColorUtility.SetColor(modelRenderer, color.Value, ref block);
                else modelRenderer.SetPropertyBlock(null);
            }
        }

        public void Remove()
        {
            if (_slot) _slot.ClearOccupied(_infraValue);
            Destroy(gameObject);
        }

        /// <summary>Called by <see cref="SpecialInteractController" /> when this utility is placed.</summary>
        public void SetSlot(SpecialInteractController slot, int infraValue = 0)
        {
            _slot = slot;
            _infraValue = infraValue;
        }

        /// <summary>
        ///     Matches the model to what it connects to on <paramref name="board" />. Only the pipe
        ///     stubs on connected sides show: the intake and each outlet of the live routes through
        ///     this cell, or, while no route reaches it yet, the sides with pipe laid next to it.
        ///     Each valve handle points down its outlet, and hides while that outlet doesn't exist.
        /// </summary>
        public void RefreshConnections(PathBuildBoard board, WaypointPath[] paths)
        {
            if (!board.TryWorldToCell(transform.position, out var cell)) return;

            FlowOffsets.Clear();
            var onRoute = false;
            Vector3? mainOutlet = null;
            Vector3? branchOutlet = null;
            foreach (var path in paths)
            {
                if (!path || !path.UsesBoard(board)) continue;
                onRoute |= path.CollectFlowOffsets(transform.position, FlowOffsets);
                path.GetOutletOffsets(transform.position, ref mainOutlet, ref branchOutlet);
            }

            if (!onRoute)
                foreach (var step in PathBuildBoard.CellNeighborOffsets)
                    if (board.IsOccupied(cell + step))
                        FlowOffsets.Add(board.GetCellTopPosition(cell + step) - board.GetCellTopPosition(cell));

            ConnectedSides.Clear();
            foreach (var offset in FlowOffsets)
                ConnectedSides.Add(GetSide(transform.InverseTransformDirection(offset)));

            foreach (var stub in _pipeStubs)
                stub.enabled = ConnectedSides.Contains(GetModelSide(stub));

            PointHandle(_mainHandle, mainOutlet);
            PointHandle(_branchHandle, branchOutlet);
        }

        private void PointHandle(Renderer handle, Vector3? outletOffset)
        {
            if (!handle) return;

            handle.enabled = outletOffset.HasValue;
            if (!outletOffset.HasValue) return;

            var from = GetModelSide(handle);
            var to = GetSide(transform.InverseTransformDirection(outletOffset.Value));
            var angle = Vector3.SignedAngle(new Vector3(from.x, 0f, from.y), new Vector3(to.x, 0f, to.y), Vector3.up);
            handle.transform.RotateAround(transform.position, transform.up, angle);
        }

        // The model's parts share its pivot, so the side a part is on comes from where its mesh sits.
        private Vector2Int GetModelSide(Renderer part)
        {
            return GetSide(transform.InverseTransformPoint(part.transform.TransformPoint(part.localBounds.center)));
        }

        private static Vector2Int GetSide(Vector3 local)
        {
            return Mathf.Abs(local.x) > Mathf.Abs(local.z)
                ? new Vector2Int(local.x > 0f ? 1 : -1, 0)
                : new Vector2Int(0, local.z > 0f ? 1 : -1);
        }

        private void Awake()
        {
            if (!TryGetComponent<Collider>(out var trigger))
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.5f, 0f);
                box.size = new Vector3(0.9f, 1.25f, 0.9f);
                trigger = box;
            }

            trigger.isTrigger = true;

            // A splitter can sit on a bare cell, so its trigger must not swallow the clicks that
            // lay pipe through that cell.
            var ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            foreach (var child in GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = ignoreRaycastLayer;

            _modelRenderers = GetComponentsInChildren<Renderer>(true);
            _pipeStubs = Array.FindAll(_modelRenderers,
                modelRenderer => modelRenderer.name.Contains(PipeStubName));
            _mainHandle = Array.Find(_modelRenderers,
                modelRenderer => modelRenderer.name.Contains(MainHandleName));
            _branchHandle = Array.Find(_modelRenderers,
                modelRenderer => modelRenderer.name.Contains(BranchHandleName));
        }

        private void OnEnable()
        {
            LiveSplitters.Add(this);
            LiveComponentRegistry.Register(this);
            TurnController.OnTowerPhaseEntered += ResetSplit;
            AvailabilityChanged?.Invoke();
        }

        // Placement code may finish positioning the splitter after OnEnable; announce again once
        // its frame settles so the fork reflects the cell it actually landed on.
        private void Start()
        {
            AvailabilityChanged?.Invoke();
        }

        private void OnDisable()
        {
            LiveSplitters.Remove(this);
            LiveComponentRegistry.Unregister(this);
            TurnController.OnTowerPhaseEntered -= ResetSplit;
            AvailabilityChanged?.Invoke();
        }

        private void OnTriggerEnter(Collider other)
        {
            var issue = other.GetComponentInParent<IssueObject>();
            if (issue) RouteIssue(issue);
        }

        public bool RouteIssue(IssueObject issue)
        {
            if (!issue || issue.IsDirectDestination) return false;

            // The branch depends on the route the issue arrived on: a splitter on a branch splits
            // that branch, and one past a rejoin splits every route that flows through it.
            var path = issue.GetPath();
            if (!path || path.PathKind != PathKind.Pipe ||
                !path.TryGetBranchRoute(issue.GetRouteIndex(), transform.position, out var branchRoute)) return false;
            if (!_routedIssueIds.Add(issue.GetEntityId())) return false;

            // The main lane keeps the issue on the route it is already following.
            var takeBranch = ChooseBranch(issue.GetIssueType());
            if (takeBranch && !issue.TrySetRoute(branchRoute)) return false;

            if (Debugging)
                Debug.Log($"[PathSplitter] Routed issue to {(takeBranch ? $"branch route {branchRoute}" : "main lane")}.");

            return true;
        }

        public SplitterRule GetRule(IssueType issueType)
        {
            return _typeRules[(int)issueType];
        }

        public void SetRule(IssueType issueType, SplitterRule rule)
        {
            _typeRules[(int)issueType] = rule;
        }

        /// <summary>
        ///     Distance along <paramref name="ray" /> to this splitter's model, for click picking.
        ///     The splitter sits on the Ignore Raycast layer, so physics queries can't find it;
        ///     the combined bounds of its renderers stand in as the click target.
        /// </summary>
        public bool TryGetPointerDistance(Ray ray, out float distance)
        {
            distance = 0f;
            Bounds? modelBounds = null;
            foreach (var modelRenderer in _modelRenderers)
            {
                if (!modelRenderer || !modelRenderer.enabled) continue;

                if (modelBounds.HasValue)
                {
                    var combined = modelBounds.Value;
                    combined.Encapsulate(modelRenderer.bounds);
                    modelBounds = combined;
                }
                else
                {
                    modelBounds = modelRenderer.bounds;
                }
            }

            return modelBounds.HasValue && modelBounds.Value.IntersectRay(ray, out distance);
        }

        private bool ChooseBranch(IssueType issueType)
        {
            switch (GetRule(issueType))
            {
                case SplitterRule.MainOnly: return false;
                case SplitterRule.BranchOnly: return true;
            }

            _branchCredit += 100 - mainSharePercent;
            if (_branchCredit < 100) return false;

            _branchCredit -= 100;
            return true;
        }

        private void ResetSplit()
        {
            _routedIssueIds.Clear();
            _branchCredit = 0;
        }
    }
}
