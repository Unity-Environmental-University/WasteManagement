using System.Collections.Generic;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Tile/model reuse pools for <see cref="PathBuildBoard" /> pipe visuals. Rebuilding a pipe
    ///     visual reconfigures existing tiles instead of destroying and recreating them, so hover
    ///     previews and placements stop churning Instantiate/Destroy every change. Play mode only;
    ///     edit mode keeps immediate semantics.
    /// </summary>
    internal sealed class PipeTilePool
    {
        private readonly PathBuildBoard _board;

        private readonly Dictionary<GameObject, Dictionary<Vector2Int, GameObject>> _visualTiles = new();
        private readonly Dictionary<GameObject, GameObject> _tileModels = new();
        private readonly Dictionary<GameObject, GameObject> _tileModelPrefabs = new();
        private readonly Stack<GameObject> _tilePool = new();
        private readonly Dictionary<GameObject, Stack<GameObject>> _modelPool = new();
        private Transform _poolRoot;

        // What each live tile was last configured as, so hover-driven rebuilds skip the model
        // swap / bounds measurement / layer walk for tiles whose cell and connections are
        // unchanged (the common case when the preview footprint moves by one cell).
        private readonly Dictionary<GameObject, (Vector2Int cell, PipeConnections connections,
            PathPieceOrientation orientation)> _tileConfigurations = new();

        public PipeTilePool(PathBuildBoard board)
        {
            _board = board;
        }

        /// <summary>The cell-to-tile map for a visual, created on first use.</summary>
        public Dictionary<Vector2Int, GameObject> GetTileMap(GameObject visual)
        {
            if (!_visualTiles.TryGetValue(visual, out var tileMap))
            {
                tileMap = new Dictionary<Vector2Int, GameObject>();
                _visualTiles[visual] = tileMap;
            }

            return tileMap;
        }

        /// <summary>Returns a visual's tiles to the pool. Call before destroying the visual.</summary>
        public void ReleaseVisual(GameObject visual)
        {
            if (!_visualTiles.TryGetValue(visual, out var tileMap)) return;

            foreach (var tile in tileMap.Values)
                ReleaseTile(tile);
            _visualTiles.Remove(visual);
        }

        public GameObject AcquireTile(Transform parent, Dictionary<Vector2Int, GameObject> tileMap, Vector2Int cell)
        {
            if (tileMap.TryGetValue(cell, out var tile))
            {
                if (tile)
                {
                    tile.SetActive(true);
                    return tile;
                }

                tileMap.Remove(cell);
            }

            if (Application.isPlaying && _tilePool.Count > 0)
            {
                tile = _tilePool.Pop();
                tile.transform.SetParent(parent, false);
                tile.SetActive(true);
            }
            else
            {
                tile = new GameObject("Pipe Tile");
                tile.transform.SetParent(parent, false);
            }

            tileMap[cell] = tile;
            return tile;
        }

        public void ReleaseTile(GameObject tile)
        {
            if (!tile) return;

            _tileConfigurations.Remove(tile);

            if (_tileModels.TryGetValue(tile, out var model))
            {
                _tileModelPrefabs.TryGetValue(tile, out var prefab);
                ReturnModelToPool(model, prefab);
                _tileModels.Remove(tile);
                _tileModelPrefabs.Remove(tile);
            }

            if (Application.isPlaying)
            {
                tile.transform.SetParent(GetOrCreatePoolRoot(), false);
                tile.SetActive(false);
                _tilePool.Push(tile);
            }
            else
            {
                Object.DestroyImmediate(tile);
            }
        }

        public bool IsConfiguredAs(GameObject tile,
            (Vector2Int cell, PipeConnections connections, PathPieceOrientation orientation) configuration)
        {
            return _tileConfigurations.TryGetValue(tile, out var applied) && applied == configuration;
        }

        public void MarkConfigured(GameObject tile,
            (Vector2Int cell, PipeConnections connections, PathPieceOrientation orientation) configuration)
        {
            _tileConfigurations[tile] = configuration;
        }

        /// <summary>
        ///     Returns the tile's model instance for the requested prefab, reusing the current one
        ///     when it already matches and recycling it through the model pool when it does not.
        /// </summary>
        public GameObject AcquireModel(GameObject tile, GameObject prefab)
        {
            if (_tileModels.TryGetValue(tile, out var current))
            {
                GameObject source = null;
                if (current && _tileModelPrefabs.TryGetValue(tile, out source) && source == prefab)
                    return current;

                if (current)
                    ReturnModelToPool(current, source);
                _tileModels.Remove(tile);
                _tileModelPrefabs.Remove(tile);
            }

            GameObject model = null;
            if (Application.isPlaying && prefab)
            {
                if (_modelPool.TryGetValue(prefab, out var pool))
                    while (!model && pool.Count > 0)
                        model = pool.Pop();

                if (model)
                {
                    model.transform.SetParent(tile.transform, false);
                    model.SetActive(true);
                }
            }

            if (!model)
            {
                model = prefab
                    ? Object.Instantiate(prefab, tile.transform)
                    : GameObject.CreatePrimitive(PrimitiveType.Cube);
                model.transform.SetParent(tile.transform, false);
                if (!prefab && model.TryGetComponent<Renderer>(out var renderer) && _board.FallbackPipeMaterial)
                    renderer.sharedMaterial = _board.FallbackPipeMaterial;
            }

            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            _tileModels[tile] = model;
            _tileModelPrefabs[tile] = prefab;
            return model;
        }

        /// <summary>Forgets every pooled and tracked object. Used after the board's children are destroyed.</summary>
        public void Clear()
        {
            _poolRoot = null;
            _visualTiles.Clear();
            _tileModels.Clear();
            _tileModelPrefabs.Clear();
            _tilePool.Clear();
            _modelPool.Clear();
            _tileConfigurations.Clear();
        }

        private void ReturnModelToPool(GameObject model, GameObject prefab)
        {
            if (!model) return;

            // Fallback cube models have no prefab key to pool under — destroy them outright.
            if (!Application.isPlaying || !prefab)
            {
                PathBuildBoard.DestroyObject(model);
                return;
            }

            model.transform.SetParent(GetOrCreatePoolRoot(), false);
            model.SetActive(false);
            if (!_modelPool.TryGetValue(prefab, out var pool))
            {
                pool = new Stack<GameObject>();
                _modelPool[prefab] = pool;
            }

            pool.Push(model);
        }

        private Transform GetOrCreatePoolRoot()
        {
            if (_poolRoot) return _poolRoot;

            var visualRoot = _board.VisualRoot;
            var existing = visualRoot.Find("PipeTilePool");
            _poolRoot = existing ? existing : new GameObject("PipeTilePool").transform;
            if (!_poolRoot.parent)
                _poolRoot.SetParent(visualRoot, false);
            return _poolRoot;
        }
    }
}
