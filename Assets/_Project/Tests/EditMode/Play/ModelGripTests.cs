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
            Assert.That(how.AlongArm, Is.Null, "Not strapped to the arm unless it says so.");
        }

        [Test]
        public void HeldLook_SaysWhichWayItIsStrappedAlongTheArm()
        {
            _look = new GameObject("Shield");
            var how = _look.AddComponent<HeldLook>();

            how.Configure(Vector3.back, CarriedAt.Hip, new Vector3(3f, 0f, 0f));

            AssertNear(how.AlongArm.Value, Vector3.right);
        }

        [Test]
        public void HeldLook_SaysHowItSitsOnTheGripInTheHand()
        {
            _look = new GameObject("Shield");
            var how = _look.AddComponent<HeldLook>();

            Assert.That(how.InHand.position, Is.EqualTo(Vector3.zero), "Unset, it sits on the grip as modelled...");
            Assert.That(how.InHand.rotation, Is.EqualTo(Quaternion.identity), "...unturned.");

            how.Configure(Vector3.back, CarriedAt.Back, Vector3.right, new Vector3(-0.06f, 0.05f, 0.05f), new Vector3(-25f, -6f, 0f));

            AssertNear(how.InHand.position, new Vector3(-0.06f, 0.05f, 0.05f));
            Assert.That(Quaternion.Angle(how.InHand.rotation, Quaternion.Euler(-25f, -6f, 0f)), Is.LessThan(0.01f));
        }

        [Test]
        public void Strapped_InTheLeftHand_LaysTheShieldUpTheArm_ItsBackToTheHand()
        {
            // A shield's face on +Z, its middle on +X from the handle. The left grip's +X runs back up the arm, its +Z
            // out of the palm: the back comes to the palm, the middle over the forearm, the face out past the knuckles.
            var turn = HoldFinder.Strapped(Vector3.back, Vector3.right, right: false);

            AssertNear(turn * Vector3.back, Vector3.forward);
            AssertNear(turn * Vector3.right, Vector3.right);
        }

        [Test]
        public void Strapped_InTheRightHand_LaysItUpTheArm_TheOtherWayAlongTheGrip()
        {
            // The right grip lays its +X along the fingers, so up the arm is -X.
            var turn = HoldFinder.Strapped(Vector3.back, Vector3.right, right: true);

            AssertNear(turn * Vector3.back, Vector3.forward);
            AssertNear(turn * Vector3.right, Vector3.left);
        }

        [Test]
        public void Against_SlingsAShieldFlatOnTheBack_ItsBackIn_ItsTopUp()
        {
            // The back's +Z stands out of the body; up runs along its +Y and a little out, as a back leans.
            var turn = HoldFinder.Against(Vector3.back, new Vector3(0f, 1f, 0.3f));

            AssertNear(turn * Vector3.back, Vector3.back);
            AssertNear(turn * Vector3.up, Vector3.up);
        }

        [Test]
        public void Against_UpAcrossTheBack_TurnsItsTopThatWay()
        {
            // The back's frame lies across the body (a bow's diagonal): up is along its +X.
            var turn = HoldFinder.Against(Vector3.back, Vector3.right);

            AssertNear(turn * Vector3.back, Vector3.back);
            AssertNear(turn * Vector3.up, Vector3.right);
        }

        [Test]
        public void Strapped_TheTwoWaysTogether_IsNoTurn()
        {
            Assert.That(HoldFinder.Strapped(Vector3.back, Vector3.back, right: false), Is.EqualTo(Quaternion.identity));
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
