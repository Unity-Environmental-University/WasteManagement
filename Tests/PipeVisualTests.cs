using System.Collections.Generic;
using _project.Scripts.Core;
using _project.Scripts.Object_Scripts;
using NUnit.Framework;
using UnityEngine;

namespace _project.Scripts.Tests
{
    public class PipeVisualTests
    {
        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in _created)
                if (gameObject)
                    Object.DestroyImmediate(gameObject);
            _created.Clear();
        }

        [Test]
        public void HorizontalRun_UsesOpenStraightTilesAtBothEnds()
        {
            var board = CreateBoard();
            Place(board, 1, 1, 3, PathPieceOrientation.Horizontal);

            var visual = board.transform.Find("PipeVisuals/Placed Pipe 1");
            Assert.IsNotNull(visual);
            Assert.AreEqual(3, visual.childCount);
            StringAssert.Contains("brick_straightPipe",
                visual.Find("Pipe Tile 1,1").GetComponentInChildren<MeshFilter>().sharedMesh.name);
            StringAssert.Contains("brick_straightPipe",
                visual.Find("Pipe Tile 2,1").GetComponentInChildren<MeshFilter>().sharedMesh.name);
            StringAssert.Contains("brick_straightPipe",
                visual.Find("Pipe Tile 3,1").GetComponentInChildren<MeshFilter>().sharedMesh.name);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(
                visual.Find("Pipe Tile 2,1").localEulerAngles.y, 90f)), Is.LessThan(0.1f));
        }

        [Test]
        public void VerticalRun_UsesTheAuthoredFrontToBackAxis()
        {
            var board = CreateBoard();
            Place(board, 1, 1, 3, PathPieceOrientation.Vertical);

            var visual = board.transform.Find("PipeVisuals/Placed Pipe 1");
            Assert.IsNotNull(visual);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(
                visual.Find("Pipe Tile 1,2").localEulerAngles.y, 180f)), Is.LessThan(0.1f));
        }

        [Test]
        public void AdjacentPipeTiles_OverlapSlightlyAtTheCellPitch()
        {
            var board = CreateBoard();
            Place(board, 1, 1, 3, PathPieceOrientation.Horizontal);

            var visual = board.transform.Find("PipeVisuals/Placed Pipe 1");
            var leftBounds = visual.Find("Pipe Tile 1,1").GetComponentInChildren<Renderer>().bounds;
            var middleBounds = visual.Find("Pipe Tile 2,1").GetComponentInChildren<Renderer>().bounds;

            // Tiles must close the cell gap and overlap enough to hide the seam, but not so far
            // that a tile intrudes visibly into its neighbour's cell.
            Assert.That(leftBounds.max.x, Is.GreaterThan(middleBounds.min.x));
            Assert.That(leftBounds.max.x - middleBounds.min.x, Is.LessThan(board.CellWorldSize.x * 0.25f));
        }

        [Test]
        public void PipeTiles_ShareOneFootprintScaleAcrossPieceTypes()
        {
            var board = CreateBoard();
            Place(board, 1, 1, 2, PathPieceOrientation.Horizontal);
            Place(board, 2, 2, 2, PathPieceOrientation.Vertical);

            var straight = board.transform.Find("PipeVisuals/Placed Pipe 1/Pipe Tile 1,1");
            var corner = board.transform.Find("PipeVisuals/Placed Pipe 1/Pipe Tile 2,1");
            StringAssert.Contains("brick_cornerPipe",
                corner.GetComponentInChildren<MeshFilter>().sharedMesh.name);

            // X must equal Z, or the brick texture stretches along the flow axis and horizontal
            // runs stop matching vertical ones.
            foreach (var tile in new[] { straight, corner })
                Assert.That(tile.localScale.x, Is.EqualTo(tile.localScale.z).Within(0.001f));

            // Straights and corners must agree with each other on all three axes, or every join
            // shows a step. Height may be squashed for the top-down read, but only equally.
            Assert.That(straight.localScale.x, Is.EqualTo(corner.localScale.x).Within(0.02f));
            Assert.That(straight.localScale.y, Is.EqualTo(corner.localScale.y).Within(0.02f));
        }

        [Test]
        public void CornerConnection_UsesCornerModelTurnedTowardItsOpenSides()
        {
            var board = CreateBoard();
            Place(board, 1, 1, 2, PathPieceOrientation.Horizontal);
            Place(board, 2, 2, 2, PathPieceOrientation.Vertical);

            var firstVisual = board.transform.Find("PipeVisuals/Placed Pipe 1");
            var corner = firstVisual.Find("Pipe Tile 2,1");
            StringAssert.Contains("brick_cornerPipe",
                corner.GetComponentInChildren<MeshFilter>().sharedMesh.name);

            // Cell 2,1 opens west (its own run) and north (the vertical piece). brick_cornerPipe is
            // authored north+west, so the source-axis correction alone should orient it: any other
            // angle presents a wall where the pipe has to open.
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(corner.localEulerAngles.y, 270f + 90f)),
                Is.LessThan(0.1f));
        }

        [Test]
        public void ThreeWayCell_UsesTheTeeRatherThanTheFourWayMesh()
        {
            var board = CreateBoard();
            Place(board, 1, 1, 2, PathPieceOrientation.Horizontal);  // cells 1,1 and 2,1
            Place(board, 3, 1, 2, PathPieceOrientation.Horizontal);  // cells 3,1 and 4,1
            Place(board, 2, 2, 2, PathPieceOrientation.Vertical);    // cells 2,2 and 2,3

            // Cell 2,1 now opens east, west and north. Substituting brick_plusPipe here would
            // leave a fourth arm pointing south into a cell nothing connects to.
            var tee = board.transform.Find("PipeVisuals/Placed Pipe 1/Pipe Tile 2,1");
            StringAssert.Contains("brick_Tpipe",
                tee.GetComponentInChildren<MeshFilter>().sharedMesh.name);

            // The tee is authored closed on the south side, so zero board rotation is correct.
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(tee.localEulerAngles.y, 0f + 90f)),
                Is.LessThan(0.1f));
        }

        [Test]
        public void ParallelRuns_StayStraightAndDoNotOverlapSideways()
        {
            var board = CreateBoard();
            Place(board, 1, 1, 3, PathPieceOrientation.Horizontal);
            Place(board, 1, 2, 3, PathPieceOrientation.Horizontal);

            var lower = board.transform.Find("PipeVisuals/Placed Pipe 1/Pipe Tile 2,1");
            var upper = board.transform.Find("PipeVisuals/Placed Pipe 2/Pipe Tile 2,2");
            StringAssert.Contains("brick_straightPipe",
                lower.GetComponentInChildren<MeshFilter>().sharedMesh.name);
            StringAssert.Contains("brick_straightPipe",
                upper.GetComponentInChildren<MeshFilter>().sharedMesh.name);

            var lowerBounds = lower.GetComponentInChildren<Renderer>().bounds;
            var upperBounds = upper.GetComponentInChildren<Renderer>().bounds;
            Assert.That(lowerBounds.max.z, Is.LessThan(upperBounds.min.z));
        }

        [Test]
        public void PriorityRoute_ForcesCleanCornersThroughAdjacentPieces()
        {
            var board = CreateBoard();
            Place(board, 1, 1, 3, PathPieceOrientation.Horizontal);
            Place(board, 1, 2, 3, PathPieceOrientation.Horizontal);

            board.SetPriorityVisualPath(new[]
            {
                new Vector2Int(1, 1),
                new Vector2Int(2, 1),
                new Vector2Int(2, 2),
                new Vector2Int(3, 2)
            });

            var lowerCorner = board.transform.Find("PipeVisuals/Placed Pipe 1/Pipe Tile 2,1");
            var upperCorner = board.transform.Find("PipeVisuals/Placed Pipe 2/Pipe Tile 2,2");
            StringAssert.Contains("brick_cornerPipe",
                lowerCorner.GetComponentInChildren<MeshFilter>().sharedMesh.name);
            StringAssert.Contains("brick_cornerPipe",
                upperCorner.GetComponentInChildren<MeshFilter>().sharedMesh.name);

            var offRouteCell = board.transform.Find("PipeVisuals/Placed Pipe 2/Pipe Tile 1,2");
            StringAssert.Contains("brick_straightPipe",
                offRouteCell.GetComponentInChildren<MeshFilter>().sharedMesh.name);

            var routeStart = board.transform.Find("PipeVisuals/Placed Pipe 1/Pipe Tile 1,1");
            var startBounds = routeStart.GetComponentInChildren<Renderer>().bounds;
            var lowerCornerBounds = lowerCorner.GetComponentInChildren<Renderer>().bounds;
            var upperCornerBounds = upperCorner.GetComponentInChildren<Renderer>().bounds;
            var offRouteBounds = offRouteCell.GetComponentInChildren<Renderer>().bounds;
            // Route tiles are scaled like every other tile now, so the join is the seam overlap
            // rather than a bespoke overscan that made route pipes fatter than their neighbours.
            Assert.That(startBounds.max.x - lowerCornerBounds.min.x, Is.GreaterThan(0f));
            Assert.That(lowerCornerBounds.max.z - upperCornerBounds.min.z, Is.GreaterThan(0f));
            Assert.That(startBounds.max.z, Is.LessThan(offRouteBounds.min.z));
        }

        [Test]
        public void PriorityRoutes_RenderTheAlternateBranchAsATeeAtTheFork()
        {
            var board = CreateBoard();
            Place(board, 1, 0, 3, PathPieceOrientation.Vertical);
            Place(board, 1, 3, 5, PathPieceOrientation.Vertical);
            Place(board, 1, 8, 2, PathPieceOrientation.Vertical);
            Place(board, 2, 2, 2, PathPieceOrientation.Horizontal);
            Place(board, 3, 3, 5, PathPieceOrientation.Vertical);
            Place(board, 2, 8, 2, PathPieceOrientation.Horizontal);

            var primary = new[]
            {
                new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2),
                new Vector2Int(1, 3), new Vector2Int(1, 4), new Vector2Int(1, 5),
                new Vector2Int(1, 6), new Vector2Int(1, 7), new Vector2Int(1, 8),
                new Vector2Int(1, 9)
            };
            var alternate = new[]
            {
                new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2),
                new Vector2Int(2, 2), new Vector2Int(3, 2), new Vector2Int(3, 3),
                new Vector2Int(3, 4), new Vector2Int(3, 5), new Vector2Int(3, 6),
                new Vector2Int(3, 7), new Vector2Int(3, 8), new Vector2Int(2, 8),
                new Vector2Int(1, 8), new Vector2Int(1, 9)
            };

            board.SetPriorityVisualPath(primary, alternateRouteCells: alternate);

            var fork = board.transform.Find("PipeVisuals/Placed Pipe 1/Pipe Tile 1,2");
            StringAssert.Contains("brick_Tpipe",
                fork.GetComponentInChildren<MeshFilter>().sharedMesh.name);
        }

        private PathBuildBoard CreateBoard()
        {
            var gameObject = new GameObject("Path Board");
            _created.Add(gameObject);
            var board = gameObject.AddComponent<PathBuildBoard>();
            board.RebuildGrid();
            return board;
        }

        private static void Place(PathBuildBoard board, int column, int row, int length,
            PathPieceOrientation orientation)
        {
            var piece = new PathPiecePlaceable("Pipe", string.Empty, 1, length, null, 0);
            if (orientation == PathPieceOrientation.Vertical)
                piece.ToggleOrientation();

            var cell = board.transform.Find($"Path Cell {column},{row}").GetComponent<PathBuildCell>();
            Assert.IsNotNull(board.TryPlace(cell, piece));
        }
    }
}
