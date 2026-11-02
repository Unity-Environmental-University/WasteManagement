using System;
using System.Collections.Generic;
using _project.Scripts.Core;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>How a <see cref="PathSplitter" /> treats one issue type.</summary>
    public enum SplitterRule
    {
        /// <summary>Shared between connected outlets by the splitter's percentage.</summary>
        Split,

        /// <summary>Always kept on the main route.</summary>
        MainOnly,

        /// <summary>Always diverted down the branch.</summary>
        BranchOnly,

        /// <summary>Always diverted down the second branch (output 3).</summary>
        ThirdOnly
    }

    /// <summary>
    ///     Divides issues between the main route and up to two alternate branches. Each
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
        [SerializeField, Range(0, 100)] private int thirdSharePercent;

        public int BranchSharePercent => 100 - mainSharePercent - thirdSharePercent;
        public int ThirdSharePercent => thirdSharePercent;

        /// <summary>The share of output 0 (main), 1 (branch) or 2 (third).</summary>
        public int SharePercent(int output) =>
            output switch { 0 => mainSharePercent, 1 => BranchSharePercent, _ => thirdSharePercent };

        // Blue, amber and a reddish purple (after the Okabe-Ito palette) stay apart under the
        // common color-vision deficiencies; the outlet numbers back them up.
        public static Color OutputColor(int output) => output switch
        {
            0 => new Color(0.25f, 0.72f, 0.94f),
            1 => new Color(1f, 0.69f, 0.22f),
            _ => new Color(0.86f, 0.5f, 0.74f)
        };

        public void SetShares(int main, int third)
        {
            mainSharePercent = Mathf.Clamp(main, 0, 100);
            thirdSharePercent = Mathf.Clamp(third, 0, 100 - mainSharePercent);
            Array.Clear(_credits, 0, _credits.Length);
        }

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

        // Smooth weighted round robin balances the three outlets without random streaks.
        private readonly int[] _credits = new int[3];
        private Renderer[] _modelRenderers;
        private Renderer[] _pipeStubs;
        private Renderer _mainHandle;
        private Renderer _branchHandle;
        private Renderer _thirdHandle;
        private readonly TMPro.TextMeshPro[] _outletLabels = new TMPro.TextMeshPro[3];

        // Which outputs (0–2) have pipe leaving this splitter, finished or not.
        private readonly bool[] _outlets = new bool[3];

        // Output (0–2) each pipe stub feeds, or -1 for the intake and unused sides.
        private int[] _stubOutputs = Array.Empty<int>();
        private Color? _highlight;
        private MaterialPropertyBlock _colorBlock;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        /// <summary>Share (0–100) of split issues kept on the main route; the rest take the branch.</summary>
        public int MainSharePercent
        {
            get => mainSharePercent;
            set => SetShares(value, thirdSharePercent);
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

        /// <summary>True while pipe leaves this splitter through <paramref name="output" /> (0–2).</summary>
        public bool HasOutlet(int output) => _outlets[output];

        /// <summary>Tints the model with <paramref name="color" />, or restores it when null.</summary>
        public void SetHighlight(Color? color)
        {
            _highlight = color;
            ApplyColors();
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
            Vector3? thirdOutlet = null;
            foreach (var path in paths)
            {
                if (!path || !path.UsesBoard(board)) continue;
                onRoute |= path.CollectFlowOffsets(transform.position, FlowOffsets);
                path.GetOutletOffsets(transform.position, ref mainOutlet, ref branchOutlet, ref thirdOutlet);
            }

            if (!onRoute)
                foreach (var step in PathBuildBoard.CellNeighborOffsets)
                    if (board.IsOccupied(cell + step))
                        FlowOffsets.Add(board.GetCellTopPosition(cell + step) - board.GetCellTopPosition(cell));

            ConnectedSides.Clear();
            foreach (var offset in FlowOffsets)
                ConnectedSides.Add(GetSide(transform.InverseTransformDirection(offset)));

            _outlets[0] = mainOutlet.HasValue;
            _outlets[1] = branchOutlet.HasValue;
            _outlets[2] = thirdOutlet.HasValue;
            var outletSides = new Vector2Int?[]
            {
                ToSide(mainOutlet), ToSide(branchOutlet), ToSide(thirdOutlet)
            };
            for (var i = 0; i < _pipeStubs.Length; i++)
            {
                var side = GetModelSide(_pipeStubs[i]);
                _pipeStubs[i].enabled = ConnectedSides.Contains(side);
                _stubOutputs[i] = Array.IndexOf(outletSides, side);
            }

            PointHandle(_mainHandle, mainOutlet);
            PointHandle(_branchHandle, branchOutlet);
            PointHandle(_thirdHandle, thirdOutlet);
            ApplyColors();
            UpdateOutletLabel(0, _mainHandle);
            UpdateOutletLabel(1, _branchHandle);
            UpdateOutletLabel(2, _thirdHandle);
        }

        private Vector2Int? ToSide(Vector3? offset)
        {
            return offset.HasValue ? GetSide(transform.InverseTransformDirection(offset.Value)) : null;
        }

        // Each outlet's number sits on top of its valve handle, so it reads from above even where
        // the colors alone don't tell the outlets apart.
        private void UpdateOutletLabel(int output, Renderer handle)
        {
            var label = _outletLabels[output];
            if (!label) return;

            var shown = handle && handle.enabled;
            label.gameObject.SetActive(shown);
            if (!shown) return;

            var bounds = handle.bounds;
            label.transform.position = new Vector3(bounds.center.x, bounds.max.y + 0.02f, bounds.center.z);
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

        // Each share field has its own 0-100 slider, so Inspector or prefab values can add up past 100
        // and leave the branch a negative share; trim the third outlet to fit, as SetShares does.
        private void OnValidate() => ClampThirdShare();

        private void ClampThirdShare() => thirdSharePercent = Mathf.Clamp(thirdSharePercent, 0, 100 - mainSharePercent);

        private void Awake()
        {
            ClampThirdShare();
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
            if (_branchHandle)
            {
                _thirdHandle = Instantiate(_branchHandle, _branchHandle.transform.parent);
                _thirdHandle.name = "ValvePurple";
                _thirdHandle.enabled = false;
            }
            _stubOutputs = new int[_pipeStubs.Length];
            Array.Fill(_stubOutputs, -1);
            if (_mainHandle || _branchHandle)
                for (var i = 0; i < 3; i++)
                {
                    var label = new GameObject($"Output {i + 1}").AddComponent<TMPro.TextMeshPro>();
                    label.transform.SetParent(transform, false);
                    label.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    label.rectTransform.sizeDelta = new Vector2(1f, 1f);
                    label.text = (i + 1).ToString();
                    label.fontSize = 10;
                    label.fontStyle = TMPro.FontStyles.Bold;
                    label.alignment = TMPro.TextAlignmentOptions.Center;
                    // Dark numerals keep more contrast than white on all three handle colors.
                    label.color = new Color(0.06f, 0.08f, 0.11f);
                    label.gameObject.layer = ignoreRaycastLayer;
                    label.gameObject.SetActive(false);
                    _outletLabels[i] = label;
                }
            ApplyColors();
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
            if (!path || path.PathKind != PathKind.Pipe) return false;
            var hasBranch = path.TryGetBranchRoute(issue.GetRouteIndex(), transform.position, out var branchRoute);
            var hasThird = path.TryGetBranchRoute(issue.GetRouteIndex(), transform.position, out var thirdRoute, 2);
            if (!hasBranch && !hasThird) return false;
            if (!_routedIssueIds.Add(issue.GetEntityId())) return false;

            var output = ChooseOutput(issue.GetIssueType(), hasThird, hasBranch);
            var destination = output == 2 ? thirdRoute : branchRoute;
            if (output != 0 && !issue.TrySetRoute(destination))
            {
                _routedIssueIds.Remove(issue.GetEntityId());
                return false;
            }
            if (Debugging) Debug.Log($"[PathSplitter] Routed issue to output {output + 1}.");

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

        /// <summary>
        ///     Unavailable pinned outlets fall back to the main route; no issues are lost. While output 2
        ///     is still being built and output 3 already works, output 3 is the splitter's one branch and
        ///     takes output 2's traffic too, so a finished loop beside an unfinished stub isn't starved.
        /// </summary>
        public int ChooseOutput(IssueType issueType, bool hasThird = true, bool hasBranch = true)
        {
            var thirdStandsIn = !hasBranch && hasThird;
            switch (GetRule(issueType))
            {
                case SplitterRule.MainOnly: return 0;
                case SplitterRule.BranchOnly: return hasBranch ? 1 : thirdStandsIn ? 2 : 0;
                case SplitterRule.ThirdOnly: return hasThird ? 2 : 0;
            }

            int Share(int output) => output switch
            {
                1 => hasBranch ? BranchSharePercent : 0,
                2 => hasThird ? thirdSharePercent + (thirdStandsIn ? BranchSharePercent : 0) : 0,
                _ => mainSharePercent
            };

            var total = Share(0) + Share(1) + Share(2);
            if (total == 0) return 0;
            var winner = -1;
            for (var i = 0; i < 3; i++)
            {
                var share = Share(i);
                if (share == 0) continue;
                _credits[i] += share;
                if (winner < 0 || _credits[i] > _credits[winner]) winner = i;
            }
            _credits[winner] -= total;
            return winner;
        }

        /// <summary>
        ///     Paints each outlet's handle and pipe stub its output color over a plain base map, so the
        ///     hue matches the panel instead of being shifted by the model's texture. The body and
        ///     intake keep their texture and take the highlight, if any.
        /// </summary>
        private void ApplyColors()
        {
            if (_modelRenderers == null) return;

            _colorBlock ??= new MaterialPropertyBlock();
            foreach (var modelRenderer in _modelRenderers)
                if (modelRenderer) ApplyColor(modelRenderer, GetOutput(modelRenderer));
            if (_thirdHandle) ApplyColor(_thirdHandle, 2);
        }

        private void ApplyColor(Renderer modelRenderer, int output)
        {
            _colorBlock.Clear();
            Color? color = output >= 0 ? OutputColor(output) : _highlight;
            if (color.HasValue)
            {
                _colorBlock.SetColor(RendererColorUtility.BaseColorId, color.Value);
                _colorBlock.SetColor(RendererColorUtility.ColorId, color.Value);
            }

            if (output >= 0)
            {
                _colorBlock.SetTexture(BaseMapId, Texture2D.whiteTexture);
                _colorBlock.SetTexture(MainTexId, Texture2D.whiteTexture);
            }

            modelRenderer.SetPropertyBlock(_colorBlock.isEmpty ? null : _colorBlock);
        }

        private int GetOutput(Renderer modelRenderer)
        {
            if (modelRenderer == _mainHandle) return 0;
            if (modelRenderer == _branchHandle) return 1;
            if (modelRenderer == _thirdHandle) return 2;
            var stub = Array.IndexOf(_pipeStubs, modelRenderer);
            return stub >= 0 ? _stubOutputs[stub] : -1;
        }

        private void ResetSplit()
        {
            _routedIssueIds.Clear();
            Array.Clear(_credits, 0, _credits.Length);
        }
    }
}
