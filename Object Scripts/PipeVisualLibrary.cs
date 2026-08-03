using UnityEngine;
using UnityEngine.Serialization;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Runtime references for the modular pipe FBX assets. Keeping the references in a
    ///     Resources asset lets existing PathBuildBoard instances pick up the new art without
    ///     requiring every scene and prefab instance to be re-authored.
    /// </summary>
    [CreateAssetMenu(fileName = ResourceName, menuName = "Waste Management/Pipe Visual Library")]
    public sealed class PipeVisualLibrary : ScriptableObject
    {
        public const string ResourceName = "PipeVisualLibrary";

        [SerializeField] private GameObject straightPipe;
        [SerializeField] private GameObject cornerPipe;

        [Tooltip("Three-way junction (brick_TPipe). Falls back to the four-way mesh when unassigned.")]
        [FormerlySerializedAs("endPipe")]
        [SerializeField]
        private GameObject tJunctionPipe;

        [Tooltip("Four-way junction (brick_plusPipe). Only used where all four sides connect.")]
        [SerializeField]
        private GameObject junctionPipe;

        [Tooltip("Material applied to the imported models so their environment texture sheet is retained.")]
        [SerializeField]
        private Material surfaceMaterial;

        public GameObject StraightPipe => straightPipe;
        public GameObject CornerPipe => cornerPipe;

        /// <summary>Three-way mesh, falling back to the four-way when the set has no dedicated tee.</summary>
        public GameObject TJunctionPipe => tJunctionPipe ? tJunctionPipe : junctionPipe;

        public GameObject JunctionPipe => junctionPipe;
        public Material SurfaceMaterial => surfaceMaterial;

        public static PipeVisualLibrary Load()
        {
            return Resources.Load<PipeVisualLibrary>(ResourceName);
        }
    }
}
