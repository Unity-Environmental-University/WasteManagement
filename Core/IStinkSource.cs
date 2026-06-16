using System.Collections.Generic;
using UnityEngine;

namespace _project.Scripts.Core
{
    public interface IStinkSource
    {
        float CurrentStink { get; }
    }

    public static class StinkSourceRegistry
    {
        private static readonly HashSet<IStinkSource> Sources = new();

        public static void Register(IStinkSource source)
        {
            if (source != null)
                Sources.Add(source);
        }

        public static void Unregister(IStinkSource source)
        {
            if (source != null)
                Sources.Remove(source);
        }

        public static float GetCurrentStink(float baseStink)
        {
            var totalStink = baseStink;
            foreach (var source in Sources)
                totalStink += source.CurrentStink;

            return Mathf.Max(0f, totalStink);
        }
    }
}
