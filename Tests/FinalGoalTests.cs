#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using _project.Scripts.Core;
using _project.Scripts.Object_Scripts;
using _project.Scripts.Tutorial;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace _project.Scripts.Tests
{
    /// <summary>The final goal: 100 of each material once every material's bin has unlocked.</summary>
    public class FinalGoalTests : PlayModeTestBase
    {
        private const string PanelPrefabPath = "Assets/_project/Prefabs/UI/IncomingItemPanel.prefab";
        private const string GoalStoryPath = "Assets/_project/UI/Incoming Items/Final Goal Story.asset";
        private const string WinStoryPath = "Assets/_project/UI/Incoming Items/Goal Reached Story.asset";
        private const string GoalTitle = "Test Goal";
        private const string WinTitle = "Test Win";
        private const int Target = 100;

        private TurnController _turnController;
        private Receptacle[] _bins;

        [TearDown]
        public void ReleaseAnyHeldPause()
        {
            while (GameSpeed.IsPaused) GameSpeed.Resume();
        }

        [UnityTest]
        public IEnumerator BeforeEveryBinUnlocks_ShowsNoGoal([Values(2, 3, 4)] int level)
        {
            var goal = CreateGoal(level);

            EnterSetupPhase(goal);
            yield return null;
            yield return null;

            Assert.IsNull(Object.FindAnyObjectByType<IncomingItemPanel>());
            Assert.IsFalse(goal.IsGoalOpen);
            Assert.IsFalse(GameSpeed.IsPaused);
        }

        [UnityTest]
        public IEnumerator OnceEveryBinUnlocks_ShowsTheGoalOnce()
        {
            var goal = CreateGoal(4);
            EnterSetupPhase(goal);
            yield return null;
            yield return null;
            Assert.IsNull(Object.FindAnyObjectByType<IncomingItemPanel>(), "Paper is still locked at level 4.");

            SetLevel(5);
            EnterSetupPhase(goal);
            yield return null;
            yield return null;

            var panel = Object.FindAnyObjectByType<IncomingItemPanel>();
            Assert.IsTrue(panel, "Unlocking the last bin should show the goal.");
            Assert.AreEqual(GoalTitle, TitleOf(panel));
            Assert.IsTrue(goal.IsGoalOpen);
            Assert.IsTrue(GameSpeed.IsPaused);

            Accept(panel);
            yield return null;
            Assert.IsFalse(GameSpeed.IsPaused);

            EnterSetupPhase(goal);
            yield return null;
            yield return null;

            Assert.IsNull(Object.FindAnyObjectByType<IncomingItemPanel>(), "The goal shows only once per run.");
            Assert.IsFalse(GameSpeed.IsPaused);
        }

        [UnityTest]
        public IEnumerator Goal_WaitsForThePaperWarningToClose()
        {
            var goal = CreateGoal(5);
            var warning = CreateIncomingItemTutorial();

            // Worst case for ordering: the goal hears the setup phase before the warning does.
            EnterSetupPhase(goal);
            EnterSetupPhase(warning);
            yield return null;
            yield return null;

            var panels = Object.FindObjectsByType<IncomingItemPanel>(FindObjectsInactive.Exclude);
            Assert.AreEqual(1, panels.Length, "Only one popup at a time.");
            Assert.AreEqual("Paper Warning", TitleOf(panels[0]));

            Accept(panels[0]);
            yield return null;
            yield return null;

            var goalPanel = Object.FindAnyObjectByType<IncomingItemPanel>();
            Assert.IsTrue(goalPanel, "The goal follows the paper warning.");
            Assert.AreEqual(GoalTitle, TitleOf(goalPanel));
            Assert.IsTrue(GameSpeed.IsPaused);

            Accept(goalPanel);
            yield return null;

            Assert.IsFalse(GameSpeed.IsPaused, "Each popup releases only its own pause.");
        }

        [UnityTest]
        public IEnumerator EveryBinAtTheTarget_WinsOnce()
        {
            var goal = CreateGoal(5);
            EnterSetupPhase(goal);
            yield return null;
            yield return null;
            Accept(Object.FindAnyObjectByType<IncomingItemPanel>());
            yield return null;

            Collect(_bins[0], Target);
            Collect(_bins[1], Target);
            Collect(_bins[2], Target - 1);
            yield return null;
            yield return null;

            Assert.IsFalse(goal.IsWon, "One bin is still short of the target.");
            Assert.IsNull(Object.FindAnyObjectByType<IncomingItemPanel>());

            Collect(_bins[2], Target);
            yield return null;
            yield return null;

            var panel = Object.FindAnyObjectByType<IncomingItemPanel>();
            Assert.IsTrue(goal.IsWon);
            Assert.IsTrue(panel, "Reaching the target in every bin should show the win.");
            Assert.AreEqual(WinTitle, TitleOf(panel));
            Assert.IsTrue(GameSpeed.IsPaused);

            Accept(panel);
            yield return null;
            Collect(_bins[0], Target + 1);
            yield return null;
            yield return null;

            Assert.IsNull(Object.FindAnyObjectByType<IncomingItemPanel>(), "The win shows only once.");
            Assert.IsFalse(GameSpeed.IsPaused, "Play carries on after the win.");
        }

        [UnityTest]
        public IEnumerator CollectingBeforeTheGoalOpens_DoesNotWin()
        {
            var goal = CreateGoal(4);

            Collect(_bins[0], Target);
            Collect(_bins[1], Target);
            yield return null;
            yield return null;

            Assert.IsFalse(goal.IsWon);
            Assert.IsNull(Object.FindAnyObjectByType<IncomingItemPanel>());
        }

        [UnityTest]
        public IEnumerator DisablingWhileShown_ReleasesThePause()
        {
            var goal = CreateGoal(5);
            EnterSetupPhase(goal);
            yield return null;
            yield return null;
            Assert.IsTrue(GameSpeed.IsPaused);

            goal.enabled = false;
            yield return null;

            Assert.IsFalse(GameSpeed.IsPaused);
            Assert.IsNull(Object.FindAnyObjectByType<IncomingItemPanel>());
        }

        [Test]
        public void GoalStories_AreWrittenForThePopup()
        {
            // These stories are scene content tracked in Plastic SCM, not in this repo.
            foreach (var path in new[] { GoalStoryPath, WinStoryPath })
            {
                var story = AssetDatabase.LoadAssetAtPath<IncomingItemAlert>(path);
                Assert.IsTrue(story, $"Missing {path}");
                Assert.IsNotEmpty(story.title, path);
                Assert.IsNotEmpty(story.body, path);
                Assert.IsFalse(story.showSortDemo, $"{path} is not an item warning.");
            }
        }

        private FinalGoal CreateGoal(int level)
        {
            var gm = CreateGameObject("Game Master").AddComponent<GameMaster>();
            _turnController = gm.GetComponent<TurnController>();
            _turnController.enabled = false;
            _turnController.currentLevel = level;
            gm.turnController = _turnController;

            _bins = new[]
            {
                CreateBin(IssueType.Metal, 3), CreateBin(IssueType.Plastic, 4), CreateBin(IssueType.Paper, 5)
            };

            var holder = CreateGameObject("Final Goal");
            holder.SetActive(false);
            var goal = holder.AddComponent<FinalGoal>();
            SetField(goal, "panelPrefab", AssetDatabase.LoadAssetAtPath<IncomingItemPanel>(PanelPrefabPath));
            SetField(goal, "goalStory", CreateStory(GoalTitle, IssueType.Organic));
            SetField(goal, "winStory", CreateStory(WinTitle, IssueType.Organic));
            SetField(goal, "materials", _bins);
            SetField(goal, "targetPerMaterial", Target);
            holder.SetActive(true);
            return goal;
        }

        private Receptacle CreateBin(IssueType type, int unlockLevel)
        {
            // Configure before enabling: a receptacle applies the town level in OnEnable.
            var holder = CreateGameObject($"{type} Bin");
            holder.SetActive(false);
            var bin = holder.AddComponent<Receptacle>();
            SetField(bin, "acceptedType", type);
            SetField(bin, "unlockLevel", unlockLevel);
            holder.SetActive(true);
            return bin;
        }

        private void SetLevel(int level)
        {
            // Re-enabling makes each bin re-read the level, as a level change would.
            _turnController.currentLevel = level;
            foreach (var bin in _bins)
            {
                bin.enabled = false;
                bin.enabled = true;
            }
        }

        private IncomingItemTutorial CreateIncomingItemTutorial()
        {
            var issuePrefab = CreateGameObject("Issue Prefab");
            issuePrefab.SetActive(false);
            var issue = issuePrefab.AddComponent<IssueObject>();
            SetField(issue, "paperUnlockLevel", 5);

            var spawner = CreateGameObject("Spawner").AddComponent<EntitySpawner>();
            SetField(spawner, "spawnerObject", issuePrefab);
            GameMaster.Instance.entitySpawners = new List<EntitySpawner> { spawner };

            var tutorial = CreateGameObject("Incoming Item Tutorial").AddComponent<IncomingItemTutorial>();
            SetField(tutorial, "panelPrefab", AssetDatabase.LoadAssetAtPath<IncomingItemPanel>(PanelPrefabPath));
            SetField(tutorial, "alerts", new[] { CreateStory("Paper Warning", IssueType.Paper) });
            return tutorial;
        }

        private IncomingItemAlert CreateStory(string title, IssueType type)
        {
            var story = Track(ScriptableObject.CreateInstance<IncomingItemAlert>());
            story.title = title;
            story.type = type;
            return story;
        }

        /// <summary>Brings the bin's count to <paramref name="total" />, the last piece through a real delivery.</summary>
        private void Collect(Receptacle bin, int total)
        {
            SetField(bin, "<DeliveredCount>k__BackingField", total - 1);

            var holder = CreateGameObject("Delivered Issue");
            holder.SetActive(false);
            var issue = holder.AddComponent<IssueObject>();
            SetField(issue, "type", GetField<IssueType>(bin, "acceptedType"));
            SetField(issue, "<EndReceptacle>k__BackingField", bin);

            var receive = typeof(Receptacle).GetMethod("Receive", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(receive, "The receptacle's delivery handler was renamed.");
            receive.Invoke(bin, new object[] { issue });
            Assert.AreEqual(total, bin.DeliveredCount);
        }

        private static void EnterSetupPhase(MonoBehaviour listener)
        {
            var handler = listener.GetType()
                .GetMethod("HandleCardPhaseEntered", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(handler, $"{listener.GetType().Name}'s setup-phase handler was renamed.");
            handler.Invoke(listener, null);
        }

        private static void Accept(IncomingItemPanel panel)
        {
            Assert.IsTrue(panel, "Expected a popup to accept.");
            GetField<Button>(panel, "acceptButton").onClick.Invoke();
        }

        private static string TitleOf(IncomingItemPanel panel)
        {
            // The test assembly doesn't reference TextMeshPro, so read the label's text by name.
            var label = GetField<Component>(panel, "titleLabel");
            return (string)label.GetType().GetProperty("text")!.GetValue(label);
        }
    }
}
#endif
