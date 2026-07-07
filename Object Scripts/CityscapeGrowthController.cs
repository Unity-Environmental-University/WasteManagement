using System.Collections.Generic;
using System.Linq;
using _project.Scripts.Core;
using DG.Tweening;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Reveals the background cityscape as the town level rises. Each town root's
    ///     buildings are revealed center-outward: a fraction is visible at level 1,
    ///     growing linearly until the full city is visible at <see cref="maxLevel" />.
    ///     Newly revealed buildings animate up from the ground via DOTween.
    /// </summary>
    public class CityscapeGrowthController : MonoBehaviour
    {
        [Header("Setup")] [SerializeField] private List<Transform> townRoots = new();

        [Header("Growth")] [SerializeField] private int maxLevel = 3;

        [Range(0f, 1f)] [SerializeField] private float startingVisibleFraction = 0.3f;

        [Header("Animation")] [SerializeField] private float growDuration = 1.5f;

        [SerializeField] private float growStagger = 0.15f;

        public bool debugging;

        private readonly List<BuildingEntry> _buildings = new();
        private int _visibleCount = -1;

        private void Awake()
        {
            CollectBuildings();
        }

        private void OnEnable()
        {
            TurnController.OnLevelChanged += HandleLevelChanged;
        }

        private void OnDisable()
        {
            TurnController.OnLevelChanged -= HandleLevelChanged;
            foreach (var building in _buildings)
                building.Transform.DOKill();
        }

        /// <summary>
        ///     Gathers all buildings under the town roots, ordered so towns reveal
        ///     center-outward and evenly across roots.
        /// </summary>
        private void CollectBuildings()
        {
            _buildings.Clear();

            var perTown = new List<List<BuildingEntry>>();
            foreach (var root in townRoots.Where(root => root))
            {
                var entries = new List<BuildingEntry>();
                foreach (Transform child in root)
                    entries.Add(new BuildingEntry(child, child.localScale));

                if (entries.Count == 0) continue;

                var center = entries.Aggregate(Vector3.zero, (sum, e) => sum + e.Transform.position) / entries.Count;
                entries.Sort((a, b) => PlanarDistance(a.Transform.position, center)
                    .CompareTo(PlanarDistance(b.Transform.position, center)));
                perTown.Add(entries);
            }

            // Interleave towns by normalized reveal order so all towns grow together.
            _buildings.AddRange(perTown
                .SelectMany(town => town.Select((entry, index) => (entry, progress: (index + 1f) / town.Count)))
                .OrderBy(pair => pair.progress)
                .Select(pair => pair.entry));

            if (debugging) Debug.Log($"[CityscapeGrowthController] Collected {_buildings.Count} buildings.");
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return (a - b).sqrMagnitude;
        }

        private void HandleLevelChanged(int level)
        {
            // The initial level fires before anything has been revealed; snap instead of animating.
            SetLevel(level, _visibleCount >= 0);
        }

        /// <summary>
        ///     Shows the fraction of the city that matches <paramref name="level" />.
        /// </summary>
        private void SetLevel(int level, bool animate = true)
        {
            if (_buildings.Count == 0) CollectBuildings();
            if (_buildings.Count == 0) return;

            var t = maxLevel > 1 ? Mathf.Clamp01((level - 1f) / (maxLevel - 1f)) : 1f;
            var fraction = Mathf.Lerp(startingVisibleFraction, 1f, t);
            var targetCount = Mathf.Clamp(Mathf.CeilToInt(_buildings.Count * fraction), 1, _buildings.Count);

            if (targetCount == _visibleCount) return;

            if (debugging)
                Debug.Log(
                    $"[CityscapeGrowthController] Level {level}: showing {targetCount}/{_buildings.Count} buildings.");

            var revealDelay = 0f;
            for (var i = 0; i < _buildings.Count; i++)
            {
                var building = _buildings[i];
                var shouldBeVisible = i < targetCount;
                var isNewlyRevealed = shouldBeVisible && i >= Mathf.Max(0, _visibleCount);

                building.Transform.DOKill();
                building.Transform.gameObject.SetActive(shouldBeVisible);

                if (!shouldBeVisible)
                {
                    building.Transform.localScale = building.OriginalScale;
                    continue;
                }

                if (isNewlyRevealed && animate)
                {
                    var flattened = building.OriginalScale;
                    flattened.y *= 0.02f;
                    building.Transform.localScale = flattened;
                    building.Transform.DOScale(building.OriginalScale, growDuration)
                        .SetDelay(revealDelay)
                        .SetEase(Ease.OutBack);
                    revealDelay += growStagger;
                }
                else
                {
                    building.Transform.localScale = building.OriginalScale;
                }
            }

            _visibleCount = targetCount;
        }

        private readonly struct BuildingEntry
        {
            public BuildingEntry(Transform transform, Vector3 originalScale)
            {
                Transform = transform;
                OriginalScale = originalScale;
            }

            public Transform Transform { get; }
            public Vector3 OriginalScale { get; }
        }
    }
}