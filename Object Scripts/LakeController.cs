using _project.Scripts.Core;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    public class LakeController : MonoBehaviour, IStinkSource
    {
        private const float BaseHealth = 100f;
        public float health;
        [SerializeField] private float healthRecoveredPerTurn = 2f;

        [Header("Stink")]
        [SerializeField] private float stinkPerPollution = 0.05f;

        [SerializeField] private Material mat;
        private Color _originalColor;

        public float CurrentStink => Mathf.Max(0f, BaseHealth - health) * stinkPerPollution;

        public void RecoverForTurn()
        {
            if (healthRecoveredPerTurn <= 0f || health >= BaseHealth) return;

            health = Mathf.Min(BaseHealth, health + healthRecoveredPerTurn);
            UpdateLakeColor();
            GameMaster.Instance?.interfaceManager?.RefreshStinkMeter();
        }
        
        private void OnEnable()
        {
            StinkSourceRegistry.Register(this);
            IssueObject.OnReachedEnd += OnIssueReachedEnd;
        }

        private void OnDisable()
        {
            IssueObject.OnReachedEnd -= OnIssueReachedEnd;
            StinkSourceRegistry.Unregister(this);
        }

        private void OnIssueReachedEnd(IssueObject issue)
        {
            var damage = issue.ProcessCost * 5;
            health -= damage;
            UpdateLakeColor();

            var popManager = GameMaster.Instance ? GameMaster.Instance.popManager : null;
            if (popManager) popManager.RecordLakePollution(issue.ProcessCost);
            GameMaster.Instance?.interfaceManager?.RefreshStinkMeter();
            
            if (health <= 0) GameMaster.Instance.turnController.GameLost();
        }
        
        private void Awake()
        {
            /*
            Store original color,
            make new mat that's a copy of the original,
            set the material to the copy.
            */
            var lakeRenderer = GetComponent<Renderer>();
            if (!mat && lakeRenderer) mat = lakeRenderer.sharedMaterial;
            health = BaseHealth;
            if (!mat) return;

            _originalColor = mat.color;
            mat = new Material(mat);
            lakeRenderer.material = mat;
            UpdateLakeColor();
        }

        private void UpdateLakeColor()
        {
            if (!mat) return;

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
