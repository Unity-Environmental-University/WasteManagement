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

        [Test]
        public void CloggingSifter_FlushesEveryHeldNonWasteDownTheLine()
        {
            var sifter = Create("Sifter").AddComponent<WasteSifter>();
            var first = CreateJunk("First Junk");
            var second = CreateJunk("Second Junk");
            sifter.maxDebrisAccumulation = first.SiftCost + second.SiftCost;

            Enter(sifter, first);
            Assert.IsTrue(IsHeld(first), "A sifter with room left should hold junk at its screen.");
            Assert.IsFalse(first.WasFlushedFromSifter);

            Enter(sifter, second);

            Assert.IsFalse(IsHeld(first), "Clogging the sifter should let go of junk it was already holding.");
            Assert.IsFalse(IsHeld(second), "The piece that clogs the sifter should be flushed with the rest.");
            Assert.IsTrue(first.WasFlushedFromSifter);
            Assert.IsTrue(second.WasFlushedFromSifter);
        }

        [Test]
        public void CloggedSifter_LetsLaterNonWasteThroughUnscreened()
        {
            var sifter = Create("Sifter").AddComponent<WasteSifter>();
            var clog = CreateJunk("Clog");
            var late = CreateJunk("Late Junk");
            sifter.maxDebrisAccumulation = clog.SiftCost;
            Enter(sifter, clog);

            Enter(sifter, late);

            Assert.IsFalse(IsHeld(late), "An open sifter has nothing left to stop junk with.");
            Assert.IsTrue(late.WasFlushedFromSifter);
        }

        [Test]
        public void Cesspit_TakesFarMoreDamageFromFlushedJunk()
        {
            var sifter = Create("Sifter").AddComponent<WasteSifter>();
            var flushed = CreateJunk("Flushed Junk");
            var plain = CreateJunk("Plain Junk");
            sifter.maxDebrisAccumulation = flushed.SiftCost;
            Enter(sifter, flushed);
            Assert.IsTrue(flushed.WasFlushedFromSifter);

            var plainPit = Create("Plain Cesspit").AddComponent<Cesspit>();
            var flushedPit = Create("Flushed Cesspit").AddComponent<Cesspit>();
            plainPit.maxFullness = flushedPit.maxFullness = 1000f;
            var plainDeposit = plain.SiftCost;

            Enter(plainPit, plain);
            Enter(flushedPit, flushed);

            Assert.AreEqual(plainDeposit, plainPit.fullness, 0.001f,
                "Junk that never went through a clogged sifter should deposit normally.");
            Assert.Greater(flushedPit.fullness, plainPit.fullness * 5f);
        }

        private IssueObject CreateJunk(string name)
        {
            var go = Create(name);
            go.tag = "IssueObject";
            go.AddComponent<SphereCollider>();
            var issue = go.AddComponent<IssueObject>();
            issue.SetType(IssueType.NonWaste);
            issue.SetSize(3);
            return issue;
        }

        private static void Enter(MonoBehaviour utility, IssueObject issue)
        {
            var onTriggerEnter = utility.GetType().GetMethod("OnTriggerEnter",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(onTriggerEnter);
            onTriggerEnter.Invoke(utility, new object[] { issue.GetComponent<Collider>() });
        }

        private static bool IsHeld(IssueObject issue)
        {
            var heldField = typeof(IssueObject).GetField("_heldBySifter",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(heldField);
            return (bool)heldField.GetValue(issue);
        }

        private GameObject Create(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }
    }
}
