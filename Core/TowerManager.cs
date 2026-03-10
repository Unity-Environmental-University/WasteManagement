using System.Collections.Generic;
using UnityEngine;

namespace _project.Scripts.Core
{
    public class TowerManager : MonoBehaviour
    {
        public List<TowerController> towers;

        private void Start()
        {
            // Find our towers
            var T = GameObject.FindGameObjectsWithTag("Tower");
            foreach (var tower in T) towers.Add(gameObject.GetComponent<TowerController>()); ;
        }
    }
}