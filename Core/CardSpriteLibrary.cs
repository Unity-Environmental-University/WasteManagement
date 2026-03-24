using System;
using System.Collections.Generic;
using UnityEngine;

namespace _project.Scripts.Core
{
    [CreateAssetMenu(fileName = "CardSpriteLibrary", menuName = "WasteManagement/Card Sprite Library")]
    public class CardSpriteLibrary : ScriptableObject
    {
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        private Dictionary<string, Sprite> _lookup;

        public Sprite GetSprite(string cardName)
        {
            if (_lookup != null) return _lookup.GetValueOrDefault(cardName);
            _lookup = new Dictionary<string, Sprite>(entries.Length);
            foreach (var e in entries)
                if (!string.IsNullOrEmpty(e.cardName))
                    _lookup[e.cardName] = e.sprite;

            return _lookup.GetValueOrDefault(cardName);
        }

        [Serializable]
        public struct Entry
        {
            public string cardName;
            public Sprite sprite;
        }
    }
}