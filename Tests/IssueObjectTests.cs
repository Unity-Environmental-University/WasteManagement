using System.Collections;
using System.Collections.Generic;
using System.Linq;
using _project.Scripts.Core;
using _project.Scripts.Object_Scripts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace _project.Scripts.Tests
{
    /// <summary>Issue movement through route changes while held, merging, and pipe blockages.</summary>
    public class IssueObjectTests : PathTestBase
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [TestCase(true)]
        [TestCase(false)]
        public void HeldIssue_ResumesAtItsCell_AfterRouteIsShortened(bool flush)
        {
            var fixture = CreateDetourPathFixture(shortcut: false);
            Assert.AreEqual(16, fixture.Path.Count);

            var issue = CreateIssue("Held Junk", fixture.Path);
            issue.SetType(IssueType.NonWaste);
            issue.transform.position = fixture.Board.GetPathWaypointPosition(new Vector2Int(1, 7));
            SetField(issue, "_waypointIndex", fixture.Path.FindClosestWaypointIndex(0, issue.transform.position));
            issue.SetHeldBySifter(true);
            var unheldCount = IssueObject.ActiveUnheldCount;

            PlaceVertical(fixture.Board, 1, 2, 3);
            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.AreEqual(12, fixture.Path.Count);
            Assert.GreaterOrEqual(issue.GetWaypointIndex(), fixture.Path.Count);

            Release(issue, flush);

            Assert.AreEqual(8, issue.GetWaypointIndex());
            Assert.Less(Vector3.SqrMagnitude(issue.transform.position -
                fixture.Path.GetPosition(issue.GetWaypointIndex())), 0.0001f);
            Assert.Less(Vector3.SqrMagnitude(fixture.Board.GetPathWaypointPosition(new Vector2Int(1, 8)) -
                fixture.Path.GetPosition(issue.GetWaypointIndex() + 1)), 0.0001f);
            Assert.AreEqual(unheldCount + 1, IssueObject.ActiveUnheldCount);
        }

        [Test]
        public void HeldIssue_RebasesTemporarySpeedDuration_WhenRouteChanges(
            [Values] bool shorten, [Values] bool flush, [Values(1f, 4f)] float speed)
        {
            var fixture = CreateDetourPathFixture(shortcut: !shorten);
            Assert.AreEqual(shorten ? 16 : 12, fixture.Path.Count);

            CreateGameObject("Game Master").AddComponent<GameMaster>().pathBuildBoard = fixture.Board;
            var issue = CreateIssue("Held Junk", fixture.Path);
            issue.SetType(IssueType.NonWaste);
            issue.SetSize(2);
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

            Release(issue, flush);

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
            var issue = CreateIssue("Held Branch Junk", fixture.Path);
            issue.SetType(IssueType.NonWaste);
            Assert.IsTrue(issue.TrySetRoute(1));
            issue.transform.position = fixture.Board.GetPathWaypointPosition(new Vector2Int(1, 8));
            SetField(issue, "_waypointIndex", fixture.Path.FindClosestWaypointIndex(1, issue.transform.position));
            issue.SetHeldBySifter(true);

            Assert.IsTrue(fixture.Board.TryBreak(GetCell(fixture.Board, 3, 3), out _));
            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.IsFalse(fixture.Path.HasAlternateRoute);

            Release(issue, flush);

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
            var issue = CreateIssue("Held Junk", fixture.Path);
            issue.transform.position = fixture.Board.GetPathWaypointPosition(new Vector2Int(1, 7));
            SetField(issue, "_waypointIndex", 9);
            issue.SetHeldBySifter(true);

            Assert.IsTrue(fixture.Path.Rebuild());
            issue.FlushFromSifter();

            Assert.AreEqual(9, issue.GetWaypointIndex(), "Releasing should not send it backward to the nearest cell.");
        }

        [UnityTest]
        public IEnumerator Issue_RidesOverAFullCesspit_ThenDropsBackOntoThePipe()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 10);
            Assert.IsTrue(fixture.Path.Rebuild());
            fixture.Board.entityOnBoardHeight = 0.5f;
            CreateGameObject("Game Master").AddComponent<GameMaster>().pathBuildBoard = fixture.Board;

            var pitPosition = fixture.Board.GetPathWaypointPosition(new Vector2Int(1, 5));
            var pit = CreateGameObject("Full Cesspit").AddComponent<Cesspit>();
            pit.transform.position = pitPosition;
            pit.gameObject.AddComponent<SphereCollider>().radius = fixture.Board.CellWorldPitch.x * 0.75f;
            pit.maxFullness = pit.fullness = 10f;

            var issue = CreateIssue("Passing Issue", fixture.Path);
            issue.tag = "IssueObject";
            issue.SetSize(1);
            issue.SetMoveSpeed(fixture.Board.CellWorldPitch.x * 2f);
            issue.transform.position = fixture.Board.GetPathWaypointPosition(new Vector2Int(1, 2));
            var pipeY = pitPosition.y + issue.transform.localScale.y * 0.5f;

            var highest = float.MinValue;
            var pastPit = pitPosition.z + fixture.Board.CellWorldPitch.x * 2f;
            for (var frame = 0; frame < 600 && issue && issue.transform.position.z < pastPit; frame++)
            {
                highest = Mathf.Max(highest, issue.transform.position.y);
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(issue, "The issue should still be travelling once it has passed the pit.");
            Assert.Greater(issue.transform.position.z, pitPosition.z);
            Assert.AreEqual(10f, pit.fullness, 0.0001f, "A full pit takes nothing in.");
            Assert.Greater(highest, pipeY + 0.3f, "The issue should climb over the heap.");
            Assert.AreEqual(pipeY, issue.transform.position.y, 0.05f, "It should drop back onto the pipe afterward.");
        }

        [Test]
        public void IssueVisualOverride_PersistsAfterProcessingChangesSize()
        {
            var issue = CreatePrimitive("Runaway Issue").AddComponent<IssueObject>();
            // One model per size tier, so shrinking swaps in a model that was never tinted.
            var tiers = new[] { "Small", "Medium", "Big" }
                .Select(name => CreatePrimitive(name, issue.transform)).ToArray();
            SetField(issue, "sizeVisuals", tiers);
            var runawayColor = new Color(1f, 0.45f, 0f);

            issue.SetSize(3);
            Assert.IsTrue(tiers[2].activeSelf);
            issue.SetVisualOverride(runawayColor);
            issue.Process(1, "Test Process");

            Assert.IsTrue(tiers[1].activeSelf, "Shrinking to size 2 shows the medium model.");
            Assert.IsFalse(tiers[2].activeSelf);
            // The override tint rides in a MaterialPropertyBlock (no per-renderer material
            // instance), so read it back from the block rather than .material.color.
            var propertyBlock = new MaterialPropertyBlock();
            tiers[1].GetComponent<Renderer>().GetPropertyBlock(propertyBlock);
            Assert.AreEqual(runawayColor, propertyBlock.GetColor(ColorId),
                "The model swapped in must wear the override too.");
        }

        [UnityTest]
        public IEnumerator IssueObject_OnPathCollision_MergesIntoNextSizeStage()
        {
            // The merged issue stays below pipeBlockSize, so it keeps moving after the merge —
            // it needs a real path, or its first Update treats the empty path as "reached the end".
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 10);
            Assert.IsTrue(fixture.Path.Rebuild());
            // Path movement reads the board height through GameMaster.Instance; without one here
            // the test would lean on whatever instance an earlier test left behind.
            CreateGameObject("Game Master").AddComponent<GameMaster>().pathBuildBoard = fixture.Board;

            var issueA = CreateIssue("Issue A", fixture.Path);
            var issueB = CreateIssue("Issue B", fixture.Path);
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
            var issueA = CreateIssue("Blocking Issue A", fixture.Path);
            var issueB = CreateIssue("Blocking Issue B", fixture.Path);
            var survivorPosition = new Vector3(2f, 0f, 3f);

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
        public IEnumerator Click_OnlyCountsForBlockagesAndPoppableIssues_DuringTheRun()
        {
            // The passing issue rides a real pipe so it survives the frame below on its own merits.
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 10);
            Assert.IsTrue(fixture.Path.Rebuild());
            var gm = CreateGameObject("Game Master").AddComponent<GameMaster>();
            gm.pathBuildBoard = fixture.Board;
            // Only the phase matters here; a live turn controller would start running the wave.
            gm.turnController.enabled = false;
            gm.turnController.currentPhase = GamePhase.Tower;
            var passing = CreateIssue("Passing Issue", fixture.Path);
            passing.SetSize(1);
            var blockage = CreatePrimitive("Blocking Issue").AddComponent<IssueObject>();
            blockage.SetSize(4);

            Assert.IsFalse(passing.IsClickable, "Waste flowing past leaves the click for what's behind it.");
            Assert.IsFalse(passing.TryHandleClick());
            // Destroy is deferred to the end of the frame, so only a frame later can show it wasn't popped.
            yield return null;
            Assert.IsTrue(passing, "An unusable click leaves the issue alone.");
            Assert.IsTrue(blockage.IsClickable);

            passing.EnableClickPop();
            Assert.IsTrue(passing.IsClickable);

            gm.turnController.currentPhase = GamePhase.Card;
            Assert.IsFalse(blockage.IsClickable, "Issues only take clicks during the run.");
        }

        [UnityTest]
        public IEnumerator IssueObject_BlockageBurstsAfterDeadline_WhenNotBrokenDown()
        {
            var issue = CreatePrimitive("Timed Blocking Issue").AddComponent<IssueObject>();
            // 0.1 s is the shortest deadline Update honors; the default is several seconds.
            SetField(issue, "blockedBurstDelay", 0.1f);
            issue.SetSize(4);

            Assert.IsTrue(issue.IsBlockingPipe);

            // Poll instead of waiting exactly the deadline, so slow frames can't make it flaky.
            var giveUpAt = Time.realtimeSinceStartup + 2f;
            while (issue && Time.realtimeSinceStartup < giveUpAt)
                yield return null;

            Assert.IsFalse(issue, "An uncleared pipe blockage should burst and destroy itself after its deadline.");
        }

        [UnityTest]
        public IEnumerator IssueObject_DirectDestinationCollision_DoesNotMerge()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 10);
            Assert.IsTrue(fixture.Path.Rebuild());

            CreateGameObject("Game Master").AddComponent<GameMaster>().pathBuildBoard = fixture.Board;

            var pathIssue = CreateIssue("Path Issue", fixture.Path);
            var runawayIssue = CreateIssue("Runaway Issue", fixture.Path);
            runawayIssue.SetDirectDestination(Vector3.forward * 100f);
            // SetDirectDestination drops the path, and a path mismatch alone already blocks a merge.
            // Put it back so only the direct-destination rule stands between the two.
            SetField(runawayIssue, "path", fixture.Path);
            pathIssue.SetSize(1);
            runawayIssue.SetSize(1);

            // A merge deactivates the absorbed issue at once, so check before colliding the other way.
            pathIssue.SendMessage("OnTriggerEnter", runawayIssue.GetComponent<Collider>());
            Assert.IsTrue(runawayIssue.gameObject.activeSelf, "The path issue absorbed the runaway.");
            runawayIssue.SendMessage("OnTriggerEnter", pathIssue.GetComponent<Collider>());
            Assert.IsTrue(pathIssue.gameObject.activeSelf, "The runaway absorbed the path issue.");
            yield return null;

            Assert.IsTrue(pathIssue);
            Assert.IsTrue(runawayIssue);
            Assert.AreEqual(1f, pathIssue.ProcessCost);
            Assert.AreEqual(1f, runawayIssue.ProcessCost);
        }

        [UnityTest]
        public IEnumerator MetalIssues_NeverMergeAndKeepTheirFixedSize()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 10);
            Assert.IsTrue(fixture.Path.Rebuild());
            CreateGameObject("Game Master").AddComponent<GameMaster>().pathBuildBoard = fixture.Board;

            var metal = CreateIssue("Metal Issue", fixture.Path);
            metal.SetType(IssueType.Metal);
            var otherMetal = CreateIssue("Other Metal Issue", fixture.Path);
            otherMetal.SetType(IssueType.Metal);
            var waste = CreateIssue("Waste Issue", fixture.Path);
            waste.SetType(IssueType.Organic);
            waste.SetSize(1);
            var metalSize = metal.ProcessCost;
            var wasteSize = waste.ProcessCost;

            // The same collisions that merge two waste issues (see MergesIntoNextSizeStage).
            metal.SendMessage("OnTriggerEnter", waste.GetComponent<Collider>());
            waste.SendMessage("OnTriggerEnter", metal.GetComponent<Collider>());
            metal.SendMessage("OnTriggerEnter", otherMetal.GetComponent<Collider>());
            yield return null;

            Assert.IsTrue(metal);
            Assert.IsTrue(otherMetal, "Metal doesn't clump with metal either.");
            Assert.IsTrue(waste);
            Assert.AreEqual(metalSize, metal.ProcessCost);
            Assert.AreEqual(metalSize, otherMetal.ProcessCost);
            Assert.AreEqual(wasteSize, waste.ProcessCost);
            Assert.IsFalse(metal.IsBlockingPipe);
        }

        [Test]
        public void AssignType_MixesMetalIn_OnlyFromLevelThree()
        {
            var gm = CreateGameObject("Game Master").AddComponent<GameMaster>();
            var issue = CreatePrimitive("Issue").AddComponent<IssueObject>();

            int CountMetal(int level)
            {
                gm.turnController.currentLevel = level;
                var metal = 0;
                for (var i = 0; i < 200; i++)
                {
                    issue.AssignType();
                    if (issue.GetIssueType() == IssueType.Metal) metal++;
                }

                return metal;
            }

            Assert.AreEqual(0, CountMetal(2));
            Assert.Greater(CountMetal(3), 0);
        }

        [Test]
        public void NonWaste_ShowsEveryJunkModel_NotJustTheOneForItsPinnedSize()
        {
            var issue = CreatePrimitive("Issue").AddComponent<IssueObject>();
            var junk = new[] { "Boot", "Tire", "Alligator" }
                .Select(name =>
                {
                    var model = CreateGameObject(name);
                    model.transform.SetParent(issue.transform, false);
                    return model;
                }).ToArray();
            SetField(issue, "nonWasteVisuals", junk);

            var seen = new HashSet<GameObject>();
            for (var i = 0; i < 200 && seen.Count < junk.Length; i++)
            {
                issue.AssignType();
                if (issue.GetIssueType() != IssueType.NonWaste) continue;

                var shown = junk.Where(model => model.activeSelf).ToArray();
                Assert.AreEqual(1, shown.Length, "Exactly one junk model shows.");
                seen.Add(shown[0]);
            }

            Assert.AreEqual(junk.Length, seen.Count, "Boot, tire and alligator all turn up.");
        }

        [Test]
        public void SetType_LeavingMetalDropsItsTint_AndJunkShowsAJunkModel()
        {
            var issue = CreatePrimitive("Issue").AddComponent<IssueObject>();
            // With no metal models assigned, metal borrows a junk model, so the tint lands on one of these.
            var junk = new[] { "Boot", "Tire" }.Select(name => CreatePrimitive(name, issue.transform)).ToArray();
            SetField(issue, "nonWasteVisuals", junk);

            issue.SetType(IssueType.Metal);
            Assert.IsTrue(junk.Any(model => model.activeSelf && model.GetComponent<Renderer>().HasPropertyBlock()),
                "The model metal shows wears its tint.");

            // The recycling demo spawner sets junk directly instead of through AssignType.
            issue.SetType(IssueType.NonWaste);
            Assert.IsFalse(junk.Any(model => model.GetComponent<Renderer>().HasPropertyBlock()),
                "Junk doesn't keep the metal tint.");
            Assert.AreEqual(1, junk.Count(model => model.activeSelf), "Exactly one junk model shows.");
        }

        [Test]
        public void SetType_ThroughMetalAndBack_LeavesNoTintOnTheEarlierModel()
        {
            var issue = CreatePrimitive("Issue").AddComponent<IssueObject>();
            GameObject Model(string name)
            {
                var model = CreatePrimitive(name);
                model.transform.SetParent(issue.transform, false);
                return model;
            }

            var waste = Model("Waste");
            var metal = Model("Metal");
            SetField(issue, "sizeVisuals", new[] { waste });
            SetField(issue, "metalVisuals", new[] { metal });
            SetField(issue, "nonWasteVisuals", new GameObject[0]);
            SetField(issue, "metalSize", 1);
            issue.SetType(IssueType.Organic);
            issue.SetSize(1);
            Assert.IsTrue(waste.activeSelf);

            issue.SetType(IssueType.Metal);
            Assert.IsTrue(metal.activeSelf);
            Assert.IsTrue(metal.GetComponent<Renderer>().HasPropertyBlock(), "The metal model wears the tint.");

            issue.SetType(IssueType.Organic);
            issue.SetSize(1);
            Assert.IsTrue(waste.activeSelf);
            Assert.IsFalse(waste.GetComponent<Renderer>().HasPropertyBlock(),
                "The model shown again was never tinted while metal.");
        }

        /// <summary>
        ///     A 16-waypoint route that detours right through column 3 between rows 1 and 5. With
        ///     <paramref name="shortcut" /> the straight run up column 1 is also laid, cutting it to 12.
        /// </summary>
        private PathFixture CreateDetourPathFixture(bool shortcut)
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 2);
            PlaceHorizontal(fixture.Board, 2, 1, 2);
            PlaceVertical(fixture.Board, 3, 2, 4);
            PlaceHorizontal(fixture.Board, 1, 5, 2);
            PlaceVertical(fixture.Board, 1, 6, 4);
            if (shortcut) PlaceVertical(fixture.Board, 1, 2, 3);
            Assert.IsTrue(fixture.Path.Rebuild());
            return fixture;
        }

        private static void Release(IssueObject issue, bool flush)
        {
            if (flush) issue.FlushFromSifter();
            else issue.ReleaseFromSifter(default);
        }
    }
}
