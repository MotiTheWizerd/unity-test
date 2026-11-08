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

        [Test]
        public void AModel_IsHeldByItsPivot_InEitherHand()
        {
            _look = new GameObject("Sword");
            var model = Child(_look.transform, "Model");

            Assert.That(ModelGrip.Of(_look, left: false), Is.SameAs(model));
            Assert.That(ModelGrip.Of(_look, left: true), Is.SameAs(model));
        }

        [Test]
        public void AModelWithALeftGrip_IsHeldByIt_InTheLeftHandOnly()
        {
            _look = new GameObject("Bow");
            var model = Child(_look.transform, "Model");
            var left = Child(model, ModelGrip.LeftHand);

            Assert.That(ModelGrip.Of(_look, left: true), Is.SameAs(left));
            Assert.That(ModelGrip.Of(_look, left: false), Is.SameAs(model));
        }

        [Test]
        public void AnythingElse_IsHeldByItsRoot()
        {
            _look = new GameObject("Crate");
            Child(_look.transform, "Lid");
            Child(_look.transform, "Box");

            Assert.That(ModelGrip.Of(_look, left: true), Is.SameAs(_look.transform));
        }
    }
}
