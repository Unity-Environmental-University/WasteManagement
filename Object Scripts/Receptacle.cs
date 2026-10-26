using System;
using _project.Scripts.Core;
using DG.Tweening;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     A second pipe endpoint that collects one issue type (a metal acceptor, for example). Its
    ///     transform is an endpoint marker like the lake's: a splitter branch whose pipe touches the
    ///     square it sits on ends here instead of at the lake. Only <see cref="acceptedType" /> is
    ///     collected; anything else delivered here is dumped and pollutes the lake as usual.
    ///     Stays hidden and out of the route search until the town reaches <see cref="unlockLevel" />.
    /// </summary>
    public class Receptacle : MonoBehaviour
    {
        /// <summary>Raised when a receptacle unlocks or locks, so routes can include or drop it.</summary>
        public static event Action AvailabilityChanged;

        [SerializeField] private IssueType acceptedType = IssueType.Metal;

        [Tooltip("Town level at which this receptacle appears and pipe can end at it.")]
        [SerializeField] [Min(1)] private int unlockLevel = 3;

        [Tooltip("Optional readout of how many accepted issues have been collected.")]
        [SerializeField] private TextMesh label;

        [SerializeField] private string labelTitle = "METAL";

        [Header("Collect Effect")]
        [Tooltip("Bounces when an accepted issue lands. Defaults to the first child.")]
        [SerializeField] private Transform model;

        [SerializeField] private Color[] sparkColors =
        {
            new(0.95f, 0.97f, 1f), new(0.78f, 0.84f, 0.9f), new(1f, 0.86f, 0.45f)
        };

        private ParticleSystem _sparks;

        private bool _unlocked;

        // Forces the first ApplyLevel after enabling to sync the children, whatever their saved state.
        private bool _applied;

        public bool IsUnlocked => _unlocked && isActiveAndEnabled;
        public int DeliveredCount { get; private set; }

        public bool Accepts(IssueObject issue) => issue && issue.GetIssueType() == acceptedType;

        private void OnEnable()
        {
            IssueObject.OnReachedEnd += Receive;
            TurnController.OnLevelChanged += ApplyLevel;
            var turnController = GameMaster.Instance ? GameMaster.Instance.turnController : null;
            // Without a running game (tests, an empty scene) there is no level to wait for.
            ApplyLevel(turnController ? turnController.currentLevel : unlockLevel);
        }

        private void OnDisable()
        {
            IssueObject.OnReachedEnd -= Receive;
            TurnController.OnLevelChanged -= ApplyLevel;
            _applied = false;
            if (_unlocked) AvailabilityChanged?.Invoke();
        }

        private void ApplyLevel(int level)
        {
            var unlocked = level >= unlockLevel;
            if (unlocked == _unlocked && _applied) return;

            _applied = true;
            _unlocked = unlocked;
            // The receptacle's model and readout are its children; the root stays active as the marker.
            foreach (Transform child in transform)
                child.gameObject.SetActive(unlocked);
            RefreshLabel();
            AvailabilityChanged?.Invoke();
        }

        private void Receive(IssueObject issue)
        {
            if (issue.EndReceptacle != this || !IsUnlocked || !Accepts(issue)) return;

            DeliveredCount++;
            RefreshLabel();
            PlayCollectEffect();
        }

        /// <summary>A quick clank: the bin bounces and a burst of glints jumps out of its mouth.</summary>
        private void PlayCollectEffect()
        {
            if (!model && transform.childCount > 0) model = transform.GetChild(0);
            if (!model) return;

            model.DOComplete();
            model.DOPunchScale(Vector3.one * 0.12f, 0.35f, 8, 0.6f);
            if (label) label.transform.DOComplete();
            if (label) label.transform.DOPunchScale(Vector3.one * 0.25f, 0.3f, 6, 0.5f);

            var sparks = GetSparks();
            if (!sparks) return;

            var bounds = new Bounds(model.position, Vector3.zero);
            foreach (var part in model.GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(part.bounds);
            var size = Mathf.Max(0.3f, Mathf.Min(bounds.size.x, bounds.size.z));
            for (var i = 0; i < 14; i++)
            {
                // Golden-angle spread so the burst fans out evenly without random clumps.
                var angle = i * 2.399963f;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) + outward * size * 0.2f,
                    velocity = (outward * (0.4f + i % 3 * 0.25f) + Vector3.up * (1.6f + i % 4 * 0.3f)) * size,
                    startColor = sparkColors.Length > 0 ? sparkColors[i % sparkColors.Length] : Color.white,
                    startSize = size * (i % 2 == 0 ? 0.16f : 0.1f),
                    startLifetime = 0.45f + i % 3 * 0.1f
                }, 1);
            }
        }

        private ParticleSystem GetSparks()
        {
            if (_sparks) return _sparks;

            var material = Resources.Load<Material>("PipePlacementSmoke");
            if (!material) return null;

            var sparksObject = new GameObject("Collect Sparks") { layer = gameObject.layer };
            sparksObject.transform.SetParent(transform, false);
            _sparks = sparksObject.AddComponent<ParticleSystem>();
            _sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _sparks.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = 64;
            main.gravityModifier = 2.5f;
            var emission = _sparks.emission;
            emission.enabled = false;
            var shape = _sparks.shape;
            shape.enabled = false;
            var fade = _sparks.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var sparkRenderer = _sparks.GetComponent<ParticleSystemRenderer>();
            sparkRenderer.sharedMaterial = material;
            sparkRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sparkRenderer.receiveShadows = false;
            _sparks.Play();
            return _sparks;
        }

        private void RefreshLabel()
        {
            if (label) label.text = $"{labelTitle}\n{DeliveredCount} collected";
        }
    }
}
