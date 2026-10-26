using System.Collections;
using System.Linq;
using _project.Scripts.Core;
using _project.Scripts.Object_Scripts;
using _project.Scripts.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace _project.Scripts.Tests
{
    /// <summary>Splitter branch routing, issue lane assignment, and the branch previews it reveals.</summary>
    public class PathSplitterTests : PathTestBase
    {
        private const string AlternatePreview = "Alternate Path Preview";

        [Test]
        public void LivePreview_ShowsAlternateBranchOnlyWhileSplitterOccupiesFork()
        {
            var fixture = CreateSplitPathFixture();
            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.IsNull(fixture.Board.transform.Find(AlternatePreview));

            var splitterObject = CreateGameObject("Path Splitter");
            splitterObject.transform.position = CellTop(fixture, 1, 2);
            splitterObject.AddComponent<PathSplitter>();

            var alternatePreview = FindPreview(fixture, AlternatePreview);
            Assert.IsNotNull(alternatePreview);
            Assert.IsTrue(alternatePreview.IsShowing);
            Assert.Greater(alternatePreview.PointCount, 2);

            splitterObject.SetActive(false);

            Assert.IsFalse(alternatePreview.IsShowing);
            Assert.AreEqual(0, alternatePreview.PointCount);
        }

        [Test]
        public void LivePreview_DoesNotShowAlternateBranchForSplitterAwayFromFork()
        {
            var fixture = CreateSplitPathFixture();
            Assert.IsTrue(fixture.Path.Rebuild());

            var splitterObject = CreateGameObject("Path Splitter");
            splitterObject.transform.position = CellTop(fixture, 1, 1);
            splitterObject.AddComponent<PathSplitter>();

            Assert.IsNull(fixture.Board.transform.Find(AlternatePreview));
        }

        [UnityTest]
        public IEnumerator LivePreview_RefreshesAfterPlacedSplitterMovesOntoFork()
        {
            var fixture = CreateSplitPathFixture();
            Assert.IsTrue(fixture.Path.Rebuild());

            // Reproduce prefab placement lifecycle: OnEnable can run before the final slot
            // position is visible to listeners, leaving the initial availability event stale.
            var splitterObject = CreateGameObject("Path Splitter");
            splitterObject.transform.position = CellTop(fixture, 1, 1);
            splitterObject.AddComponent<PathSplitter>();
            splitterObject.transform.position = CellTop(fixture, 1, 2);

            // The splitter re-announces itself from Start once its placement frame settles.
            yield return null;

            var alternatePreview = FindPreview(fixture, AlternatePreview);
            Assert.IsNotNull(alternatePreview);
            Assert.IsTrue(alternatePreview.IsShowing);
            Assert.Greater(alternatePreview.PointCount, 2);
        }

        [Test]
        public void LivePreview_ShowsUnfinishedSplitterBranch_BeforeTheRouteIsComplete()
        {
            var fixture = CreatePathFixture();

            // The splitter goes down first, on a bare cell; pipes are laid through it afterward.
            var splitterObject = CreateGameObject("Path Splitter");
            splitterObject.transform.position = CellTop(fixture, 1, 2);
            splitterObject.AddComponent<PathSplitter>();

            PlaceVertical(fixture.Board, 1, 0, 4);
            Assert.IsNull(fixture.Board.transform.Find(AlternatePreview));

            PlaceHorizontal(fixture.Board, 2, 2, 2);

            var alternatePreview = FindPreview(fixture, AlternatePreview);
            Assert.IsNotNull(alternatePreview);
            Assert.IsTrue(alternatePreview.IsShowing);
            // Fork cell plus both cells of the branch built so far.
            Assert.AreEqual(3, alternatePreview.PointCount);
            Assert.IsFalse(fixture.Path.Rebuild());
        }

        [Test]
        public void PathSplitter_ShowsItsConnectedStubsAndPointsHandlesDownItsOutlets()
        {
            var fixture = CreateSplitPathFixture();
            Assert.IsTrue(fixture.Path.Rebuild());

            var host = CreateGameObject("Path Splitter");
            host.transform.position = CellTop(fixture, 1, 2);

            Vector3 StubPosition(Vector2Int neighbor) =>
                Vector3.Lerp(host.transform.position, CellTop(fixture, neighbor.x, neighbor.y), 0.4f);

            Renderer AddPart(string partName, Vector2Int neighbor)
            {
                var stub = CreatePrimitive($"Model_{partName} {neighbor}", host.transform);
                stub.transform.position = StubPosition(neighbor);
                return stub.GetComponent<Renderer>();
            }

            var intake = AddPart("Pipe", new Vector2Int(1, 1));
            var mainOutlet = AddPart("Pipe", new Vector2Int(1, 3));
            var branchOutlet = AddPart("Pipe", new Vector2Int(2, 2));
            var unused = AddPart("Pipe", new Vector2Int(0, 2));
            var mainHandle = AddPart("ValveBlue", new Vector2Int(0, 2));
            var branchHandle = AddPart("ValveOrange", new Vector2Int(1, 3));

            void AssertPointsAt(Renderer handle, Vector2Int neighbor)
            {
                Assert.IsTrue(handle.enabled, handle.name);
                Assert.Less(Vector3.Distance(StubPosition(neighbor), handle.transform.position), 0.001f,
                    handle.name);
            }

            // The splitter finds its stubs by name and announces itself, which refreshes them.
            var splitter = host.AddComponent<PathSplitter>();

            Assert.IsTrue(intake.enabled);
            Assert.IsTrue(mainOutlet.enabled);
            Assert.IsTrue(branchOutlet.enabled);
            Assert.IsFalse(unused.enabled, "No route uses the fourth side.");
            AssertPointsAt(mainHandle, new Vector2Int(1, 3));
            AssertPointsAt(branchHandle, new Vector2Int(2, 2));

            // Placed ahead of any pipe, the splitter has nothing to connect to.
            splitter.transform.position = CellTop(fixture, 5, 5);
            splitter.enabled = false;
            splitter.enabled = true;
            foreach (var part in new[] { intake, mainOutlet, branchOutlet, unused, mainHandle, branchHandle })
                Assert.IsFalse(part.enabled, part.name);

            // Pipe laid beside it shows that side at once, before any route reaches the splitter.
            PlaceVertical(fixture.Board, 5, 5, 2);
            Assert.IsTrue(mainOutlet.enabled);
            Assert.IsFalse(intake.enabled);
            Assert.IsFalse(branchOutlet.enabled);
            Assert.IsFalse(unused.enabled);
        }

        [Test]
        public void Rebuild_ForksAtTheSplitterCell_WhenAnEarlierForkExists()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 10);
            // Two side loops: one leaving the route at (1,1), a later one at (1,5).
            PlaceVertical(fixture.Board, 0, 1, 3);
            PlaceVertical(fixture.Board, 2, 5, 3);

            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.IsTrue(fixture.Path.IsSplitPoint(CellTop(fixture, 1, 1)));

            var splitter = AddSplitter(fixture, new Vector2Int(1, 5));

            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.IsTrue(fixture.Path.HasAlternateRoute);
            Assert.IsTrue(fixture.Path.IsSplitPoint(splitter.transform.position));
            Assert.IsFalse(fixture.Path.IsSplitPoint(CellTop(fixture, 1, 1)));
        }

        [Test]
        public void Rebuild_GivesEverySplitterOnTheMainRouteItsOwnBranch()
        {
            var fixture = CreateSplitPathFixture();
            // A second, left-hand loop leaving the main route at (1,4) and rejoining at (1,5).
            PlaceVertical(fixture.Board, 0, 4, 2);
            AddSplitter(fixture, new Vector2Int(1, 2));
            AddSplitter(fixture, new Vector2Int(1, 4));

            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.AreEqual(3, fixture.Path.RouteCount);
            Assert.IsTrue(fixture.Path.TryGetBranchRoute(0, CellTop(fixture, 1, 2), out var rightBranch));
            Assert.IsTrue(fixture.Path.TryGetBranchRoute(0, CellTop(fixture, 1, 4), out var leftBranch));
            Assert.AreNotEqual(rightBranch, leftBranch);
            CollectionAssert.Contains(fixture.Path.GetRouteCells(leftBranch), new Vector2Int(0, 5));

            AssertBranchPreviewsShowing(fixture, 2);
        }

        [Test]
        public void Rebuild_SplitsABranchAgain_AtASplitterOnThatBranch()
        {
            var fixture = CreateNestedSplitPathFixture();

            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.AreEqual(3, fixture.Path.RouteCount);
            Assert.IsTrue(fixture.Path.TryGetBranchRoute(0, CellTop(fixture, 1, 2), out var branch));
            Assert.IsTrue(fixture.Path.TryGetBranchRoute(branch, CellTop(fixture, 3, 4), out var subBranch));
            Assert.IsFalse(fixture.Path.TryGetBranchRoute(0, CellTop(fixture, 3, 4), out _),
                "The main route never reaches the branch's splitter.");
            CollectionAssert.Contains(fixture.Path.GetRouteCells(subBranch), new Vector2Int(5, 5));

            AssertBranchPreviewsShowing(fixture, 2);
        }

        [Test]
        public void PathSplitter_OnABranch_SendsIssuesFromThatBranchDownItsOwnBranch()
        {
            var fixture = CreateNestedSplitPathFixture();
            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.IsTrue(fixture.Path.TryGetBranchRoute(0, CellTop(fixture, 1, 2), out var branch));
            Assert.IsTrue(fixture.Path.TryGetBranchRoute(branch, CellTop(fixture, 3, 4), out var subBranch));

            var branchSplitter = PathSplitter.Live.Single(s => s.transform.position == CellTop(fixture, 3, 4));
            branchSplitter.SetRule(IssueType.Organic, SplitterRule.BranchOnly);
            branchSplitter.SetRule(IssueType.Chemical, SplitterRule.MainOnly);

            var diverted = CreateIssue("Diverted Issue", fixture.Path);
            diverted.SetType(IssueType.Organic);
            Assert.IsTrue(diverted.TrySetRoute(branch));
            Assert.IsTrue(branchSplitter.RouteIssue(diverted));
            Assert.AreEqual(subBranch, diverted.GetRouteIndex());

            var kept = CreateIssue("Kept Issue", fixture.Path);
            kept.SetType(IssueType.Chemical);
            Assert.IsTrue(kept.TrySetRoute(branch));
            Assert.IsTrue(branchSplitter.RouteIssue(kept));
            Assert.AreEqual(branch, kept.GetRouteIndex(), "The main lane keeps an issue on the branch it arrived on.");

            // Issues on the main route pass this splitter's cell by without being split.
            Assert.IsFalse(branchSplitter.RouteIssue(CreateIssue("Main Issue", fixture.Path)));
        }

        [Test]
        public void RejoinedSubBranch_BecomesPartOfThePipeItRejoins()
        {
            var fixture = CreateNestedSplitPathFixture();
            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.IsTrue(fixture.Path.TryGetBranchRoute(0, CellTop(fixture, 1, 2), out var branch));
            Assert.IsTrue(fixture.Path.TryGetBranchRoute(branch, CellTop(fixture, 3, 4), out var subBranch));

            // The sub-branch rejoins its parent at (3,7) and follows that branch's pipe exactly from there.
            var parentCells = fixture.Path.GetRouteCells(branch).ToList();
            var subCells = fixture.Path.GetRouteCells(subBranch).ToList();
            var rejoin = new Vector2Int(3, 7);
            CollectionAssert.AreEqual(parentCells.Skip(parentCells.IndexOf(rejoin)),
                subCells.Skip(subCells.IndexOf(rejoin)));

            // Issues on the two routes may merge once both are past the rejoin...
            Assert.IsTrue(fixture.Path.CanRoutesMergeAtProgress(
                branch, fixture.Path.GetWaypointCount(branch) - 3,
                subBranch, fixture.Path.GetWaypointCount(subBranch) - 3));
            // ...but not while the sub-branch is still out on its own loop.
            var onLoop = subCells.IndexOf(new Vector2Int(5, 5)) + 1;
            Assert.IsFalse(fixture.Path.CanRoutesMergeAtProgress(branch, onLoop, subBranch, onLoop));
        }

        [Test]
        public void PathSplitter_Awake_IgnoresPointerRaycastsWithoutBuildingVisuals()
        {
            var instance = CreateGameObject("Path Splitter");
            var child = CreateGameObject("Path Splitter Child");
            var ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            child.transform.SetParent(instance.transform);

            instance.AddComponent<PathSplitter>();

            Assert.AreEqual(ignoreRaycastLayer, instance.layer);
            Assert.AreEqual(ignoreRaycastLayer, child.layer);
            Assert.IsTrue(instance.GetComponent<Collider>().isTrigger);
            Assert.IsNull(instance.transform.Find("Path Splitter Visual"));
        }

        [Test]
        public void PathSplitter_AlternatesIssuesExactlyFiftyFifty()
        {
            var fixture = CreateSplitPathFixture();
            Assert.IsTrue(fixture.Path.Rebuild());
            var splitter = AddSplitter(fixture, new Vector2Int(1, 2));

            for (var i = 0; i < 4; i++)
            {
                var issue = CreateIssue($"Issue {i}", fixture.Path);
                Assert.IsTrue(splitter.RouteIssue(issue));
                Assert.AreEqual(i % 2, issue.GetRouteIndex(), $"issue {i}");
            }
        }

        [Test]
        public void PathSplitter_SharesSplitIssuesByMainSharePercent()
        {
            var fixture = CreateSplitPathFixture();
            Assert.IsTrue(fixture.Path.Rebuild());
            var splitter = AddSplitter(fixture, new Vector2Int(1, 2));
            splitter.MainSharePercent = 75;

            var branchCount = 0;
            for (var i = 0; i < 8; i++)
            {
                var issue = CreateIssue($"Issue {i}", fixture.Path);
                Assert.IsTrue(splitter.RouteIssue(issue));
                branchCount += issue.GetRouteIndex();
            }

            Assert.AreEqual(2, branchCount);
        }

        [Test]
        public void PathSplitter_PinsIssueTypesToTheirChosenLane()
        {
            var fixture = CreateSplitPathFixture();
            Assert.IsTrue(fixture.Path.Rebuild());
            var splitter = AddSplitter(fixture, new Vector2Int(1, 2));
            splitter.SetRule(IssueType.NonWaste, SplitterRule.BranchOnly);
            splitter.SetRule(IssueType.Organic, SplitterRule.MainOnly);

            IssueType[] arrivals =
            {
                IssueType.NonWaste, IssueType.Organic, IssueType.Chemical,
                IssueType.NonWaste, IssueType.Organic, IssueType.Chemical
            };
            // Chemical is still split, and pinned types don't disturb its 50/50 rotation.
            int[] expectedRoutes = { 1, 0, 0, 1, 0, 1 };

            for (var i = 0; i < arrivals.Length; i++)
            {
                var issue = CreateIssue($"Issue {i}", fixture.Path);
                issue.SetType(arrivals[i]);
                Assert.IsTrue(splitter.RouteIssue(issue));
                Assert.AreEqual(expectedRoutes[i], issue.GetRouteIndex(), $"{arrivals[i]} issue {i}");
            }
        }

        [Test]
        public void PathSplitter_DoesNothing_WhenThereIsOnlyOneCompleteRoute()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 10);
            Assert.IsTrue(fixture.Path.Rebuild());

            var splitter = CreateGameObject("Path Splitter").AddComponent<PathSplitter>();
            var issue = CreateIssue("Issue", fixture.Path);

            Assert.IsFalse(splitter.RouteIssue(issue));
            Assert.AreEqual(0, issue.GetRouteIndex());
        }

