using _project.Scripts.Core;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     A placed utility the remove tool can take back off the board. Utilities holding
    ///     fullness or carrying damage report <see cref="CanRemove" /> false and stay put.
    /// </summary>
    public interface IRemovableUtility
    {
        bool CanRemove { get; }

        /// <summary>Called by <see cref="SpecialInteractController" /> when this utility is placed.</summary>
        void SetSlot(SpecialInteractController slot, int infraValue = 0);

        /// <summary>Frees the utility's slot, refunds its infrastructure value, and destroys it.</summary>
        void Remove();
    }

    public static class UtilityRemoval
    {
        /// <summary>
        ///     Removes <paramref name="utility" /> if the remove tool is armed during the Card phase
        ///     and the utility allows it. Counts as a move, like removing a pipe.
        /// </summary>
        public static bool TryRemove(IRemovableUtility utility)
        {
            var gm = GameMaster.Instance;
            var board = gm ? gm.pathBuildBoard : null;
            var turnController = gm ? gm.turnController : null;
            if (utility == null || !board || !turnController || board.ActiveTool != PathBuildTool.Break)
                return false;
            if (turnController.currentPhase != GamePhase.Card || !utility.CanRemove)
                return false;

            utility.Remove();
            turnController.RegisterMove();
            gm.interfaceManager?.RefreshStinkMeter();
            return true;
        }

        /// <summary>The removable splitter standing on the board cell at <paramref name="cell" />, if any.</summary>
        public static PathSplitter FindSplitterAt(PathBuildBoard board, UnityEngine.Vector2Int cell)
        {
            foreach (var splitter in PathSplitter.Live)
                if (board.TryWorldToCell(splitter.transform.position, out var splitterCell) && splitterCell == cell)
                    return splitter;
            return null;
        }
    }
}
