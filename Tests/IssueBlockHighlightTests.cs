using System.Collections.Generic;
using System.Text.RegularExpressions;
using _project.Scripts.Object_Scripts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace _project.Scripts.Tests
{
    public class IssueBlockHighlightTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
                if (created)
                    Object.DestroyImmediate(created);
            _created.Clear();
        }

        [Test]
        public void Show_BuildsOneGhostPerMesh_UnderItsOwnSourceTransform()
        {
            var root = CreatePrimitive("Visual");
            var child = CreatePrimitive("Child", root.transform);

            var highlight = CreateHighlight();
            highlight.Show(root.GetComponentsInChildren<Renderer>(true));

            // Each ghost sits under the transform of the mesh it mirrors, so any nested
            // scale/rotation between the root and that mesh is reproduced exactly.
            var rootGhost = root.transform.Find("OutlineGhost");
            var childGhost = child.transform.Find("OutlineGhost");
            Assert.IsNotNull(rootGhost);
            Assert.IsNotNull(childGhost);

            // The ghost renders a smoothed-normal copy rather than the source mesh itself,
            // so match on geometry: same vertices, same triangles.
            var sourceMesh = root.GetComponent<MeshFilter>().sharedMesh;
            foreach (var ghostMesh in new[]
                     {
                         rootGhost.GetComponent<MeshFilter>().sharedMesh,
                         childGhost.GetComponent<MeshFilter>().sharedMesh
                     })
            {
                Assert.AreEqual(sourceMesh.vertexCount, ghostMesh.vertexCount);
                CollectionAssert.AreEqual(sourceMesh.vertices, ghostMesh.vertices);
                CollectionAssert.AreEqual(sourceMesh.triangles, ghostMesh.triangles);
            }
        }

        [Test]
        public void GhostMesh_WeldsNormalsAcrossSplitVertices()
        {
            // The poop meshes carry hard-edge (split) normals — a cube is the same shape of
            // problem, 24 vertices over 8 positions. Extruding along those tears the hull open
            // at every seam, so the ghost mesh must average them per position first.
            var root = CreatePrimitive("Visual");
            var sourceMesh = root.GetComponent<MeshFilter>().sharedMesh;

            var highlight = CreateHighlight();
            highlight.Show(root.GetComponentsInChildren<Renderer>(true));

            var ghostMesh = GhostMesh(CollectGhosts(root.transform)[0]);
            var ghostVertices = ghostMesh.vertices;
            var ghostNormals = ghostMesh.normals;

            var normalsByPosition = new Dictionary<Vector3, Vector3>();
            for (var i = 0; i < ghostVertices.Length; i++)
            {
                if (normalsByPosition.TryGetValue(ghostVertices[i], out var seen))
                    Assert.That(Vector3.Angle(seen, ghostNormals[i]), Is.LessThan(0.01f),
                        "Coincident vertices must share one extrusion direction.");
                normalsByPosition[ghostVertices[i]] = ghostNormals[i];
            }

            // Sanity check that the fixture actually exercises the welding path.
            Assert.Less(normalsByPosition.Count, sourceMesh.vertexCount);
            // The source itself must not be mutated — it is a shared asset.
            Assert.AreNotSame(sourceMesh, ghostMesh);
        }

        [Test]
        public void Show_DoesNotBuildAShellAroundAnExistingGhost()
        {
            // A caller that hands the highlight a raw hierarchy walk (rather than IssueObject's
            // cached, ghost-free renderer list) can still include an outgoing ghost whose deferred
            // Destroy hasn't landed yet. The marker component on every ghost keeps that from being
            // wrapped in a shell of its own, compounding into
            // "(Outline Smoothed) (Outline Smoothed) (Outline Smoothed)" meshes.
            var root = CreatePrimitive("Visual");
            var stale = CreatePrimitive("OutlineGhost", root.transform);
            var markerType = typeof(IssueBlockHighlight).GetNestedType("GhostMarker",
                System.Reflection.BindingFlags.NonPublic);
            stale.AddComponent(markerType);

            var highlight = CreateHighlight();
            highlight.Show(new[] { root.GetComponent<Renderer>(), stale.GetComponent<Renderer>() });

            Assert.AreEqual(0, stale.transform.childCount, "A ghost must never be wrapped in a ghost.");
            // The stale ghost plus exactly one real shell for the model itself.
            Assert.AreEqual(2, root.transform.childCount);
        }

        [Test]
        public void Show_WithAnUnreadableMesh_FallsBackToTheSourceMeshAndWarnsOnce()
        {
            // Models imported without Read/Write throw a Unity error on every vertex access,
            // so the weld has to be skipped rather than attempted.
            var root = CreateSkinnedVisual("Unreadable Visual", out var mesh);
            mesh.UploadMeshData(true);
            Assert.IsFalse(mesh.isReadable, "Fixture must actually be unreadable.");

            LogAssert.Expect(LogType.Warning, new Regex("is not readable"));

            var highlight = CreateHighlight();
            highlight.Show(root.GetComponentsInChildren<Renderer>(true));

            var ghost = (SkinnedMeshRenderer)CollectActiveGhosts(root.transform)[0];
            Assert.AreSame(mesh, ghost.sharedMesh);

            // Second show must stay quiet: the fallback is cached, so it is diagnosed once.
            highlight.Show(root.GetComponentsInChildren<Renderer>(true));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void GhostMesh_IsSharedBetweenHighlightsOfTheSameSourceMesh()
        {
            var first = CreatePrimitive("First Visual");
            var second = CreatePrimitive("Second Visual");

            CreateHighlight().Show(first.GetComponentsInChildren<Renderer>(true));
            CreateHighlight().Show(second.GetComponentsInChildren<Renderer>(true));

            // Cached per source mesh: every issue on screen reuses one smoothed copy.
            Assert.AreSame(GhostMesh(CollectGhosts(first.transform)[0]),
                GhostMesh(CollectGhosts(second.transform)[0]));
        }

        [Test]
        public void Show_BuildsSkinnedGhostsAroundSkinnedMeshRenderers()
        {
            // The real issue models are animated FBX meshes rendered by SkinnedMeshRenderers,
            // which carry no MeshFilter — the shell must still be built around them, and as a
            // SkinnedMeshRenderer of its own so it deforms with the model instead of rendering
            // the rigid base mesh.
            var root = CreateSkinnedVisual("Skinned Visual", out var mesh);

            var highlight = CreateHighlight();
            highlight.Show(root.GetComponentsInChildren<Renderer>(true));

            var ghosts = CollectActiveGhosts(root.transform);
            Assert.IsNotEmpty(ghosts);

            var skinnedGhost = ghosts[0] as SkinnedMeshRenderer;
            Assert.IsNotNull(skinnedGhost, "A skinned source must produce a skinned ghost.");
            CollectionAssert.AreEqual(mesh.vertices, skinnedGhost.sharedMesh.vertices);
            Assert.AreEqual(mesh.blendShapeCount, skinnedGhost.sharedMesh.blendShapeCount);
        }

        [Test]
        public void GhostBlendShapeWeights_TrackTheSourceModel()
        {
            // squash / stretch / scream are animated every frame; a shell frozen at weight 0
            // slides off the silhouette as the model deforms.
            var root = CreateSkinnedVisual("Skinned Visual", out _);
            var source = root.GetComponent<SkinnedMeshRenderer>();

            var highlight = CreateHighlight();
            highlight.Show(root.GetComponentsInChildren<Renderer>(true));
            var ghost = (SkinnedMeshRenderer)CollectActiveGhosts(root.transform)[0];

            source.SetBlendShapeWeight(0, 73f);
            InvokeLateUpdate(highlight);

            Assert.AreEqual(73f, ghost.GetBlendShapeWeight(0), 0.001f);
        }

        private static void InvokeLateUpdate(IssueBlockHighlight highlight)
        {
            // The sync runs in LateUpdate, which no test-runner frame drives here.
            typeof(IssueBlockHighlight)
                .GetMethod("LateUpdate", System.Reflection.BindingFlags.NonPublic |
                                         System.Reflection.BindingFlags.Instance)
                .Invoke(highlight, null);
        }

        private GameObject CreateSkinnedVisual(string name, out Mesh mesh)
        {
            var root = CreatePrimitive(name);
            Object.DestroyImmediate(root.GetComponent<MeshFilter>());
            Object.DestroyImmediate(root.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(root.GetComponent<Collider>());

            var vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f)
            };
            mesh = new Mesh { vertices = vertices, triangles = new[] { 0, 2, 1, 1, 2, 3 } };
            mesh.RecalculateNormals();
            mesh.AddBlendShapeFrame("squash", 100f, new[]
            {
                Vector3.up, Vector3.up, Vector3.up, Vector3.up
            }, null, null);
            _created.Add(mesh);

            var skinned = root.AddComponent<SkinnedMeshRenderer>();
            skinned.sharedMesh = mesh;
            return root;
        }

        [Test]
        public void GhostRenderers_IgnoreRaycastAndCastNoShadows()
        {
            var root = CreatePrimitive("Visual");

            var highlight = CreateHighlight();
            highlight.Show(root.GetComponentsInChildren<Renderer>(true));

            var ghosts = CollectGhosts(root.transform);
            Assert.IsNotEmpty(ghosts);

            foreach (var ghostRenderer in ghosts)
            {
                Assert.AreEqual(LayerMask.NameToLayer("Ignore Raycast"), ghostRenderer.gameObject.layer);
                Assert.AreEqual(ShadowCastingMode.Off, ghostRenderer.shadowCastingMode);
                Assert.IsFalse(ghostRenderer.receiveShadows);
            }
        }

        [Test]
        public void GhostMaterial_UsesTheIssueBlockOutlineShader()
        {
            var root = CreatePrimitive("Visual");

            var highlight = CreateHighlight();
            highlight.Show(root.GetComponentsInChildren<Renderer>(true));

            var ghostRenderer = CollectGhosts(root.transform)[0];
            StringAssert.Contains("IssueBlockOutline", ghostRenderer.sharedMaterial.shader.name);
        }

        [Test]
        public void Hide_DeactivatesEveryGhostAndReportsNotVisible()
        {
            var root = CreatePrimitive("Visual");

            var highlight = CreateHighlight();
            highlight.Show(root.GetComponentsInChildren<Renderer>(true));

            highlight.Hide();

            Assert.IsFalse(highlight.Visible);
            foreach (var ghostRenderer in CollectGhosts(root.transform))
                Assert.IsFalse(ghostRenderer.gameObject.activeInHierarchy);
        }

        [Test]
        public void Show_AfterHide_RebuildsActiveGhosts()
        {
            var root = CreatePrimitive("Visual");
            var renderers = root.GetComponentsInChildren<Renderer>(true);

            var highlight = CreateHighlight();
            highlight.Show(renderers);
            highlight.Hide();
            highlight.Show(renderers);

            Assert.IsTrue(highlight.Visible);
            // Play-mode Destroy is deferred, so the hidden generation may still linger
            // briefly — what matters is that an active shell exists again.
            Assert.IsNotEmpty(CollectActiveGhosts(root.transform));
        }

        [Test]
        public void Refresh_WhileHidden_DoesNotShowAnything()
        {
            var root = CreatePrimitive("Visual");
            var renderers = root.GetComponentsInChildren<Renderer>(true);

            var highlight = CreateHighlight();
            highlight.Show(renderers);
            highlight.Hide();
            highlight.Refresh(renderers);

            // Hidden means hidden: the stale ghosts stay deactivated rather than
            // being rebuilt against the new root.
            Assert.IsFalse(highlight.Visible);
            foreach (var ghostRenderer in CollectGhosts(root.transform))
                Assert.IsFalse(ghostRenderer.gameObject.activeInHierarchy);
        }

        [Test]
        public void Refresh_WhileVisible_RebuildsAroundTheNewRootWithoutStaleGhosts()
        {
            var oldRoot = CreatePrimitive("Old Visual");
            var newRoot = CreatePrimitive("New Visual");

            var highlight = CreateHighlight();
            highlight.Show(oldRoot.GetComponentsInChildren<Renderer>(true));
            highlight.Refresh(newRoot.GetComponentsInChildren<Renderer>(true));

            Assert.IsTrue(highlight.Visible);
            Assert.IsNotEmpty(CollectActiveGhosts(newRoot.transform));

            // The old model must be fully clean — no leftover shell from the previous tier.
            CollectionAssert.IsEmpty(CollectActiveGhosts(oldRoot.transform));
        }

        [Test]
        public void Show_WithNoVisualRoot_IsANoOpShell()
        {
            var highlight = CreateHighlight();
            highlight.Show(null);

            // Visible flips so a later Refresh/Show still knows the issue is blocked,
            // but without a root there is nothing to build a shell around.
            Assert.IsTrue(highlight.Visible);
        }

        // Renderer, not MeshRenderer: ghosts mirroring a skinned source are themselves
        // SkinnedMeshRenderers so they follow the model's blendshape deformation.
        private static List<Renderer> CollectGhosts(Transform root)
        {
            return CollectGhostsWhere(root, _ => true);
        }

        private static List<Renderer> CollectActiveGhosts(Transform root)
        {
            return CollectGhostsWhere(root, renderer => renderer.gameObject.activeInHierarchy);
        }

        private static List<Renderer> CollectGhostsWhere(Transform root,
            System.Func<Renderer, bool> predicate)
        {
            var ghosts = new List<Renderer>();
            foreach (var ghostRenderer in root.GetComponentsInChildren<Renderer>(true))
                if (ghostRenderer.name == "OutlineGhost" && predicate(ghostRenderer))
                    ghosts.Add(ghostRenderer);
            return ghosts;
        }

        private static Mesh GhostMesh(Renderer ghostRenderer)
        {
            return ghostRenderer is SkinnedMeshRenderer skinned
                ? skinned.sharedMesh
                : ghostRenderer.GetComponent<MeshFilter>().sharedMesh;
        }

        private IssueBlockHighlight CreateHighlight()
        {
            var gameObject = new GameObject("Issue");
            _created.Add(gameObject);
            return gameObject.AddComponent<IssueBlockHighlight>();
        }

        private GameObject CreatePrimitive(string name, Transform parent = null)
        {
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            _created.Add(primitive);
            return primitive;
        }
    }
}
