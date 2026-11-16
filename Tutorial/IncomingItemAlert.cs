using _project.Scripts.Object_Scripts;
using UnityEngine;

namespace _project.Scripts.Tutorial
{
    /// <summary>
    ///     A front-page story for the newspaper popup. Add one asset per new item type; the final
    ///     goal's announcement and win stories use the same asset without the sort demo.
    /// </summary>
    [CreateAssetMenu(fileName = "Alert", menuName = "WasteManagement/Incoming Item Alert", order = 2)]
    public class IncomingItemAlert : ScriptableObject
    {
        [Tooltip("The new item type. Stories that are not item warnings, like the final goal, can leave it.")]
        public IssueType type;
        public string title;
        public string subhead;
        [TextArea(3, 8)] public string body;
        [TextArea(1, 4)] public string caption;

        [Tooltip("Show the animated diagram of a splitter sorting this item out of the waste.")]
        public bool showSortDemo;
    }
}
