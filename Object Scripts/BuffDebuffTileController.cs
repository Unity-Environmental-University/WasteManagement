using System.Collections.Generic;
using UnityEngine;

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
        [Tooltip("Trigger size in world units, used when the tile has no collider of its own.")]
        [SerializeField] private Vector3 triggerWorldSize = new(5.114832f, 1.5f, 3.690889f);

        [Header("Effects")]
        [SerializeField] private List<BuffDebuffTileEffect> effects = new();

        // Issues already affected by this tile, so the effect fires at most once per issue.
        private readonly HashSet<EntityId> _affectedIssueIds = new();

        public BuffDebuffKind Kind => kind;

        private void Awake()
        {
            EnsureTriggerCollider();
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

            // The tile mesh is scaled very flat, so convert the desired world-space trigger
            // size into local size — otherwise the trigger would be too thin to catch issues.
            var scale = transform.lossyScale;
            box.size = new Vector3(
                triggerWorldSize.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                triggerWorldSize.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                triggerWorldSize.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
        }

        private void ApplyColor()
        {
            var color = kind == BuffDebuffKind.Buff ? buffColor : debuffColor;
            foreach (var rend in GetComponentsInChildren<Renderer>(true))
                rend.material.color = color;
        }
    }
}
