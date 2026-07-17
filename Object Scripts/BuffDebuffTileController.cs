using System.Collections.Generic;
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

        [Header("Triggering")]
        [Tooltip("How tall the trigger reaches in world units, so issues passing over the flat tile are caught.")]
        [SerializeField] private float triggerWorldHeight = 1.5f;

        [Tooltip("Keeps this trigger from blocking pointer clicks on utility slots occupying the same cell.")]
        [SerializeField] private bool ignorePointerRaycasts = true;

        [Header("Effects")]
        [SerializeField] private List<BuffDebuffTileEffect> effects = new();

        // Issues already affected by this tile, so the effect fires at most once per issue.
        private readonly HashSet<EntityId> _affectedIssueIds = new();

        // Scene whose PhysicsRaycasters have already had the Ignore Raycast layer stripped.
        private static SceneHandle _raycastersPatchedSceneHandle;

        public BuffDebuffKind Kind => kind;

        private void Awake()
        {
            if (ignorePointerRaycasts)
            {
                SetLayerRecursively(transform, LayerMask.NameToLayer("Ignore Raycast"));
                ExcludeLayerFromPointerRaycasters();
            }

            EnsureTriggerCollider();
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
            kind = newKind;
            ApplyColor();
        }

        public void SetEffect(BuffDebuffTileEffect effect)
        {
            effects.Clear();

            if (effect)
                effects.Add(effect);
        }

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
            var scaleY = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));
            var size = box.size;
            size.y = triggerWorldHeight / scaleY;
            box.size = size;
        }

        private void ApplyColor()
        {
            var color = kind == BuffDebuffKind.Buff ? buffColor : debuffColor;
            foreach (var rend in GetComponentsInChildren<Renderer>(true))
                rend.material.color = color;
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;

            for (var i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i), layer);
        }
    }
}
