using System.Collections.Generic;
using _project.Scripts.Core;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    internal static class ConveyorVisual
    {
        public static readonly Color BeltColor = new(.22f, .85f, .38f);

        public static void Build(Transform parent, PathBuildBoard board, IReadOnlyList<Vector2Int> cells,
            PathPieceOrientation orientation)
        {
            var pitch = board.CellWorldPitch.x;
            var scale = Mathf.Abs(board.transform.lossyScale.x);
            var tileSize = pitch / Mathf.Max(scale, .001f);
            foreach (var cell in cells)
            {
                if (!board.IsCellInBounds(cell)) continue;
                var tile = new GameObject("Conveyor tile").transform;
                tile.SetParent(parent, false);
                tile.position = board.GetPathWaypointPosition(cell, PathKind.RecyclingBelt);
                tile.rotation = board.transform.rotation * Quaternion.Euler(0,
                    orientation == PathPieceOrientation.Horizontal ? 90 : 0, 0);
                Box(tile, new Vector3(0, -.025f, 0), new Vector3(tileSize * .7f, .05f, tileSize + .01f), board.ConveyorMaterial);
                Box(tile, new Vector3(-tileSize * .38f, .04f, 0), new Vector3(.04f, .08f, tileSize), board.ConveyorMaterial);
                Box(tile, new Vector3(tileSize * .38f, .04f, 0), new Vector3(.04f, .08f, tileSize), board.ConveyorMaterial);
                for (var z = -tileSize * .4f; z <= tileSize * .4f; z += tileSize * .2f)
                    Box(tile, new Vector3(0, .015f, z), new Vector3(tileSize * .65f, .025f, .03f), board.ConveyorMaterial);
            }
        }

        private static void Box(Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = "Conveyor deck / rollers";
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = size;
            var collider = obj.GetComponent<Collider>();
            collider.enabled = false;
            if (Application.isPlaying) Object.Destroy(collider); else Object.DestroyImmediate(collider);
            if (material) obj.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
