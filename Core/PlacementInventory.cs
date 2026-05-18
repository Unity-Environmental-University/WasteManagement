using System;
using System.Collections.Generic;
using UnityEngine;

namespace _project.Scripts.Core
{
    public class PlacementInventory : MonoBehaviour
    {
        private readonly List<IPlaceable> _items = new();
        private int _selectedIndex = -1;


        public IReadOnlyList<IPlaceable> Items => _items;

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


        public bool SelectItem(int index)
        {
            if (index < 0 || index >= _items.Count) return false;
            if (_selectedIndex == index) return true;

            _selectedIndex = index;
            SelectionChanged?.Invoke(SelectedItem);
            return true;
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
            if (_selectedIndex < 0) return;

            _selectedIndex = -1;
            SelectionChanged?.Invoke(null);
        }


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