using _project.Scripts.Object_Scripts;
using NUnit.Framework;
using UnityEngine;

namespace _project.Scripts.Tests
{
    /// <summary>Strict endpoint validation of the placed route, and how placements face along it.</summary>
    public class WaypointPathTests : PathTestBase
    {
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
        public void Rebuild_ReturnsFalseWithNoFallbackRoute_WhenPathIsNearLowerEndpointButNotEdgeAdjacent()
        {
            var fixture = CreatePathFixture(0);
            PlaceVertical(fixture.Board, 1, 0, 10);

            Assert.IsFalse(fixture.Path.Rebuild());
            Assert.AreEqual("No placed path cell touches the lower endpoint square.", fixture.Path.InvalidReason);
            Assert.AreEqual(0, fixture.Path.Count, "A failed rebuild must not emit a direct start-to-end route.");
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

        [TestCase(false)]
        [TestCase(true)]
        public void TryGetPathFacingRotation_FacesAlongPipe_WhenPlacedOnPipe(bool vertical)
        {
            var board = CreateGameObject("Path Board").AddComponent<PathBuildBoard>();
            if (vertical) PlaceVertical(board, 1, 1, 3);
            else PlaceHorizontal(board, 1, 1, 3);

            Assert.IsTrue(board.TryGetPathFacingRotation(board.GetCellTopPosition(new Vector2Int(1, 1)),
                out var rotation));

            AssertFaces(rotation, vertical ? Vector3.forward : Vector3.right);
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

        private static void AssertFaces(Quaternion rotation, Vector3 expectedDirection)
        {
            Assert.Greater(Vector3.Dot(rotation * Vector3.forward, expectedDirection.normalized), 0.99f);
        }
    }
}
