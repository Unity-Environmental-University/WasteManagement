using _project.Scripts.Object_Scripts;
using NUnit.Framework;
using UnityEngine;

namespace Tests
{
    public class IssueObjectTests
    {
        private GameObject _go;
        private IssueObject _issue;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject();
            _issue = _go.AddComponent<IssueObject>();
            _issue.SetSize(2); // known baseline; Awake sets random size so override it
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void Sift_ReducesSizeByPower()
        {
            _issue.Sift(1);
            Assert.AreEqual(1, _issue.Size);
        }

        [Test]
        public void Sift_ClampsAtZero()
        {
            _issue.SetSize(1);
            _issue.Sift(1);
            Assert.AreEqual(0, _issue.Size);
        }

        [Test]
        public void TryRegisterSifter_ReturnsTrueFirstTime()
        {
            Assert.IsTrue(_issue.TryRegisterSifter(42));
        }

        [Test]
        public void TryRegisterSifter_ReturnsFalseForSameSifter()
        {
            _issue.TryRegisterSifter(42);
            Assert.IsFalse(_issue.TryRegisterSifter(42));
        }

        [Test]
        public void TryRegisterSifter_ReturnsTrueForDifferentSifter()
        {
            _issue.TryRegisterSifter(42);
            Assert.IsTrue(_issue.TryRegisterSifter(99));
        }

        // Regression: compound trigger colliders on the same sifter must not sift twice
        [Test]
        public void Size2Issue_CompoundTrigger_SiftsOnlyOnce()
        {
            const int sifterId = 1;

            if (_issue.TryRegisterSifter(sifterId)) _issue.Sift(1);
            if (_issue.TryRegisterSifter(sifterId)) _issue.Sift(1); // duplicate — must be skipped

            Assert.AreEqual(1, _issue.Size);
        }

        // Regression: sequential sifters in series must each get one sift
        [Test]
        public void Issue_TwoSiftersInSeries_EachSiftsOnce()
        {
            _issue.TryRegisterSifter(1);
            _issue.Sift(1); // first sifter: 2 → 1
            _issue.TryRegisterSifter(2);
            _issue.Sift(1); // second sifter: 1 → 0

            Assert.AreEqual(0, _issue.Size);
        }
    }
}
