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
    /// <summary>Plastic and paper issues, and the board widening at each level past metal.</summary>
    public class BoardGrowthTests : PathTestBase
    {
        [Test]
        public void ExpandColumns_KeepsPipeInPlace_AndShiftsItsColumn()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 10);
            var before = fixture.Board.GetCellTopPosition(new Vector2Int(1, 0));
            var columns = fixture.Board.Columns;

            fixture.Board.ExpandColumns(2);

            Assert.AreEqual(columns + 4, fixture.Board.Columns);
            Assert.Less(Vector3.Distance(before, fixture.Board.GetCellTopPosition(new Vector2Int(3, 0))), 0.001f,
                "The board grows evenly on both sides, so old cells keep their world position.");
            Assert.AreEqual(new Vector2Int(3, 0), fixture.Board.WorldToCell(before));
            Assert.AreEqual(3, GetCell(fixture.Board, 3, 0).Column);
            Assert.IsTrue(fixture.Board.IsOccupied(new Vector2Int(3, 0)));
            Assert.IsFalse(fixture.Board.IsOccupied(new Vector2Int(1, 0)));
            Assert.IsTrue(fixture.Path.Rebuild(), "The route still connects its bottom and top endpoints.");
        }

        [Test]
        public void ExpandColumns_RecordsResizeAndReindexesExistingReplayContent()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 2);
            var slot = CreatePrimitive("Utility Slot").AddComponent<SpecialInteractController>();
            slot.transform.position = fixture.Board.GetCellTopPosition(new Vector2Int(1, 0));
            var recorder = CreateGameObject("Recorder").AddComponent<WasteBoardReplayRecorder>();
            recorder.enabled = false;
            SetField(recorder, "_board", fixture.Board);
            var trailhead = recorder.GetComponent<TrailheadRecorder>();
            trailhead.enableEditorNetworking = false;
            trailhead.StartRecording("Board Growth Test");
            recorder.SendMessage("RecordBoardMetadata");
            recorder.SendMessage("SyncPaths", true);
            recorder.SendMessage("SyncSquareContents", true);
            fixture.Board.PathLayoutChanged += () => recorder.SendMessage("HandlePathLayoutChanged");
            var events = GetField<IList>(trailhead, "_recordingEvents");
            var initialCount = events.Count;
            var initialColumns = fixture.Board.Columns;
            var oldCells = EventData(events[0])["cells"];

            fixture.Board.ExpandColumns(2);

            var added = events.Cast<object>().Skip(initialCount).ToArray();
            Assert.AreEqual("Board Resized", EventName(added[0]));
            Assert.AreEqual(initialColumns.ToString(), EventData(added[0])["previous-columns"]);
            Assert.AreEqual((initialColumns + 4).ToString(), EventData(added[0])["board-columns"]);
            Assert.AreEqual("2", EventData(added[0])["column-shift"]);
            var removed = added.Single(e => EventName(e) == "Path Removed");
            var placed = added.Single(e => EventName(e) == "Path Placed");
            Assert.AreEqual(oldCells, EventData(removed)["cells"]);
            Assert.AreEqual("3,0;3,1", EventData(placed)["cells"]);
            Assert.AreEqual(EventData(removed)["piece-id"], EventData(placed)["piece-id"]);
            Assert.IsTrue(added.Any(e => EventName(e) == "Square Content Changed"));
            Assert.AreEqual(initialColumns.ToString(),
                GetField<Dictionary<string, string>>(trailhead, "_recordingMetadata")["board-columns"],
                "Opening metadata must still describe frames before the resize.");

            var afterResize = events.Count;
            recorder.SendMessage("HandlePathLayoutChanged");
            Assert.AreEqual(afterResize, events.Count, "An unchanged board must not duplicate mutations.");

            fixture.Board.ExpandColumns(2);
            Assert.AreEqual((initialColumns + 4).ToString(), EventData(events[afterResize])["previous-columns"]);
            Assert.AreEqual((initialColumns + 8).ToString(), EventData(events[afterResize])["board-columns"]);
        }

        private static string EventName(object entry) => (string)entry.GetType().GetField("Name").GetValue(entry);
        private static Dictionary<string, string> EventData(object entry) =>
            (Dictionary<string, string>)entry.GetType().GetField("Data").GetValue(entry);

        [Test]
        public void ExpandColumns_MovesASideReceptacleOutWithTheEdge()
        {
            var fixture = CreatePathFixture();
            var receptacle = CreateGameObject("Receptacle").AddComponent<Receptacle>();
            receptacle.transform.position =
                fixture.Board.GetPathWaypointPosition(new Vector2Int(fixture.Board.Columns, 4));
            var left = CreateGameObject("Left Receptacle").AddComponent<Receptacle>();
            left.transform.position = fixture.Board.GetPathWaypointPosition(new Vector2Int(-1, 4));
            var lowerBefore = fixture.Path.StartPoint.position;

            fixture.Board.ExpandColumns(2);

            Assert.AreEqual(new Vector2Int(fixture.Board.Columns, 4),
                fixture.Board.WorldToCellUnclamped(receptacle.transform.position));
            Assert.AreEqual(new Vector2Int(-1, 4), fixture.Board.WorldToCellUnclamped(left.transform.position));
            Assert.AreEqual(lowerBefore, fixture.Path.StartPoint.position, "Endpoints below the board stay put.");
        }

        [Test]
        public void LevelUps_WidenTheBoardOncePerLevelFromFour()
        {
            var fixture = CreatePathFixture();
            var columns = fixture.Board.Columns;

            fixture.Board.SendMessage("ApplyLevelExpansions", 3);
            Assert.AreEqual(columns, fixture.Board.Columns, "Nothing changes up to the metal level.");

            fixture.Board.SendMessage("ApplyLevelExpansions", 4);
            Assert.AreEqual(columns + 4, fixture.Board.Columns);

            fixture.Board.SendMessage("ApplyLevelExpansions", 6);
            Assert.AreEqual(columns + 12, fixture.Board.Columns, "Skipped levels each widen the board.");

            fixture.Board.SendMessage("ApplyLevelExpansions", 6);
            Assert.AreEqual(columns + 12, fixture.Board.Columns, "The same level never widens twice.");
        }

        [UnityTest]
        public IEnumerator Recyclables_UnlockAfterMetal_TakeTheirOwnSize_AndNeverMerge(
            [Values(IssueType.Plastic, IssueType.Paper)] IssueType type)
        {
            // Merging needs a real route; with no pipe the issues would "reach the end" on their first Update.
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 10);
            Assert.IsTrue(fixture.Path.Rebuild());
            // Issues read the board's ride height through GameMaster when they move.
            var gm = CreateGameObject("Game Master").AddComponent<GameMaster>();
            gm.pathBuildBoard = fixture.Board;

            var issue = CreateRecyclable(type, fixture.Path);
            Assert.AreEqual(type == IssueType.Plastic ? 4 : 5, issue.GetUnlockLevel(type));
            Assert.IsTrue(IssueObject.IsRecyclable(type));
            Assert.AreEqual(type == IssueType.Plastic ? 2f : 3f, issue.ProcessCost,
                "Each recyclable takes its own configured size, not another type's.");

            // Neither another piece of the same kind nor ordinary waste clumps onto a recyclable.
            var twin = CreateRecyclable(type, fixture.Path);
            var waste = CreateIssue("Organic", fixture.Path);
            waste.SetType(IssueType.Organic);
            waste.SetSize(1);

            issue.SendMessage("OnTriggerEnter", twin.GetComponent<Collider>());
            issue.SendMessage("OnTriggerEnter", waste.GetComponent<Collider>());
            waste.SendMessage("OnTriggerEnter", issue.GetComponent<Collider>());
            yield return null;

            Assert.IsTrue(issue);
            Assert.IsTrue(twin);
            Assert.IsTrue(waste);
            Assert.AreEqual(type == IssueType.Plastic ? 2f : 3f, issue.ProcessCost);
            Assert.AreEqual(1f, waste.ProcessCost);
        }

        // Distinct sizes per recyclable (all below pipeBlockSize), so a mix-up between types shows.
        private IssueObject CreateRecyclable(IssueType type, WaypointPath path)
        {
            var issue = CreateIssue(type.ToString(), path);
            SetField(issue, "metalSize", 1);
            SetField(issue, "plasticSize", 2);
            SetField(issue, "paperSize", 3);
            issue.SetType(type);
            return issue;
        }
    }
}
