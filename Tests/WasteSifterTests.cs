using System.Collections.Generic;
using _project.Scripts.Core;
using _project.Scripts.Object_Scripts;
using NUnit.Framework;
using UnityEngine;

namespace _project.Scripts.Tests
{
    public class WasteSifterTests : PlayModeTestBase
    {
        [Test]
        public void DisablingSifter_ReleasesHeldIssueAndAllowsRecapture()
        {
            var sifter = CreateGameObject("Sifter").AddComponent<WasteSifter>();
            var issue = CreateGameObject("Issue").AddComponent<IssueObject>();
            var sifterId = sifter.GetEntityId();
            Assert.IsTrue(issue.TryRegisterSifter(sifterId));
            issue.SetHeldBySifter(true);

            GetField<HashSet<IssueObject>>(sifter, "_heldIssues").Add(issue);

            sifter.gameObject.SetActive(false);

            Assert.IsFalse(IsHeld(issue));
            Assert.IsTrue(issue.TryRegisterSifter(sifterId),
                "Re-enabling the same sifter must allow it to catch this issue again.");
        }

        [Test]
        public void CloggingSifter_FlushesEveryHeldNonWasteDownTheLine()
        {
            var sifter = CreateGameObject("Sifter").AddComponent<WasteSifter>();
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
            var sifter = CreateGameObject("Sifter").AddComponent<WasteSifter>();
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
            var sifter = CreateGameObject("Sifter").AddComponent<WasteSifter>();
            var flushed = CreateJunk("Flushed Junk");
            var plain = CreateJunk("Plain Junk");
            sifter.maxDebrisAccumulation = flushed.SiftCost;
            Enter(sifter, flushed);
            Assert.IsTrue(flushed.WasFlushedFromSifter);

            var plainPit = CreateGameObject("Plain Cesspit").AddComponent<Cesspit>();
            var flushedPit = CreateGameObject("Flushed Cesspit").AddComponent<Cesspit>();
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
            var go = CreateGameObject(name);
            go.tag = "IssueObject";
            go.AddComponent<SphereCollider>();
            var issue = go.AddComponent<IssueObject>();
            issue.SetType(IssueType.NonWaste);
            issue.SetSize(3);
            return issue;
        }

        private static void Enter(MonoBehaviour utility, IssueObject issue) =>
            utility.SendMessage("OnTriggerEnter", issue.GetComponent<Collider>(),
                SendMessageOptions.RequireReceiver);

        private static bool IsHeld(IssueObject issue) => GetField<bool>(issue, "_heldBySifter");
    }
}