#if UNITY_EDITOR
        [Test]
        public void PathSplitterPanel_Prefab_TunesTheSplitterItIsShownFor()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<PathSplitterPanel>(
                "Assets/_project/Prefabs/UI/PathSplitterPanel.prefab");
            Assert.IsNotNull(prefab);

            var canvas = CreateGameObject("Canvas").AddComponent<Canvas>();
            var panel = Object.Instantiate(prefab, canvas.transform);
            var window = panel.transform.Find("Window").gameObject;
            var splitter = CreateGameObject("Path Splitter").AddComponent<PathSplitter>();
            Assert.IsFalse(window.activeSelf);

            panel.Show(splitter);

            Assert.IsTrue(panel.IsOpen);
            Assert.IsTrue(window.activeSelf);
            var slider = panel.GetComponentInChildren<Slider>();
            Assert.AreEqual(10f, slider.value);

            slider.value = 15f;
            Assert.AreEqual(75, splitter.MainSharePercent);

            window.transform.Find("NON-WASTE Row/BRANCH Button").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(SplitterRule.BranchOnly, splitter.GetRule(IssueType.NonWaste));
            Assert.AreEqual(SplitterRule.Split, splitter.GetRule(IssueType.Organic));

            window.transform.Find("Header/CLOSE Button").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(panel.IsOpen);
            Assert.IsFalse(window.activeSelf);

            // A splitter that goes away takes its window with it.
            panel.Show(splitter);
            splitter.gameObject.SetActive(false);
            panel.SendMessage("Update");
            Assert.IsFalse(window.activeSelf);
        }

