using Forge.Play;
using NUnit.Framework;
using UnityEngine;

namespace Forge.Tests.Play
{
    public class ModelGripTests
    {
        GameObject _look;

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_look);

        static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        static void AssertNear(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), $"{actual} is not {expected}");

        [Test]
        public void AModel_IsHeldByItsPivot()
        {
            _look = new GameObject("Sword");
            var model = Child(_look.transform, "Model");

            Assert.That(ModelGrip.Of(_look), Is.SameAs(model));
        }

        [Test]
        public void AnythingElse_IsHeldByItsRoot()
        {
            _look = new GameObject("Crate");
            Child(_look.transform, "Lid");
            Child(_look.transform, "Box");

            Assert.That(ModelGrip.Of(_look), Is.SameAs(_look.transform));
        }

        [Test]
        public void HeldLook_SaysWhichSideFacesTheHolder_AndWhereItIsCarried()
        {
            _look = new GameObject("Bow");
            var how = _look.AddComponent<HeldLook>();

            Assert.That(how.FacesHolder, Is.Null, "Unset, any side may face the holder...");
            Assert.That(how.Carried, Is.EqualTo(CarriedAt.Hip), "...and it is carried at the hip.");

            how.Configure(new Vector3(-2f, 0f, 0f), CarriedAt.Back);
            AssertNear(how.FacesHolder.Value, Vector3.left);
            Assert.That(how.Carried, Is.EqualTo(CarriedAt.Back));
        }

        [Test]
        public void Facing_TurnsAboutTheLength_SoTheSideFacesTheBody()
        {
            // The string on -X, the body ahead along +Z and a little up: a quarter turn brings -X round to +Z.
            var turn = HoldFinder.Facing(Vector3.left, new Vector3(0f, 0.5f, 1f));

            AssertNear(turn * Vector3.left, Vector3.forward);
            AssertNear(turn * Vector3.up, Vector3.up);
        }

        [Test]
        public void Facing_AlongTheLength_LeavesItAsItIs()
        {
            Assert.That(HoldFinder.Facing(Vector3.left, Vector3.up), Is.EqualTo(Quaternion.identity));
        }
    }
}
