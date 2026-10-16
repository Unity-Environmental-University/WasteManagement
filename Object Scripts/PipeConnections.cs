using System;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>The sides of a board cell a pipe tile opens toward.</summary>
    [Flags]
    internal enum PipeConnections
    {
        None = 0,
        East = 1 << 0,
        North = 1 << 1,
        West = 1 << 2,
        South = 1 << 3
    }

    /// <summary>
    ///     Maps a cell's <see cref="PipeConnections" /> to the pipe model that shows it and the
    ///     board rotation that turns that model toward its open sides.
    /// </summary>
    internal static class PipeShapes
    {
        public static PipeConnections GetConnectionDirection(Vector2Int delta)
        {
            if (delta == Vector2Int.right) return PipeConnections.East;
            if (delta == Vector2Int.up) return PipeConnections.North;
            if (delta == Vector2Int.left) return PipeConnections.West;
            return delta == Vector2Int.down ? PipeConnections.South : PipeConnections.None;
        }

        public static int CountConnections(PipeConnections connections)
        {
            var count = 0;
            if ((connections & PipeConnections.East) != 0) count++;
            if ((connections & PipeConnections.North) != 0) count++;
            if ((connections & PipeConnections.West) != 0) count++;
            if ((connections & PipeConnections.South) != 0) count++;
            return count;
        }

        public static bool IsStraight(PipeConnections connections)
        {
            return connections == (PipeConnections.East | PipeConnections.West) ||
                   connections == (PipeConnections.North | PipeConnections.South);
        }

        public static GameObject SelectPipePrefab(PipeVisualLibrary library, PipeConnections connections)
        {
            if (!library) return null;

            var connectionCount = CountConnections(connections);
            // No dedicated cap is required: the straight model keeps isolated/off-route cells
            // visually tidy and gives one-neighbor route cells an opening toward an endpoint.
            if (connectionCount <= 1) return library.StraightPipe;

            // A three-way cell must use the tee. Substituting the four-way mesh leaves its
            // unused arm poking into a neighbor as a dead-end stub.
            if (connectionCount == 3)
                return library.TJunctionPipe ? library.TJunctionPipe : library.StraightPipe;

            if (connectionCount >= 4)
                return library.JunctionPipe ? library.JunctionPipe : library.StraightPipe;

            return IsStraight(connections)
                ? library.StraightPipe
                : library.CornerPipe ? library.CornerPipe : library.StraightPipe;
        }

        public static float GetPipeRotation(PipeConnections connections, PathPieceOrientation fallbackOrientation)
        {
            var count = CountConnections(connections);
            if (count == 0)
                return fallbackOrientation == PathPieceOrientation.Horizontal ? 0f : 90f;

            if (count == 1)
            {
                if ((connections & PipeConnections.East) != 0) return 0f;
                if ((connections & PipeConnections.South) != 0) return 90f;
                if ((connections & PipeConnections.West) != 0) return 180f;
                return 270f;
            }

            if (count == 2)
            {
                if (IsStraight(connections))
                    return (connections & PipeConnections.East) != 0 ? 0f : 90f;

                // brick_cornerPipe is authored opening north+west; after the source-axis
                // correction it opens north+east at zero, so each quadrant steps by 90.
                if (connections == (PipeConnections.North | PipeConnections.East)) return 0f;
                if (connections == (PipeConnections.East | PipeConnections.South)) return 90f;
                if (connections == (PipeConnections.South | PipeConnections.West)) return 180f;
                return 270f;
            }

            if (count == 3)
            {
                // brick_TPipe is authored opening north+south+west, so after the correction its
                // closed side faces south at zero. Rotate by whichever side has no connection.
                if ((connections & PipeConnections.South) == 0) return 0f;
                if ((connections & PipeConnections.West) == 0) return 90f;
                if ((connections & PipeConnections.North) == 0) return 180f;
                return 270f;
            }

            // The four-way mesh is rotationally symmetric.
            return 0f;
        }
    }
}