#endif

        /// <summary>
        ///     The split fixture with splitters at its fork (1,2) and on its right-hand branch at
        ///     (3,4), where a further loop leaves the branch and rejoins it at (3,7).
        /// </summary>
        private PathFixture CreateNestedSplitPathFixture()
        {
            var fixture = CreateSplitPathFixture();
            PlaceHorizontal(fixture.Board, 4, 4, 2);
            PlaceVertical(fixture.Board, 5, 5, 2);
            PlaceHorizontal(fixture.Board, 4, 7, 2);
            AddSplitter(fixture, new Vector2Int(1, 2));
            AddSplitter(fixture, new Vector2Int(3, 4));
            return fixture;
        }

        private static PathWaterTube FindPreview(PathFixture fixture, string name) =>
            fixture.Board.transform.Find(name)?.GetComponentInChildren<PathWaterTube>();

        private static void AssertBranchPreviewsShowing(PathFixture fixture, int count)
        {
            fixture.Path.RefreshLivePreview();
            for (var i = 0; i < count; i++)
            {
                var name = i == 0 ? AlternatePreview : $"{AlternatePreview} {i + 1}";
                var tube = FindPreview(fixture, name);
                Assert.IsNotNull(tube, name);
                Assert.IsTrue(tube.IsShowing, name);
                Assert.Greater(tube.PointCount, 2, name);
            }

            var extra = FindPreview(fixture, $"{AlternatePreview} {count + 1}");
            Assert.IsTrue(!extra || !extra.IsShowing, "No preview beyond one per distinct branch.");
        }
    }
}
