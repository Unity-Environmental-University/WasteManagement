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

        private void ClearHand()
        {
            for (var i = handContainer.childCount - 1; i >= 0; i--)
                Destroy(handContainer.GetChild(i).gameObject);
        }
    }
}
