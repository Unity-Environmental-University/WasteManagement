using System.Collections;
using System.Linq;
using _project.Scripts.Core;
using _project.Scripts.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

namespace _project.Scripts.Object_Scripts
{
    public class Cesspit : MonoBehaviour, IStinkSource, IPointerClickHandler
    { 
        [SerializeField] private int processPower = 3;
        [SerializeField] private GameObject runawayPrefab;
        [SerializeField] private Transform runawayDestination;
        [SerializeField] private float runawaySpawnInterval = 10f;
        [SerializeField] private float runawayMoveSpeed = 12f;
        [SerializeField] private Color runawayColor = new(1f, 0.45f, 0f);
        [SerializeField] private Material runawayMaterial;

        [Header("Stink")]
        [SerializeField] private float baseStink = 1f;
        [SerializeField] private float fullStinkBonus = 2f;

        [Header("Fill Visual")]
        [SerializeField] private Transform fillVisual;
        [SerializeField] private Renderer fillRenderer;
        [SerializeField] private Color emptyFillColor = new(0.42f, 0.45f, 0.2f);
        [SerializeField] private Color fullFillColor = new(0.3f, 0.18f, 0.08f);
        [SerializeField] private float minFillHeight = 0.03f;
        [SerializeField] private float maxFillHeight = 0.45f;

        [Header("Seal")]
        [SerializeField] private GameObject sealedVisual;

        [FormerlySerializedAs("healthBar")] public HealthBar fullnessBar;
        [FormerlySerializedAs("maxHealth")] public float maxFullness;
        [FormerlySerializedAs("health")] public float fullness;

        private bool _spawningRunaways;
        private Coroutine _runawayCoroutine;
        private SpecialInteractController _slot;
        private int _infraValue;
        private Material _fillMaterial;
        private float _stinkReduction;

        public float CurrentStink => Mathf.Max(0f, baseStink + FullnessRatio * fullStinkBonus - _stinkReduction);

        /// <summary>
        ///     Suppresses this pit's stink by <paramref name="amount" /> for as long as the source
        ///     stays applied — a <see cref="LimeSprinkler" /> placed on a neighboring cell, say.
        ///     Reductions accumulate; pass a negative amount to lift one. Stink is derived from
        ///     <see cref="FullnessRatio" /> every read, so this has to be stored separately rather
        ///     than written into <see cref="CurrentStink" />.
        /// </summary>
        public void ApplyStinkReduction(float amount)
        {
            _stinkReduction = Mathf.Max(0f, _stinkReduction + amount);
            GameMaster.Instance?.interfaceManager?.RefreshStinkMeter();
        }

        public bool IsSealed { get; private set; }

        private void OnEnable()
        {
            StinkSourceRegistry.Register(this);
            TurnController.OnCardPhaseEntered += PauseRunaways;
            TurnController.OnTowerPhaseEntered += ResumeRunaways;
        }

        private void OnDisable()
        {
            TurnController.OnCardPhaseEntered -= PauseRunaways;
            TurnController.OnTowerPhaseEntered -= ResumeRunaways;
            StinkSourceRegistry.Unregister(this);
        }

        private void Start()
        {
            ResolveRunawayReferences();

            if (fullnessBar) fullnessBar.gameObject.SetActive(true);
            if (sealedVisual) sealedVisual.SetActive(IsSealed);
            UpdateFullnessBar();
            UpdateFillVisual();

            if (IsFull)
                StartRunaways();
        }

        private void SetFullness(float newFullness)
        {
            fullness = Mathf.Clamp(newFullness, 0f, maxFullness);
            UpdateFullnessBar();
            UpdateFillVisual();
            GameMaster.Instance?.interfaceManager?.RefreshStinkMeter();

            if (IsFull)
                StartRunaways();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var gm = GameMaster.Instance;
            if (!gm) return;

            switch (gm.PendingPlacement)
            {
                case CesspitCapShopItem when !IsSealed:
                    Seal();
                    gm.CompletePlacement();
                    break;
                case BuryCesspitShopItem:
                    Bury();
                    gm.CompletePlacement();
                    break;
            }
        }

        private void Seal()
        {
            PauseRunaways();
            _spawningRunaways = false;
            IsSealed = true;
            if (sealedVisual) sealedVisual.SetActive(true);
        }

