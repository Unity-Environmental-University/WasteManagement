using System;
using System.Collections.Generic;
using UnityEngine;

namespace _project.Scripts.Core
{
    /// <summary>
    ///     Temporary storage for purchased placeables. Items stay here until they are placed,
    ///     while one item at a time can be selected as the active placement target.
    /// </summary>
    public class PlacementInventory : MonoBehaviour
    {
        private readonly List<IPlaceable> _items = new();
        private int _selectedIndex = -1;

        public event Action InventoryChanged;
        public event Action<IPlaceable> SelectionChanged;

        /// <summary>
        ///     Full set of purchased placeables waiting to be placed.
        /// </summary>
        public IReadOnlyList<IPlaceable> Items => _items;

        /// <summary>
        ///     Index of the currently selected placeable, or -1 when nothing is selected.
        /// </summary>
        public int SelectedIndex => SelectedItem == null ? -1 : _selectedIndex;

        /// <summary>
        ///     The placeable currently armed for placement.
        /// </summary>
        public IPlaceable SelectedItem =>
            _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;

        /// <summary>
        ///     Adds newly purchased placeable. The first stored item is auto-selected, so placement
        ///     can begin immediately without an extra UI.
        /// </summary>
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
        ///     Selects a specific stored item. Intended for future inventory UI.
        /// </summary>
        public bool SelectItem(int index)
        {
            if (index < 0 || index >= _items.Count) return false;
            if (_selectedIndex == index) return true;

            _selectedIndex = index;
            SelectionChanged?.Invoke(SelectedItem);
            return true;
        }

        /// <summary>
        ///     Restores selection after placement has been disabled between phases.
        /// </summary>
        public bool SelectFirstAvailable()
        {
            if (SelectedItem != null) return true;
            if (_items.Count == 0) return false;

            _selectedIndex = 0;
            SelectionChanged?.Invoke(SelectedItem);
            return true;
        }

        /// <summary>
        ///     Clears the active selection without removing any stored placeables.
        /// </summary>
        public void ClearSelection()
        {
            if (_selectedIndex < 0) return;

            _selectedIndex = -1;
            SelectionChanged?.Invoke(null);
        }

        /// <summary>
        ///     Removes the currently selected item after it has been placed, then keeps the selection
        ///     pointing at a valid remaining item when possible.
        /// </summary>
        public IPlaceable ConsumeSelected()
        {
            var selectedItem = SelectedItem;
            if (selectedItem == null) return null;

            _items.RemoveAt(_selectedIndex);

            // After consuming an item, keep selection valid so repeated placements can continue.
            if (_items.Count == 0)
                _selectedIndex = -1;
            else if (_selectedIndex >= _items.Count)
                _selectedIndex = _items.Count - 1;

            InventoryChanged?.Invoke();
            SelectionChanged?.Invoke(SelectedItem);
            return selectedItem;
        }
    }
}
