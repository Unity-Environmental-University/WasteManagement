using System;
using _project.Scripts.UI;
using UnityEngine;
using UnityEngine.UI;

namespace _project.Scripts.Object_Scripts
{
    public class PipelineComponentManager : MonoBehaviour
    {
        public GameObject wasteSifter;
        public Transform sifterSpawnLocation1;
        public Transform sifterSpawnLocation2;
        public Slider spawn1Bar;
        public Slider spawn2Bar;

        public LocationHealthBarRelation spawn1;
        public LocationHealthBarRelation spawn2;

        private void Awake()
        {
            spawn1.spawnLocation = sifterSpawnLocation1;
            spawn1.healthBar = spawn1Bar;
            spawn2.spawnLocation = sifterSpawnLocation2;
            spawn2.healthBar = spawn2Bar;
        }

        public void SpawnOpeningSifter()
        {
            var sifter = Instantiate(wasteSifter, spawn1.spawnLocation);
            var siftCont = sifter.GetComponent<WasteSifter>();
            AssignHealthBar(sifter, spawn1.healthBar);
            siftCont.SetHealth(siftCont.maxHealth / 2);
        }

        public void SpawnSifter(int side)
        {
            var l = side switch
            {
                1 => spawn1,
                2 => spawn2,
                _ => spawn1
            };

            var s = Instantiate(wasteSifter, l.spawnLocation);
            AssignHealthBar(s,l.healthBar);
        }

        public void AssignHealthBar(GameObject sifter, Slider healthBar)
        {
            var cont = sifter.GetComponent<WasteSifter>();
            var hBar = healthBar.GetComponent<HealthBar>();
            cont.healthBar = hBar;
        }
        
        [Serializable]
        public struct LocationHealthBarRelation
        {
            public Transform spawnLocation;
            public Slider healthBar;
        }
    }
}