using System.Collections.Generic;
using _project.Scripts.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace _project.Scripts.Object_Scripts
{
    public enum BuffDebuffKind
    {
        Buff,
        Debuff
    }

    /// <summary>
    ///     A single buff/debuff tile sitting on one board cell. It owns the tile identity,
    ///     trigger setup, one-time issue hit detection, and the effect assets assigned by
    ///     the spawner.
    /// </summary>
    public class BuffDebuffTileController : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private BuffDebuffKind kind = BuffDebuffKind.Buff;
        [SerializeField] private Color buffColor = new(0.30f, 0.85f, 0.40f, 1f);
        [SerializeField] private Color debuffColor = new(0.85f, 0.30f, 0.30f, 1f);

        [Tooltip("Material drawn on top of speed-debuff tiles (the down-chevron marker). Kept square on the tile.")]
        [SerializeField] private Material slowMarkerMaterial;

        [Header("Triggering")]
        [Tooltip("How tall the trigger reaches in world units, so issues passing over the flat tile are caught.")]
        [SerializeField] private float triggerWorldHeight = 1.5f;

        [Tooltip("Keeps this trigger from blocking pointer clicks on utility slots occupying the same cell.")]
        [SerializeField] private bool ignorePointerRaycasts = true;

        [Header("Effects")]
        [SerializeField] private List<BuffDebuffTileEffect> effects = new();

        // Issues already affected by this tile, so the effect fires at most once per issue.
        private readonly HashSet<EntityId> _affectedIssueIds = new();

        private MaterialPropertyBlock _colorPropertyBlock;

        // The tile's own renderers, without the slow marker.
        private Renderer[] _bodyRenderers;

        // Double down-chevron shown on speed-debuff tiles; built on demand.
        private MeshRenderer _slowMarker;

        private void OnEnable()
        {
            LiveComponentRegistry.Register(this);
        }

        private void OnDisable()
        {
            LiveComponentRegistry.Unregister(this);
        }

        // Scene whose PhysicsRaycasters have already had the Ignore Raycast layer stripped.
        private static SceneHandle _raycastersPatchedSceneHandle;

        public BuffDebuffKind Kind => kind;
        public IReadOnlyList<BuffDebuffTileEffect> Effects => effects;

        private void Awake()
        {
            if (ignorePointerRaycasts)
            {
                SetLayerRecursively(transform, LayerMask.NameToLayer("Ignore Raycast"));
                ExcludeLayerFromPointerRaycasters();
            }

            EnsureTriggerCollider();
            CacheBodyRenderers();
        }

        // Gathered before the marker child exists, so tinting never touches it. Lazy as well, since
        // edit-mode spawning and inactive tiles can be set up before Awake runs.
        private void CacheBodyRenderers()
        {
            _bodyRenderers ??= GetComponentsInChildren<Renderer>(true);
        }

        /// <summary>
        ///     The Ignore Raycast layer only exempts objects from default-mask physics queries;
        ///     PhysicsRaycaster defaults to Everything, so pointer events would still hit this
        ///     tile's tall trigger and swallow clicks meant for a slot sharing the cell. Strip
        ///     the layer from every raycaster's event mask (including the inactive phase camera).
        ///     Runs once per scene load; a PhysicsRaycaster instantiated after that is not
        ///     covered and must exclude the layer itself.
        /// </summary>
        private static void ExcludeLayerFromPointerRaycasters()
        {
            var sceneHandle = SceneManager.GetActiveScene().handle;
            if (_raycastersPatchedSceneHandle.Equals(sceneHandle)) return;
            _raycastersPatchedSceneHandle = sceneHandle;

            var mask = ~(1 << LayerMask.NameToLayer("Ignore Raycast"));
            foreach (var raycaster in FindObjectsByType<PhysicsRaycaster>(FindObjectsInactive.Include))
                raycaster.eventMask &= mask;
        }

        private void Start()
        {
            ApplyColor();
        }

        public void SetKind(BuffDebuffKind newKind)
        {
            // A tile that changes identity may affect each issue once more (e.g., a buff
            // flipped to a debuff when a cesspit is buried on its cell).
            if (kind != newKind)
                _affectedIssueIds.Clear();

            kind = newKind;
            ApplyColor();
        }

        public void SetEffect(BuffDebuffTileEffect effect)
        {
            effects.Clear();

            if (effect)
                effects.Add(effect);

            RefreshSlowMarker();
        }

        // Whether this tile slows the issues that cross it.
        private bool IsSpeedDebuff =>
            kind == BuffDebuffKind.Debuff && effects.Exists(effect => effect is MoveSpeedBuffDebuffTileEffect);

        private void OnTriggerEnter(Collider other)
        {
            var issue = other.GetComponent<IssueObject>();
            if (!issue)
                issue = other.GetComponentInParent<IssueObject>();
            if (!issue)
                return;

            if (!_affectedIssueIds.Add(issue.GetEntityId()))
                return;

            ApplyEffects(issue);
        }

        private void ApplyEffects(IssueObject issue)
        {
            var context = new BuffDebuffEffectContext(this, issue);

            foreach (var effect in effects)
            {
                if (effect)
                    effect.Apply(context);
            }
        }

        private void EnsureTriggerCollider()
        {
            // Reuse the tile's box collider if present (e.g., the spawner's cube primitive);
            // only fall back to another collider type, or add a box when there isn't one.
            if (!TryGetComponent<BoxCollider>(out var box))
            {
                if (TryGetComponent<Collider>(out var existing))
                {
                    existing.isTrigger = true;
                    return;
                }

                box = gameObject.AddComponent<BoxCollider>();
            }

            box.isTrigger = true;
            box.center = Vector3.zero;

            // Keep the collider's X/Z footprint (already matches the tile), but make sure it reaches
            // triggerWorldHeight in world space — the tile mesh is flat, so divide out its Y scale.
            var size = box.size;
            size.y = triggerWorldHeight / AbsLossyScale().y;
            box.size = size;
        }

        private void ApplyColor()
        {
            // Property-block tint: `.material.color` would clone a material instance per
            // renderer per tile, breaking batching for the whole tile population.
            var color = kind == BuffDebuffKind.Buff ? buffColor : debuffColor;
            CacheBodyRenderers();
            foreach (var rend in _bodyRenderers)
                if (rend)
                    RendererColorUtility.SetColor(rend, color, ref _colorPropertyBlock);

            RefreshSlowMarker();
        }

        private void RefreshSlowMarker()
        {
            if (!IsSpeedDebuff || !slowMarkerMaterial)
            {
                if (_slowMarker) _slowMarker.gameObject.SetActive(false);
                return;
            }

            if (!_slowMarker)
            {
                CacheBodyRenderers();
                var marker = new GameObject("Slow Marker", typeof(MeshFilter), typeof(MeshRenderer));
                marker.layer = gameObject.layer;
                marker.transform.SetParent(transform, false);
                // Quads face -Z; tipping one 90° about X lays it flat facing up, with the image's
                // top edge toward +Z so its chevrons point down the screen from the board camera.
                marker.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                marker.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                _slowMarker = marker.GetComponent<MeshRenderer>();
                _slowMarker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _slowMarker.receiveShadows = false;
                _slowMarker.sharedMaterial = slowMarkerMaterial;
            }

            // Sit just above the cube's top face, and undo the tile's stretch so the art stays
            // square at the tile's shorter side.
            // The quad's local Y maps to the tile's Z once tipped flat.
            var scale = AbsLossyScale();
            var side = Mathf.Min(scale.x, scale.z);
            _slowMarker.transform.localPosition = new Vector3(0f, 0.5f + 0.02f / scale.y, 0f);
            _slowMarker.transform.localScale = new Vector3(side / scale.x, side / scale.z, 1f);
            _slowMarker.gameObject.SetActive(true);
        }

        // Per-axis world scale, kept positive and non-zero so it can be divided out.
        private Vector3 AbsLossyScale()
        {
            var scale = transform.lossyScale;
            return new Vector3(
                Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;

            for (var i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i), layer);
        }
    }
}
