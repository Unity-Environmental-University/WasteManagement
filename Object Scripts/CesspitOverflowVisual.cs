using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     The brimmed-over look of a full cesspit: a heap of waste bulging above the rim, tongues of
    ///     it running down the outside, blobs slopping over the edge, and a puddle around the base.
    ///     Built from primitives on the waste's own material the first time a pit overflows, and
    ///     eased in and out as the pit fills and recovers.
    /// </summary>
    public class CesspitOverflowVisual : MonoBehaviour
    {
        private const float FadeSpeed = 1.5f;
        private const int TongueCount = 7;
        private const int LumpCount = 4;

        private Transform _mound;
        private Transform[] _tongues;
        private Vector3[] _tongueScales;
        private Transform[] _lumps;
        private Vector3[] _lumpPositions;
        private Transform _puddle;
        private ParticleSystem _slop;
        private Vector3 _moundScale;
        private Vector3 _puddleScale;
        private float _amount;

        public bool Showing { get; private set; }

        /// <summary>
        ///     Heights are world space, like the rest of the pit's fill visuals. The visual's root
        ///     cancels the pit's non-uniform scale, so everything under it is sized in world units.
        /// </summary>
        public static CesspitOverflowVisual Create(Transform pit, Material waste, float rimY, float rimRadius,
            float moundHeight)
        {
            var root = new GameObject("Overflow");
            root.transform.SetParent(pit, false);
            var pitScale = pit.lossyScale;
            root.transform.localScale = new Vector3(1f / pitScale.x, 1f / pitScale.y, 1f / pitScale.z);

            var visual = root.AddComponent<CesspitOverflowVisual>();
            visual.Build(waste, rimY - pit.position.y, rimRadius, moundHeight);
            root.SetActive(false);
            return visual;
        }

        private void Build(Material waste, float rimHeight, float rimRadius, float moundHeight)
        {
            var moundDiameter = rimRadius * 2.06f;
            _moundScale = new Vector3(moundDiameter, moundHeight * 2f, moundDiameter);
            _mound = CreateBlob(waste, new Vector3(0f, rimHeight, 0f), _moundScale);

            // The pit's origin sits on the ground; the vessel's mesh carries on down below the board.
            var wallHeight = Mathf.Max(0.1f, rimHeight);
            _tongues = new Transform[TongueCount];
            _tongueScales = new Vector3[TongueCount];
            for (var i = 0; i < TongueCount; i++)
            {
                // Uneven spacing and lengths, so it reads as a spill and not a pattern.
                var angle = (i + 0.35f * Mathf.Sin(i * 2.4f)) * Mathf.PI * 2f / TongueCount;
                var length = wallHeight * (0.75f + 0.45f * Mathf.Abs(Mathf.Sin(i * 1.7f)));
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                _tongueScales[i] = new Vector3(rimRadius * 0.3f, length * 1.3f, rimRadius * 0.3f);
                _tongues[i] = CreateBlob(waste, outward * (rimRadius * 0.96f) + Vector3.up * (rimHeight - length * 0.4f),
                    _tongueScales[i]);
            }

            _lumps = new Transform[LumpCount];
            _lumpPositions = new Vector3[LumpCount];
            for (var i = 0; i < LumpCount; i++)
            {
                var angle = i * 2.3f;
                _lumpPositions[i] = new Vector3(Mathf.Cos(angle) * rimRadius * 0.45f, rimHeight + moundHeight * 0.72f,
                    Mathf.Sin(angle) * rimRadius * 0.45f);
                _lumps[i] = CreateBlob(waste, _lumpPositions[i], Vector3.one * (rimRadius * (0.3f + 0.08f * i)));
            }

            var puddleDiameter = rimRadius * 2.35f;
            // A low dome, not a thin sheet, so the tilted board doesn't slice through it.
            _puddleScale = new Vector3(puddleDiameter, 0.5f, puddleDiameter);
            _puddle = CreateBlob(waste, Vector3.up * 0.1f, _puddleScale);

            _slop = CreateSlop(waste, rimHeight, rimRadius);
        }

        private Transform CreateBlob(Material waste, Vector3 localPosition, Vector3 scale)
        {
            var blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            blob.name = "Waste Blob";
            // Purely visual: it must not catch clicks meant for the pit or trip issue triggers.
            Destroy(blob.GetComponent<Collider>());
            blob.transform.SetParent(transform, false);
            blob.transform.localPosition = localPosition;
            blob.transform.localScale = scale;
            blob.GetComponent<Renderer>().sharedMaterial = waste;
            return blob.transform;
        }

        private ParticleSystem CreateSlop(Material waste, float rimHeight, float rimRadius)
        {
            var slopObject = new GameObject("Slop");
            slopObject.transform.SetParent(transform, false);
            slopObject.transform.localPosition = Vector3.up * rimHeight;

            var slop = slopObject.AddComponent<ParticleSystem>();
            slop.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = slop.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 0.55f;
            main.startSpeed = 0.9f;
            main.startSize = new ParticleSystem.MinMaxCurve(rimRadius * 0.1f, rimRadius * 0.22f);
            main.gravityModifier = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 32;

            var emission = slop.emission;
            emission.rateOverTime = 14f;

            var shape = slop.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = rimRadius;
            shape.radiusThickness = 0f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            var particleRenderer = slop.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            particleRenderer.mesh = _mound.GetComponent<MeshFilter>().sharedMesh;
            particleRenderer.sharedMaterial = waste;
            return slop;
        }

        private void Update()
        {
            _amount = Mathf.MoveTowards(_amount, Showing ? 1f : 0f, FadeSpeed * Time.deltaTime);
            ApplyAmount();

            if (!Showing && _amount <= 0f)
                gameObject.SetActive(false);
        }

        /// <summary>Eases the visual in or out. Fading out deactivates it once it's gone.</summary>
        public void SetShowing(bool showing)
        {
            Showing = showing;
            if (showing)
            {
                gameObject.SetActive(true);
                if (!_slop.isPlaying) _slop.Play(true);
            }
            else if (_slop.isPlaying)
            {
                _slop.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void ApplyAmount()
        {
            var eased = Mathf.SmoothStep(0f, 1f, _amount);
            var time = Time.time;

            // The heap heaves slowly and the lumps roll on it, so a full pit never looks static.
            var heave = 1f + 0.06f * Mathf.Sin(time * 1.8f);
            _mound.localScale = new Vector3(_moundScale.x, _moundScale.y * eased * heave, _moundScale.z);

            for (var i = 0; i < _tongues.Length; i++)
            {
                var stretch = 1f + 0.08f * Mathf.Sin(time * 1.3f + i * 1.9f);
                var scale = _tongueScales[i];
                _tongues[i].localScale = new Vector3(scale.x * eased, scale.y * eased * stretch, scale.z * eased);
            }

            for (var i = 0; i < _lumps.Length; i++)
            {
                var bob = 0.04f * Mathf.Sin(time * 2.2f + i * 1.4f);
                _lumps[i].localPosition = _lumpPositions[i] + Vector3.up * bob;
                _lumps[i].gameObject.SetActive(eased > 0.5f);
            }

            _puddle.localScale = new Vector3(_puddleScale.x * eased, _puddleScale.y, _puddleScale.z * eased);
        }
    }
}
