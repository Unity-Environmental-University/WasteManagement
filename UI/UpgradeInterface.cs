using System;
using _project.Scripts.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _project.Scripts.UI
{
    public class UpgradeInterface : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private Image highlight;
        [SerializeField] private TowerController towerController;

        private void Start()
        {
            if (towerController == null) Debug.LogError("Tower Controller not found!");
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            highlight.gameObject.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            highlight.gameObject.SetActive(false);
        }
        
        public void OnPointerClick(PointerEventData eventData)
        {
            towerController.AddUpgrade(GameMaster.Instance.selectedCard.interFaceCard);
        }
    }
}