using _project.Scripts.Object_Scripts;
using NUnit.Framework;
using UnityEngine;

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
            var bubble = duck.GetComponentInChildren<SpeechBubble>(true) ?? Object.FindAnyObjectByType<SpeechBubble>();
            Assert.IsNotNull(bubble, "The duck brings its own speech bubble.");
            Track(bubble.gameObject);
            Assert.IsFalse(bubble.IsShowing, "Nothing to say yet.");

            // The lake taking damage makes the duck react out loud.
            lake.health -= 10f;
            duck.SendMessage("Update");

            Assert.IsTrue(bubble.IsShowing, "Every quack shows a bubble.");
            StringAssert.IsMatch("QUACK!!|Quack\\?!", bubble.Text, "A hurt duck shouts.");
        }

        [Test]
        public void SpeechBubble_PopsAwayAfterItsTime()
        {
            var anchor = CreateGameObject("Anchor").transform;
            var bubble = SpeechBubble.Create(anchor, 1f, 2f);
            Track(bubble.gameObject);

            bubble.Say("Quack!", Color.black, 0.5f);
            Assert.IsTrue(bubble.IsShowing);
            Assert.AreEqual("Quack!", bubble.Text);
            Assert.Greater(Vector3.Distance(anchor.position, bubble.transform.position), 0.5f, "It floats off its anchor.");

            bubble.Say("Quack!", Color.black, 0f);
            bubble.SendMessage("LateUpdate");
            Assert.IsTrue(bubble.IsShowing, "It always stays up long enough to pop in and out.");
        }
    }
}
