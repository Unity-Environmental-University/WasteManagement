using System.Collections.Generic;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>A short construction-dust puff for placed pipes and shop items.</summary>
    public sealed class PipePlacementPuff : MonoBehaviour
    {
        private static readonly Color[] SmokeColors =
        {
            new(0.65f, 0.61f, 0.54f), new(0.76f, 0.73f, 0.67f),
            new(0.57f, 0.55f, 0.51f), new(0.83f, 0.8f, 0.74f)
        };

        public static void Play(PathBuildBoard board, IReadOnlyList<Vector2Int> cells, float pipeHeight)
        {
            var prefab = Resources.Load<ParticleSystem>("ConstructionDust");
            if (!prefab || cells.Count == 0) return;

            var root = new GameObject("Pipe Construction Puff") { layer = board.gameObject.layer };
            root.transform.SetParent(board.transform, false);
            root.AddComponent<PipePlacementPuff>();
            var pitch = Mathf.Min(board.CellWorldPitch.x, board.CellWorldPitch.y);
            var smoke = CreateSystem(root.transform, prefab, cells.Count * 10);

            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                var local = board.GetLocalPosition(cell.x, cell.y);
                local.y = board.CellHeight * 0.5f + pipeHeight;
                var position = board.transform.TransformPoint(local);
                EmitDust(smoke, board.transform, position, pitch, i);
            }

            if (Application.isPlaying) Destroy(root, 0.9f);
        }

        public static void Play(GameObject placedItem)
        {
            if (!placedItem) return;
            var prefab = Resources.Load<ParticleSystem>("ConstructionDust");
            if (!prefab) return;

            var bounds = new Bounds(placedItem.transform.position, Vector3.one);
            var hasBounds = false;
            foreach (var renderer in placedItem.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer is ParticleSystemRenderer) continue;
                if (!hasBounds) bounds = renderer.bounds;
                else bounds.Encapsulate(renderer.bounds);
                hasBounds = true;
            }

            var root = new GameObject("Item Construction Puff") { layer = placedItem.layer };
            root.transform.SetParent(placedItem.transform, false);
            root.AddComponent<PipePlacementPuff>();
            var pitch = Mathf.Max(0.3f, Mathf.Min(bounds.size.x, bounds.size.z) * 0.65f);
            var smoke = CreateSystem(root.transform, prefab, 40);
            for (var i = 0; i < 4; i++)
            {
                var position = new Vector3(
                    bounds.center.x + bounds.extents.x * (i % 2 == 0 ? -0.85f : 0.85f),
                    bounds.min.y + pitch * 0.08f,
                    bounds.center.z + bounds.extents.z * (i < 2 ? -0.85f : 0.85f));
                // Bounds are world-aligned, so use world axes even on rotated shop items.
                EmitDust(smoke, null, position, pitch, i);
            }

            if (Application.isPlaying) Destroy(root, 0.9f);
        }

        private static void EmitDust(ParticleSystem smoke, Transform axes, Vector3 position, float pitch, int variation)
        {
            for (var j = 0; j < 10; j++)
            {
                // Scatter unevenly so neighbouring puffs form one loose cloud.
                var angle = j * 2.399963f + variation * 0.8f;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                if (axes) outward = axes.TransformDirection(outward);
                var up = axes ? axes.up : Vector3.up;
                smoke.Emit(new ParticleSystem.EmitParams
                {
                    position = position + outward * (pitch * (0.04f + j % 4 * 0.045f)),
                    velocity = (outward * (0.15f + j % 3 * 0.08f) + up * 0.35f) * pitch,
                    startColor = SmokeColors[(j + variation) % SmokeColors.Length],
                    startSize = pitch * (j % 2 == 0 ? 0.45f : 0.3f),
                    startLifetime = 0.5f + j % 3 * 0.08f,
                    rotation = angle * Mathf.Rad2Deg
                }, 1);
            }
        }

        private static ParticleSystem CreateSystem(Transform parent, ParticleSystem prefab, int capacity)
        {
            var particles = Instantiate(prefab, parent, false);
            particles.gameObject.layer = parent.gameObject.layer;
            var main = particles.main;
            main.maxParticles = capacity;
            particles.Play();
            return particles;
        }
    }
}
