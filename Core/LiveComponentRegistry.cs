using System;
using System.Collections.Generic;
using UnityEngine;

namespace _project.Scripts.Core
{
    /// <summary>
    ///     Static registry of live (enabled and active) components, maintained from each
    ///     component's OnEnable/OnDisable. Replaces periodic FindObjectsByType scene scans
    ///     for gameplay types that are observed continuously (e.g., by
    ///     WasteBoardReplayRecorder's subject discovery), the same way PathSplitter.Live and
    ///     StinkSourceRegistry already track their own instances.
    ///     Entries are keyed by the component's concrete type: GetLive&lt;T&gt; returns only
    ///     components registered as exactly T, not subclasses. Like StinkSourceRegistry, the
    ///     registry is a process-wide static state, but OnDisable fires on scene teardown, so
    ///     lists empty themselves across reloads.
    /// </summary>
    public static class LiveComponentRegistry
    {
        private static readonly Dictionary<Type, object> Lists = new();

        public static void Register<T>(T component) where T : Component
        {
            if (!component) return;

            var list = GetList<T>();
            if (!list.Contains(component))
                list.Add(component);
        }

        public static void Unregister<T>(T component) where T : Component
        {
            if (Lists.TryGetValue(typeof(T), out var list))
                ((List<T>)list).Remove(component);
        }

        public static IReadOnlyList<T> GetLive<T>() where T : Component
        {
            return GetList<T>();
        }

        private static List<T> GetList<T>() where T : Component
        {
            if (Lists.TryGetValue(typeof(T), out var existing))
                return (List<T>)existing;

            var created = new List<T>();
            Lists[typeof(T)] = created;
            return created;
        }
    }
}