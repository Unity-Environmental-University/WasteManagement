using System.Collections;
using _project.Scripts.Object_Scripts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace _project.Scripts.Tests
{
    public class LakeDuckTests : PlayModeTestBase
    {
        [Test]
        public void Quacking_PopsASpeechBubble_ThatShoutsWhenTheLakeIsHurt()
        {
            var lake = CreatePrimitive("Lake").AddComponent<LakeController>();
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<LakeDuck>(
                "Assets/_project/LakeDuck/Lake Duck.prefab");
            Assert.IsNotNull(prefab);
            var duck = Track(Object.Instantiate(prefab.gameObject)).GetComponent<LakeDuck>();
            duck.SendMessage("Start");
            var bubble = duck.GetComponentInChildren<SpeechBubble>(true);
            Assert.IsNotNull(bubble, "The duck brings its own speech bubble.");
            Assert.IsFalse(bubble.IsShowing, "Nothing to say yet.");

            // The lake taking damage makes the duck react out loud.
            lake.health -= 10f;
            duck.SendMessage("Update");

            Assert.IsTrue(bubble.IsShowing, "Every quack shows a bubble.");
            StringAssert.IsMatch("QUACK!!|Quack\\?!", bubble.Text, "A hurt duck shouts.");
        }

        [Test]
        public void DamageDuringAReaction_StillGetsShoutedOnceItEnds()
        {
            var lake = CreatePrimitive("Lake").AddComponent<LakeController>();
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<LakeDuck>(
                "Assets/_project/LakeDuck/Lake Duck.prefab");
            var duck = Track(Object.Instantiate(prefab.gameObject)).GetComponent<LakeDuck>();
            duck.SendMessage("Start");
            var bubble = duck.GetComponentInChildren<SpeechBubble>(true);

            // Mid-reaction (say, a happy quack), the lake takes a hit too small to send the duck ashore.
            SetField(duck, "_reactionUntil", float.MaxValue);
            lake.health -= 10f;
            duck.SendMessage("Update");
            Assert.IsFalse(bubble.IsShowing, "It doesn't interrupt the reaction already playing.");

            SetField(duck, "_reactionUntil", 0f);
            duck.SendMessage("Update");
            Assert.IsTrue(bubble.IsShowing);
            StringAssert.IsMatch("QUACK!!|Quack\\?!", bubble.Text, "The hit is still shouted about.");
        }

        [Test]
        public void DuckPrefab_BringsItsOwnParts()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_project/LakeDuck/Lake Duck.prefab");
            Assert.IsNotNull(prefab.GetComponent<LakeDuckSwimmer>(), "Something moves it.");
            Assert.IsNotNull(prefab.GetComponent<LakeDuckVoice>(), "Something quacks.");
            var animator = prefab.GetComponent<Animator>();
            Assert.IsTrue(animator && animator.runtimeAnimatorController, "Its poses come from animation clips.");
            var source = prefab.GetComponent<AudioSource>();
            Assert.IsNotNull(source, "It has a voice to quack with.");
            Assert.IsNotNull(prefab.GetComponentInChildren<SpeechBubble>(true), "The bubble is part of the prefab.");
        }

        [Test]
        public void SpeechBubble_FloatsAboveItsAnchor_AndStaysUpLongEnoughToPop()
        {
            var anchor = CreateGameObject("Anchor").transform;
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<SpeechBubble>(
                "Assets/_project/Prefabs/Speech Bubble.prefab");
            Assert.IsNotNull(prefab);
            var bubble = Track(Object.Instantiate(prefab.gameObject)).GetComponent<SpeechBubble>();
            Assert.IsFalse(bubble.IsShowing, "The prefab starts hidden.");
            bubble.Follow(anchor, 1f);

            bubble.Say("Quack!", Color.black, 0.5f);
            Assert.IsTrue(bubble.IsShowing);
            Assert.AreEqual("Quack!", bubble.Text);
            Assert.Greater(Vector3.Distance(anchor.position, bubble.transform.position), 0.5f, "It floats off its anchor.");

            bubble.Say("Quack!", Color.black, 0f);
            bubble.SendMessage("LateUpdate");
            Assert.IsTrue(bubble.IsShowing, "It always stays up long enough to pop in and out.");
        }

        [UnityTest]
        public IEnumerator SpeechBubble_PopsAwayAfterItsTime()
        {
            const float duration = 0.5f;
            var anchor = CreateGameObject("Anchor").transform;
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<SpeechBubble>(
                "Assets/_project/Prefabs/Speech Bubble.prefab");
            var bubble = Track(Object.Instantiate(prefab.gameObject)).GetComponent<SpeechBubble>();
            bubble.Follow(anchor, 1f);

            bubble.Say("Quack!", Color.black, duration);
            var shownAt = Time.time;

            // Polled against real time so a leaked pause (timeScale 0) fails the test instead of hanging it.
            var giveUpAt = Time.realtimeSinceStartup + 5f;
            while (bubble.IsShowing && Time.realtimeSinceStartup < giveUpAt)
                yield return null;

            Assert.IsFalse(bubble.IsShowing, "The bubble must hide once its time is up.");
            Assert.GreaterOrEqual(Time.time - shownAt, duration, "It must not hide before its time.");
        }
    }
}
