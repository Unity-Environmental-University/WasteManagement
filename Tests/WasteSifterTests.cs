using System.Collections.Generic;
using _project.Scripts.Core;
using _project.Scripts.Object_Scripts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

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
        public void UpstreamFlush_AddsQuarterLoadToDownstreamSifterWithoutCascading()
        {
            var upstream = CreateGameObject("Upstream Sifter").AddComponent<WasteSifter>();
            var downstream = CreateGameObject("Downstream Sifter").AddComponent<WasteSifter>();
            var first = CreateJunk("First Junk");
            var second = CreateJunk("Second Junk");
            var fullLoad = first.SiftCost + second.SiftCost;
            upstream.maxDebrisAccumulation = downstream.maxDebrisAccumulation = fullLoad;

            Enter(upstream, first);
            Enter(upstream, second);
            Enter(downstream, first);
            Enter(downstream, second);

            Assert.AreEqual(fullLoad, upstream.debrisAccumulation, 0.001f,
                "Fresh junk must still fill the upstream screen normally.");
            Assert.AreEqual(fullLoad * 0.25f, downstream.debrisAccumulation, 0.001f);
            Assert.IsTrue(IsHeld(first));
            Assert.IsTrue(IsHeld(second), "The next screen should contain the flush, not cascade.");
            Assert.AreEqual(fullLoad * 0.5f, first.SiftCost, 0.001f,
                "Overflow relief must not shrink the issue itself.");

            Enter(downstream, first);
            Assert.AreEqual(fullLoad * 0.25f, downstream.debrisAccumulation, 0.001f,
                "Duplicate trigger entries must not add more debris.");
        }

        [Test]
        public void DownstreamSifter_StillClogsWhenReducedFlushExceedsRemainingCapacity()
        {
            var sifter = CreateGameObject("Nearly Full Sifter").AddComponent<WasteSifter>();
            var junk = CreateJunk("Flushed Junk");
            junk.FlushFromSifter();
            sifter.maxDebrisAccumulation = junk.SiftCost;
            sifter.debrisAccumulation = junk.SiftCost * 0.8f;

            Enter(sifter, junk);

            Assert.AreEqual(sifter.maxDebrisAccumulation, sifter.debrisAccumulation, 0.001f);
            Assert.IsFalse(IsHeld(junk), "Overflow relief is not immunity to clogging.");
        }

        [Test]
        public void ClearingDownstreamSifter_RemovesRecapturedFlush()
        {
            var sifter = CreateGameObject("Recovery Sifter").AddComponent<WasteSifter>();
            var junk = CreateJunk("Flushed Junk");
            junk.FlushFromSifter();
            sifter.maxDebrisAccumulation = junk.SiftCost;
            Enter(sifter, junk);

            sifter.ClearDebris();

            Assert.AreEqual(0f, sifter.debrisAccumulation);
            Assert.IsEmpty(GetField<HashSet<IssueObject>>(sifter, "_heldIssues"));
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

        [Test]
        public void Metal_BreaksTheSifterOpenAndStaysStuckUntilItIsCleared()
        {
            var sifter = CreateGameObject("Sifter").AddComponent<WasteSifter>();
            sifter.maxDebrisAccumulation = 100f;
            var junk = CreateJunk("Held Junk");
            var metal = CreateMetal("Metal");

            Enter(sifter, junk);
            Assert.IsTrue(IsHeld(junk));

            Enter(sifter, metal);

            Assert.AreEqual(sifter.maxDebrisAccumulation, sifter.debrisAccumulation, 0.001f,
                "One piece of metal breaks the screen open outright.");
            Assert.IsFalse(IsHeld(junk), "The broken screen lets go of what it was holding.");
            Assert.IsTrue(junk.WasFlushedFromSifter);
            Assert.IsTrue(IsHeld(metal), "The metal stays wedged in the screen.");
            Assert.AreEqual(2, metal.ProcessCost, 0.001f, "The screen doesn't grind metal down.");

            sifter.ClearDebris();
            Assert.AreEqual(0f, sifter.debrisAccumulation, 0.001f);
        }

        [Test]
        public void Metal_PassesAnAlreadyBrokenSifterUntouched()
        {
            var sifter = CreateGameObject("Sifter").AddComponent<WasteSifter>();
            sifter.maxDebrisAccumulation = 100f;
            sifter.debrisAccumulation = 100f;
            var metal = CreateMetal("Metal");
            var size = metal.ProcessCost;

            Enter(sifter, metal);

            Assert.IsFalse(IsHeld(metal));
            Assert.AreEqual(size, metal.ProcessCost, 0.001f);
        }

        [Test]
        public void Metal_DoesFarMoreDamageToACesspitThanWaste()
        {
            var waste = CreateGameObject("Waste");
            waste.tag = "IssueObject";
            waste.AddComponent<SphereCollider>();
            var wasteIssue = waste.AddComponent<IssueObject>();
            wasteIssue.SetType(IssueType.Organic);
            wasteIssue.SetSize(2);
            var metal = CreateMetal("Metal");

            var wastePit = CreateGameObject("Waste Cesspit").AddComponent<Cesspit>();
            var metalPit = CreateGameObject("Metal Cesspit").AddComponent<Cesspit>();
            wastePit.maxFullness = metalPit.maxFullness = 1000f;

            Enter(wastePit, wasteIssue);
            Enter(metalPit, metal);

            Assert.Greater(metalPit.fullness, wastePit.fullness * 10f);
        }

        [Test]
        public void Minigame_SortingPiecesDrainsDebrisAndProgressSurvivesClosing()
        {
            var controller = CreateMinigame(4, out var pieces);
            var sifter = CreateGameObject("Sifter").AddComponent<WasteSifter>();
            var otherSifter = CreateGameObject("Other Sifter").AddComponent<WasteSifter>();
            sifter.maxDebrisAccumulation = otherSifter.maxDebrisAccumulation = 100f;
            sifter.debrisAccumulation = otherSifter.debrisAccumulation = 100f;

            controller.StartMiniGame(sifter);
            Assert.IsTrue(controller.IsRunning);
            Sort(pieces[0]);
            Sort(pieces[1]);

            Assert.AreEqual(50f, sifter.debrisAccumulation, 0.001f, "Two of four pieces take half the load.");
            var leftTypes = new[] { pieces[2].GetDebrisType(), pieces[3].GetDebrisType() };

            controller.CloseMiniGame();
            Assert.IsFalse(controller.IsRunning);
            Assert.AreEqual(50f, sifter.debrisAccumulation, 0.001f, "Walking away keeps the progress.");

            controller.StartMiniGame(otherSifter);
            Assert.AreEqual(4, CountActive(pieces), "Another sifter has its own, untouched round.");
            controller.CloseMiniGame();

            controller.StartMiniGame(sifter);
            Assert.AreEqual(2, CountActive(pieces), "Coming back resumes with only the unsorted pieces.");
            Assert.IsFalse(pieces[0].gameObject.activeSelf);
            Assert.IsFalse(pieces[1].gameObject.activeSelf);
            Assert.AreEqual(leftTypes[0], pieces[2].GetDebrisType(), "Remaining pieces look as they did.");
            Assert.AreEqual(leftTypes[1], pieces[3].GetDebrisType());

            Sort(pieces[2]);
            Assert.AreEqual(25f, sifter.debrisAccumulation, 0.001f);
            Sort(pieces[3]);
            controller.SendMessage("LateUpdate");

            Assert.IsFalse(controller.IsRunning);
            Assert.AreEqual(0f, sifter.debrisAccumulation, 0.001f);

            sifter.debrisAccumulation = 100f;
            controller.StartMiniGame(sifter);
            Assert.AreEqual(4, CountActive(pieces), "A cleared sifter starts its next round fresh.");
        }

        [Test]
        public void Minigame_PiecesTrackTheDebrisCaughtSinceTheLastVisit()
        {
            var controller = CreateMinigame(4, out var pieces);
            var sifter = CreateGameObject("Sifter").AddComponent<WasteSifter>();
            sifter.maxDebrisAccumulation = 100f;
            sifter.debrisAccumulation = 100f;

            controller.StartMiniGame(sifter);
            Sort(pieces[0]);
            Sort(pieces[1]);
            Sort(pieces[2]);
            Assert.AreEqual(25f, sifter.debrisAccumulation, 0.001f, "Three of four pieces take three quarters.");
            var lastType = pieces[3].GetDebrisType();
            controller.CloseMiniGame();

            // More junk lands while the player is away: a quarter-full sifter climbs back to half.
            sifter.debrisAccumulation = 50f;
            controller.StartMiniGame(sifter);
            Assert.AreEqual(2, CountActive(pieces), "Half a load is two pieces, not the one left last time.");
            Assert.IsTrue(pieces[3].gameObject.activeSelf, "The piece left behind is still there.");
            Assert.AreEqual(lastType, pieces[3].GetDebrisType());

            Sort(pieces[3]);
            Assert.AreEqual(25f, sifter.debrisAccumulation, 0.001f, "One piece is still only a quarter's worth.");
            Assert.IsTrue(controller.IsRunning, "The new junk still has to be sorted.");

            // And junk caught with the window open shows up straight away.
            sifter.debrisAccumulation = 75f;
            controller.SendMessage("Update");
            Assert.AreEqual(3, CountActive(pieces));

            controller.CloseMiniGame();
            var fresh = CreateGameObject("Fresh Sifter").AddComponent<WasteSifter>();
            fresh.maxDebrisAccumulation = 100f;
            fresh.debrisAccumulation = 50f;
            controller.StartMiniGame(fresh);
            Assert.AreEqual(2, CountActive(pieces), "A sifter opened half full starts with half the pieces.");
        }

        [Test]
        public void SortingDebris_BelowTheLimit_ReopensTheScreen()
        {
            var sifter = CreateGameObject("Sifter").AddComponent<WasteSifter>();
            sifter.maxDebrisAccumulation = 100f;
            var metal = CreateMetal("Metal");
            Enter(sifter, metal);
            var junk = CreateJunk("Late Junk");
            Enter(sifter, junk);
            Assert.IsTrue(junk.WasFlushedFromSifter, "A broken screen waves junk through.");

            sifter.SortDebrisPiece(4);

            var fresh = CreateJunk("Fresh Junk");
            Enter(sifter, fresh);
            Assert.IsTrue(IsHeld(fresh), "With debris back under the limit the screen catches junk again.");
        }

        private SifterMiniGameController CreateMinigame(int pieceCount, out DebrisHandler[] pieces)
        {
            var gm = CreateGameObject("Game Master").AddComponent<GameMaster>();
            var controller = CreateGameObject("Sifter Minigame").AddComponent<SifterMiniGameController>();
            gm.sifterMiniController = controller;

            var panel = CreateGameObject("Minigame Panel");
            pieces = new DebrisHandler[pieceCount];
            for (var i = 0; i < pieceCount; i++)
            {
                var piece = new GameObject($"Debris {i}", typeof(RectTransform), typeof(Image));
                piece.transform.SetParent(panel.transform, false);
                pieces[i] = piece.AddComponent<DebrisHandler>();
            }

            SetField(controller, "minigamePanel", panel);
            panel.SetActive(false);
            return controller;
        }

        private static void Sort(DebrisHandler piece)
        {
            piece.HandleBucketDrop(piece.GetDebrisType());
            Assert.IsFalse(piece.gameObject.activeSelf);
        }

        private static int CountActive(DebrisHandler[] pieces)
        {
            var active = 0;
            foreach (var piece in pieces)
                if (piece.gameObject.activeSelf)
                    active++;
            return active;
        }

        private IssueObject CreateMetal(string name)
        {
            var go = CreateGameObject(name);
            go.tag = "IssueObject";
            go.AddComponent<SphereCollider>();
            var issue = go.AddComponent<IssueObject>();
            issue.SetType(IssueType.Metal);
            return issue;
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
