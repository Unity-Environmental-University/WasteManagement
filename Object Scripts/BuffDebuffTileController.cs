using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    public enum BuffDebuffKind
    {
        Buff,
        Debuff
    }

    /// <summary>
    ///     A single buff/debuff tile sitting on one board cell. For now it only knows
    ///     whether it is a buff or a debuff and colors itself accordingly — there is no
    ///     gameplay effect yet. Effects will be layered on in a later step.
    /// </summary>
    public class BuffDebuffTileController : MonoBehaviour
    {
        [SerializeField] private BuffDebuffKind kind = BuffDebuffKind.Buff;
        [SerializeField] private Color buffColor = new(0.30f, 0.85f, 0.40f, 1f);
        [SerializeField] private Color debuffColor = new(0.85f, 0.30f, 0.30f, 1f);

        public BuffDebuffKind Kind => kind;

        private void Start()
        {
            ApplyColor();
        }

        public void SetKind(BuffDebuffKind newKind)
        {
            kind = newKind;
            ApplyColor();
        }

        private void ApplyColor()
        {
            var color = kind == BuffDebuffKind.Buff ? buffColor : debuffColor;
            foreach (var rend in GetComponentsInChildren<Renderer>(true))
                rend.material.color = color;
        }
    }
}
