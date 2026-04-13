using System.Collections.Generic;
using UnityEngine;

namespace _project.Scripts.Core
{
    public class TowerManager : MonoBehaviour
    {
        public List<TowerController> towers = new();

        private void Start()
        {
            // Find our towers
            var T = GameObject.FindGameObjectsWithTag("Tower");
            foreach (var tower in T) towers.Add(tower.GetComponent<TowerController>());
        }

        public void RegisterTower(TowerController tc)
        {
            if (tc && !towers.Contains(tc)) towers.Add(tc);
        }
    }
}