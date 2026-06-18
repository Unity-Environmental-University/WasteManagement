using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    public readonly struct BuffDebuffEffectContext
    {
        public BuffDebuffEffectContext(BuffDebuffTileController tile, IssueObject issue)
        {
            Tile = tile;
            Issue = issue;
        }

        public BuffDebuffTileController Tile { get; }
        public IssueObject Issue { get; }
        public BuffDebuffKind Kind => Tile.Kind;
    }

    /// <summary>
    ///     Base component for any behavior a buff/debuff tile should apply when an issue
    ///     enters it. Create effect assets and add them to the spawner's effect options.
    /// </summary>
    public abstract class BuffDebuffTileEffect : ScriptableObject
    {
        public abstract void Apply(BuffDebuffEffectContext context);
    }
}
