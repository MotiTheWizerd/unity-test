using Forge.Game.Actors;
using Forge.Game.Motion;
using Forge.Play;
using NUnit.Framework;
using UnityEngine;

namespace Forge.Tests.Play
{
    public class ActorBodyTests
    {
        static readonly BodyShape Shape = new BodyShape(height: 1.8f, radius: 0.3f, crouchHeight: 1f, stepHeight: 0.4f, slopeLimit: 50f);
        static readonly ActorId Someone = new ActorId("someone");

        ActorBody _body;

        [SetUp]
        public void SetUp() => _body = new ActorBody("Body", Shape, Someone);

        // Outside play a destroy is immediate or nothing (Dispose defers it to the end of a frame that never comes).
        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_body.Body.gameObject);

        [Test]
        public void ABody_StandsOnItsLayer_TaggedWithWhoseItIs()
        {
            Assert.That(_body.Body.gameObject.layer, Is.EqualTo(ActorLayer.Index));
            Assert.That(_body.Body.GetComponent<ActorLink>().Actor, Is.EqualTo(Someone));
            Assert.That(_body.Lying, Is.False);
            Assert.That(_body.Collider.enabled, Is.True);
        }

        [Test]
        public void Lying_TheCapsuleIsOffWhereTheFeetAre_UntilItStandsAgain()
        {
            _body.Teleport(new System.Numerics.Vector3(1f, 2f, 3f));

            _body.Lie(true);

            Assert.That(_body.Lying, Is.True);
            Assert.That(_body.Collider.enabled, Is.False, "A corpse is walked over and struck by nothing.");
            Assert.That(_body.Position, Is.EqualTo(new System.Numerics.Vector3(1f, 2f, 3f)), "Where the feet were.");

            _body.Teleport(new System.Numerics.Vector3(4f, 5f, 6f));
            Assert.That(_body.Collider.enabled, Is.False, "Moved by hand (a rebase), it lies on.");
            _body.Move(new System.Numerics.Vector3(1f, 0f, 0f));
            Assert.That(_body.Position, Is.EqualTo(new System.Numerics.Vector3(4f, 5f, 6f)), "Told to move, it goes nowhere.");

            _body.Lie(false);
            Assert.That(_body.Lying, Is.False);
            Assert.That(_body.Collider.enabled, Is.True, "Up again, it stands in the way as before.");
        }
    }
}
