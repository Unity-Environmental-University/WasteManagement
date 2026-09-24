using System.Collections.Generic;
using System.Reflection;
using _project.Scripts.Core;
using _project.Scripts.Object_Scripts;
using NUnit.Framework;
using UnityEngine;

namespace _project.Scripts.Tests
{
    public class WasteSifterTests
    {
        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
                if (go)
                    Object.DestroyImmediate(go);
            _created.Clear();
        }

        [Test]
        public void DisablingSifter_ReleasesHeldIssueAndAllowsRecapture()
        {
            var sifter = Create("Sifter").AddComponent<WasteSifter>();
            var issue = Create("Issue").AddComponent<IssueObject>();
            var sifterId = sifter.GetEntityId();
            Assert.IsTrue(issue.TryRegisterSifter(sifterId));
            issue.SetHeldBySifter(true);

            var heldIssuesField = typeof(WasteSifter).GetField("_heldIssues",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(heldIssuesField);
            ((HashSet<IssueObject>)heldIssuesField.GetValue(sifter)).Add(issue);

            sifter.gameObject.SetActive(false);

            var heldField = typeof(IssueObject).GetField("_heldBySifter",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(heldField);
            Assert.IsFalse((bool)heldField.GetValue(issue));
            Assert.IsTrue(issue.TryRegisterSifter(sifterId),
                "Re-enabling the same sifter must allow it to catch this issue again.");
        }

        private GameObject Create(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }
    }
}
