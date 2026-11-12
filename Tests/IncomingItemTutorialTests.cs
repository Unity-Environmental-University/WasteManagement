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
    /// <summary>The setup-phase warning that a new item type is about to come down the line.</summary>
    public class IncomingItemTutorialTests : PlayModeTestBase
    {
        private const string PanelPrefabPath = "Assets/_project/Prefabs/UI/IncomingItemPanel.prefab";
        private const string MetalAlertPath = "Assets/_project/UI/Incoming Items/Metal Alert.asset";

        [UnityTest]
        public IEnumerator BelowUnlockLevel_ShowsNothing()
        {
            var tutorial = CreateTutorial(2, false);

            EnterSetupPhase(tutorial);
            yield return null;

            Assert.IsNull(Object.FindAnyObjectByType<IncomingItemPanel>());
            Assert.IsFalse(GameSpeed.IsPaused);
        }

        [UnityTest]
        public IEnumerator AtUnlockLevel_FreezesTheGameUntilDismissed()
        {
            var tutorial = CreateTutorial(3, false);

            EnterSetupPhase(tutorial);
            yield return null;

            var panel = Object.FindAnyObjectByType<IncomingItemPanel>();
            Assert.IsTrue(panel, "Reaching the unlock level should show the popup.");
            Assert.IsTrue(GameSpeed.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);

            GetField<Button>(panel, "acceptButton").onClick.Invoke();
            yield return null;

            Assert.IsFalse(panel, "Accept should close the popup.");
            Assert.IsFalse(GameSpeed.IsPaused);
            Assert.AreEqual(GameSpeed.BaseScale, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator LaterSetupPhases_DoNotRepeatTheWarning()
        {
            var tutorial = CreateTutorial(3, false);

            EnterSetupPhase(tutorial);
            yield return null;
            GetField<Button>(Object.FindAnyObjectByType<IncomingItemPanel>(), "acceptButton").onClick.Invoke();
            yield return null;

            EnterSetupPhase(tutorial);
            yield return null;

            Assert.IsNull(Object.FindAnyObjectByType<IncomingItemPanel>());
            Assert.IsFalse(GameSpeed.IsPaused);
        }

        [UnityTest]
        public IEnumerator SpawnerThatNeverRollsTheItem_ShowsNothing()
        {
            var tutorial = CreateTutorial(3, true);

            EnterSetupPhase(tutorial);
            yield return null;

            Assert.IsNull(Object.FindAnyObjectByType<IncomingItemPanel>());
            Assert.IsFalse(GameSpeed.IsPaused);
        }

        [UnityTest]
        public IEnumerator DisablingWhileShown_ReleasesThePause()
        {
            var tutorial = CreateTutorial(3, false);

            EnterSetupPhase(tutorial);
            yield return null;
            Assert.IsTrue(GameSpeed.IsPaused);

            tutorial.enabled = false;
            yield return null;

            Assert.IsFalse(GameSpeed.IsPaused);
            Assert.IsNull(Object.FindAnyObjectByType<IncomingItemPanel>());
        }

        [Test]
        public void PanelPrefab_IsFullyWired()
        {
            var panel = AssetDatabase.LoadAssetAtPath<IncomingItemPanel>(PanelPrefabPath);

            Assert.IsTrue(panel, $"Missing {PanelPrefabPath}");
            Assert.IsTrue(panel.IsConfigured());
            foreach (var field in new[]
                     {
                         "titleLabel", "subheadLabel", "bodyLabel", "captionLabel", "datelineLabel", "sortDemo", "paper",
                         "paperByEra", "mayorPortrait", "mayorByEra", "splitter", "splitterByEra", "sortDemoBinLabel"
                     })
                Assert.IsTrue(GetField<Object>(panel, field), $"{field} is not assigned.");

            var animator = GetField<GameObject>(panel, "sortDemo").GetComponent<Animator>();
            Assert.IsTrue(animator && animator.runtimeAnimatorController, "The sort demo needs its Animator.");
            Assert.AreEqual(AnimatorUpdateMode.UnscaledTime, animator.updateMode,
                "The demo plays while the game is frozen, so it must run on unscaled time.");
        }

        [Test]
        public void Show_LabelsTheSortDemoBinWithTheIncomingItem()
        {
            // The demo bin is shared by every alert, so its label must come from the alert's type
            // rather than whatever text was authored into the prefab.
            var prefab = AssetDatabase.LoadAssetAtPath<IncomingItemPanel>(PanelPrefabPath);
            var panel = Track(Object.Instantiate(prefab.gameObject)).GetComponent<IncomingItemPanel>();
            var alert = Track(ScriptableObject.CreateInstance<IncomingItemAlert>());
            alert.type = IssueType.Plastic;
            alert.showSortDemo = true;

            panel.Show(alert, 1);

            // The test assembly doesn't reference TextMeshPro, so read the label's text by name.
            var label = GetField<Component>(panel, "sortDemoBinLabel");
            Assert.IsTrue(label, "The sort demo's bin label is wired.");
            Assert.AreEqual("PLASTIC BIN", label.GetType().GetProperty("text")!.GetValue(label));
        }

        [Test]
        public void MetalAlert_IsWrittenAndMarkedAsMetal()
        {
            var alert = AssetDatabase.LoadAssetAtPath<IncomingItemAlert>(MetalAlertPath);

            Assert.IsTrue(alert, $"Missing {MetalAlertPath}");
            Assert.AreEqual(IssueType.Metal, alert.type);
            Assert.IsNotEmpty(alert.title);
            Assert.IsNotEmpty(alert.body);
        }

        private IncomingItemTutorial CreateTutorial(int level, bool nonWasteOnlySpawner)
        {
            var gm = CreateGameObject("Game Master").AddComponent<GameMaster>();
            var turnController = gm.GetComponent<TurnController>();
            turnController.enabled = false;
            turnController.currentLevel = level;
            gm.turnController = turnController;

            var issuePrefab = CreateGameObject("Issue Prefab");
            issuePrefab.SetActive(false);
            var issue = issuePrefab.AddComponent<IssueObject>();
            SetField(issue, "metalUnlockLevel", 3);

            var spawner = CreateGameObject("Spawner").AddComponent<EntitySpawner>();
            SetField(spawner, "spawnerObject", issuePrefab);
            SetField(spawner, "nonWasteOnly", nonWasteOnlySpawner);
            gm.entitySpawners = new List<EntitySpawner> { spawner };

            var tutorial = CreateGameObject("Incoming Item Tutorial").AddComponent<IncomingItemTutorial>();
            SetField(tutorial, "panelPrefab", AssetDatabase.LoadAssetAtPath<IncomingItemPanel>(PanelPrefabPath));
            SetField(tutorial, "alerts",
                new[] { AssetDatabase.LoadAssetAtPath<IncomingItemAlert>(MetalAlertPath) });
            return tutorial;
        }

        private static void EnterSetupPhase(IncomingItemTutorial tutorial)
        {
            var handler = typeof(IncomingItemTutorial)
                .GetMethod("HandleCardPhaseEntered", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(handler, "The tutorial's setup-phase handler was renamed.");
            handler.Invoke(tutorial, null);
        }
    }
}
#endif
