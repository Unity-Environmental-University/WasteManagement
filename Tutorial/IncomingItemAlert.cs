using _project.Scripts.Object_Scripts;
using UnityEngine;

namespace _project.Scripts.Tutorial
{
    /// <summary>The front-page story announcing one new item type. Add one asset per item.</summary>
    [CreateAssetMenu(fileName = "Alert", menuName = "WasteManagement/Incoming Item Alert", order = 2)]
    public class IncomingItemAlert : ScriptableObject
    {
        public IssueType type;
        public string title;
        public string subhead;
        [TextArea(3, 8)] public string body;
        [TextArea(1, 4)] public string caption;

        [Tooltip("Show the animated diagram of a splitter sorting this item out of the waste.")]
        public bool showSortDemo;
    }
}
