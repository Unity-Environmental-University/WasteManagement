using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using _project.Scripts.Core;
using _project.Scripts.Object_Scripts;
using _project.Scripts.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace _project.Scripts.Tests
{
    public class StrictEndpointPathValidationTests
    {
        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
                if (go)
                    Object.DestroyImmediate(go);

            _created.Clear();
        }

        [Test]
    public void Rebuild_ReturnsTrue_WhenPathTouchesLowerAndUpperEndpoints()
    {
        var fixture = CreatePathFixture();
        PlaceVertical(fixture.Board, 1, 0, 10);

            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.IsTrue(fixture.Path.IsValid);
        Assert.AreEqual(12, fixture.Path.Count);
    }

    [Test]
    public void Rebuild_ReturnsTrue_WhenEndpointMarkerIsFarOutsideBoardButAlignedWithEdgeCell()
    {
        var fixture = CreatePathFixture(upperRow: 12);
        PlaceVertical(fixture.Board, 1, 0, 10);

        Assert.IsTrue(fixture.Path.Rebuild());
        Assert.IsTrue(fixture.Path.IsValid);
    }

        [Test]
        public void Rebuild_ReturnsFalse_WhenPathIsNearLowerEndpointButNotEdgeAdjacent()
        {
            var fixture = CreatePathFixture(0);
            PlaceVertical(fixture.Board, 1, 0, 10);

            Assert.IsFalse(fixture.Path.Rebuild());
            Assert.AreEqual("No placed path cell touches the lower endpoint square.", fixture.Path.InvalidReason);
        }

        [Test]
        public void Rebuild_ReturnsFalse_WhenPathIsNearUpperEndpointButNotEdgeAdjacent()
        {
            var fixture = CreatePathFixture(1, 0);
            PlaceVertical(fixture.Board, 1, 0, 10);

            Assert.IsFalse(fixture.Path.Rebuild());
            Assert.AreEqual("No placed path cell touches the upper endpoint square.", fixture.Path.InvalidReason);
        }

        [Test]
        public void Rebuild_ReturnsFalse_WhenOnlyLowerEndpointIsConnected()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 2);

            Assert.IsFalse(fixture.Path.Rebuild());
            Assert.AreEqual("No placed path cell touches the upper endpoint square.", fixture.Path.InvalidReason);
        }

        [Test]
        public void Rebuild_ReturnsFalse_WhenOnlyUpperEndpointIsConnected()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 8, 2);

            Assert.IsFalse(fixture.Path.Rebuild());
            Assert.AreEqual("No placed path cell touches the lower endpoint square.", fixture.Path.InvalidReason);
        }

        [Test]
        public void Rebuild_ReturnsFalse_WhenEndpointCandidatesAreDisconnected()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 2);
            PlaceVertical(fixture.Board, 1, 8, 2);

            Assert.IsFalse(fixture.Path.Rebuild());
            Assert.AreEqual("Placed path does not connect lower endpoint to upper endpoint.",
                fixture.Path.InvalidReason);
        }

        [Test]
        public void Rebuild_ReturnsTrue_WhenConnectedPathHasSideTouchingBranch()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 10);
            PlaceHorizontal(fixture.Board, 2, 5, 2);

            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.IsTrue(fixture.Path.IsValid);
            Assert.AreEqual(12, fixture.Path.Count);
        }

        [Test]
        public void Rebuild_CachesOneAlternateRoute_WhenPathHasTwoCompleteOptions()
        {
            var fixture = CreateSplitPathFixture();

            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.IsTrue(fixture.Path.HasAlternateRoute);
            Assert.AreEqual(12, fixture.Path.GetWaypointCount(0));
            Assert.Greater(fixture.Path.GetWaypointCount(1), fixture.Path.GetWaypointCount(0));
        }

        [Test]
        public void LivePreview_ShowsAlternateBranchOnlyWhileSplitterOccupiesFork()
        {
            var fixture = CreateSplitPathFixture();
            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.IsNull(fixture.Board.transform.Find("Alternate Path Preview"));

            var splitterObject = CreateGameObject("Path Splitter");
            splitterObject.transform.position = fixture.Board.GetCellTopPosition(new Vector2Int(1, 2));
            splitterObject.AddComponent<PathSplitter>();

            var alternatePreview = fixture.Board.transform.Find("Alternate Path Preview")
                ?.GetComponentInChildren<PathWaterTube>();
            Assert.IsNotNull(alternatePreview);
            Assert.IsTrue(alternatePreview.IsShowing);
            Assert.Greater(alternatePreview.PointCount, 2);

            splitterObject.SetActive(false);

            Assert.IsFalse(alternatePreview.IsShowing);
            Assert.AreEqual(0, alternatePreview.PointCount);
        }

        [Test]
        public void PathSplitter_ShowsOnlyThePipeStubsItsRoutesUse()
        {
            var fixture = CreateSplitPathFixture();
            Assert.IsTrue(fixture.Path.Rebuild());

            var cell = new Vector2Int(1, 2);
            var host = CreateGameObject("Path Splitter");
            host.transform.position = CellTop(fixture, cell.x, cell.y);

            Renderer AddStub(Vector2Int neighbor)
            {
                var stub = CreatePrimitive($"Model_Pipe {neighbor}");
                stub.transform.SetParent(host.transform, false);
                stub.transform.position = Vector3.Lerp(host.transform.position,
                    CellTop(fixture, neighbor.x, neighbor.y), 0.4f);
                return stub.GetComponent<Renderer>();
            }

            var intake = AddStub(new Vector2Int(1, 1));
            var mainOutlet = AddStub(new Vector2Int(1, 3));
            var branchOutlet = AddStub(new Vector2Int(2, 2));
            var unused = AddStub(new Vector2Int(0, 2));

            // The splitter finds its stubs by name and announces itself, which refreshes them.
            var splitter = host.AddComponent<PathSplitter>();

            Assert.IsTrue(intake.enabled);
            Assert.IsTrue(mainOutlet.enabled);
            Assert.IsTrue(branchOutlet.enabled);
            Assert.IsFalse(unused.enabled, "No route uses the fourth side.");

            // Placed ahead of any pipe, the splitter has nothing to connect to.
            splitter.transform.position = CellTop(fixture, 5, 5);
            splitter.enabled = false;
            splitter.enabled = true;
            foreach (var stub in new[] { intake, mainOutlet, branchOutlet, unused })
                Assert.IsFalse(stub.enabled, stub.name);

            // Pipe laid beside it shows that side at once, before any route reaches the splitter.
            PlaceVertical(fixture.Board, 5, 5, 2);
            Assert.IsTrue(mainOutlet.enabled);
            Assert.IsFalse(intake.enabled);
            Assert.IsFalse(branchOutlet.enabled);
            Assert.IsFalse(unused.enabled);
        }

        [Test]
        public void LivePreview_DoesNotShowAlternateBranchForSplitterAwayFromFork()
        {
            var fixture = CreateSplitPathFixture();
            Assert.IsTrue(fixture.Path.Rebuild());

            var splitterObject = CreateGameObject("Path Splitter");
            splitterObject.transform.position = fixture.Board.GetCellTopPosition(new Vector2Int(1, 1));
            splitterObject.AddComponent<PathSplitter>();

            Assert.IsNull(fixture.Board.transform.Find("Alternate Path Preview"));
        }

        [UnityTest]
        public IEnumerator LivePreview_RefreshesAfterPlacedSplitterMovesOntoFork()
        {
            var fixture = CreateSplitPathFixture();
            Assert.IsTrue(fixture.Path.Rebuild());

            // Reproduce prefab placement lifecycle: OnEnable can run before the final slot
            // position is visible to listeners, leaving the initial availability event stale.
            var splitterObject = CreateGameObject("Path Splitter");
            splitterObject.transform.position = fixture.Board.GetCellTopPosition(new Vector2Int(1, 1));
            splitterObject.AddComponent<PathSplitter>();
            splitterObject.transform.position = fixture.Board.GetCellTopPosition(new Vector2Int(1, 2));

            // The splitter re-announces itself from Start once its placement frame settles.
            yield return null;

            var alternatePreview = fixture.Board.transform.Find("Alternate Path Preview")
                ?.GetComponentInChildren<PathWaterTube>();
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
            splitterObject.transform.position = fixture.Board.GetCellTopPosition(new Vector2Int(1, 2));
            splitterObject.AddComponent<PathSplitter>();

            PlaceVertical(fixture.Board, 1, 0, 4);
            Assert.IsNull(fixture.Board.transform.Find("Alternate Path Preview"));

            PlaceHorizontal(fixture.Board, 2, 2, 2);

            var alternatePreview = fixture.Board.transform.Find("Alternate Path Preview")
                ?.GetComponentInChildren<PathWaterTube>();
            Assert.IsNotNull(alternatePreview);
            Assert.IsTrue(alternatePreview.IsShowing);
            // Fork cell plus both cells of the branch built so far.
            Assert.AreEqual(3, alternatePreview.PointCount);
            Assert.IsFalse(fixture.Path.Rebuild());
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
            Assert.IsTrue(fixture.Path.IsSplitPoint(fixture.Board.GetCellTopPosition(new Vector2Int(1, 1))));

            var splitter = CreateGameObject("Path Splitter").AddComponent<PathSplitter>();
            splitter.transform.position = fixture.Board.GetCellTopPosition(new Vector2Int(1, 5));

            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.IsTrue(fixture.Path.HasAlternateRoute);
            Assert.IsTrue(fixture.Path.IsSplitPoint(splitter.transform.position));
            Assert.IsFalse(fixture.Path.IsSplitPoint(fixture.Board.GetCellTopPosition(new Vector2Int(1, 1))));
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

            var diverted = CreatePrimitive("Diverted Issue").AddComponent<IssueObject>();
            diverted.SetType(IssueType.Organic);
            diverted.SetPath(fixture.Path);
            Assert.IsTrue(diverted.TrySetRoute(branch));
            Assert.IsTrue(branchSplitter.RouteIssue(diverted));
            Assert.AreEqual(subBranch, diverted.GetRouteIndex());

            var kept = CreatePrimitive("Kept Issue").AddComponent<IssueObject>();
            kept.SetType(IssueType.Chemical);
            kept.SetPath(fixture.Path);
            Assert.IsTrue(kept.TrySetRoute(branch));
            Assert.IsTrue(branchSplitter.RouteIssue(kept));
            Assert.AreEqual(branch, kept.GetRouteIndex(), "The main lane keeps an issue on the branch it arrived on.");

            // Issues on the main route pass this splitter's cell by without being split.
            var mainIssue = CreatePrimitive("Main Issue").AddComponent<IssueObject>();
            mainIssue.SetPath(fixture.Path);
            Assert.IsFalse(branchSplitter.RouteIssue(mainIssue));
        }

        [Test]
        public void RejoinedSubBranch_BecomesPartOfThePipeItRejoins()
        {
            var fixture = CreateNestedSplitPathFixture();
            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.IsTrue(fixture.Path.TryGetBranchRoute(0, CellTop(fixture, 1, 2), out var branch));
            Assert.IsTrue(fixture.Path.TryGetBranchRoute(branch, CellTop(fixture, 3, 4), out var subBranch));

            // The sub-branch rejoins its parent at (3,7) and follows that branch's pipe exactly from there.
            var parentCells = fixture.Path.GetRouteCells(branch);
            var subCells = fixture.Path.GetRouteCells(subBranch);
            var rejoin = new Vector2Int(3, 7);
            var parentTail = parentCells.Skip(parentCells.ToList().IndexOf(rejoin)).ToList();
            var subTail = subCells.Skip(subCells.ToList().IndexOf(rejoin)).ToList();
            CollectionAssert.AreEqual(parentTail, subTail);

            // Issues on the two routes may merge once both are past the rejoin...
            Assert.IsTrue(fixture.Path.CanRoutesMergeAtProgress(
                branch, fixture.Path.GetWaypointCount(branch) - 3,
                subBranch, fixture.Path.GetWaypointCount(subBranch) - 3));
            // ...but not while the sub-branch is still out on its own loop.
            Assert.IsFalse(fixture.Path.CanRoutesMergeAtProgress(
                branch, subCells.ToList().IndexOf(new Vector2Int(5, 5)) + 1,
                subBranch, subCells.ToList().IndexOf(new Vector2Int(5, 5)) + 1));
        }

        [Test]
        public void PathSplitter_Awake_IgnoresPointerRaycastsWhileKeepingTrigger()
        {
            var instance = CreateGameObject("Path Splitter");
            var child = CreateGameObject("Path Splitter Child");
            var ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            child.transform.SetParent(instance.transform);

            instance.AddComponent<PathSplitter>();

            Assert.AreEqual(ignoreRaycastLayer, instance.layer);
            Assert.AreEqual(ignoreRaycastLayer, child.layer);
            Assert.IsTrue(instance.GetComponent<Collider>().isTrigger);
        }

        [Test]
        public void PathSplitter_Awake_DoesNotGenerateVisualHierarchy()
        {
            var instance = CreateGameObject("Path Splitter");
            instance.AddComponent<PathSplitter>();

            Assert.IsNull(instance.transform.Find("Path Splitter Visual"));
            Assert.IsTrue(instance.GetComponent<Collider>().isTrigger);
        }

        [Test]
        public void RoutesCanMerge_AfterBranchesRejoin_ButNotWhileTheyAreSeparated()
        {
            var fixture = CreateSplitPathFixture();
            Assert.IsTrue(fixture.Path.Rebuild());

            // Both routes share waypoint 3 at the fork but target different cells afterward.
            Assert.IsFalse(fixture.Path.CanRoutesMergeAtProgress(0, 4, 1, 4));

            // The last three targets are the shared rejoined cell, final board cell, and endpoint.
            Assert.IsTrue(fixture.Path.CanRoutesMergeAtProgress(
                0, fixture.Path.GetWaypointCount(0) - 3,
                1, fixture.Path.GetWaypointCount(1) - 3));
        }

        [Test]
        public void PathSplitterShopItem_PlacesOnCellWithoutPipe()
        {
            var fixture = CreatePathFixture();

            var gameMaster = CreateGameObject("Game Master").AddComponent<GameMaster>();
            gameMaster.pathBuildBoard = fixture.Board;
            var prefab = CreateGameObject("Path Splitter Prefab");
            prefab.AddComponent<PathSplitter>();
            var item = new PathSplitterShopItem("Path Splitter", string.Empty, 1, prefab, null, 1);
            var slot = GetCell(fixture.Board, 1, 2).transform;

            Assert.IsFalse(fixture.Board.IsOccupied(new Vector2Int(1, 2)));
            var placed = item.Place(slot);
            _created.Add(placed);

            Assert.IsNotNull(placed);
            Assert.AreEqual(slot.position, placed.transform.position);
        }

        [Test]
        public void PathSplitter_AlternatesIssuesExactlyFiftyFifty()
        {
            var fixture = CreateSplitPathFixture();
            Assert.IsTrue(fixture.Path.Rebuild());

            var splitter = CreateGameObject("Path Splitter").AddComponent<PathSplitter>();
            splitter.transform.position = fixture.Board.GetCellTopPosition(new Vector2Int(1, 2));
            var issues = new IssueObject[4];
            for (var i = 0; i < issues.Length; i++)
            {
                issues[i] = CreatePrimitive($"Issue {i}").AddComponent<IssueObject>();
                issues[i].SetPath(fixture.Path);
                Assert.IsTrue(splitter.RouteIssue(issues[i]));
            }

            Assert.AreEqual(0, issues[0].GetRouteIndex());
            Assert.AreEqual(1, issues[1].GetRouteIndex());
            Assert.AreEqual(0, issues[2].GetRouteIndex());
            Assert.AreEqual(1, issues[3].GetRouteIndex());
        }

        [Test]
        public void PathSplitter_SharesSplitIssuesByMainSharePercent()
        {
            var fixture = CreateSplitPathFixture();
            Assert.IsTrue(fixture.Path.Rebuild());

            var splitter = CreateGameObject("Path Splitter").AddComponent<PathSplitter>();
            splitter.transform.position = fixture.Board.GetCellTopPosition(new Vector2Int(1, 2));
            splitter.MainSharePercent = 75;

            var branchCount = 0;
            for (var i = 0; i < 8; i++)
            {
                var issue = CreatePrimitive($"Issue {i}").AddComponent<IssueObject>();
                issue.SetPath(fixture.Path);
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

            var splitter = CreateGameObject("Path Splitter").AddComponent<PathSplitter>();
            splitter.transform.position = fixture.Board.GetCellTopPosition(new Vector2Int(1, 2));
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
                var issue = CreatePrimitive($"Issue {i}").AddComponent<IssueObject>();
                issue.SetPath(fixture.Path);
                issue.SetType(arrivals[i]);
                Assert.IsTrue(splitter.RouteIssue(issue));
                Assert.AreEqual(expectedRoutes[i], issue.GetRouteIndex(), $"{arrivals[i]} issue {i}");
            }
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

        [Test]
        public void PathSplitter_DoesNothing_WhenThereIsOnlyOneCompleteRoute()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 10);
            Assert.IsTrue(fixture.Path.Rebuild());

            var splitter = CreateGameObject("Path Splitter").AddComponent<PathSplitter>();
            var issue = CreatePrimitive("Issue").AddComponent<IssueObject>();
            issue.SetPath(fixture.Path);

            Assert.IsFalse(splitter.RouteIssue(issue));
            Assert.AreEqual(0, issue.GetRouteIndex());
        }

        [Test]
        public void Rebuild_LeavesCountZero_WhenValidationFails()
        {
            var fixture = CreatePathFixture(0);
            PlaceVertical(fixture.Board, 1, 0, 10);

            fixture.Path.Rebuild();

            Assert.AreEqual(0, fixture.Path.Count);
        }

        [Test]
        public void Rebuild_ReturnsFalse_WhenValidationFails()
        {
            var fixture = CreatePathFixture(0);
            PlaceVertical(fixture.Board, 1, 0, 10);

            Assert.IsFalse(fixture.Path.Rebuild());
        }

        [Test]
        public void Rebuild_DoesNotEmitDirectStartToEndFallback_WhenValidationFails()
        {
            var fixture = CreatePathFixture(0);
            PlaceVertical(fixture.Board, 1, 0, 10);

            fixture.Path.Rebuild();

            Assert.AreEqual(0, fixture.Path.Count);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void HeldIssue_ResumesAtItsCell_AfterRouteIsShortened(bool flush)
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 2);
            PlaceHorizontal(fixture.Board, 2, 1, 2);
            PlaceVertical(fixture.Board, 3, 2, 4);
            PlaceHorizontal(fixture.Board, 1, 5, 2);
            PlaceVertical(fixture.Board, 1, 6, 4);
            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.AreEqual(16, fixture.Path.Count);

            var issue = CreatePrimitive("Held Junk").AddComponent<IssueObject>();
            issue.SetType(IssueType.NonWaste);
            issue.SetPath(fixture.Path);
            issue.transform.position = fixture.Board.GetPathWaypointPosition(new Vector2Int(1, 7));
            SetField(issue, "_waypointIndex", fixture.Path.FindClosestWaypointIndex(0, issue.transform.position));
            issue.SetHeldBySifter(true);
            var unheldCount = IssueObject.ActiveUnheldCount;

            PlaceVertical(fixture.Board, 1, 2, 3);
            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.AreEqual(12, fixture.Path.Count);
            Assert.GreaterOrEqual(issue.GetWaypointIndex(), fixture.Path.Count);

            if (flush) issue.FlushFromSifter();
            else issue.ReleaseFromSifter(default);

            Assert.AreEqual(8, issue.GetWaypointIndex());
            Assert.Less(Vector3.SqrMagnitude(issue.transform.position -
                fixture.Path.GetPosition(issue.GetWaypointIndex())), 0.0001f);
            Assert.Less(Vector3.SqrMagnitude(fixture.Board.GetPathWaypointPosition(new Vector2Int(1, 8)) -
                fixture.Path.GetPosition(issue.GetWaypointIndex() + 1)), 0.0001f);
            Assert.AreEqual(unheldCount + 1, IssueObject.ActiveUnheldCount);
        }

        [TestCase(true, true, 1f)]
        [TestCase(true, true, 4f)]
        [TestCase(true, false, 1f)]
        [TestCase(true, false, 4f)]
        [TestCase(false, true, 1f)]
        [TestCase(false, true, 4f)]
        [TestCase(false, false, 1f)]
        [TestCase(false, false, 4f)]
        public void HeldIssue_RebasesTemporarySpeedDuration_WhenRouteChanges(bool shorten, bool flush, float speed)
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 2);
            PlaceHorizontal(fixture.Board, 2, 1, 2);
            PlaceVertical(fixture.Board, 3, 2, 4);
            PlaceHorizontal(fixture.Board, 1, 5, 2);
            PlaceVertical(fixture.Board, 1, 6, 4);
            if (!shorten) PlaceVertical(fixture.Board, 1, 2, 3);
            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.AreEqual(shorten ? 16 : 12, fixture.Path.Count);

            var gm = CreateGameObject("Game Master").AddComponent<GameMaster>();
            gm.pathBuildBoard = fixture.Board;
            var issue = CreatePrimitive("Held Junk").AddComponent<IssueObject>();
            issue.SetType(IssueType.NonWaste);
            issue.SetSize(2);
            issue.SetPath(fixture.Path);
            issue.SetMoveSpeed(2f);
            var heldPosition = fixture.Board.GetPathWaypointPosition(new Vector2Int(1, 7));
            var heldIndex = fixture.Path.FindClosestWaypointIndex(0, heldPosition);
            SetField(issue, "_waypointIndex", heldIndex - 1);
            issue.SetTemporaryMoveSpeed(speed, 3);
            AdvanceIssueOneWaypoint(issue, fixture.Board);
            issue.transform.position = heldPosition;
            issue.SetHeldBySifter(true);

            if (shorten) PlaceVertical(fixture.Board, 1, 2, 3);
            else Assert.IsTrue(fixture.Board.TryBreak(GetCell(fixture.Board, 1, 3), out _));
            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.AreEqual(shorten ? 12 : 16, fixture.Path.Count);

            if (flush) issue.FlushFromSifter();
            else issue.ReleaseFromSifter(default);

            Assert.AreEqual(shorten ? 8 : 12, issue.GetWaypointIndex());
            Assert.AreEqual(speed, GetField<float>(issue, "moveSpeed"),
                "Releasing must preserve the speed effect that still has two waypoints left.");
            AdvanceIssueOneWaypoint(issue, fixture.Board);
            Assert.AreEqual(speed, GetField<float>(issue, "moveSpeed"),
                "The effect must stay active until both remaining waypoints have been traversed.");
            AdvanceIssueOneWaypoint(issue, fixture.Board);
            Assert.AreEqual(2f, GetField<float>(issue, "moveSpeed"),
                "The effect must restore the original speed after its two remaining waypoints.");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void HeldIssue_ResumesOnMainRoute_WhenAlternateRouteIsRemoved(bool flush)
        {
            var fixture = CreateSplitPathFixture();
            Assert.IsTrue(fixture.Path.Rebuild());
            var issue = CreatePrimitive("Held Branch Junk").AddComponent<IssueObject>();
            issue.SetType(IssueType.NonWaste);
            issue.SetPath(fixture.Path);
            Assert.IsTrue(issue.TrySetRoute(1));
            issue.transform.position = fixture.Board.GetPathWaypointPosition(new Vector2Int(1, 8));
            SetField(issue, "_waypointIndex", fixture.Path.FindClosestWaypointIndex(1, issue.transform.position));
            issue.SetHeldBySifter(true);

            Assert.IsTrue(fixture.Board.TryBreak(GetCell(fixture.Board, 3, 3), out _));
            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.IsFalse(fixture.Path.HasAlternateRoute);

            if (flush) issue.FlushFromSifter();
            else issue.ReleaseFromSifter(default);

            Assert.AreEqual(0, issue.GetRouteIndex());
            Assert.AreEqual(9, issue.GetWaypointIndex());
            Assert.Less(Vector3.SqrMagnitude(issue.transform.position -
                fixture.Path.GetPosition(issue.GetWaypointIndex())), 0.0001f);
        }

        [Test]
        public void HeldIssue_PreservesItsNextTarget_WhenRouteIsRebuiltWithoutChanges()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 10);
            Assert.IsTrue(fixture.Path.Rebuild());
            var issue = CreatePrimitive("Held Junk").AddComponent<IssueObject>();
            issue.SetPath(fixture.Path);
            issue.transform.position = fixture.Board.GetPathWaypointPosition(new Vector2Int(1, 7));
            SetField(issue, "_waypointIndex", 9);
            issue.SetHeldBySifter(true);

            Assert.IsTrue(fixture.Path.Rebuild());
            issue.FlushFromSifter();

            Assert.AreEqual(9, issue.GetWaypointIndex(), "Releasing should not send it backward to the nearest cell.");
        }

        [Test]
        public void TryGetPathFacingRotation_FacesAlongHorizontalPipe_WhenPlacedOnPipe()
        {
            var board = CreateGameObject("Path Board").AddComponent<PathBuildBoard>();
            PlaceHorizontal(board, 1, 1, 3);

            Assert.IsTrue(board.TryGetPathFacingRotation(board.GetCellTopPosition(new Vector2Int(1, 1)),
                out var rotation));

            AssertFaces(rotation, Vector3.right);
        }

        [Test]
        public void TryGetPathFacingRotation_FacesAlongVerticalPipe_WhenPlacedOnPipe()
        {
            var board = CreateGameObject("Path Board").AddComponent<PathBuildBoard>();
            PlaceVertical(board, 1, 1, 3);

            Assert.IsTrue(board.TryGetPathFacingRotation(board.GetCellTopPosition(new Vector2Int(1, 1)),
                out var rotation));

            AssertFaces(rotation, Vector3.forward);
        }

        [Test]
        public void TryGetPathFacingRotation_FacesDownstream_WhenLiveRouteFlowsAgainstPipeAxis()
        {
            // Water enters up column 3, runs right-to-left along row 2, then turns up column 1.
            var fixture = CreatePathFixture(3);
            PlaceVertical(fixture.Board, 3, 0, 2);
            PlaceHorizontal(fixture.Board, 1, 2, 3);
            PlaceVertical(fixture.Board, 1, 3, 7);
            fixture.Path.RefreshLivePreview();

            Assert.IsTrue(fixture.Board.TryGetPathFacingRotation(
                fixture.Board.GetCellTopPosition(new Vector2Int(2, 2)), out var horizontal));
            AssertFaces(horizontal, Vector3.left);

            Assert.IsTrue(fixture.Board.TryGetPathFacingRotation(
                fixture.Board.GetCellTopPosition(new Vector2Int(1, 5)), out var vertical));
            AssertFaces(vertical, Vector3.forward);
        }

        [Test]
        public void TryGetPathFacingRotation_ReturnsFalse_WhenPlacedOffPipe()
        {
            var board = CreateGameObject("Path Board").AddComponent<PathBuildBoard>();
            PlaceVertical(board, 1, 1, 3);

            Assert.IsFalse(board.TryGetPathFacingRotation(board.GetCellTopPosition(new Vector2Int(2, 1)),
                out _));
        }

        [Test]
        public void TryGetPathFacingRotation_ReturnsFalse_WhenPlacedOutsideBoardNearOccupiedEdge()
        {
            var board = CreateGameObject("Path Board").AddComponent<PathBuildBoard>();
            PlaceVertical(board, 1, 0, 2);

            Assert.IsFalse(board.TryGetPathFacingRotation(board.GetCellTopPosition(new Vector2Int(1, -1)),
                out _));
        }

        [Test]
        public void IssueVisualOverride_PersistsAfterProcessingChangesSize()
        {
            var issue = CreatePrimitive("Runaway Issue").AddComponent<IssueObject>();
            var runawayColor = new Color(1f, 0.45f, 0f);

            issue.SetSize(3);
            issue.SetVisualOverride(runawayColor);
            issue.Process(1, "Test Process");

            // The override tint rides in a MaterialPropertyBlock (no per-renderer material
            // instance), so read it back from the block rather than .material.color.
            var propertyBlock = new MaterialPropertyBlock();
            issue.GetComponent<Renderer>().GetPropertyBlock(propertyBlock);
            Assert.AreEqual(runawayColor, propertyBlock.GetColor("_Color"));
        }

        [UnityTest]
        public IEnumerator IssueObject_OnPathCollision_MergesIntoNextSizeStage()
        {
            // The merged issue stays below pipeBlockSize, so it keeps moving after the merge —
            // it needs a real path, or its first Update treats the empty path as "reached the end".
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 10);
            Assert.IsTrue(fixture.Path.Rebuild());

            var issueA = CreatePrimitive("Issue A").AddComponent<IssueObject>();
            var issueB = CreatePrimitive("Issue B").AddComponent<IssueObject>();

            issueA.SetPath(fixture.Path);
            issueB.SetPath(fixture.Path);
            issueA.SetSize(1);
            issueB.SetSize(1);

            issueA.SendMessage("OnTriggerEnter", issueB.GetComponent<Collider>());
            yield return null;

            Assert.IsTrue(issueA);
            Assert.IsFalse(issueB);
            Assert.AreEqual(2f, issueA.ProcessCost);
            Assert.AreEqual(Vector3.one * 2f, issueA.transform.localScale);
        }

        [UnityTest]
        public IEnumerator IssueObject_BlockingMerge_PreservesSurvivorPathPosition()
        {
            var fixture = CreatePathFixture();
            var issueA = CreatePrimitive("Blocking Issue A").AddComponent<IssueObject>();
            var issueB = CreatePrimitive("Blocking Issue B").AddComponent<IssueObject>();
            var survivorPosition = new Vector3(2f, 0f, 3f);

            issueA.SetPath(fixture.Path);
            issueB.SetPath(fixture.Path);
            issueA.SetSize(3);
            issueB.SetSize(3);
            issueA.transform.position = survivorPosition;
            issueB.transform.position = survivorPosition + new Vector3(0.25f, 0f, 0.25f);

            issueA.SendMessage("OnTriggerEnter", issueB.GetComponent<Collider>());
            yield return null;

            Assert.IsTrue(issueA.IsBlockingPipe);
            Assert.AreEqual(survivorPosition.x, issueA.transform.position.x, 0.0001f);
            Assert.AreEqual(survivorPosition.z, issueA.transform.position.z, 0.0001f);
        }

        [UnityTest]
        public IEnumerator IssueObject_BlockageBurstsAfterDeadline_WhenNotBrokenDown()
        {
            var issue = CreatePrimitive("Timed Blocking Issue").AddComponent<IssueObject>();
            SetField(issue, "blockedBurstDelay", 0.05f);
            issue.SetSize(4);

            Assert.IsTrue(issue.IsBlockingPipe);

            yield return new WaitForSeconds(0.1f);
            yield return null;

            Assert.IsFalse(issue, "An uncleared pipe blockage should burst and destroy itself after its deadline.");
        }

        [UnityTest]
        public IEnumerator IssueObject_DirectDestinationCollision_DoesNotMerge()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 10);
            Assert.IsTrue(fixture.Path.Rebuild());

            var pathIssue = CreatePrimitive("Path Issue").AddComponent<IssueObject>();
            var runawayIssue = CreatePrimitive("Runaway Issue").AddComponent<IssueObject>();

            pathIssue.SetPath(fixture.Path);
            runawayIssue.SetDirectDestination(Vector3.forward);
            pathIssue.SetSize(1);
            runawayIssue.SetSize(1);

            pathIssue.SendMessage("OnTriggerEnter", runawayIssue.GetComponent<Collider>());
            yield return null;

            Assert.IsTrue(pathIssue);
            Assert.IsTrue(runawayIssue);
            Assert.AreEqual(1f, pathIssue.ProcessCost);
            Assert.AreEqual(1f, runawayIssue.ProcessCost);
        }

        [Test]
        public void BuffDebuffTile_Awake_IgnoresPointerRaycastsWhileKeepingTrigger()
        {
            var tileGo = CreateGameObject("Buff Tile");
            var childGo = CreateGameObject("Buff Tile Child");
            var ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            childGo.transform.SetParent(tileGo.transform);

            tileGo.AddComponent<BuffDebuffTileController>();

            Assert.AreEqual(ignoreRaycastLayer, tileGo.layer);
            Assert.AreEqual(ignoreRaycastLayer, childGo.layer);
            Assert.IsTrue(tileGo.GetComponent<Collider>().isTrigger);
        }

        [Test]
        public void SifterPlace_ReturnsNull_WhenSlotIsOutsideBoardNearOccupiedEdge()
        {
            var gm = CreateGameObject("Game Master").AddComponent<GameMaster>();
            var board = CreateGameObject("Path Board").AddComponent<PathBuildBoard>();
            var prefab = CreatePrimitive("Sifter Prefab");
            var slot = CreateGameObject("Outside Slot").transform;
            var item = new SifterShopItem("Sifter", "", 1, prefab, null, 1);

            gm.pathBuildBoard = board;
            PlaceVertical(board, 1, 0, 2);
            slot.position = board.GetCellTopPosition(new Vector2Int(1, -1));

            Assert.IsNull(item.Place(slot));
        }

        [Test]
        public void LimeSprinklerPlace_SucceedsOnEmptyUtilityCell()
        {
            var gm = CreateGameObject("Game Master").AddComponent<GameMaster>();
            var board = CreateGameObject("Path Board").AddComponent<PathBuildBoard>();
            var prefab = CreatePrimitive("Lime Sprinkler Prefab");
            var slot = CreateGameObject("Empty Utility Slot").transform;
            var item = new LimeSprinklerShopItem("Lime Sprinkler", "", 1, prefab, null, 2);

            gm.pathBuildBoard = board;
            var cell = new Vector2Int(1, 1);
            slot.position = board.GetCellTopPosition(cell);

            Assert.IsFalse(board.IsOccupied(cell));
            var placed = item.Place(slot);
            _created.Add(placed);

            Assert.IsNotNull(placed);
            Assert.AreEqual(slot.position, placed.transform.position);
        }

        [Test]
        public void NonLimePlaceables_ReturnNullOnEmptyUtilityCell()
        {
            var gm = CreateGameObject("Game Master").AddComponent<GameMaster>();
            var board = CreateGameObject("Path Board").AddComponent<PathBuildBoard>();
            var prefab = CreatePrimitive("Placement Prefab");
            var slot = CreateGameObject("Empty Utility Slot").transform;
            var cell = new Vector2Int(1, 1);

            gm.pathBuildBoard = board;
            slot.position = board.GetCellTopPosition(cell);

            IPlaceable[] items =
            {
                new TowerShopItem("Tower", "", 1, prefab, null, 1),
                new SifterShopItem("Sifter", "", 1, prefab, null, 1),
                new CesspitShopItem("Cesspit", "", 1, prefab, null, 1),
                new TreatmentTankShopItem("Treatment Tank", "", 1, prefab, null, 1)
            };

            Assert.IsFalse(board.IsOccupied(cell));
            foreach (var item in items)
                Assert.IsNull(item.Place(slot), $"{item.DisplayName} should require pipe beneath its slot.");
        }

        [UnityTest]
        public IEnumerator EndPhase_InvalidPath_KeepsCardPhase()
        {
            var fixture = CreateTurnFixture(false);
            LogAssert.Expect(LogType.Warning,
                "Cannot begin wave: No placed path cell touches the lower endpoint square.");

            fixture.TurnController.EndPhase();
            yield return null;

            Assert.AreEqual(GamePhase.Card, fixture.TurnController.currentPhase);
        }

        [UnityTest]
        public IEnumerator EndPhase_InvalidPath_DoesNotStartSpawnerCoroutine()
        {
            var fixture = CreateTurnFixture(false);
            LogAssert.Expect(LogType.Warning,
                "Cannot begin wave: No placed path cell touches the lower endpoint square.");

            fixture.TurnController.EndPhase();
            yield return null;

            Assert.IsNull(GetField<Coroutine>(fixture.Spawner, "_spawnCoroutine"));
        }

        [UnityTest]
        public IEnumerator EndPhase_ValidPath_TransitionsToTowerPhase()
        {
            var fixture = CreateTurnFixture(true);

            fixture.TurnController.EndPhase();
            yield return null;

            Assert.AreEqual(GamePhase.Tower, fixture.TurnController.currentPhase);
        }

        [UnityTest]
        public IEnumerator WaveTimer_WaitsForActiveIssuesBeforeApplyingPostWaveGrowth()
        {
            var fixture = CreateTurnFixture(true);
            fixture.TurnController.waveDuration = 0f;

            fixture.TurnController.EndPhase();
            var issue = CreatePrimitive("Late Issue").AddComponent<IssueObject>();
            issue.SetPath(fixture.Path);
            issue.SetMoveSpeed(0f);

            yield return null;

            Assert.AreEqual(GamePhase.Tower, fixture.TurnController.currentPhase);
            fixture.GameMaster.popManager.RecordLakePollution(3f);

            Object.Destroy(issue.gameObject);
            yield return null;
            yield return null;

            Assert.AreEqual(GamePhase.Card, fixture.TurnController.currentPhase);
            Assert.AreEqual(10, fixture.GameMaster.popManager.GetPopulationSize());
            Assert.AreEqual(0f, fixture.GameMaster.popManager.GetWavePollution());
        }

        [UnityTest]
        public IEnumerator WaveTimer_RecoversLakeBeforeNextCardPhase()
        {
            var fixture = CreateTurnFixture(true);
            fixture.TurnController.waveDuration = 0f;
            var lake = CreatePrimitive("Lake").AddComponent<LakeController>();
            lake.health = 90f;

            fixture.TurnController.EndPhase();
            yield return null;
            yield return null;

            Assert.AreEqual(GamePhase.Card, fixture.TurnController.currentPhase);
            Assert.AreEqual(92f, lake.health, 0.0001f);
        }

        [Test]
        public void LakeController_RecoverForTurn_CapsAtFullHealth()
        {
            var lake = CreatePrimitive("Lake").AddComponent<LakeController>();
            lake.health = 99f;

            lake.RecoverForTurn();

            Assert.AreEqual(100f, lake.health, 0.0001f);
        }

        [UnityTest]
        public IEnumerator WaveTimer_StopsCesspitRunawaysWhenSpawnersStop()
        {
            var fixture = CreateTurnFixture(true);
            fixture.TurnController.waveDuration = 0f;
            fixture.TurnController.currentLevel = 3;
            var cesspit = CreateGameObject("Cesspit").AddComponent<Cesspit>();
            cesspit.maxFullness = 1f;
            cesspit.fullness = 1f;

            yield return null;
            Assert.IsNotNull(GetField<Coroutine>(cesspit, "_runawayCoroutine"));

            fixture.TurnController.EndPhase();
            yield return null;
            yield return null;

            Assert.AreEqual(GamePhase.Card, fixture.TurnController.currentPhase);
            Assert.IsNull(GetField<Coroutine>(cesspit, "_runawayCoroutine"));
        }

        [UnityTest]
        public IEnumerator EndPhase_TowerToSummary_StopsCesspitRunawaysImmediately()
        {
            var fixture = CreateTurnFixture(true);
            var cesspit = CreateGameObject("Cesspit").AddComponent<Cesspit>();

            fixture.TurnController.currentLevel = 3;

            cesspit.maxFullness = 1f;
            cesspit.fullness = 1f;

            yield return null;
            fixture.TurnController.EndPhase();

            Assert.IsNotNull(GetField<Coroutine>(cesspit, "_runawayCoroutine"));
            Assert.IsNotNull(GetField<Coroutine>(fixture.Spawner, "_spawnCoroutine"));

            fixture.TurnController.EndPhase();

            Assert.IsNull(GetField<Coroutine>(cesspit, "_runawayCoroutine"));
            Assert.IsNull(GetField<Coroutine>(fixture.Spawner, "_spawnCoroutine"));
        }

        [UnityTest]
        public IEnumerator Cesspit_UsesIssuePathDestinationForRunaways()
        {
            var fixture = CreateTurnFixture(true);
            var cesspit = CreateGameObject("Cesspit").AddComponent<Cesspit>();
            var oldDestination = CreateGameObject("Old Runaway Destination").transform;
            SetField(cesspit, "runawayDestination", oldDestination);

            yield return null;

            Assert.AreSame(fixture.Path.Destination, GetField<Transform>(cesspit, "runawayDestination"));
        }

        [UnityTest]
        public IEnumerator EndPhase_TowerToCard_DoesNotThrowWhenInfoBarTextIsUnset()
        {
            var fixture = CreateTurnFixture(true);
            fixture.TurnController.currentPhase = GamePhase.Tower;

            fixture.TurnController.EndPhase();
            yield return null;

            Assert.AreEqual(GamePhase.Card, fixture.TurnController.currentPhase);
        }

        [Test]
        public void OnMouseDown_PathPlacementAddsInfrastructureValue()
        {
            var board = CreateGameObject("Path Board").AddComponent<PathBuildBoard>();
            var gm = CreateGameObject("Game Master").AddComponent<GameMaster>();
            var piece = new PathPiecePlaceable("Pipe", "", 1, 2, null, 3);
            var targetCell = GetCell(board, 1, 1);

            gm.turnController.currentPhase = GamePhase.Card;
            board.SetActivePiece(piece);

            targetCell.SendMessage("OnMouseDown");

            Assert.AreEqual(3, gm.turnController.infrastructureValue);
            Assert.AreEqual(1, gm.turnController.moveCount);
            Assert.IsNull(gm.PendingPlacement);
            Assert.AreEqual(piece, board.ActivePiece);
        }

        [Test]
        public void OnMouseDown_BreakToolRemovesPathPieceAndCountsMove()
        {
            var board = CreateGameObject("Path Board").AddComponent<PathBuildBoard>();
            var gm = CreateGameObject("Game Master").AddComponent<GameMaster>();
            var piece = new PathPiecePlaceable("Pipe", "", 1, 2, null, 3);
            var targetCell = GetCell(board, 1, 1);

            gm.turnController.currentPhase = GamePhase.Card;
            board.SetActivePiece(piece);
            targetCell.SendMessage("OnMouseDown");

            Assert.AreEqual(1, board.PlacedPieces.Count);
            Assert.IsTrue(board.IsOccupied(new Vector2Int(1, 1)));
            Assert.IsTrue(board.IsOccupied(new Vector2Int(2, 1)));

            board.SetActiveBreakTool();
            targetCell.SendMessage("OnMouseDown");

            Assert.AreEqual(0, board.PlacedPieces.Count);
            Assert.IsFalse(board.IsOccupied(new Vector2Int(1, 1)));
            Assert.IsFalse(board.IsOccupied(new Vector2Int(2, 1)));
            Assert.AreEqual(2, gm.turnController.moveCount);
            Assert.AreEqual(0, gm.turnController.infrastructureValue);
        }

        [Test]
        public void PointerUi_InputGuard_IgnoresPassiveGraphicsButBlocksButtons()
        {
            var passiveGraphic = CreateGameObject("Passive UI Graphic");
            passiveGraphic.AddComponent<Image>();

            var button = CreateGameObject("Interactive UI Button");
            button.AddComponent<Button>();

            var guard = typeof(PointerUi).GetMethod("HandlesPointerInput",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(guard);
            Assert.IsFalse((bool)guard.Invoke(null, new object[] { passiveGraphic }));
            Assert.IsTrue((bool)guard.Invoke(null, new object[] { button }));
        }

    private PathFixture CreatePathFixture(int lowerColumn = 1, int upperColumn = 1, int lowerRow = -1, int upperRow = 10)
        {
            var board = CreateGameObject("Path Board").AddComponent<PathBuildBoard>();
            var path = CreateGameObject("Waypoint Path").AddComponent<WaypointPath>();
            var lower = CreateGameObject("Lower Endpoint").transform;
            var upper = CreateGameObject("Upper Endpoint").transform;

        lower.position = board.GetPathWaypointPosition(new Vector2Int(lowerColumn, lowerRow));
        upper.position = board.GetPathWaypointPosition(new Vector2Int(upperColumn, upperRow));

            SetField(path, "pathBuildBoard", board);
            SetField(path, "startPoint", lower);
            SetField(path, "endPoint", upper);

            return new PathFixture(board, path, lower, upper);
        }

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

        private PathSplitter AddSplitter(PathFixture fixture, Vector2Int cell)
        {
            var splitter = CreateGameObject($"Path Splitter {cell}").AddComponent<PathSplitter>();
            splitter.transform.position = fixture.Board.GetCellTopPosition(cell);
            return splitter;
        }

        private static Vector3 CellTop(PathFixture fixture, int column, int row)
        {
            return fixture.Board.GetCellTopPosition(new Vector2Int(column, row));
        }

        private static void AssertBranchPreviewsShowing(PathFixture fixture, int count)
        {
            fixture.Path.RefreshLivePreview();
            for (var i = 0; i < count; i++)
            {
                var name = i == 0 ? "Alternate Path Preview" : $"Alternate Path Preview {i + 1}";
                var tube = fixture.Board.transform.Find(name)?.GetComponentInChildren<PathWaterTube>();
                Assert.IsNotNull(tube, name);
                Assert.IsTrue(tube.IsShowing, name);
                Assert.Greater(tube.PointCount, 2, name);
            }

            var extra = fixture.Board.transform.Find($"Alternate Path Preview {count + 1}")
                ?.GetComponentInChildren<PathWaterTube>();
            Assert.IsTrue(!extra || !extra.IsShowing, "No preview beyond one per distinct branch.");
        }

        private PathFixture CreateSplitPathFixture()
        {
            var fixture = CreatePathFixture();

            // Shared start, short center route, shared end.
            PlaceVertical(fixture.Board, 1, 0, 3);
            PlaceVertical(fixture.Board, 1, 3, 5);
            PlaceVertical(fixture.Board, 1, 8, 2);

            // Longer right-hand option from the fork at (1,2), rejoining at (1,8).
            PlaceHorizontal(fixture.Board, 2, 2, 2);
            PlaceVertical(fixture.Board, 3, 3, 5);
            PlaceHorizontal(fixture.Board, 2, 8, 2);

            return fixture;
        }

        private TurnFixture CreateTurnFixture(bool validPath)
        {
            var pathFixture = validPath ? CreatePathFixture() : CreatePathFixture(0);
            PlaceVertical(pathFixture.Board, 1, 0, 10);

            var gm = CreateGameObject("Game Master").AddComponent<GameMaster>();
            var turnController = gm.GetComponent<TurnController>();
            turnController.enabled = false;
            var placementInventory = gm.GetComponent<PlacementInventory>();
            var popManager = gm.gameObject.AddComponent<PopulationManager>();
            var interfaceManager = CreateInterfaceManager();
            var mainCamera = CreateGameObject("Main Camera").AddComponent<Camera>();
            var topDownCamera = CreateGameObject("Top Camera").AddComponent<Camera>();
            var spawner = CreateGameObject("Spawner").AddComponent<EntitySpawner>();

            mainCamera.gameObject.SetActive(true);
            topDownCamera.gameObject.SetActive(false);

            SetField(spawner, "path", pathFixture.Path);
            SetField(spawner, "spawnPoint", CreateGameObject("Spawn Point").transform);
            spawner.spawnInterval = 1000f;

            gm.turnController = turnController;
            gm.placementInventory = placementInventory;
            gm.popManager = popManager;
            gm.interfaceManager = interfaceManager;
            gm.cameraController = gm.gameObject.AddComponent<CameraController>();
            SetField(gm.cameraController, "mainCamera", mainCamera);
            SetField(gm.cameraController, "secondaryCamera", topDownCamera);
            gm.pathBuildBoard = pathFixture.Board;
            gm.entitySpawners = new List<EntitySpawner> { spawner };

            SetField(turnController, "_gm", gm);
            turnController.currentPhase = GamePhase.Card;
            turnController.waveDuration = 1000f;

            return new TurnFixture(gm, turnController, spawner, pathFixture.Path);
        }

        private InterfaceManager CreateInterfaceManager()
        {
            var manager = CreateGameObject("Interface Manager").AddComponent<InterfaceManager>();
            SetField(manager, "quitButton", CreateUiObject<Button>("Quit Button"));
            SetField(manager, "nextButton", CreateUiObject<Button>("Next Button"));
            SetField(manager, "openShopButton", CreateUiObject<Button>("Open Shop Button"));
            SetField(manager, "closeShopButton", CreateUiObject<Button>("Close Shop Button"));
            SetField(manager, "mTowerUpgrades", CreateUiObject<Image>("Middle Tower Upgrades"));
            SetField(manager, "rTowerUpgrades", CreateUiObject<Image>("Right Tower Upgrades"));
            SetField(manager, "lTowerUpgrades", CreateUiObject<Image>("Left Tower Upgrades"));
            SetField(manager, "handContainer", CreateGameObject("Hand Container").transform);
            return manager;
        }

        private T CreateUiObject<T>(string name) where T : Component
        {
            return CreateGameObject(name).AddComponent<T>();
        }

        private void PlaceVertical(PathBuildBoard board, int column, int row, int length)
        {
            var piece = new PathPiecePlaceable("Pipe", "", 1, length, null,0);
            piece.ToggleOrientation();
            Assert.IsTrue(GetCell(board, column, row).TryPlace(piece));
        }

        private void PlaceHorizontal(PathBuildBoard board, int column, int row, int length)
        {
            var piece = new PathPiecePlaceable("Pipe", "", 1, length, null, 0);
            Assert.IsTrue(GetCell(board, column, row).TryPlace(piece));
        }

        private static void AssertFaces(Quaternion rotation, Vector3 expectedDirection)
        {
            Assert.Greater(Vector3.Dot(rotation * Vector3.forward, expectedDirection.normalized), 0.99f);
        }

        private PathBuildCell GetCell(PathBuildBoard board, int column, int row)
        {
            var child = board.transform.Find($"Path Cell {column},{row}");
            Assert.IsTrue(child, $"Missing generated cell {column},{row}");
            return child.GetComponent<PathBuildCell>();
        }

        private GameObject CreateGameObject(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        private GameObject CreatePrimitive(string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            _created.Add(go);
            return go;
        }

        private static void AdvanceIssueOneWaypoint(IssueObject issue, PathBuildBoard board)
        {
            var index = issue.GetWaypointIndex();
            var target = issue.GetPath().GetPosition(issue.GetRouteIndex(), index);
            target.y += issue.transform.localScale.y * board.entityOnBoardHeight;
            issue.transform.position = target;
            issue.SendMessage("Update", SendMessageOptions.RequireReceiver);
            Assert.AreEqual(index + 1, issue.GetWaypointIndex());
        }

        private static void SetField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field {fieldName} on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static T GetField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field {fieldName} on {target.GetType().Name}");
            return (T)field.GetValue(target);
        }

        private readonly struct PathFixture
        {
            public PathFixture(PathBuildBoard board, WaypointPath path, Transform lower, Transform upper)
            {
                Board = board;
                Path = path;
                Lower = lower;
                Upper = upper;
            }

            public PathBuildBoard Board { get; }
            public WaypointPath Path { get; }
            public Transform Lower { get; }
            public Transform Upper { get; }
        }

        private readonly struct TurnFixture
        {
            public TurnFixture(GameMaster gameMaster, TurnController turnController, EntitySpawner spawner,
                WaypointPath path)
            {
                GameMaster = gameMaster;
                TurnController = turnController;
                Spawner = spawner;
                Path = path;
            }

            public GameMaster GameMaster { get; }
            public TurnController TurnController { get; }
            public EntitySpawner Spawner { get; }
            public WaypointPath Path { get; }
        }
    }
}
