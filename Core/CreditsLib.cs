using System;
using UnityEngine;

namespace _project.Scripts.Core
{
    [CreateAssetMenu(fileName = "CreditsLibrary", menuName = "WasteManagement/Credits Library")]
    public class CreditsLib : ScriptableObject
    {
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        [Serializable]
        public struct Entry
        {
            public string credited;
            public string creditedAsset;
            public string creditType;
        }
    }
}