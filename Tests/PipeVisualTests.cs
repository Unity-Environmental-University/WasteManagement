using _project.Scripts.Core;
using _project.Scripts.Object_Scripts;
using NUnit.Framework;
using UnityEngine;

namespace _project.Scripts.Tests
{
    public class PipeVisualTests : PlayModeTestBase
    {
        [Test]
        public void HorizontalRun_UsesOpenStraightTilesAtBothEnds()
        {
            var board = CreateBoard();
            Place(board, 1, 1, 3, PathPieceOrientation.Horizontal);

            var visual = board.transform.Find("PipeVisuals/Placed Pipe 1");
            Assert.IsNotNull(visual);
            Assert.AreEqual(3, visual.childCount);
            AssertMesh("brick_straightPipe", Tile(board, 1, 1, 1));
            AssertMesh("brick_straightPipe", Tile(board, 1, 2, 1));
            AssertMesh("brick_straightPipe", Tile(board, 1, 3, 1));
            AssertYaw(Tile(board, 1, 2, 1), 90f);
        }

        [Test]
        public void VerticalRun_UsesTheAuthoredFrontToBackAxis()
        {
            var board = CreateBoard();
            Place(board, 1, 1, 3, PathPieceOrientation.Vertical);

            AssertYaw(Tile(board, 1, 1, 2), 180f);
        }

        [Test]
        public void AdjacentPipeTiles_OverlapSlightlyAtTheCellPitch()
        {
            var board = CreateBoard();
            Place(board, 1, 1, 3, PathPieceOrientation.Horizontal);

            var leftBounds = RenderBounds(Tile(board, 1, 1, 1));
            var middleBounds = RenderBounds(Tile(board, 1, 2, 1));

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

            var straight = Tile(board, 1, 1, 1);
            var corner = Tile(board, 1, 2, 1);
            AssertMesh("brick_cornerPipe", corner);

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

            var corner = Tile(board, 1, 2, 1);
            AssertMesh("brick_cornerPipe", corner);

            // Cell 2,1 opens west (its own run) and north (the vertical piece). brick_cornerPipe is
            // authored north+west, so the source-axis correction alone should orient it: any other
            // angle presents a wall where the pipe has to open.
            AssertYaw(corner, 270f + 90f);
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
            var tee = Tile(board, 1, 2, 1);
            AssertMesh("brick_Tpipe", tee);

            // The tee is authored closed on the south side, so zero board rotation is correct.
            AssertYaw(tee, 0f + 90f);
        }

        [Test]
        public void ParallelRuns_StayStraightAndDoNotOverlapSideways()
        {
            var board = CreateBoard();
            Place(board, 1, 1, 3, PathPieceOrientation.Horizontal);
            Place(board, 1, 2, 3, PathPieceOrientation.Horizontal);

            var lower = Tile(board, 1, 2, 1);
            var upper = Tile(board, 2, 2, 2);
            AssertMesh("brick_straightPipe", lower);
            AssertMesh("brick_straightPipe", upper);

            var lowerBounds = RenderBounds(lower);
            var upperBounds = RenderBounds(upper);
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

            var lowerCorner = Tile(board, 1, 2, 1);
            var upperCorner = Tile(board, 2, 2, 2);
            AssertMesh("brick_cornerPipe", lowerCorner);
            AssertMesh("brick_cornerPipe", upperCorner);

            var offRouteCell = Tile(board, 2, 1, 2);
            AssertMesh("brick_straightPipe", offRouteCell);

            var routeStart = Tile(board, 1, 1, 1);
            var startBounds = RenderBounds(routeStart);
            var lowerCornerBounds = RenderBounds(lowerCorner);
            var upperCornerBounds = RenderBounds(upperCorner);
            var offRouteBounds = RenderBounds(offRouteCell);
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

            board.SetPriorityVisualPath(primary, branchRouteCells: new[] { alternate });

            var fork = Tile(board, 1, 1, 2);
            AssertMesh("brick_Tpipe", fork);
        }

        private PathBuildBoard CreateBoard()
        {
            var board = CreateGameObject("Path Board").AddComponent<PathBuildBoard>();
            board.RebuildGrid();
            return board;
        }

        private static void Place(PathBuildBoard board, int column, int row, int length,
            PathPieceOrientation orientation) =>
            Assert.IsNotNull(board.TryPlace(GetCell(board, column, row),
                Pipe(length, orientation == PathPieceOrientation.Vertical)));

        private static Transform Tile(PathBuildBoard board, int piece, int column, int row)
        {
            var tile = board.transform.Find($"PipeVisuals/Placed Pipe {piece}/Pipe Tile {column},{row}");
            Assert.IsNotNull(tile, $"Missing tile {column},{row} of placed pipe {piece}");
            return tile;
        }

        private static void AssertMesh(string meshName, Transform tile) =>
            StringAssert.Contains(meshName, tile.GetComponentInChildren<MeshFilter>().sharedMesh.name, tile.name);

        private static void AssertYaw(Transform tile, float degrees) =>
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(tile.localEulerAngles.y, degrees)), Is.LessThan(0.1f), tile.name);

        private static Bounds RenderBounds(Transform tile) => tile.GetComponentInChildren<Renderer>().bounds;
    }
}
