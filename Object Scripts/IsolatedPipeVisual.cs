using System;
using System.Collections.Generic;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Draws isolated pipe as a slim, closed metal tube on short stands, instead of the open
    ///     channel tiles regular pipe uses. It rides above the channel walls, so where it crosses
    ///     regular pipe it visibly bridges over it. Each cell gets a tube leg toward every side it
    ///     connects on; straight runs get a flange collar and bends get a round joint. A leg that
    ///     leaves the board runs on into whatever it feeds, such as a receptacle. An end facing
    ///     regular pipe gets a <see cref="DrainValve" />: capped while closed, and once opened a
    ///     spout drops over the channel wall and splashes into the water.
    /// </summary>
    internal static class IsolatedPipeVisual
    {
        private const string RootName = "Isolated Pipe";

        // How far, in cells, a leg into a regular channel reaches before turning down: past the
        // channel's wall, short of its middle.
        private const float DownspoutReach = 0.75f;

        private static Mesh _cylinder;
        private static Mesh _sphere;
        private static Mesh _cube;
        private static Material _metal;
        private static Material _metalSource;
        private static Material _paint;
        private static Material _paintSource;

        /// <summary>Rebuilds the tube for <paramref name="cells" /> under <paramref name="visual" />.</summary>
        public static void Build(PathBuildBoard board, GameObject visual, IReadOnlyList<Vector2Int> cells,
            PathPieceOrientation orientation, Func<Vector2Int, PipeConnections> getConnections)
        {
            var root = GetRoot(visual.transform);
            ClearChildren(root);
            var material = GetMetal(board);
            var pitch = board.PipeCellPitch;
            var radius = board.IsolatedPipeRadius;
            var centerHeight = board.IsolatedPipeCenterHeight;

            foreach (var cell in cells)
            {
                var connections = getConnections(cell);
                var center = board.GetLocalPosition(cell.x, cell.y) + Vector3.up * centerHeight;
                if (connections == PipeConnections.None)
                {
                    // Pipe the water never reaches (past where a route turns off it): a loose capped length.
                    var axis = orientation == PathPieceOrientation.Horizontal ? Vector3.right : Vector3.forward;
                    var span = pitch * 0.35f;
                    AddPart(root, GetCylinder(), material, center, Quaternion.FromToRotation(Vector3.up, axis),
                        new Vector3(radius * 2f, span, radius * 2f));
                    AddPart(root, GetSphere(), material, center + axis * span, Quaternion.identity, Vector3.one * radius * 2f);
                    AddPart(root, GetSphere(), material, center - axis * span, Quaternion.identity, Vector3.one * radius * 2f);
                    AddStand(board, root, material, cell, centerHeight, radius);
                    continue;
                }

                // A drain end draws its leg toward the regular pipe whether or not the valve is open.
                var isDrain = board.TryGetDrainTarget(cell, out var drainTarget);
                var drainOpen = isDrain && board.IsDrainOpen(cell);
                var drainStep = drainTarget - cell;
                var drainFlag = isDrain ? PipeShapes.GetConnectionDirection(drainStep) : PipeConnections.None;
                var drawn = connections | drainFlag;
                Transform splash = null;

                foreach (var (flag, direction) in Sides)
                {
                    if ((drawn & flag) == 0) continue;

                    // Each leg runs from the cell center just past the cell edge, so neighbors' legs meet.
                    // Off the board edge it carries on a whole cell, into what sits there. Into a regular
                    // channel (an open drain, or a splitter) it reaches over the wall and turns down into
                    // it; a closed drain stops at the cell edge.
                    var step = new Vector2Int(Mathf.RoundToInt(direction.x), Mathf.RoundToInt(direction.z));
                    var neighbor = cell + step;
                    var closedDrain = flag == drainFlag && !drainOpen;
                    var intoChannel = !closedDrain && board.HasPipe(neighbor, false) && !board.HasPipe(neighbor, true);
                    var length = !board.IsCellInBounds(neighbor) ? pitch
                        : intoChannel ? pitch * DownspoutReach
                        : closedDrain ? pitch * 0.45f
                        : pitch * 0.5f + radius * 0.2f;
                    AddPart(root, GetCylinder(), material, center + direction * (length * 0.5f),
                        Quaternion.FromToRotation(Vector3.up, direction), new Vector3(radius * 2f, length * 0.5f, radius * 2f));
                    if (intoChannel)
                    {
                        var bottom = AddDownspout(board, root, material, center + direction * length, radius);
                        if (flag == drainFlag) splash = AddSplash(root, GetPaint(material), bottom, radius);
                    }
                }

                if (isDrain)
                    AddValve(board, root, material, cell, center, new Vector3(drainStep.x, 0f, drainStep.y), radius,
                        drainOpen, splash);

                if (PipeShapes.IsStraight(drawn))
                {
                    var axis = (drawn & PipeConnections.East) != 0 ? Vector3.right : Vector3.forward;
                    AddPart(root, GetCylinder(), material, center, Quaternion.FromToRotation(Vector3.up, axis),
                        new Vector3(radius * 2.6f, pitch * 0.05f, radius * 2.6f));
                }
                else
                {
                    // A bend, junction or dead end: a round joint closes the corner.
                    var joint = radius * (PipeShapes.CountConnections(drawn) <= 1 ? 2f : 2.3f);
                    AddPart(root, GetSphere(), material, center, Quaternion.identity, Vector3.one * joint);
                }

                AddStand(board, root, material, cell, centerHeight, radius);
            }
        }

        /// <summary>An elbow over the channel and a short drop down to the water in it; returns where it lands.</summary>
        private static Vector3 AddDownspout(PathBuildBoard board, Transform root, Material material, Vector3 elbow,
            float radius)
        {
            AddPart(root, GetSphere(), material, elbow, Quaternion.identity, Vector3.one * radius * 2f);
            var bottom = new Vector3(elbow.x, board.ChannelFlowHeight, elbow.z);
            var drop = elbow.y - bottom.y;
            if (drop > 0f)
                AddPart(root, GetCylinder(), material, new Vector3(elbow.x, bottom.y + drop * 0.5f, elbow.z),
                    Quaternion.identity, new Vector3(radius * 2f, drop * 0.5f, radius * 2f));
            return bottom;
        }

        /// <summary>Ripples where an open drain pours into the channel; <see cref="DrainValve" /> animates them.</summary>
        private static Transform AddSplash(Transform root, Material material, Vector3 landing, float radius)
        {
            var splash = new GameObject("Splash") { layer = root.gameObject.layer }.transform;
            splash.SetParent(root, false);
            splash.localPosition = landing;
            for (var i = 0; i < 2; i++)
                AddPart(splash, GetCylinder(), material, Vector3.zero, Quaternion.identity,
                    new Vector3(radius * 3f, radius * 0.08f, radius * 3f));
            return splash;
        }

        /// <summary>
        ///     A ball valve on the leg toward the regular pipe: a collar, a stem, and a big lever the
        ///     player clicks. Closed, the lever lies across the pipe and a cap seals the end; open, it
        ///     lies along the pipe. <see cref="DrainValve" /> colors it and handles the click.
        /// </summary>
        private static void AddValve(PathBuildBoard board, Transform root, Material material, Vector2Int cell,
            Vector3 center, Vector3 toward, float radius, bool open, Transform splash)
        {
            var pitch = board.PipeCellPitch;
            var valve = new GameObject("Drain Valve") { layer = root.gameObject.layer }.transform;
            valve.SetParent(root, false);
            valve.SetLocalPositionAndRotation(center + toward * (pitch * 0.25f), Quaternion.LookRotation(toward));

            AddPart(valve, GetCylinder(), material, Vector3.zero, Quaternion.FromToRotation(Vector3.up, Vector3.forward),
                new Vector3(radius * 2.8f, pitch * 0.07f, radius * 2.8f));
            var stemHeight = radius * 2.2f;
            AddPart(valve, GetCylinder(), material, Vector3.up * (stemHeight * 0.5f), Quaternion.identity,
                new Vector3(radius * 0.7f, stemHeight * 0.5f, radius * 0.7f));

            // The lever pivots on the stem and reaches mostly to one side, like a real ball valve.
            var leverLength = pitch * 0.6f;
            var lever = new GameObject("Lever") { layer = root.gameObject.layer }.transform;
            lever.SetParent(valve, false);
            lever.SetLocalPositionAndRotation(Vector3.up * stemHeight,
                open ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f));
            var paint = GetPaint(material);
            var colored = new List<Renderer>
            {
                AddPart(lever, GetCube(), paint, Vector3.forward * (leverLength * 0.35f), Quaternion.identity,
                    new Vector3(radius * 1.1f, radius * 0.6f, leverLength)),
                AddPart(lever, GetSphere(), paint, Vector3.zero, Quaternion.identity, Vector3.one * radius * 1.5f)
            };

            if (!open)
                colored.Add(AddPart(root, GetCylinder(), paint, center + toward * (pitch * 0.45f),
                    Quaternion.FromToRotation(Vector3.up, toward), new Vector3(radius * 2.6f, radius * 0.25f, radius * 2.6f)));

            // A generous click target around the whole valve.
            var hitBox = valve.gameObject.AddComponent<BoxCollider>();
            hitBox.center = Vector3.up * (stemHeight * 0.6f);
            hitBox.size = new Vector3(pitch * 0.7f, stemHeight * 2f + radius * 2f, pitch * 0.5f);

            valve.gameObject.AddComponent<DrainValve>().Init(board, cell, open, colored, lever, splash);
        }

        // Stands hold the tube clear of the board; over regular pipe it bridges across instead.
        private static void AddStand(PathBuildBoard board, Transform root, Material material, Vector2Int cell,
            float centerHeight, float radius)
        {
            if (board.HasPipe(cell, false)) return;

            var top = centerHeight - radius;
            var bottom = board.CellHeight * 0.5f;
            if (top > bottom)
                AddPart(root, GetCylinder(), material,
                    board.GetLocalPosition(cell.x, cell.y) + Vector3.up * ((top + bottom) * 0.5f),
                    Quaternion.identity, new Vector3(radius * 0.7f, (top - bottom) * 0.5f, radius * 0.7f));
        }

        /// <summary>Removes the tube from a visual that now shows channel tiles, or nothing.</summary>
        public static void Clear(GameObject visual)
        {
            var root = visual.transform.Find(RootName);
            if (!root) return;

            root.SetParent(null, false);
            PathBuildBoard.DestroyObject(root.gameObject);
        }

        private static readonly (PipeConnections flag, Vector3 direction)[] Sides =
        {
            (PipeConnections.East, Vector3.right),
            (PipeConnections.North, Vector3.forward),
            (PipeConnections.West, Vector3.left),
            (PipeConnections.South, Vector3.back)
        };

        private static Transform GetRoot(Transform visual)
        {
            var root = visual.Find(RootName);
            if (root) return root;

            root = new GameObject(RootName) { layer = visual.gameObject.layer }.transform;
            root.SetParent(visual, false);
            return root;
        }

        private static void ClearChildren(Transform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                // Detach first: Destroy waits for the end of the frame, and the visual is recolored now.
                child.SetParent(null, false);
                PathBuildBoard.DestroyObject(child.gameObject);
            }
        }

        private static Renderer AddPart(Transform root, Mesh mesh, Material material, Vector3 localPosition,
            Quaternion localRotation, Vector3 localScale)
        {
            var part = new GameObject("Part") { layer = root.gameObject.layer };
            part.transform.SetParent(root, false);
            part.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            part.transform.localScale = localScale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private static Mesh GetCylinder() => _cylinder ? _cylinder : _cylinder = BorrowMesh(PrimitiveType.Cylinder);

        internal static Mesh GetSphere() => _sphere ? _sphere : _sphere = BorrowMesh(PrimitiveType.Sphere);

        private static Mesh GetCube() => _cube ? _cube : _cube = BorrowMesh(PrimitiveType.Cube);

        private static Mesh BorrowMesh(PrimitiveType type)
        {
            var primitive = GameObject.CreatePrimitive(type);
            var mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(primitive);
            return mesh;
        }

        /// <summary>
        ///     A bare, polished copy of the board's pipe material: same shader, no brick texture, so the
        ///     tube reads as metal next to the channels. The tint comes from the board's color.
        /// </summary>
        internal static Material GetMetal(PathBuildBoard board)
        {
            var library = board.PipeLibrary;
            var source = board.FallbackPipeMaterial ? board.FallbackPipeMaterial
                : library ? library.SurfaceMaterial : null;
            if (_metal && _metalSource == source) return _metal;

            _metalSource = source;
            _metal = source ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _metal.name = "Isolated Pipe Metal";
            foreach (var texture in new[] { "_BaseMap", "_MainTex", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" })
                if (_metal.HasProperty(texture))
                    _metal.SetTexture(texture, null);
            if (_metal.HasProperty("_Metallic")) _metal.SetFloat("_Metallic", 0.85f);
            if (_metal.HasProperty("_Smoothness")) _metal.SetFloat("_Smoothness", 0.7f);
            if (_metal.HasProperty("_Glossiness")) _metal.SetFloat("_Glossiness", 0.7f);
            return _metal;
        }

        /// <summary>A matte copy of the metal for valve levers and caps, so their colors read bright.</summary>
        private static Material GetPaint(Material metal)
        {
            if (_paint && _paintSource == metal) return _paint;

            _paintSource = metal;
            _paint = new Material(metal) { name = "Drain Valve Paint" };
            if (_paint.HasProperty("_Metallic")) _paint.SetFloat("_Metallic", 0f);
            if (_paint.HasProperty("_Smoothness")) _paint.SetFloat("_Smoothness", 0.35f);
            if (_paint.HasProperty("_Glossiness")) _paint.SetFloat("_Glossiness", 0.35f);
            return _paint;
        }
    }
}
