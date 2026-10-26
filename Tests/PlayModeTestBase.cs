using System.Collections.Generic;
using System.Reflection;
using _project.Scripts.Core;
using _project.Scripts.Object_Scripts;
using NUnit.Framework;
using UnityEngine;

namespace _project.Scripts.Tests
{
    /// <summary>
    ///     Shared scaffolding for the PlayMode suite: tracks every object a test creates so it is
    ///     destroyed afterward, and wraps the reflection used to reach serialized private state.
    /// </summary>
    public abstract class PlayModeTestBase
    {
        private const BindingFlags InstanceFields =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly List<Object> _created = new();

        [TearDown]
        public void DestroyCreatedObjects()
        {
            // Newest first, so dependents go before the objects they were built on.
            for (var i = _created.Count - 1; i >= 0; i--)
                if (_created[i])
                    Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        protected T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        protected GameObject CreateGameObject(string name) => Track(new GameObject(name));

        protected GameObject CreatePrimitive(string name, Transform parent = null)
        {
            var primitive = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            return primitive;
        }

        protected static void SetField(object target, string fieldName, object value) =>
            Field(target, fieldName).SetValue(target, value);

        protected static T GetField<T>(object target, string fieldName) =>
            (T)Field(target, fieldName).GetValue(target);

        protected static PathBuildCell GetCell(PathBuildBoard board, int column, int row)
        {
            var child = board.transform.Find($"Path Cell {column},{row}");
            Assert.IsTrue(child, $"Missing generated cell {column},{row}");
            return child.GetComponent<PathBuildCell>();
        }

        protected static PathPiecePlaceable Pipe(int length, bool vertical, PathKind kind = PathKind.Pipe,
            bool isolated = false)
        {
            var piece = new PathPiecePlaceable("Pipe", string.Empty, 1, length, null, 0, kind, isolated);
            if (vertical) piece.ToggleOrientation();
            return piece;
        }

        protected static void PlaceVertical(PathBuildBoard board, int column, int row, int length,
            bool isolated = false) =>
            Assert.IsNotNull(board.TryPlace(GetCell(board, column, row), Pipe(length, true, isolated: isolated)));

        protected static void PlaceHorizontal(PathBuildBoard board, int column, int row, int length,
            bool isolated = false) =>
            Assert.IsNotNull(board.TryPlace(GetCell(board, column, row), Pipe(length, false, isolated: isolated)));

        private static FieldInfo Field(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, InstanceFields);
            Assert.IsNotNull(field, $"Missing field {fieldName} on {target.GetType().Name}");
            return field;
        }
    }
}
