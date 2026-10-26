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
        public void Rebuild_EndsASplitterBranchAtAnUnlockedReceptacle()
        {
            var fixture = CreateReceptacleFixture(out var receptacle);

            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.IsNull(fixture.Path.GetRouteReceptacle(0), "The main route always runs to the lake.");
            Assert.IsTrue(fixture.Path.TryGetBranchRoute(0, CellTop(fixture, 1, 4), out var branch));
            Assert.AreSame(receptacle, fixture.Path.GetRouteReceptacle(branch));

            // Flow runs level out of the last pipe into the receptacle.
            var lastWaypoint = fixture.Path.GetWaypointCount(branch) - 1;
            var end = fixture.Path.GetPosition(branch, lastWaypoint);
            var lastPipe = fixture.Path.GetPosition(branch, lastWaypoint - 1);
            Assert.AreEqual(receptacle.transform.position.x, end.x, 0.001f);
            Assert.AreEqual(receptacle.transform.position.z, end.z, 0.001f);
            Assert.AreEqual(lastPipe.y, end.y, 0.001f);
        }

        [Test]
        public void Rebuild_IgnoresALockedReceptacle()
        {
            var fixture = CreateReceptacleFixture(out var receptacle);
            SetField(receptacle, "unlockLevel", 3);
            receptacle.SendMessage("ApplyLevel", 2);
            Assert.IsFalse(receptacle.IsUnlocked);

            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.IsFalse(fixture.Path.TryGetBranchRoute(0, CellTop(fixture, 1, 4), out _),
                "A dead-end spur to a locked receptacle is no branch.");
        }

        [Test]
        public void ReceptacleBranch_LastCellFlowsIntoTheReceptacle_NotTowardTheLake()
        {
            var fixture = CreateReceptacleFixture(out _);
            Assert.IsTrue(fixture.Path.Rebuild());
            var lastCell = new Vector2Int(fixture.Board.Columns - 1, 4);
            var offsets = new List<Vector3>();

            Assert.IsTrue(fixture.Path.CollectFlowOffsets(fixture.Board.GetCellTopPosition(lastCell), offsets));
            Assert.AreEqual(2, offsets.Count, "In from the left, out into the receptacle.");
            var outward = offsets.OrderByDescending(offset => offset.x).First();
            Assert.Greater(outward.x, 0f, "The last pipe leads on into the receptacle beside it...");
            Assert.AreEqual(0f, outward.z, 0.001f, "...not off toward the lake end.");
        }

        [Test]
        public void MetalPinnedToAReceptacleBranch_IsCollectedThereAndSparesTheLake()
        {
            var fixture = CreateReceptacleFixture(out var receptacle);
            Assert.IsTrue(fixture.Path.Rebuild());
            var splitter = PathSplitter.Live.Single(s => s.transform.position == CellTop(fixture, 1, 4));
            splitter.SetRule(IssueType.Metal, SplitterRule.BranchOnly);
            var gm = CreateGameObject("Game Master").AddComponent<GameMaster>();
            gm.pathBuildBoard = fixture.Board;

            var lake = CreateGameObject("Lake").AddComponent<LakeController>();
            var metal = CreatePrimitive("Metal Issue").AddComponent<IssueObject>();
            metal.SetType(IssueType.Metal);
            metal.SetPath(fixture.Path);
            Assert.IsTrue(splitter.RouteIssue(metal));

            var junk = CreatePrimitive("Junk Issue").AddComponent<IssueObject>();
            junk.SetType(IssueType.Organic);
            junk.SetPath(fixture.Path);
            Assert.IsTrue(junk.TrySetRoute(metal.GetRouteIndex()));

            foreach (var issue in new[] { metal, junk })
            {
                while (issue.GetWaypointIndex() < fixture.Path.GetWaypointCount(issue.GetRouteIndex()))
                    AdvanceIssueOneWaypoint(issue, fixture.Board);
                issue.SendMessage("Update");
            }

            Assert.AreSame(receptacle, metal.EndReceptacle);
            Assert.AreEqual(1, receptacle.DeliveredCount, "Only the metal is collected.");
            Assert.Less(lake.health, 100f, "Waste dumped at the metal receptacle still pollutes the lake.");
            Assert.AreEqual(100f - junk.ProcessCost * 5, lake.health, 0.001f, "The metal itself spares the lake.");
        }

        [Test]
        public void MetalHeadedForAReceptacleThatLocks_PollutesTheLakeInstead()
        {
            var fixture = CreateReceptacleFixture(out var receptacle);
            Assert.IsTrue(fixture.Path.Rebuild());
            var splitter = PathSplitter.Live.Single(s => s.transform.position == CellTop(fixture, 1, 4));
            splitter.SetRule(IssueType.Metal, SplitterRule.BranchOnly);
            var gm = CreateGameObject("Game Master").AddComponent<GameMaster>();
            gm.pathBuildBoard = fixture.Board;

            var lake = CreateGameObject("Lake").AddComponent<LakeController>();
            var metal = CreatePrimitive("Metal Issue").AddComponent<IssueObject>();
            metal.SetType(IssueType.Metal);
            metal.SetPath(fixture.Path);
            Assert.IsTrue(splitter.RouteIssue(metal));

            SetField(receptacle, "unlockLevel", 3);
            receptacle.SendMessage("ApplyLevel", 2);
            Assert.IsFalse(receptacle.IsUnlocked);

            while (metal.GetWaypointIndex() < fixture.Path.GetWaypointCount(metal.GetRouteIndex()))
                AdvanceIssueOneWaypoint(metal, fixture.Board);
            metal.SendMessage("Update");

            Assert.AreEqual(0, receptacle.DeliveredCount, "A locked receptacle collects nothing.");
            Assert.AreEqual(100f - metal.ProcessCost * 5, lake.health, 0.001f);
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
        public void PathSplitter_ThreeOutputsHaveDistinctRoutesAndExactShares()
        {
            var fixture = CreateSplitPathFixture();
            PlaceVertical(fixture.Board, 0, 2, 7);
            var splitter = AddSplitter(fixture, new Vector2Int(1, 2));
            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.IsTrue(fixture.Path.TryGetBranchRoute(0, splitter.transform.position, out var second));
            Assert.IsTrue(fixture.Path.TryGetBranchRoute(0, splitter.transform.position, out var third, 2));
            Assert.AreNotEqual(second, third);
            splitter.SetShares(20, 50);
            var counts = new int[3];
            for (var i = 0; i < 100; i++)
            {
                var issue = CreateGameObject($"Issue {i}").AddComponent<IssueObject>();
                issue.SetPath(fixture.Path);
                Assert.IsTrue(splitter.RouteIssue(issue));
                var route = issue.GetRouteIndex();
                counts[route == 0 ? 0 : route == second ? 1 : 2]++;
                Assert.IsFalse(splitter.RouteIssue(issue), "The same issue is routed only once.");
            }
            CollectionAssert.AreEqual(new[] {20, 30, 50}, counts);
            splitter.SetRule(IssueType.NonWaste, SplitterRule.ThirdOnly);
            Assert.AreEqual(2, splitter.ChooseOutput(IssueType.NonWaste));
            Assert.AreEqual(0, splitter.ChooseOutput(IssueType.NonWaste, false));
        }

        [Test]
        public void PathSplitter_FinishedLoopBesideAnUnfinishedStub_StillGetsTheBranchTraffic()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 10);
            // A dead-end stub to the right, searched first, and a finished loop to the left that rejoins at (1,8).
            PlaceHorizontal(fixture.Board, 2, 2, 1);
            PlaceVertical(fixture.Board, 0, 2, 7);
            var splitter = AddSplitter(fixture, new Vector2Int(1, 2));
            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.IsFalse(fixture.Path.TryGetBranchRoute(0, splitter.transform.position, out _),
                "The stub keeps output 2's number while it's unfinished.");
            Assert.IsTrue(fixture.Path.TryGetBranchRoute(0, splitter.transform.position, out var loop, 2));

            var counts = new int[3];
            for (var i = 0; i < 100; i++)
            {
                var issue = CreateGameObject($"Issue {i}").AddComponent<IssueObject>();
                issue.SetPath(fixture.Path);
                Assert.IsTrue(splitter.RouteIssue(issue));
                counts[issue.GetRouteIndex() == loop ? 2 : issue.GetRouteIndex() == 0 ? 0 : 1]++;
            }

            CollectionAssert.AreEqual(new[]
            {
                splitter.MainSharePercent, 0, splitter.BranchSharePercent + splitter.ThirdSharePercent
            }, counts, "The loop takes the stub's share as well as its own.");
            splitter.SetRule(IssueType.Organic, SplitterRule.BranchOnly);
            Assert.AreEqual(2, splitter.ChooseOutput(IssueType.Organic, true, false));
        }

        [Test]
        public void PathSplitter_ColorsEachOutletLikeTheRouteItFeeds()
        {
            var fixture = CreateSplitPathFixture();
            PlaceVertical(fixture.Board, 0, 2, 7);
            Assert.IsTrue(fixture.Path.Rebuild());

            var cell = new Vector2Int(1, 2);
            var host = CreateGameObject("Path Splitter");
            host.transform.position = CellTop(fixture, cell.x, cell.y);
            var stubs = new Dictionary<Vector2Int, Renderer>();
            foreach (var side in new[] { Vector2Int.down, Vector2Int.up, Vector2Int.right, Vector2Int.left })
            {
                var stub = CreatePrimitive($"Model_Pipe {side}");
                stub.transform.SetParent(host.transform, false);
                stub.transform.position = Vector3.Lerp(host.transform.position,
                    CellTop(fixture, cell.x + side.x, cell.y + side.y), 0.4f);
                stubs[cell + side] = stub.GetComponent<Renderer>();
            }

            host.AddComponent<PathSplitter>();
            Assert.IsTrue(fixture.Path.Rebuild());

            var block = new MaterialPropertyBlock();
            var expected = new MaterialPropertyBlock();
            for (var output = 0; output < 3; output++)
            {
                var route = 0;
                if (output > 0)
                    Assert.IsTrue(fixture.Path.TryGetBranchRoute(0, host.transform.position, out route, output));
                var cells = fixture.Path.GetRouteCells(route).ToList();
                var exit = cells[cells.IndexOf(cell) + 1];

                stubs[exit].GetPropertyBlock(block);
                expected.SetColor("_BaseColor", PathSplitter.OutputColor(output));
                Assert.AreEqual(expected.GetColor("_BaseColor"), block.GetColor("_BaseColor"),
                    $"The stub leading down output {output + 1}'s route wears its color.");
            }

            stubs[cell + Vector2Int.down].GetPropertyBlock(block);
            Assert.IsTrue(block.isEmpty, "The intake keeps the model's own look.");
        }

        [Test]
        public void PathSplitter_UnavailableOutputRenormalizesTheOtherShares()
        {
            var splitter = CreateGameObject("Splitter").AddComponent<PathSplitter>();
            splitter.SetShares(20, 60);
            var counts = new int[3];
            for (var i = 0; i < 100; i++) counts[splitter.ChooseOutput(IssueType.Organic, false)]++;
            CollectionAssert.AreEqual(new[] {50, 50, 0}, counts);
            splitter.SetShares(0, 100);
            Assert.AreEqual(0, splitter.ChooseOutput(IssueType.Organic, false));
            splitter.SetShares(90, 90);
            Assert.AreEqual(100, splitter.MainSharePercent + splitter.BranchSharePercent + splitter.ThirdSharePercent);
        }

        [Test]
        public void PathSplitter_SavedSharesOver100_TrimTheThirdOutlet()
        {
            var host = CreateGameObject("Splitter");
            host.SetActive(false);
            var splitter = host.AddComponent<PathSplitter>();
            SetField(splitter, "mainSharePercent", 80);
            SetField(splitter, "thirdSharePercent", 50);

            host.SetActive(true);

            Assert.AreEqual(80, splitter.MainSharePercent);
            Assert.AreEqual(20, splitter.ThirdSharePercent);
            Assert.AreEqual(0, splitter.BranchSharePercent, "The branch never goes negative.");
        }

        [Test]
        public void SplitRatioTriangle_CornersAndOutsideDragStayNormalized()
        {
            var triangle = CreateGameObject("Triangle").AddComponent<SplitRatioTriangle>();
            triangle.rectTransform.sizeDelta = new Vector2(240, 180);
            var pick = typeof(SplitRatioTriangle).GetMethod("Pick", BindingFlags.Instance | BindingFlags.NonPublic);
            var rect = triangle.rectTransform.rect;
            pick.Invoke(triangle, new object[] {new Vector2(rect.center.x, rect.yMax - 18)});
            Assert.AreEqual(new Vector3(0, 0, 1), triangle.Shares);
            pick.Invoke(triangle, new object[] {new Vector2(rect.xMin + 18, rect.yMin + 18)});
            Assert.AreEqual(new Vector3(1, 0, 0), triangle.Shares);
            pick.Invoke(triangle, new object[] {new Vector2(999, -999)});
            Assert.AreEqual(new Vector3(0, 1, 0), triangle.Shares);
            pick.Invoke(triangle, new object[] {Vector2.zero});
            Assert.AreEqual(1, triangle.Shares.x + triangle.Shares.y + triangle.Shares.z, .00001f);
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
            var triangle = panel.GetComponentInChildren<SplitRatioTriangle>(true);
            Assert.IsNotNull(triangle);
            Assert.IsNotNull(triangle.GetComponent<CanvasRenderer>(), "Without a CanvasRenderer the triangle never draws.");

            // Without a second branch the panel keeps the two-way slider.
            Assert.AreEqual(1, panel.SliderOutput);
            Assert.IsFalse(triangle.gameObject.activeInHierarchy, "The triangle waits for a second branch.");
            var slider = panel.GetComponentInChildren<Slider>();
            Assert.IsNotNull(slider, "One branch splits with the slider.");
            Assert.IsFalse(window.transform.Find("NON-WASTE Row/THIRD Button").GetComponent<Button>().interactable);
            slider.value = 14;
            Assert.AreEqual(70, splitter.MainSharePercent);
            Assert.AreEqual(30, splitter.BranchSharePercent);
            Assert.AreEqual(0, splitter.ThirdSharePercent);

            window.transform.Find("NON-WASTE Row/THIRD Button").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(SplitterRule.ThirdOnly, splitter.GetRule(IssueType.NonWaste));

            window.transform.Find("NON-WASTE Row/BRANCH Button").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(SplitterRule.BranchOnly, splitter.GetRule(IssueType.NonWaste));
            Assert.AreEqual(SplitterRule.Split, splitter.GetRule(IssueType.Organic));

            window.transform.Find("METAL Row/MAIN Button").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(SplitterRule.MainOnly, splitter.GetRule(IssueType.Metal));

            window.transform.Find("Header/CLOSE Button").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(panel.IsOpen);
            Assert.IsFalse(window.activeSelf);

            // A splitter that goes away takes its window with it.
            panel.Show(splitter);
            splitter.gameObject.SetActive(false);
            panel.SendMessage("Update");
            Assert.IsFalse(window.activeSelf);
        }

        [Test]
        public void PathSplitterPanel_UsesTheTriangleOnlyWhileBothBranchesHavePipe()
        {
            var fixture = CreateSplitPathFixture();
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<PathSplitterPanel>(
                "Assets/_project/Prefabs/UI/PathSplitterPanel.prefab");
            var canvas = CreateGameObject("Canvas").AddComponent<Canvas>();
            var panel = Object.Instantiate(prefab, canvas.transform);
            var thirdButton = panel.transform.Find("Window/NON-WASTE Row/THIRD Button").GetComponent<Button>();
            var splitter = AddSplitter(fixture, new Vector2Int(1, 2));
            Assert.IsTrue(fixture.Path.Rebuild());

            panel.Show(splitter);
            Assert.AreEqual(1, panel.SliderOutput);
            Assert.IsNotNull(panel.GetComponentInChildren<Slider>());
            Assert.IsNull(panel.GetComponentInChildren<SplitRatioTriangle>());
            Assert.IsFalse(thirdButton.interactable);

            // Laying the second branch while the window is open swaps in the triangle.
            PlaceVertical(fixture.Board, 0, 2, 7);
            Assert.IsTrue(fixture.Path.Rebuild());
            panel.SendMessage("Update");

            Assert.AreEqual(-1, panel.SliderOutput);
            Assert.IsNotNull(panel.GetComponentInChildren<SplitRatioTriangle>());
            Assert.IsNull(panel.GetComponentInChildren<Slider>());
            Assert.IsTrue(thirdButton.interactable);
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

        [Test]
        public void IsolatedPipe_BesideTheMainLine_JoinsOnlyThroughASplitter()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 10);
            var lastColumn = fixture.Board.Columns - 1;
            // Isolated pipe hugging the main line for four cells, then off to the right edge.
            PlaceVertical(fixture.Board, 2, 0, 4, true);
            PlaceHorizontal(fixture.Board, 2, 4, lastColumn - 1, true);
            var receptacle = AddReceptacle(fixture, 4);

            Assert.IsTrue(fixture.Board.HasPipe(new Vector2Int(2, 2), true));
            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.IsFalse(fixture.Path.HasAlternateRoute, "Side by side, isolated pipe never joins the main line.");

            var splitter = AddSplitter(fixture, new Vector2Int(1, 4));
            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.IsTrue(fixture.Path.TryGetBranchRoute(0, CellTop(fixture, 1, 4), out var branch),
                "A splitter feeds the isolated line.");
            Assert.AreSame(receptacle, fixture.Path.GetRouteReceptacle(branch));
            Assert.IsFalse(fixture.Path.GetRouteCells(0).Any(cell => cell.x == 2),
                "The main route stays on ordinary pipe.");

            // The tube's last cell opens toward the receptacle and runs on into it.
            var tubeEnd = fixture.Board.transform.Find("PipeVisuals").GetComponentsInChildren<Renderer>()
                .Where(part => part.transform.parent.name == "Isolated Pipe")
                .Max(part => part.bounds.max.x);
            var lastCellCenter = fixture.Board.GetCellTopPosition(new Vector2Int(lastColumn, 4)).x;
            Assert.Greater(tubeEnd, lastCellCenter + fixture.Board.CellWorldPitch.x * 0.75f);

            splitter.SetRule(IssueType.Metal, SplitterRule.BranchOnly);
            var metal = CreatePrimitive("Metal Issue").AddComponent<IssueObject>();
            metal.SetType(IssueType.Metal);
            metal.SetPath(fixture.Path);
            Assert.IsTrue(splitter.RouteIssue(metal));
            Assert.AreEqual(branch, metal.GetRouteIndex());
        }

        [Test]
        public void IsolatedPipe_CrossesRegularPipe_AtRightAnglesWithoutJoiningIt()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 10);
            var lastColumn = fixture.Board.Columns - 1;
            // A stray regular pipe up column 4, and isolated pipe from the splitter across it to the edge.
            PlaceVertical(fixture.Board, 4, 0, 6);
            PlaceHorizontal(fixture.Board, 2, 2, lastColumn - 1, true);
            var crossing = new Vector2Int(4, 2);
            var receptacle = AddReceptacle(fixture, 2);
            AddSplitter(fixture, new Vector2Int(1, 2));

            Assert.IsTrue(fixture.Board.IsPipeCrossing(crossing));
            Assert.IsNull(fixture.Board.TryPlace(GetCell(fixture.Board, 4, 3), Pipe(2, true, isolated: true)),
                "Isolated pipe can't run along regular pipe in the same cells.");
            Assert.IsNull(fixture.Board.TryPlace(GetCell(fixture.Board, 6, 2), Pipe(2, false)),
                "Nor regular pipe along isolated pipe.");

            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.IsTrue(fixture.Path.TryGetBranchRoute(0, CellTop(fixture, 1, 2), out var branch));
            Assert.AreSame(receptacle, fixture.Path.GetRouteReceptacle(branch));
            var branchCells = fixture.Path.GetRouteCells(branch).ToList();
            CollectionAssert.Contains(branchCells, crossing);
            CollectionAssert.DoesNotContain(branchCells, new Vector2Int(4, 1), "The branch passes over the stray pipe.");
            CollectionAssert.DoesNotContain(branchCells, new Vector2Int(4, 3));

            // Issues ride through the isolated tube, which runs above the regular channel.
            var crossingWaypoint = fixture.Path.GetPosition(branch, branchCells.IndexOf(crossing) + 1);
            Assert.Less(Vector3.Distance(fixture.Board.GetPathWaypointPosition(crossing, PathKind.Pipe, true),
                crossingWaypoint), 0.001f);
            Assert.Greater(crossingWaypoint.y, fixture.Board.GetPathWaypointPosition(crossing).y);
        }

        [Test]
        public void PathSplitterShopItem_RefusesAPipeCrossing()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 4, 0, 6);
            PlaceHorizontal(fixture.Board, 2, 2, 4, true);
            var gm = CreateGameObject("Game Master").AddComponent<GameMaster>();
            gm.pathBuildBoard = fixture.Board;
            var prefab = CreateGameObject("Path Splitter Prefab");
            prefab.AddComponent<PathSplitter>();
            var item = new PathSplitterShopItem("Path Splitter", string.Empty, 1, prefab, null, 1);

            Assert.IsTrue(fixture.Board.IsPipeCrossing(new Vector2Int(4, 2)));
            Assert.IsNull(item.Place(GetCell(fixture.Board, 4, 2).transform),
                "A splitter on a crossing would catch both lines at once.");
            Assert.IsNotNull(Track(item.Place(GetCell(fixture.Board, 4, 4).transform)));
        }

        [Test]
        public void IsolatedPipe_DeadEndingIntoRegularPipe_DrainsIntoIt()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 10);
            // Out of the splitter at (1,2), up column 3, and back left to end head-on at the main line.
            PlaceHorizontal(fixture.Board, 2, 2, 2, true);
            PlaceVertical(fixture.Board, 3, 3, 3, true);
            PlaceHorizontal(fixture.Board, 2, 6, 2, true);
            AddSplitter(fixture, new Vector2Int(1, 2));
            Assert.IsFalse(fixture.Board.DrainsInto(new Vector2Int(2, 6), new Vector2Int(1, 6)),
                "A drain end starts closed.");

            // The player opens it by clicking its valve.
            FindValve(fixture, new Vector2Int(2, 6)).OnPointerClick(null);

            Assert.IsTrue(fixture.Board.DrainsInto(new Vector2Int(2, 6), new Vector2Int(1, 6)));
            Assert.IsFalse(fixture.Board.CanDrainInto(new Vector2Int(3, 4), new Vector2Int(2, 4)),
                "Mid-run isolated pipe never drains.");
            Assert.IsFalse(fixture.Board.CanDrainInto(new Vector2Int(2, 2), new Vector2Int(1, 2)),
                "Pipe leaving a splitter gets no valve.");

            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.IsTrue(fixture.Path.TryGetBranchRoute(0, CellTop(fixture, 1, 2), out var branch));
            var cells = fixture.Path.GetRouteCells(branch).ToList();
            var drain = cells.IndexOf(new Vector2Int(2, 6));
            Assert.Greater(drain, 0, "The branch runs through the isolated pipe.");
            Assert.AreEqual(new Vector2Int(1, 6), cells[drain + 1], "It empties into the main line...");
            Assert.AreEqual(new Vector2Int(1, 9), cells[^1], "...and rides it on to the end.");
            Assert.IsFalse(fixture.Path.GetRouteCells(0).Any(cell => cell.x > 1),
                "Nothing flows from the main line back up into isolated pipe.");

            AddSplitter(fixture, new Vector2Int(2, 6));
            Assert.IsFalse(fixture.Board.CanDrainInto(new Vector2Int(2, 6), new Vector2Int(1, 6)),
                "Nor does pipe ending in a splitter, which already joins the lines.");
        }

        [Test]
        public void DrainValve_StartsClosed_AndClickingTogglesTheRouteAndItsLook()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 10);
            PlaceHorizontal(fixture.Board, 2, 2, 2, true);
            PlaceVertical(fixture.Board, 3, 3, 3, true);
            PlaceHorizontal(fixture.Board, 2, 6, 2, true);
            // Settling the splitter redraws the pipe it lands beside on the board's next update.
            AddSplitter(fixture, new Vector2Int(1, 2));
            fixture.Board.RedrawStaleIsolatedPipe();
            var end = new Vector2Int(2, 6);
            Assert.AreEqual(1, fixture.Board.GetComponentsInChildren<DrainValve>().Length,
                "Only the end facing the main line gets a valve, not the one leaving the splitter.");

            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.IsFalse(fixture.Path.TryGetBranchRoute(0, CellTop(fixture, 1, 2), out _),
                "A closed drain leaves the isolated pipe going nowhere.");
            var valve = FindValve(fixture, end);
            Assert.IsFalse(valve.IsOpen);
            Assert.IsNull(valve.transform.parent.Find("Splash"), "Nothing pours out of a closed drain.");

            valve.OnPointerClick(null);
            valve = FindValve(fixture, end);
            Assert.IsTrue(valve.IsOpen);
            Assert.IsNotNull(valve.transform.parent.Find("Splash"), "An open drain splashes into the channel.");
            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.IsTrue(fixture.Path.TryGetBranchRoute(0, CellTop(fixture, 1, 2), out _),
                "The next run's routes go through the open drain.");

            valve.OnPointerClick(null);
            Assert.IsFalse(fixture.Board.IsDrainOpen(end), "A second click closes it again.");

            // Lifting the pipe forgets the valve, so pipe laid there again starts closed.
            fixture.Board.SetDrainOpen(end, true);
            Assert.IsTrue(fixture.Board.TryBreak(GetCell(fixture.Board, 2, 6), PathKind.Pipe, out _));
            PlaceHorizontal(fixture.Board, 2, 6, 2, true);
            Assert.IsFalse(fixture.Board.IsDrainOpen(end));
        }

        private static DrainValve FindValve(PathFixture fixture, Vector2Int cell)
        {
            foreach (var valve in fixture.Board.GetComponentsInChildren<DrainValve>())
                if (valve.Cell == cell)
                    return valve;
            Assert.Fail($"No drain valve at {cell}.");
            return null;
        }

        [Test]
        public void IsolatedPipe_EndingBesideRegularPipe_DoesNotDrain()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 10);
            // Isolated pipe whose end lies alongside the main line rather than pointing into it.
            PlaceVertical(fixture.Board, 2, 4, 2, true);

            Assert.IsFalse(fixture.Board.DrainsInto(new Vector2Int(2, 4), new Vector2Int(1, 4)));
            Assert.IsFalse(fixture.Board.DrainsInto(new Vector2Int(2, 5), new Vector2Int(1, 5)));
            Assert.IsEmpty(fixture.Board.GetComponentsInChildren<DrainValve>(), "No valve where it can't drain.");
        }

        [Test]
        public void Remove_AtACrossing_TakesTheIsolatedPipeOnTopFirst()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 4, 0, 6);
            PlaceHorizontal(fixture.Board, 2, 2, 4, true);
            var crossing = new Vector2Int(4, 2);

            Assert.IsTrue(fixture.Board.TryBreak(GetCell(fixture.Board, 4, 2), PathKind.Pipe, out _));
            Assert.IsFalse(fixture.Board.HasPipe(crossing, true));
            Assert.IsTrue(fixture.Board.HasPipe(crossing, false));
            Assert.IsFalse(fixture.Board.HasPipe(new Vector2Int(2, 2), true), "The whole isolated piece goes.");

            Assert.IsTrue(fixture.Board.TryBreak(GetCell(fixture.Board, 4, 2), PathKind.Pipe, out _));
            Assert.IsFalse(fixture.Board.IsOccupied(crossing));
        }

        [Test]
        public void IsolatedPipe_IsDrawnAsASlimMetalTube()
        {
            var fixture = CreatePathFixture();
            PlaceHorizontal(fixture.Board, 2, 2, 3, true);

            var visual = fixture.Board.transform.Find("PipeVisuals").Cast<Transform>()
                .Single(child => child.name.StartsWith("Placed"));
            var tube = visual.Find("Isolated Pipe");
            Assert.IsNotNull(tube);
            Assert.Greater(tube.childCount, 0);
            Assert.IsFalse(visual.Cast<Transform>().Any(child => child.name.StartsWith("Pipe Tile")),
                "No channel tiles under isolated pipe.");

            var bounds = tube.GetComponentsInChildren<Renderer>()
                .Select(part => part.bounds).Aggregate((all, next) =>
                {
                    all.Encapsulate(next);
                    return all;
                });
            Assert.Less(bounds.size.z, fixture.Board.CellWorldPitch.y * 0.6f, "Thinner than a regular pipe's cell.");
        }

        [Test]
        public void IssueInIsolatedPipe_IsShownAsABulgeInThePipe()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 10);
            PlaceHorizontal(fixture.Board, 2, 4, fixture.Board.Columns - 2, true);
            AddReceptacle(fixture, 4);
            var splitter = AddSplitter(fixture, new Vector2Int(1, 4));
            Assert.IsTrue(fixture.Path.Rebuild());
            splitter.SetRule(IssueType.Metal, SplitterRule.BranchOnly);
            var gm = CreateGameObject("Game Master").AddComponent<GameMaster>();
            gm.pathBuildBoard = fixture.Board;

            var metal = CreateIssue("Metal Issue", fixture.Path);
            metal.SetType(IssueType.Metal);
            var rider = metal.GetComponent<IsolatedPipeRider>();
            var model = metal.GetComponent<Renderer>();
            Assert.IsNotNull(rider, "Every issue can ride isolated pipe.");

            // Up the main line to the splitter: the model shows.
            while (metal.GetWaypointIndex() <= 5) AdvanceIssueOneWaypoint(metal, fixture.Board);
            rider.SendMessage("LateUpdate");
            Assert.IsFalse(rider.InsideIsolatedPipe);
            Assert.IsTrue(model.enabled);

            // Sent into the tube: the model hides and a bulge rides the pipe in its place.
            Assert.IsTrue(splitter.RouteIssue(metal));
            AdvanceIssueOneWaypoint(metal, fixture.Board);
            rider.SendMessage("LateUpdate");
            Assert.IsTrue(rider.InsideIsolatedPipe);
            Assert.IsFalse(model.enabled);
            var bulge = GameObject.Find("Pipe Bulge");
            Assert.IsNotNull(bulge);
            var tubeCenter = fixture.Path.GetPosition(metal.GetRouteIndex(), metal.GetWaypointIndex());
            Assert.AreEqual(tubeCenter.y, bulge.transform.position.y, 0.001f, "The bulge sits in the tube.");

            // Back on ordinary pipe, the model returns and the bulge goes.
            metal.TrySetRoute(0);
            rider.SendMessage("LateUpdate");
            Assert.IsFalse(rider.InsideIsolatedPipe);
            Assert.IsTrue(model.enabled);
            Assert.IsFalse(bulge.activeSelf);
            Object.Destroy(bulge);
        }

        private Receptacle AddReceptacle(PathFixture fixture, int row)
        {
            var receptacle = CreateGameObject("Metal Receptacle").AddComponent<Receptacle>();
            receptacle.transform.position =
                fixture.Board.GetPathWaypointPosition(new Vector2Int(fixture.Board.Columns, row));
            SetField(receptacle, "unlockLevel", 1);
            SetField(fixture.Path, "receptacles", new[] { receptacle });
            return receptacle;
        }

        /// <summary>
        ///     A straight main route up column 1 with a splitter at (1,4) whose right-hand spur runs
        ///     to the board's right edge, where a metal receptacle sits just outside it.
        /// </summary>
        private PathFixture CreateReceptacleFixture(out Receptacle receptacle)
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 10);
            var lastColumn = fixture.Board.Columns - 1;
            PlaceHorizontal(fixture.Board, 2, 4, lastColumn - 1);
            AddSplitter(fixture, new Vector2Int(1, 4));
            receptacle = AddReceptacle(fixture, 4);
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
