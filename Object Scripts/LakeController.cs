using _project.Scripts.Core;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    public class LakeController : MonoBehaviour
    {
        private const float BaseHealth = 100f;
        public float health;

        [SerializeField] private Material mat;
        private Color _originalColor;
        
        private void OnEnable() => IssueObject.OnReachedEnd += OnIssueReachedEnd;
        private void OnDisable() => IssueObject.OnReachedEnd -= OnIssueReachedEnd;

        private void OnIssueReachedEnd(IssueObject issue)
        {
            health -= issue.ProcessCost * 5;
            UpdateLakeColor();
            
            if (health <= 0) GameMaster.Instance.turnController.GameLost();
        }
        
        private void Awake()
        {
            /*
            Store original color,
            make new mat that's a copy of the original,
            set the material to the copy.
            */
            _originalColor = mat.color;
            mat = new Material(mat);
            GetComponent<Renderer>().material = mat;
            health = BaseHealth;
            UpdateLakeColor();
        }

        private void UpdateLakeColor()
        {
            mat.color = health switch
            {
                < 30 => Color.saddleBrown,
                < 50 => Color.darkOliveGreen,
                < 80 => Color.yellowGreen,
                _ => _originalColor
            };
        }
    }
}