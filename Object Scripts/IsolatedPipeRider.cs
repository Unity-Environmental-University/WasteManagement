using System.Collections.Generic;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Isolated pipe is a closed tube too slim for an issue's model, so while an issue travels
    ///     through it the model is hidden and a bulge in the pipe slides along in its place, as if
    ///     the pipe were swelling around whatever is being pushed through. The bulge is tinted by the
    ///     issue's type, and a clog swells and flashes the block highlight's orange, since the hidden
    ///     model can't show that warning. Colliders stay on, so sifters, merging and clicks work as usual.
    /// </summary>
    [RequireComponent(typeof(IssueObject))]
    public class IsolatedPipeRider : MonoBehaviour
    {
        private const float BulgeWidth = 1.45f; // Times the tube's diameter.
        private const float BulgeLength = 2.2f;
        private const float PulseSpeed = 14f;
        private const float PulseAmount = 0.05f;
        private const float BlockedSwell = 1.3f;
        private const float BlockedPulseAmount = 0.15f;

        private static readonly Color MetalBulge = new(0.86f, 0.9f, 0.96f);
        private static readonly Color OrganicBulge = new(0.66f, 0.52f, 0.36f);
        private static readonly Color ChemicalBulge = new(0.52f, 0.74f, 0.44f);
        private static readonly Color JunkBulge = new(0.58f, 0.58f, 0.64f);
        private static readonly Color BlockedBulge = new(1f, 0.45f, 0.05f); // The block outline's default.

        private readonly List<Renderer> _renderers = new();
        private readonly List<Renderer> _hidden = new();
        private IssueObject _issue;
        private Transform _bulge;
        private Renderer _bulgeRenderer;
        private MaterialPropertyBlock _colorBlock;
        private Vector3 _heading = Vector3.forward;

        /// <summary>True while the issue is inside isolated pipe and shown as a bulge.</summary>
        public bool InsideIsolatedPipe { get; private set; }

        private void Awake() => _issue = GetComponent<IssueObject>();

        private void LateUpdate()
        {
            var path = _issue.GetPath();
            var board = path ? path.Board : null;
            var inside = board && path.IsInsideIsolatedPipe(_issue.GetRouteIndex(), _issue.GetWaypointIndex());
            if (inside != InsideIsolatedPipe) SetInside(inside, board);
            if (!inside) return;

            // A model swapped in mid-trip (a merge, say) arrives visible; keep it out of sight.
            HideModel();
            PlaceBulge(path, board);
        }

        private void OnDisable()
        {
            if (InsideIsolatedPipe) SetInside(false, null);
        }

        private void OnDestroy()
        {
            if (_bulge) PathBuildBoard.DestroyObject(_bulge.gameObject);
        }

        private void SetInside(bool inside, PathBuildBoard board)
        {
            InsideIsolatedPipe = inside;
            if (inside)
            {
                HideModel();
                GetBulge(board).gameObject.SetActive(true);
                return;
            }

            foreach (var hidden in _hidden)
                if (hidden)
                    hidden.enabled = true;
            _hidden.Clear();
            if (_bulge) _bulge.gameObject.SetActive(false);
        }

        private void HideModel()
        {
            GetComponentsInChildren(true, _renderers);
            foreach (var model in _renderers)
            {
                if (!model.enabled) continue;
                model.enabled = false;
                _hidden.Add(model);
            }
        }

        /// <summary>Centers the bulge in the tube under the issue, stretched along the way it is heading.</summary>
        private void PlaceBulge(WaypointPath path, PathBuildBoard board)
        {
            var boardTransform = board.transform;
            var local = boardTransform.InverseTransformPoint(transform.position);
            local.y = board.IsolatedPipeCenterHeight;
            _bulge.position = boardTransform.TransformPoint(local);

            // Point along the pipe toward the next waypoint; slow or stalled issues keep their heading.
            var route = _issue.GetRouteIndex();
            var waypoint = _issue.GetWaypointIndex();
            if (waypoint < path.GetWaypointCount(route))
            {
                var toward = Vector3.ProjectOnPlane(path.GetPosition(route, waypoint) - _bulge.position,
                    boardTransform.up);
                if (toward.sqrMagnitude > 0.0001f) _heading = toward.normalized;
            }

            _bulge.rotation = Quaternion.LookRotation(_heading, boardTransform.up);

            var blocked = _issue.IsBlockingPipe;
            var wave = Mathf.Sin(Time.time * PulseSpeed);
            var pulse = 1f + wave * (blocked ? BlockedPulseAmount : PulseAmount);
            var width = BulgeWidth * (blocked ? BlockedSwell : 1f) * pulse;
            var length = BulgeLength * (blocked ? BlockedSwell : 1f) / pulse;
            var diameter = board.IsolatedPipeRadius * 2f * boardTransform.lossyScale.x;
            _bulge.localScale = new Vector3(width, width, length) * diameter;

            var color = BulgeColor(_issue.GetIssueType());
            if (blocked) color = Color.Lerp(color, BlockedBulge, 0.6f + 0.4f * wave);
            RendererColorUtility.SetColor(_bulgeRenderer, color, ref _colorBlock);
        }

        private Transform GetBulge(PathBuildBoard board)
        {
            if (_bulge) return _bulge;

            var bulge = new GameObject("Pipe Bulge") { layer = board.gameObject.layer };
            bulge.AddComponent<MeshFilter>().sharedMesh = IsolatedPipeVisual.GetSphere();
            _bulgeRenderer = bulge.AddComponent<MeshRenderer>();
            _bulgeRenderer.sharedMaterial = IsolatedPipeVisual.GetMetal(board);
            _bulge = bulge.transform;
            return _bulge;
        }

        private static Color BulgeColor(IssueType type) => type switch
        {
            IssueType.Metal => MetalBulge,
            IssueType.Organic => OrganicBulge,
            IssueType.Chemical => ChemicalBulge,
            _ => JunkBulge
        };
    }
}
