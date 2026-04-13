using System.Collections.Generic;
using _project.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;

namespace _project.Scripts.UI
{
    public class InterfaceManager : MonoBehaviour
    {
        [SerializeField] private Button quitButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button openShopButton;
        [SerializeField] private Button closeShopButton;
        [SerializeField] private Image mTowerUpgrades;
        [SerializeField] private Image rTowerUpgrades;
        [SerializeField] private Image lTowerUpgrades;

        [Header("Hand")]
        [SerializeField] private Transform handContainer;
        [SerializeField] private CardController cardPrefab;
        [SerializeField] private float cardSpacing = 200f;

        private void Start()
        {
            quitButton.onClick.AddListener(Application.Quit);
        }

        public void PopulateHand(IReadOnlyList<ICard> hand)
        {
            ClearHand();
            var count = hand.Count;
            var totalWidth = (count - 1) * cardSpacing;

            for (var i = 0; i < count; i++)
            {
                var cardController = Instantiate(cardPrefab, handContainer);
                cardController.AssignCard(hand[i]);

                var rt = cardController.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(-totalWidth / 2f + i * cardSpacing, 0f);
            }
        }

        public void HidePrepUI()
        {
            lTowerUpgrades.gameObject.SetActive(false);
            mTowerUpgrades.gameObject.SetActive(false);
            rTowerUpgrades.gameObject.SetActive(false);
            nextButton.gameObject.SetActive(false);
            openShopButton.gameObject.SetActive(false);
            closeShopButton.gameObject.SetActive(false);
        }

        public void ShowPrepUI()
        {
            /* disabled for testing
            lTowerUpgrades.gameObject.SetActive(true);
            mTowerUpgrades.gameObject.SetActive(true);
            rTowerUpgrades.gameObject.SetActive(true);
            */
            nextButton.gameObject.SetActive(true);
            openShopButton.gameObject.SetActive(true);
            closeShopButton.gameObject.SetActive(true);
        }

        public void NextButtonPressed()
        {
            if (GameMaster.Instance.turnController.currentPhase == GamePhase.Tower) return;
            GameMaster.Instance.turnController.EndPhase();
        }

        public void ClearHand()
        {
            for (var i = handContainer.childCount - 1; i >= 0; i--)
                Destroy(handContainer.GetChild(i).gameObject);
        }

        public void HideUIForShop()
        {
            quitButton.gameObject.SetActive(false);
            nextButton.gameObject.SetActive(false);
            openShopButton.gameObject.SetActive(false);
            closeShopButton.gameObject.SetActive(true);
        }

        public void RecoverUIForShop()
        {
            quitButton.gameObject.SetActive(true);
            nextButton.gameObject.SetActive(true);
            openShopButton.gameObject.SetActive(true);
            closeShopButton.gameObject.SetActive(false);
        }
    }
}
