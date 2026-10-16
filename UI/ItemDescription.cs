using TMPro;
using UnityEngine;

namespace _project.Scripts.UI
{
    public class ItemDescription : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI itemDescriptionText;

        public void SetItemDescription(string itemName, string itemDescription)
        {
            itemNameText.text = itemName;
            itemDescriptionText.text = itemDescription;
        }
    }
}
