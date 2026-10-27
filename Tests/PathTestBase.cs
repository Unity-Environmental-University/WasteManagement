using _project.Scripts.Object_Scripts;
using NUnit.Framework;
using UnityEngine;

namespace _project.Scripts.Tests
{
    /// <summary>
    ///     Fixtures for tests that need a <see cref="PathBuildBoard" /> wired to a
    ///     <see cref="WaypointPath" /> between a lower and an upper endpoint.
    /// </summary>
    public abstract class PathTestBase : PlayModeTestBase
    {
        protected PathFixture CreatePathFixture(int lowerColumn = 1, int upperColumn = 1, int lowerRow = -1,
            int upperRow = 10)
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

            return new PathFixture(board, path);
        }

        /// <summary>
        ///     A route up column 1 with a longer right-hand option that forks at (1,2), runs up
        ///     column 3, and rejoins at (1,8).
        /// </summary>
        protected PathFixture CreateSplitPathFixture()
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

        protected PathSplitter AddSplitter(PathFixture fixture, Vector2Int cell)
        {
            var splitter = CreateGameObject($"Path Splitter {cell}").AddComponent<PathSplitter>();
            splitter.transform.position = fixture.Board.GetCellTopPosition(cell);
            return splitter;
        }

        protected static Vector3 CellTop(PathFixture fixture, int column, int row) =>
            fixture.Board.GetCellTopPosition(new Vector2Int(column, row));

        protected IssueObject CreateIssue(string name, WaypointPath path)
        {
            var issue = CreatePrimitive(name).AddComponent<IssueObject>();
            issue.SetPath(path);
            return issue;
        }

        protected static void AdvanceIssueOneWaypoint(IssueObject issue, PathBuildBoard board)
        {
            var index = issue.GetWaypointIndex();
            var target = issue.GetPath().GetPosition(issue.GetRouteIndex(), index);
            target.y += issue.transform.localScale.y * board.entityOnBoardHeight;
            issue.transform.position = target;
            issue.SendMessage("Update", SendMessageOptions.RequireReceiver);
            Assert.AreEqual(index + 1, issue.GetWaypointIndex());
        }

        protected readonly struct PathFixture
        {
            public PathFixture(PathBuildBoard board, WaypointPath path)
            {
                Board = board;
                Path = path;
            }

            public PathBuildBoard Board { get; }
            public WaypointPath Path { get; }
        }
    }
}
