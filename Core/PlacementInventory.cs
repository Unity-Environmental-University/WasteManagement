using System;
using System.Collections.Generic;
using UnityEngine;

namespace _project.Scripts.Core
{
    public class PlacementInventory : MonoBehaviour
    {
        private readonly List<IPlaceable> _items = new();
        private int _selectedIndex = -1;

        // A persistent tool (armed from the shop palette) has infinite supply: it is not consumed
        // on placement, so the player keeps placing copies until they disarm it or pick another
        // tool — mirroring the pipe tools on the PathBuildBoard. Items added via Add() are the old
        // one-shot placements and remain consumed on placement.
        private bool _persistentTool;


        public IReadOnlyList<IPlaceable> Items => _items;

        /// <summary>True while a persistent, infinite-supply tool is armed as the current selection.</summary>
        public bool HasPersistentTool => _persistentTool && SelectedItem != null;

        public int SelectedIndex => SelectedItem == null ? -1 : _selectedIndex;


        public IPlaceable SelectedItem =>
            _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;

        public event Action InventoryChanged;
        public event Action<IPlaceable> SelectionChanged;


        public void Add(IPlaceable item)
        {
            if (item == null) return;

            _items.Add(item);
            InventoryChanged?.Invoke();

            if (_selectedIndex >= 0) return;
            _selectedIndex = _items.Count - 1;
            SelectionChanged?.Invoke(SelectedItem);
        }

        /// <summary>
        ///     Arms a single placeable as a persistent, infinite-supply tool (the shop palette's model).
        ///     Replaces any current selection; the tool stays armed across placements until it is
        ///     disarmed (<see cref="ClearSelection" />) or another tool is armed.
        /// </summary>
        public void SetActiveTool(IPlaceable item)
        {
            _items.Clear();
            _selectedIndex = -1;
            _persistentTool = false;

            if (item != null)
            {
                _items.Add(item);
                _selectedIndex = 0;
                _persistentTool = true;
            }

            InventoryChanged?.Invoke();
            SelectionChanged?.Invoke(SelectedItem);
        }


        public bool SelectItem(int index)
        {
            if (index < 0 || index >= _items.Count) return false;
            if (_selectedIndex == index) return true;

            _selectedIndex = index;
            SelectionChanged?.Invoke(SelectedItem);
            return true;
        }

        public bool SelectItem(IPlaceable item)
        {
            return item != null && SelectItem(_items.IndexOf(item));
        }

        public bool Contains(IPlaceable item)
        {
            return item != null && _items.Contains(item);
        }

        public bool SelectFirstAvailable()
        {
            if (SelectedItem != null) return true;
            if (_items.Count == 0) return false;

            _selectedIndex = 0;
            SelectionChanged?.Invoke(SelectedItem);
            return true;
        }


        public void ClearSelection()
        {
            var wasPersistent = _persistentTool;
            _persistentTool = false;

            // A persistent tool keeps its single entry in _items while armed; disarming must drop
            // it too so the palette fully clears (there is no queue to fall back to).
            if (wasPersistent)
            {
                _items.Clear();
                _selectedIndex = -1;
                InventoryChanged?.Invoke();
                SelectionChanged?.Invoke(null);
                return;
            }

            if (_selectedIndex < 0) return;

            _selectedIndex = -1;
            SelectionChanged?.Invoke(null);
        }

        public void Clear()
        {
            _persistentTool = false;
            if (_items.Count == 0 && _selectedIndex < 0) return;

            _items.Clear();
            _selectedIndex = -1;
            InventoryChanged?.Invoke();
            SelectionChanged?.Invoke(null);
        }


        public IPlaceable ConsumeSelected()
        {
            var selectedItem = SelectedItem;
            if (selectedItem == null) return null;

            // Persistent tools have infinite supply: report the placement so the caller can register
            // the move and infra cost, but keep the tool armed so the next placement needs no reselect.
            if (_persistentTool) return selectedItem;

            _items.RemoveAt(_selectedIndex);
            _selectedIndex = -1;

            InventoryChanged?.Invoke();
            SelectionChanged?.Invoke(null);
            return selectedItem;
        }
    }
}
