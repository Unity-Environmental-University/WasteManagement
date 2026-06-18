using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Preserves the current speed buff/debuff behavior as a replaceable effect option.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Move Speed Buff Debuff Effect",
        menuName = "Waste Management/Buff Debuff Effects/Move Speed")]
    public class MoveSpeedBuffDebuffTileEffect : BuffDebuffTileEffect
    {
        [Tooltip("Absolute move speed applied to an issue that crosses a buff tile.")]
        [SerializeField] private float buffMoveSpeed = 4f;

        [Tooltip("Absolute move speed applied to an issue that crosses a debuff tile.")]
        [SerializeField] private float debuffMoveSpeed = 1f;

        public override void Apply(BuffDebuffEffectContext context)
        {
            context.Issue.SetMoveSpeed(context.Kind == BuffDebuffKind.Buff ? buffMoveSpeed : debuffMoveSpeed);
        }
    }
}
