using System.Collections.Generic;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Renders a path polyline as a solid, extruded tube of flowing water. Being real geometry, it
    ///     keeps its raised profile from every camera angle.
    ///     Mesh layout expected by the <c>WasteManagement/PathWaterPreview</c> shader: U = world distance
    ///     along the path, V = position across the channel (0–1), outward normals for the silhouette
    ///     edge fade, and a per-vertex tint.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PathWaterTube : MonoBehaviour
    {
        private const string WaterShaderName = "WasteManagement/PathWaterPreview";

        // Vertices around each cross-section ring; +1 duplicates the seam vertex so UVs don't pinch.
        private const int RadialSegments = 12;
        private const int RingVertices = RadialSegments + 1;

        // Rings used to round each open end into a hemisphere.
        private const int CapRings = 4;

        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<Vector2> _uvs = new();
        private readonly List<Color> _colors = new();
        private readonly List<int> _triangles = new();

        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private Material _generatedMaterial;

        private AnimationCurve _widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
        private float _widthMultiplier = 1f;

        /// <summary>True while a tube is currently built and visible (≥ 2 path points).</summary>
        public bool IsShowing => PointCount > 0;

        /// <summary>Number of path points the tube currently spans (0 while hidden).</summary>
        public int PointCount { get; private set; }

        private void OnDestroy()
        {
            if (_mesh) Destroy(_mesh);
            if (_generatedMaterial) Destroy(_generatedMaterial);
        }

        /// <summary>
        ///     Sets the tube's styling; call before <see cref="SetPath" />. Width along the path is
        ///     <paramref name="widthMultiplier" /> × <paramref name="widthCurve" /> evaluated at the
        ///     normalized distance (the LineRenderer rule, so an authored taper carries over). A missing
        ///     or broken material falls back to the water shader.
        /// </summary>
        public void Configure(Material material, AnimationCurve widthCurve, float widthMultiplier)
        {
            EnsureComponents();
            _meshRenderer.sharedMaterial = IsUsable(material) ? material : GetFallbackMaterial();
            if (widthCurve != null && widthCurve.length > 0) _widthCurve = widthCurve;
            _widthMultiplier = Mathf.Max(0.001f, widthMultiplier);
        }

        /// <summary>
        ///     Rebuilds the tube so it spans <paramref name="points" /> (world space), or hides it when
        ///     there are too few points to form a segment.
        /// </summary>
        /// <param name="up">Reference up axis used to orient each ring (usually the board's up).</param>
        /// <param name="color">Per-vertex tint multiplied into the water shader.</param>
        public void SetPath(IReadOnlyList<Vector3> points, Vector3 up, Color color)
        {
            EnsureComponents();

            if (points == null || points.Count < 2)
            {
                Clear();
                return;
            }

            up = up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up;

            _vertices.Clear();
            _normals.Clear();
            _uvs.Clear();
            _colors.Clear();
            _triangles.Clear();

            var totalLength = 0f;
            for (var i = 1; i < points.Count; i++)
                totalLength += Vector3.Distance(points[i], points[i - 1]);
            totalLength = Mathf.Max(totalLength, 1e-4f);

            var distanceAlong = 0f;
            for (var i = 0; i < points.Count; i++)
            {
                if (i > 0) distanceAlong += Vector3.Distance(points[i], points[i - 1]);

                var toNext = i < points.Count - 1 ? (points[i + 1] - points[i]).normalized : Vector3.zero;
                var fromPrev = i > 0 ? (points[i] - points[i - 1]).normalized : Vector3.zero;

                // Tangent = the bisector of the two adjacent segments so corners don't kink; a full
                // U-turn cancels the sum, so fall back to the incoming direction.
                var tangent = toNext + fromPrev;
                tangent = tangent.sqrMagnitude > 1e-6f ? tangent.normalized : fromPrev;
                if (tangent.sqrMagnitude < 1e-6f) tangent = Vector3.forward;

                GetFrame(tangent, up, out var side, out var lift);

                // Miter: stretch the ring across the bend so the stream keeps its full width through
                // a corner instead of pinching (≈1.41× at the board's 90° turns).
                var miter = 1f;
                if (i > 0 && i < points.Count - 1)
                    miter = 1f / Mathf.Max(0.35f, Vector3.Dot(fromPrev, tangent));

                var radius = GetRadius(distanceAlong / totalLength);

                if (i == 0) AddCap(points[i], -tangent, side, lift, radius, distanceAlong, color, false);
                AddRing(points[i], side, lift, miter, radius, Vector3.zero, 1f, distanceAlong, color);
                if (i == points.Count - 1) AddCap(points[i], tangent, side, lift, radius, distanceAlong, color, true);
            }

            var ringCount = _vertices.Count / RingVertices;
            for (var r = 0; r < ringCount - 1; r++)
            {
                var ringA = r * RingVertices;
                var ringB = ringA + RingVertices;
                for (var j = 0; j < RadialSegments; j++)
                {
                    var a = ringA + j;
                    var b = a + 1;
                    var c = ringB + j;
                    var d = c + 1;
                    _triangles.Add(a);
                    _triangles.Add(b);
                    _triangles.Add(c);
                    _triangles.Add(b);
                    _triangles.Add(d);
                    _triangles.Add(c);
                }
            }

            // The object sits under the (scaled) build board, so convert the world-space geometry
            // into this transform's space; the renderer then culls against correct bounds.
            var worldToLocal = transform.worldToLocalMatrix;
            for (var v = 0; v < _vertices.Count; v++)
            {
                _vertices[v] = worldToLocal.MultiplyPoint3x4(_vertices[v]);
                _normals[v] = worldToLocal.MultiplyVector(_normals[v]).normalized;
            }

            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetNormals(_normals);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_triangles, 0); // also recalculates bounds

            _meshRenderer.enabled = true;
            PointCount = points.Count;
        }

        /// <summary>Hides the tube; the mesh keeps its buffers for the next <see cref="SetPath" />.</summary>
        public void Clear()
        {
            if (_meshRenderer) _meshRenderer.enabled = false;
            PointCount = 0;
        }

        private float GetRadius(float normalizedDistance)
        {
            return Mathf.Max(0.001f, _widthMultiplier * _widthCurve.Evaluate(normalizedDistance) * 0.5f);
        }

        private static void GetFrame(Vector3 tangent, Vector3 up, out Vector3 side, out Vector3 lift)
        {
            // 'side' spans the channel, 'lift' rises toward 'up'.
            side = Vector3.Cross(up, tangent);
            if (side.sqrMagnitude < 1e-6f) side = Vector3.Cross(Vector3.right, tangent);
            side.Normalize();
            lift = Vector3.Cross(tangent, side).normalized;
        }

        /// <summary>
        ///     Rounds an open end into a hemisphere bulging along <paramref name="outward" />. Rings are
        ///     emitted in path order: for the start cap from the tip inward, for the end cap outward.
        /// </summary>
        private void AddCap(Vector3 center, Vector3 outward, Vector3 side, Vector3 lift, float radius,
            float u, Color color, bool isEnd)
        {
            for (var k = 0; k < CapRings; k++)
            {
                var step = isEnd ? k + 1 : CapRings - k;
                var phi = (float)step / CapRings * Mathf.PI * 0.5f;
                var sin = Mathf.Sin(phi);
                var cos = Mathf.Cos(phi);
                AddRing(center + outward * (sin * radius), side, lift, 1f, radius * cos, outward * sin, cos,
                    u + (isEnd ? sin : -sin) * radius, color);
            }
        }

        /// <param name="sideScale">Extra stretch across the channel (the corner miter).</param>
        /// <param name="normalOffset">Added to the weighted radial normal; tilts cap rings outward.</param>
        private void AddRing(Vector3 center, Vector3 side, Vector3 lift, float sideScale, float radius,
            Vector3 normalOffset, float radialWeight, float u, Color color)
        {
            for (var j = 0; j < RingVertices; j++)
            {
                // Seam at the bottom so any stitching sits on the underside, away from the camera.
                var angle = -Mathf.PI * 0.5f + (float)j / RadialSegments * Mathf.PI * 2f;
                var cos = Mathf.Cos(angle);
                var sin = Mathf.Sin(angle);

                _vertices.Add(center + (cos * sideScale * side + sin * lift) * radius);
                _normals.Add((cos * side + sin * lift) * radialWeight + normalOffset);
                // V = position across the channel as seen from above; U = world distance along the path.
                _uvs.Add(new Vector2(u, 0.5f + 0.5f * cos));
                _colors.Add(color);
            }
        }

        private void EnsureComponents()
        {
            if (_mesh) return;

            _meshRenderer = GetComponent<MeshRenderer>();

            _mesh = new Mesh { name = "Path Water Tube" };
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = _mesh;

            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _meshRenderer.enabled = false;
        }

        /// <summary>
        ///     True when the material's shader actually compiles. Catches both a missing material and
        ///     one whose shader is broken/stripped (which renders magenta).
        /// </summary>
        private static bool IsUsable(Material material)
        {
            return material && material.shader && material.shader.isSupported &&
                   material.shader.name != "Hidden/InternalErrorShader";
        }

        private Material GetFallbackMaterial()
        {
            if (_generatedMaterial) return _generatedMaterial;

            var shader = Shader.Find(WaterShaderName);
            if (!shader) shader = Shader.Find("Sprites/Default");
            if (shader) _generatedMaterial = new Material(shader);
            return _generatedMaterial;
        }
    }
}
