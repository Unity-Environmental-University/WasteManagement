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

        [Tooltip("Minimum number of path tiles the temporary speed change lasts.")]
        [SerializeField] [Min(1)] private int minimumTileDuration = 2;

        [Tooltip("Maximum number of path tiles the temporary speed change lasts (inclusive).")]
        [SerializeField] [Min(1)] private int maximumTileDuration = 3;

        public override void Apply(BuffDebuffEffectContext context)
        {
            var minimum = Mathf.Max(1, minimumTileDuration);
            var maximum = Mathf.Max(minimum, maximumTileDuration);
            var duration = Random.Range(minimum, maximum + 1);
            var speed = context.Kind == BuffDebuffKind.Buff ? buffMoveSpeed : debuffMoveSpeed;

            context.Issue.SetTemporaryMoveSpeed(speed, duration);
        }
    }
}
