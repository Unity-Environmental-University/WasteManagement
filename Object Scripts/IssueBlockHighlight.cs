using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Draws an attention-grabbing outline around an issue while it blocks (clogs) the pipe.
    ///     The outline is an inverted-hull shell: ghost copies of the active size model's meshes
    ///     parented under that model's own mesh transforms, rendered with the
    ///     <see cref="outlineMaterial" /> back-face extrusion shader. Both plain MeshRenderers and
    ///     SkinnedMeshRenderers (the animated poop models) are covered. Parenting under each source
    ///     mesh means the outline follows model swaps, size scaling, burst pulses, and trembles
    ///     for free, and hides itself whenever that model is disabled. Ghosts are tracked in a
    ///     list because they live outside any single container node.
    /// </summary>
    public class IssueBlockHighlight : MonoBehaviour
    {
        private static readonly int Thickness = Shader.PropertyToID("_Thickness");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private const string OutlineShaderName = "WasteManagement/IssueBlockOutline";
        private const string GhostName = "OutlineGhost";

        /// <summary>
        ///     Smoothed-normal copies keyed by source mesh. The poop meshes carry split (hard-edge)
        ///     normals — roughly 400 of their ~1000 unique positions are duplicated — and an
        ///     inverted hull built on those tears open along every seam, so each mesh gets one
        ///     shared re-normalized copy. Static process-wide, like StinkSourceRegistry: the keys
        ///     are shared mesh assets that outlive scene reloads, so entries stay valid rather
        ///     than accumulating per-scene garbage.
        /// </summary>
        private static readonly Dictionary<Mesh, Mesh> SmoothedMeshes = new();

        [Tooltip(
            "Material for the outline shell. Left empty, a runtime instance of the IssueBlockOutline shader is built from the color/thickness below.")]
        [SerializeField]
        private Material outlineMaterial;

        [Tooltip("Outline color used when building the fallback material.")] [SerializeField]
        private Color outlineColor = new(1f, 0.45f, 0.05f);

        [Tooltip(
            "Outline width as a fraction of screen height (0.008 is roughly 6px at 720p), used when building the fallback material.")]
        [SerializeField]
        [Range(0.0005f, 0.05f)]
        private float outlineThicknessFraction = 0.008f;

        /// <summary>Marks a ghost shell's root so a rebuild never wraps a shell around another shell.</summary>
        private sealed class GhostMarker : MonoBehaviour
        {
        }

        private readonly struct Ghost
        {
            public readonly GameObject GameObject;

            /// <summary>Non-null only when this ghost mirrors a skinned source (needs per-frame blendshape sync).</summary>
            public readonly SkinnedMeshRenderer Source;
            public readonly SkinnedMeshRenderer Skinned;

            public Ghost(GameObject gameObject, SkinnedMeshRenderer source, SkinnedMeshRenderer skinned)
            {
                GameObject = gameObject;
                Source = source;
                Skinned = skinned;
            }
        }

        private readonly List<Ghost> _ghosts = new();

        /// <summary>
        ///     The renderer set the current ghosts were built from. Rebuild() short-circuits to a
        ///     plain reactivate when called again with this same array (e.g. a sift/process/merge
        ///     refresh that didn't swap size tiers), since the ghosts already mirror it correctly —
        ///     parenting means they track scale/position changes without any rebuilding at all.
        /// </summary>
        private Renderer[] _builtFrom;

        public bool Visible { get; private set; }

        /// <summary>Shows the outline around <paramref name="sourceRenderers" />, rebuilding the shell if needed.</summary>
        public void Show(Renderer[] sourceRenderers)
        {
            Visible = true;
            enabled = true;
            Rebuild(sourceRenderers);
        }

        public void Hide()
        {
            Visible = false;
            enabled = false;
            SetGhostsActive(false);
        }

        /// <summary>
        ///     Rebuilds the shell around a (possibly new) renderer set. No-op while hidden —
        ///     the next Show() builds against whatever set is then active.
        /// </summary>
        public void Refresh(Renderer[] sourceRenderers)
        {
            if (Visible)
                Rebuild(sourceRenderers);
        }

        /// <summary>
        ///     The models' silhouettes are driven by animated blendshapes (squash / stretch /
        ///     scream), which a ghost only reproduces if it carries the same weights. Synced after
        ///     the Animator has written this frame's values. Disabled whenever hidden, so an issue
        ///     that blocked once and never will again stops paying for this dispatch.
        /// </summary>
        private void LateUpdate()
        {
            if (!Visible) return;

            foreach (var ghost in _ghosts)
            {
                if (!ghost.Skinned || !ghost.Source) continue;

                var shapeCount = ghost.Skinned.sharedMesh ? ghost.Skinned.sharedMesh.blendShapeCount : 0;
                for (var i = 0; i < shapeCount; i++)
                    ghost.Skinned.SetBlendShapeWeight(i, ghost.Source.GetBlendShapeWeight(i));
            }
        }

        private void Rebuild(Renderer[] sourceRenderers)
        {
            // Same renderer set as last build (typical for a size-change refresh that didn't swap
            // model tiers) — the existing ghosts already mirror it via transform parenting, so
            // just make sure they're visible instead of tearing down and recreating them.
            if (sourceRenderers != null && sourceRenderers == _builtFrom && _ghosts.Count > 0)
            {
                SetGhostsActive(true);
                return;
            }

            Clear();
            _builtFrom = sourceRenderers;

            if (sourceRenderers == null || sourceRenderers.Length == 0 || !isActiveAndEnabled) return;

            var material = GetMaterial();
            if (!material) return;

            // The poop models are animated FBX meshes rendered by SkinnedMeshRenderers,
            // which carry their mesh themselves; plain models use MeshFilter+MeshRenderer.
            foreach (var sourceRenderer in sourceRenderers)
            {
                if (!sourceRenderer) continue;

                // Never build a shell around a shell. In the normal flow the cached renderer
                // set passed in here never contains a ghost, but this stays as a safety net
                // against ever-growing "(Outline Smoothed) (Outline Smoothed)" meshes.
                if (sourceRenderer.GetComponent<GhostMarker>()) continue;

                var skinnedSource = sourceRenderer as SkinnedMeshRenderer;
                var sourceMesh = skinnedSource
                    ? skinnedSource.sharedMesh
                    : sourceRenderer is MeshRenderer
                        ? sourceRenderer.GetComponent<MeshFilter>()?.sharedMesh
                        : null;
                if (!sourceMesh) continue;

                var ghostObject = new GameObject(GhostName);
                // Identity local transform under the source mesh's own transform mirrors it exactly,
                // including any nested scale between the visual root and the mesh.
                ghostObject.transform.SetParent(sourceRenderer.transform, false);
                ghostObject.layer = LayerMask.NameToLayer("Ignore Raycast");
                ghostObject.AddComponent<GhostMarker>();

                var ghostMesh = GetSmoothedMesh(sourceMesh);

                // A skinned source needs a skinned ghost: a static MeshRenderer renders the
                // undeformed base mesh and visibly slides off the model as it squashes and stretches.
                Renderer ghostRenderer;
                SkinnedMeshRenderer skinnedGhost = null;
                if (skinnedSource)
                {
                    skinnedGhost = ghostObject.AddComponent<SkinnedMeshRenderer>();
                    skinnedGhost.sharedMesh = ghostMesh;
                    skinnedGhost.bones = skinnedSource.bones;
                    skinnedGhost.rootBone = skinnedSource.rootBone;
                    // Extrusion is screen-space, so the shell's geometry bounds match the source's.
                    skinnedGhost.localBounds = skinnedSource.localBounds;
                    skinnedGhost.updateWhenOffscreen = skinnedSource.updateWhenOffscreen;
                    skinnedGhost.quality = skinnedSource.quality;

                    ghostRenderer = skinnedGhost;
                }
                else
                {
                    ghostObject.AddComponent<MeshFilter>().sharedMesh = ghostMesh;
                    ghostRenderer = ghostObject.AddComponent<MeshRenderer>();
                }

                ghostRenderer.sharedMaterial = material;
                ghostRenderer.shadowCastingMode = ShadowCastingMode.Off;
                ghostRenderer.receiveShadows = false;

                _ghosts.Add(new Ghost(ghostObject, skinnedSource, skinnedGhost));
            }
        }

        /// <summary>
        ///     Returns a copy of <paramref name="sourceMesh" /> whose normals are averaged across
        ///     coincident vertex positions, so the extruded hull stays welded at hard edges.
        ///     Blendshapes survive the copy; only the normal channel is rewritten, which is safe
        ///     because the outline shader uses normals purely as an extrusion direction and
        ///     outputs a flat color.
        ///     Falls back to the source mesh — a hull with visible seams, but a drawn one — when
        ///     the geometry cannot be read.
        /// </summary>
        private static Mesh GetSmoothedMesh(Mesh sourceMesh)
        {
            // Fallbacks are cached as source-maps-to-itself, so an unreadable mesh is diagnosed
            // once instead of re-reported on every rebuild.
            if (SmoothedMeshes.TryGetValue(sourceMesh, out var cached) && cached)
                return cached;

            // Reading vertices/normals off a mesh imported without Read/Write throws a Unity
            // error per access, so this has to be checked up front rather than caught.
            if (!sourceMesh.isReadable)
            {
                Debug.LogWarning(
                    $"[IssueBlockHighlight] Mesh '{sourceMesh.name}' is not readable, so its outline " +
                    "cannot be normal-welded and will show seams at hard edges. Enable Read/Write " +
                    "in the model's import settings.");
                SmoothedMeshes[sourceMesh] = sourceMesh;
                return sourceMesh;
            }

            var vertices = sourceMesh.vertices;
            var normals = sourceMesh.normals;

            // Without usable normals there is nothing to average; hand back the source untouched.
            // Length-checking against 0 matters: empty-and-equal must not read as "fine".
            if (vertices.Length == 0 || normals == null || normals.Length != vertices.Length)
            {
                SmoothedMeshes[sourceMesh] = sourceMesh;
                return sourceMesh;
            }

            var accumulated = new Dictionary<Vector3, Vector3>(vertices.Length);
            for (var i = 0; i < vertices.Length; i++)
            {
                accumulated.TryGetValue(vertices[i], out var sum);
                accumulated[vertices[i]] = sum + normals[i];
            }

            var smoothed = new Vector3[normals.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                var sum = accumulated[vertices[i]];
                // Opposing normals on a paper-thin seam cancel out; keep the original one there.
                smoothed[i] = sum.sqrMagnitude > 1e-8f ? sum.normalized : normals[i];
            }

            var copy = Instantiate(sourceMesh);
            copy.name = sourceMesh.name + " (Outline Smoothed)";
            copy.normals = smoothed;

            SmoothedMeshes[sourceMesh] = copy;
            return copy;
        }

        private void Clear()
        {
            foreach (var ghost in _ghosts)
            {
                if (!ghost.GameObject) continue;

                // Deactivate first: play-mode Destroy is deferred, and a doomed-but-active
                // shell would flicker for a frame during rebuilds.
                ghost.GameObject.SetActive(false);

                // Detach too, for the same reason: until the deferred Destroy lands, a doomed
                // ghost is still a child of the model and would be picked up as a source mesh
                // by the very rebuild that replaces it.
                ghost.GameObject.transform.SetParent(null, false);

                // Edit-mode callers (tests, editor tooling) forbid the deferred Destroy.
                if (Application.isPlaying)
                    Destroy(ghost.GameObject);
                else
                    DestroyImmediate(ghost.GameObject);
            }

            _ghosts.Clear();
            _builtFrom = null;
        }

        private void SetGhostsActive(bool active)
        {
            foreach (var ghost in _ghosts)
                if (ghost.GameObject)
                    ghost.GameObject.SetActive(active);
        }

        private Material GetMaterial()
        {
            if (outlineMaterial) return outlineMaterial;

            var shader = Shader.Find(OutlineShaderName);
            if (!shader)
            {
                Debug.LogWarning($"[IssueBlockHighlight] Outline shader '{OutlineShaderName}' not found.", this);
                return null;
            }

            outlineMaterial = new Material(shader);
            outlineMaterial.SetColor(BaseColor, outlineColor);
            outlineMaterial.SetFloat(Thickness, outlineThicknessFraction);
            return outlineMaterial;
        }

        private void OnDestroy()
        {
            Clear();
        }
    }
}
