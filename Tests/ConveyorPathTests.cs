using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using _project.Scripts.Core;
using _project.Scripts.Object_Scripts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace _project.Scripts.Tests
{
    public class ConveyorPathTests
    {
        private readonly List<GameObject> _objects = new();
        private PathBuildBoard _board;
        private WaypointPath _pipe;
        private WaypointPath _belt;

        [SetUp]
        public void SetUp()
        {
            var host = Make("Typed board");
            host.SetActive(false);
            _board = host.AddComponent<PathBuildBoard>();
            Set(_board, "columns", 4);
            Set(_board, "rows", 4);
            Set(_board, "cellSize", 1f);
            Set(_board, "cellGap", 0f);
            host.SetActive(true);
            _pipe = MakePath("Pipe route", new Vector2Int(1, -1), new Vector2Int(1, 4), false);
            _belt = MakePath("Belt route", new Vector2Int(-1, 1), new Vector2Int(4, 1), true);
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i]) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        [Test]
        public void PipeCellsCannotCompleteRecyclingRoute()
        {
            Place(0, 1, PathKind.Pipe, false);
            Place(2, 1, PathKind.Pipe, false);
            Assert.IsFalse(_belt.Rebuild());
            Assert.IsFalse(_board.IsOccupied(new Vector2Int(0, 1), PathKind.RecyclingBelt));
        }

        [Test]
        public void ConveyorCellsCannotCompleteLakeRoute()
        {
            Place(1, 0, PathKind.RecyclingBelt, true);
            Place(1, 2, PathKind.RecyclingBelt, true);
            Assert.IsFalse(_pipe.Rebuild());
            Assert.IsFalse(_board.IsOccupied(new Vector2Int(1, 0), PathKind.Pipe));
        }

        [Test]
        public void CrossingPreservesIndependentConnectionsAndRemoval()
        {
            PlaceBothRoutes();
            Assert.IsTrue(_pipe.Rebuild());
            Assert.IsTrue(_belt.Rebuild());
            var crossing = new Vector2Int(1, 1);
            Assert.IsTrue(_board.IsOccupied(crossing, PathKind.Pipe));
            Assert.IsTrue(_board.IsOccupied(crossing, PathKind.RecyclingBelt));
            Assert.IsNull(_board.TryPlace(Cell(0, 1), Piece(PathKind.RecyclingBelt, false)));
            _board.SetActiveBreakTool(PathKind.RecyclingBelt);
            Assert.IsTrue(_board.TryBreak(Cell(1, 1), out _));
            Assert.IsFalse(_belt.Rebuild());
            Assert.IsTrue(_pipe.Rebuild());
            Assert.IsTrue(_board.IsOccupied(crossing, PathKind.Pipe));
            Assert.IsFalse(_board.IsOccupied(crossing, PathKind.RecyclingBelt));
            Place(0, 1, PathKind.RecyclingBelt, false);
            Assert.IsTrue(_belt.Rebuild());
        }

        [Test]
        public void ConveyorRotationAndBoundsUseExistingPlacementRules()
        {
            var piece = Piece(PathKind.RecyclingBelt, false);
            _board.SetActivePiece(piece);
            piece.ToggleOrientation();
            Assert.AreEqual(PathPieceOrientation.Vertical, piece.Orientation);
            Assert.IsNull(_board.TryPlace(Cell(0, 3), piece));
            Assert.IsNotNull(_board.TryPlace(Cell(0, 0), piece));
            Assert.IsTrue(_board.IsOccupied(new Vector2Int(0, 1), PathKind.RecyclingBelt));
            Assert.IsFalse(_board.IsOccupied(new Vector2Int(1, 0), PathKind.RecyclingBelt));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RecyclingFork_DoesNotUsePipeAlternateRoutes(bool hasSplitter)
        {
            PlaceForkedRoute(PathKind.RecyclingBelt);
            if (hasSplitter) MakeSplitter();

            Assert.IsTrue(_belt.Rebuild());

            Assert.AreEqual(6, _belt.Count);
            Assert.IsFalse(_belt.HasAlternateRoute);
            Assert.IsEmpty(_belt.AlternatePathCells);
            Assert.IsFalse(_belt.IsSplitPoint(_board.GetCellTopPosition(new Vector2Int(0, 1))));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RecyclingPreview_IgnoresOverlappingPipeSplitter(bool completeBranch)
        {
            PlaceForkedRoute(PathKind.Pipe, completeBranch);
            PlaceForkedRoute(PathKind.RecyclingBelt, completeBranch);
            var pipe = MakePath("Forked pipe route", new Vector2Int(-1, 1), new Vector2Int(4, 1), false);
            Set(pipe, "showLivePreview", true);
            Set(_belt, "showLivePreview", true);
            MakeSplitter();

            Assert.IsTrue(pipe.Rebuild());
            Assert.IsTrue(_belt.Rebuild());

            var pipeBranch = _board.transform.Find("Alternate Path Preview")?.GetComponent<PathWaterTube>();
            Assert.IsNotNull(pipeBranch);
            Assert.IsTrue(pipeBranch.IsShowing, "The pipe splitter must still show its branch.");
            var beltPreview = _board.transform.Find("Recycling Live Path Preview")?.GetComponent<PathWaterTube>();
            Assert.IsNotNull(beltPreview);
            Assert.IsTrue(beltPreview.IsShowing);
            Assert.IsNull(_board.transform.Find("Recycling Alternate Path Preview"),
                "A pipe splitter must not reveal either a finished or unfinished conveyor branch.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PipeSplitter_IgnoresRecyclingIssuesWithoutConsumingPipeTurns(bool enterTrigger)
        {
            PlaceForkedRoute(PathKind.Pipe);
            PlaceForkedRoute(PathKind.RecyclingBelt);
            var pipe = MakePath("Forked pipe route", new Vector2Int(-1, 1), new Vector2Int(4, 1), false);
            var splitter = MakeSplitter();
            Assert.IsTrue(pipe.Rebuild());
            Assert.IsTrue(pipe.HasAlternateRoute);
            Assert.IsTrue(_belt.Rebuild());
            var recyclingHost = Make("Recycling issue");
            recyclingHost.AddComponent<SphereCollider>();
            var recyclingIssue = recyclingHost.AddComponent<IssueObject>();
            recyclingIssue.SetPath(_belt);

            if (enterTrigger)
                splitter.SendMessage("OnTriggerEnter", recyclingIssue.GetComponent<Collider>(),
                    SendMessageOptions.RequireReceiver);
            else
                Assert.IsFalse(splitter.RouteIssue(recyclingIssue));

            Assert.AreEqual(0, recyclingIssue.GetRouteIndex());
            for (var route = 0; route <= 1; route++)
            {
                var pipeIssue = Make("Pipe issue").AddComponent<IssueObject>();
                pipeIssue.SetPath(pipe);
                Assert.IsTrue(splitter.RouteIssue(pipeIssue));
                Assert.AreEqual(route, pipeIssue.GetRouteIndex(),
                    "An ignored recycling issue must not consume a turn in the pipe's 50/50 split.");
            }
        }

        [UnityTest]
        public IEnumerator MovingConveyorPreviewDoesNotRetainDestroyedVisuals()
        {
            _board.SetActivePiece(Piece(PathKind.RecyclingBelt, false));
            for (var i = 0; i < 50; i++)
                _board.SetHoveredCell(Cell(i % 2, 0));

            yield return null;

            var colors = (Dictionary<GameObject, Color>)typeof(PathBuildBoard)
                .GetField("_appliedVisualColors", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_board);
            Assert.AreEqual(1, colors.Count, "Only the current preview should remain cached.");
            foreach (var visual in colors.Keys)
                Assert.IsTrue(visual, "Destroyed previews must not remain in the color cache.");
        }

        [UnityTest]
        public IEnumerator ResetClearsBothTypesAndInvalidatesBothRoutes()
        {
            PlaceBothRoutes();
            _board.RebuildGrid();
            yield return null;
            Assert.AreEqual(0, _board.PlacedPieces.Count);
            Assert.IsFalse(_board.IsOccupied(new Vector2Int(1, 1), PathKind.Pipe));
            Assert.IsFalse(_board.IsOccupied(new Vector2Int(1, 1), PathKind.RecyclingBelt));
            Assert.IsFalse(_pipe.Rebuild());
            Assert.IsFalse(_belt.Rebuild());
        }

        private void PlaceBothRoutes()
        {
            Place(1, 0, PathKind.Pipe, true);
            Place(1, 2, PathKind.Pipe, true);
            Place(0, 1, PathKind.RecyclingBelt, false);
            Place(2, 1, PathKind.RecyclingBelt, false);
        }

        private void PlaceForkedRoute(PathKind kind, bool completeBranch = true)
        {
            Place(0, 1, kind, false);
            Place(2, 1, kind, false);
            Place(0, 2, kind, true);
            if (!completeBranch) return;
            Place(1, 3, kind, false);
            Place(3, 2, kind, true);
        }

        private PathSplitter MakeSplitter()
        {
            var host = Make("Pipe splitter");
            host.transform.position = _board.GetCellTopPosition(new Vector2Int(0, 1));
            return host.AddComponent<PathSplitter>();
        }

        private void Place(int x, int y, PathKind kind, bool vertical) =>
            Assert.IsNotNull(_board.TryPlace(Cell(x, y), Piece(kind, vertical)));

        private static PathPiecePlaceable Piece(PathKind kind, bool vertical)
        {
            var piece = new PathPiecePlaceable("Test segment", "", 1, 2, null, 0, kind);
            if (vertical) piece.ToggleOrientation();
            return piece;
        }

        private PathBuildCell Cell(int x, int y)
        {
            foreach (var cell in _board.GetComponentsInChildren<PathBuildCell>())
                if (cell.Column == x && cell.Row == y) return cell;
            throw new System.Exception("Test cell not found.");
        }

        private WaypointPath MakePath(string name, Vector2Int start, Vector2Int end, bool recycling)
        {
            var host = Make(name);
            host.SetActive(false);
            var path = host.AddComponent<WaypointPath>();
            var a = Make(name + " start").transform;
            var b = Make(name + " end").transform;
            a.position = new Vector3(start.x - 1.5f, 0, start.y - 1.5f);
            b.position = new Vector3(end.x - 1.5f, 0, end.y - 1.5f);
            Set(path, "pathBuildBoard", _board);
            Set(path, "startPoint", a);
            Set(path, "endPoint", b);
            Set(path, "recyclingDestination", recycling);
            Set(path, "showLivePreview", false);
            host.SetActive(true);
            return path;
        }

        private GameObject Make(string name)
        {
            var obj = new GameObject(name);
            _objects.Add(obj);
            return obj;
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
