using System.Collections.Generic;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Turns a pooled pipe tile into the right model for its cell: picks the mesh from the
    ///     cell's connections, rotates it toward its open sides, and scales and positions it from
    ///     its measured bounds so every tile meets its neighbours on the board's cell pitch.
    /// </summary>
    internal sealed class PipeTileShaper
    {
        // The authored pipe FBXs run front-to-back along local Z. Board rotation calculations
        // use zero degrees for an east/west (local X) connection, so compensate at the source.
        private const float PipeSourceAxisCorrection = 90f;

        private readonly PathBuildBoard _board;
        private readonly PipeTilePool _pool;
        private PipeVisualLibrary _footprintSource;

        // Footprint of the straight module, measured once and reused for every piece type so all
        // tiles share one scale. Cached against the library it was measured from.
        private float _sharedPipeFootprint;

        public PipeTileShaper(PathBuildBoard board, PipeTilePool pool)
        {
            _board = board;
            _pool = pool;
        }

        /// <summary>
        ///     World height of a scaled pipe tile, measured as tiles are built. Zero until the first
        ///     model-backed tile exists, in which case the fallback box height stands in.
        /// </summary>
        public float MeasuredTileHeight { get; private set; }

        // The tile transform carries the board rotation and the one shared footprint scale;
        // the model beneath it carries the per-axis seam adjustment. Splitting the two keeps
        // every tile reporting the same uniform footprint whichever mesh it happens to use.
        // Returns true when the tile was actually (re)configured; an unchanged tile is skipped.
        public bool ConfigureTile(GameObject tile, Vector2Int cell, PipeConnections connections,
            PathPieceOrientation fallbackOrientation)
        {
            var configuration = (cell, connections, fallbackOrientation);
            if (_pool.IsConfiguredAs(tile, configuration))
                return false;

            var library = _board.PipeLibrary;
            var prefab = PipeShapes.SelectPipePrefab(library, connections);

            tile.transform.localPosition = Vector3.zero;
            tile.transform.localRotation = Quaternion.Euler(0f,
                PipeShapes.GetPipeRotation(connections, fallbackOrientation) + PipeSourceAxisCorrection, 0f);
            tile.transform.localScale = Vector3.one;

            var model = _pool.AcquireModel(tile, prefab);
            ConfigurePipeTileHierarchy(tile);

            var renderers = tile.GetComponentsInChildren<Renderer>(true);
            if (prefab) ApplyPipeSurfaceMaterial(renderers, library ? library.SurfaceMaterial : null);

            // The straight module is the only mesh that stays closed on its side walls; every
            // junction opens on both axes and so has to reach past the cell edge on both.
            var opensOnBothAxes = !prefab || !library || prefab != library.StraightPipe;
            NormalizePipeTile(tile, model.transform, cell, renderers, prefab, opensOnBothAxes);

            tile.name = $"Pipe Tile {cell.x},{cell.y}";
            _pool.MarkConfigured(tile, configuration);
            return true;
        }

        private void NormalizePipeTile(GameObject tile, Transform model, Vector2Int cell,
            IReadOnlyList<Renderer> renderers, GameObject prefab, bool opensOnBothAxes)
        {
            var cellPitch = _board.PipeCellPitch;
            var cellHeight = _board.CellHeight;

            if (!TryCalculateLocalRendererBounds(tile.transform, renderers, out var bounds))
            {
                var fallbackHeight = _board.FallbackPipeHeight;
                var fallbackPosition = _board.GetLocalPosition(cell.x, cell.y);
                tile.transform.localPosition =
                    new Vector3(fallbackPosition.x, cellHeight * 0.5f + fallbackHeight * 0.5f, fallbackPosition.z);
                tile.transform.localScale = new Vector3(cellPitch, fallbackHeight, cellPitch);
                return;
            }

            // Every mesh in the kit is authored as a square single-cell module, so one footprint
            // scale serves all of them, and it is measured once from the straight module rather
            // than per mesh: a corner authored a fraction larger than its neighbors must not
            // arrive at a join a step wider. X and Z stay equal so mortar courses line up on
            // horizontal and vertical runs alike. Height is squashed separately for the top-down
            // read; because the squash is the same for every piece, it costs only brick proportion
            // on the walls, never joint alignment.
            var footprint = GetSharedPipeFootprint(prefab, bounds);
            var scale = footprint > Mathf.Epsilon ? cellPitch / footprint : 1f;
            var verticalScale = scale * _board.PipeHeightScale;
            tile.transform.localScale = new Vector3(scale, verticalScale, scale);

            // The seam overlap is directional. A tile reaches past the cell edge only on the axes
            // it actually opens on, closing those joins. It pulls in by the same amount on the
            // sides it keeps walled — otherwise two parallel runs bleed into each other's cells.
            // The authored modules run front-to-back along local Z, so that is the open axis.
            var openFactor = 1f + _board.PipeSeamOverlap / cellPitch;
            var closedFactor = 1f - _board.PipeSeamOverlap / cellPitch;
            var modelScale = new Vector3(opensOnBothAxes ? openFactor : closedFactor, 1f, openFactor);
            model.localScale = modelScale;

            var totalScale = new Vector3(scale * modelScale.x, verticalScale, scale * modelScale.z);
            var rotatedCenter = tile.transform.localRotation * Vector3.Scale(bounds.center, totalScale);
            var cellPosition = _board.GetLocalPosition(cell.x, cell.y);
            tile.transform.localPosition = new Vector3(
                cellPosition.x - rotatedCenter.x,
                cellHeight * 0.5f - bounds.min.y * verticalScale,
                cellPosition.z - rotatedCenter.z);

            MeasuredTileHeight = bounds.size.y * verticalScale;
        }

        /// <summary>
        ///     Footprint used to scale every pipe tile, taken from the straight module so that all
        ///     piece types share one scale. Falls back to the tile's own bounds when no library
        ///     model backs the tile (the untextured box).
        /// </summary>
        private float GetSharedPipeFootprint(GameObject prefab, Bounds tileBounds)
        {
            var tileFootprint = Mathf.Max(tileBounds.size.x, tileBounds.size.z);
            var library = _board.PipeLibrary;
            var reference = library ? library.StraightPipe : null;
            if (!prefab || !reference) return tileFootprint;

            if (_footprintSource != library || _sharedPipeFootprint <= Mathf.Epsilon)
            {
                _footprintSource = library;
                _sharedPipeFootprint = MeasurePrefabFootprint(reference);
            }

            return _sharedPipeFootprint > Mathf.Epsilon ? _sharedPipeFootprint : tileFootprint;
        }

        private static float MeasurePrefabFootprint(GameObject prefab)
        {
            var bounds = default(Bounds);
            var hasBounds = false;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh) continue;
                EncapsulateLocalBounds(prefab.transform, filter.transform, filter.sharedMesh.bounds,
                    ref bounds, ref hasBounds);
            }

            return hasBounds ? Mathf.Max(bounds.size.x, bounds.size.z) : 0f;
        }

        private static bool TryCalculateLocalRendererBounds(Transform root, IReadOnlyList<Renderer> renderers,
            out Bounds bounds)
        {
            bounds = default;
            var hasBounds = false;
            foreach (var renderer in renderers)
                EncapsulateLocalBounds(root, renderer.transform, renderer.localBounds, ref bounds, ref hasBounds);

            return hasBounds;
        }

        /// <summary>
        ///     Expands <paramref name="bounds" /> (expressed in <paramref name="root" />'s local space)
        ///     to cover the eight corners of <paramref name="localBounds" /> taken in
        ///     <paramref name="source" />'s local space.
        /// </summary>
        private static void EncapsulateLocalBounds(Transform root, Transform source, Bounds localBounds,
            ref Bounds bounds, ref bool hasBounds)
        {
            var min = localBounds.min;
            var max = localBounds.max;

            for (var x = 0; x < 2; x++)
            for (var y = 0; y < 2; y++)
            for (var z = 0; z < 2; z++)
            {
                var sourcePoint = new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y,
                    z == 0 ? min.z : max.z);
                var rootPoint = root.InverseTransformPoint(source.TransformPoint(sourcePoint));
                if (!hasBounds)
                {
                    bounds = new Bounds(rootPoint, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(rootPoint);
                }
            }
        }

        private static void ApplyPipeSurfaceMaterial(IReadOnlyList<Renderer> renderers, Material surfaceMaterial)
        {
            if (!surfaceMaterial) return;
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                    materials[i] = surfaceMaterial;
                renderer.sharedMaterials = materials;
            }
        }

        private void ConfigurePipeTileHierarchy(GameObject tile)
        {
            var layer = _board.gameObject.layer;
            foreach (var child in tile.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;

            foreach (var collisionComp in tile.GetComponentsInChildren<Collider>(true))
                collisionComp.enabled = false;
        }
    }
}