        /// <summary>Demolishes this cesspit: frees its slot, leaves a debuff tile on the cell, and destroys the object.</summary>
        private void Bury()
        {
            PauseRunaways();
            _spawningRunaways = false;

            if (_slot) _slot.ClearOccupied(_infraValue);

            var tileSpawner = FindAnyObjectByType<SpecialTileSpawner>();
            if (tileSpawner)
                tileSpawner.SpawnBuffTile(true,transform.position);
            else
                Debug.LogWarning("[Cesspit] No SpecialTileSpawner in scene; buried cesspit left no debuff tile.");

            // Destroy() defers OnDisable to end of frame; unregister now so the refresh excludes this pit's stink
            StinkSourceRegistry.Unregister(this);
            GameMaster.Instance?.interfaceManager?.RefreshStinkMeter();
            Destroy(gameObject);
        }

        public void SetSlot(SpecialInteractController slot, int infraValue = 0)
        {
            _slot = slot;
            _infraValue = infraValue;

            // Pick up sprinklers already covering this cell. Sprinklers placed later apply
            // themselves on their own placement, so each pit/sprinkler pairing lands exactly once.
            foreach (var sprinkler in FindObjectsByType<LimeSprinkler>())
                sprinkler.TryApplyTo(this);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsFull) return;
            if (!other.gameObject.CompareTag("IssueObject")) return;
            var issue = other.GetComponent<IssueObject>();
            if (issue != null && issue.IsDirectDestination)
                return;

            if (issue == null || !issue.TryRegisterSifter(GetEntityId())) return;

            SetFullness(fullness + issue.SiftCost);
            issue.Process(processPower, "Deposited into Cesspit");
        }

        public bool IsFull => maxFullness > 0f && fullness >= maxFullness;

        private float FullnessRatio => maxFullness > 0f ? Mathf.Clamp01(fullness / maxFullness) : 0f;

        private void UpdateFullnessBar()
        {
            if (fullnessBar) fullnessBar.SetValue(fullness, maxFullness);
        }

        private void UpdateFillVisual()
        {
            var ratio = FullnessRatio;

            if (fillVisual)
            {
                var scale = fillVisual.localScale;
                scale.y = Mathf.Lerp(minFillHeight, maxFillHeight, ratio);
                fillVisual.localScale = scale;

                // Cylinder mesh spans ±scale.y; keep the sludge anchored to the pit's top face (local y = 1)
                var pos = fillVisual.localPosition;
                pos.y = 1f + scale.y;
                fillVisual.localPosition = pos;
            }

            if (fillRenderer)
            {
                _fillMaterial ??= fillRenderer.material;
                _fillMaterial.color = Color.Lerp(emptyFillColor, fullFillColor, ratio);
            }
        }

        private void StartRunaways()
        {
            if (IsSealed || _spawningRunaways)
                return;

            _spawningRunaways = true;
            _runawayCoroutine = StartCoroutine(SpawnRunaway());
        }

        private void PauseRunaways()
        {
            if (!_spawningRunaways) return;

            if (_runawayCoroutine == null) return;
            StopCoroutine(_runawayCoroutine);
            _runawayCoroutine = null;
        }

        public void StopRunaways()
        {
            PauseRunaways();
        }

        private void ResumeRunaways()
        {
            if (IsSealed || !_spawningRunaways) return;
            if (_runawayCoroutine != null) return;

            _runawayCoroutine = StartCoroutine(SpawnRunaway());
        }

        private void ResolveRunawayReferences()
        {
            if (GameMaster.Instance?.entitySpawners == null) return;

            var resolvedPathDestination = false;
            foreach (var spawner in GameMaster.Instance.entitySpawners.Where(spawner => spawner))
            {
                if (!runawayPrefab && spawner.SpawnPrefab)
                    runawayPrefab = spawner.SpawnPrefab;

                if (spawner.Path && spawner.Path.Destination)
                {
                    runawayDestination = spawner.Path.Destination;
                    resolvedPathDestination = true;
                }

                if (runawayPrefab && resolvedPathDestination)
                    break;
            }
        }

        private IEnumerator SpawnRunaway()
        {
            while (_spawningRunaways)
            {
                yield return new WaitForSeconds(runawaySpawnInterval);

                ResolveRunawayReferences();

                if (!runawayPrefab || !runawayDestination)
                    continue;

                var obj = Instantiate(runawayPrefab, transform.position, transform.rotation);
                if (!obj.TryGetComponent<IssueObject>(out var issue))
                    continue;

                issue.AssignType();
                issue.SetDirectDestination(runawayDestination.position);
                issue.SetMoveSpeed(runawayMoveSpeed);
                issue.SetVisualOverride(runawayColor, runawayMaterial);
                issue.EnableClickPop();
            }
        }
    }
}
