using System.Collections.Generic;
using System.Linq;
using Forge.Play;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Forge.Tests.Play
{
    // A character's look goes limp (Ragdoll): its hitboxes become the solid parts of bodies joined at its bones, which bend
    // as a body does, and it falls and lies as the world has it; stiffened, it is as it was.
    public class RagdollTests
    {
        const string VikingF1 = "Assets/_Project/Content/Characters/VikingF1/VikingF1.prefab";
        const float Step = 0.02f;

        GameObject _body;
        GameObject _ground;
        CharacterModel _model;
        IReadOnlyList<Hitbox> _hitboxes;
        Ragdoll _ragdoll;
        SimulationMode _simulation;

        [SetUp]
        public void SetUp()
        {
            _simulation = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VikingF1);
            _body = new GameObject("Body");
            _model = new CharacterModel(_body.transform, prefab);
            _hitboxes = Hitboxes.Build(_model, prefab);
            _ragdoll = new Ragdoll(_model, _hitboxes);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_body);
            if (_ground != null)
                Object.DestroyImmediate(_ground);
            Physics.simulationMode = _simulation;
        }

        [Test]
        public void GoneLimp_ItsPartsAreSolidClutter_OnElevenBodiesJoinedAtTheBones_AsHeavyAsAPerson_AndItStaysAsItWas()
        {
            var hand = _model.Bone(HumanBodyBones.RightHand);
            var before = hand.position;

            _ragdoll.GoLimp();

            var bodies = _model.Root.GetComponentsInChildren<Rigidbody>().Where(body => !body.isKinematic).ToArray();
            Assert.That(_ragdoll.Limp, Is.True);
            Assert.That(_model.Animated, Is.False, "Its animations stop.");
            Assert.That(bodies.Length, Is.EqualTo(11), "Pelvis, chest, head, and each upper arm, forearm, thigh and shin.");
            Assert.That(bodies.Sum(body => body.mass), Is.EqualTo(Ragdoll.Mass).Within(1e-3f));
            Assert.That(_model.Root.GetComponentsInChildren<CharacterJoint>().Length, Is.EqualTo(10), "Each but the pelvis hangs from one.");
            Assert.That(bodies.All(body => body.GetComponent<Carriable>() != null), "Each may be taken up.");
            foreach (var hitbox in _hitboxes)
            {
                var collider = hitbox.GetComponent<Collider>();
                Assert.That((collider.isTrigger, hitbox.gameObject.layer, hitbox.GetComponent<Rigidbody>() == null),
                    Is.EqualTo((false, ClutterLayer.Index, true)), hitbox.name);
                Assert.That(collider.attachedRigidbody, Is.Not.Null.And.Property(nameof(Rigidbody.isKinematic)).False, hitbox.name);
            }

            Assert.That(Vector3.Distance(hand.position, before), Is.LessThan(1e-4f), "It goes limp where it stood, as it was posed.");
            Assert.That(_model.Bone(HumanBodyBones.RightHand).GetComponentInParent<Rigidbody>(),
                Is.EqualTo(_model.Bone(HumanBodyBones.RightLowerArm).GetComponent<Rigidbody>()), "A hand goes with its forearm.");
        }

        [Test]
        public void Stiffened_ItIsALookItsAnimationsPoseAgain_ItsHitboxesAsTheyWere()
        {
            _ragdoll.GoLimp();
            _ragdoll.Stiffen();

            Assert.That(_ragdoll.Limp, Is.False);
            Assert.That(_model.Animated, Is.True);
            Assert.That(_model.Root.GetComponentsInChildren<CharacterJoint>(), Is.Empty);
            Assert.That(_model.Root.GetComponentsInChildren<Carriable>(), Is.Empty);
            foreach (var hitbox in _hitboxes)
            {
                Assert.That((hitbox.gameObject.layer, hitbox.GetComponent<Collider>().isTrigger, hitbox.GetComponent<Rigidbody>().isKinematic),
                    Is.EqualTo((HitboxLayer.Index, true, true)), hitbox.name);
            }

            Assert.That(_model.Root.GetComponentsInChildren<Rigidbody>().All(body => body.isKinematic), "No body of its own is left loose.");
        }

        [TestCase(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, -1f)]
        [TestCase(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, -1f)]
        [TestCase(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 1f)]
        [TestCase(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 1f)]
        public void AKneeBendsBack_AndAnElbowAhead_AsFarAs140Degrees_NeverTheOtherWay(HumanBodyBones part, HumanBodyBones end, float ahead)
        {
            var bends = new List<(float Bent, bool Anatomical)>();
            foreach (var spin in new[] { 1f, -1f })
            {
                TearDown();
                SetUp();
                PoseT();
                _ragdoll.GoLimp();
                var bone = _model.Bone(part);
                var start = (_model.Bone(end).position - bone.position).normalized;
                var axis = bone.TransformDirection(bone.GetComponent<CharacterJoint>().axis);
                // Everything held still but the part, which is turned hard about its knee or elbow one way.
                var body = bone.GetComponent<Rigidbody>();
                foreach (var other in _model.Root.GetComponentsInChildren<Rigidbody>())
                {
                    other.useGravity = false;
                    other.isKinematic = other != body;
                }

                for (var i = 0; i < 100; i++)
                {
                    body.angularVelocity = axis * (3f * spin);
                    Physics.Simulate(Step);
                }

                var now = (_model.Bone(end).position - bone.position).normalized;
                bends.Add((Vector3.Angle(start, now), Vector3.Dot(now - start, _model.Root.transform.forward * ahead) > 0f));
            }

            var way = ahead > 0f ? "ahead" : "back";
            var far = bends.OrderByDescending(bend => bend.Bent).First();
            var near = bends.OrderBy(bend => bend.Bent).First();
            Assert.That(far.Bent, Is.InRange(130f, 145f), $"{part} bends to its limit one way...");
            Assert.That(far.Anatomical, Is.True, $"...and that way is {way}.");
            Assert.That(near.Bent, Is.LessThan(5f), $"{part} doesn't bend the other way.");
        }

        [Test]
        public void Dropped_ItFallsAndLiesOnTheGround_InOnePiece()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0f, -1f, 0f);
            _ground.transform.localScale = new Vector3(20f, 1f, 20f);
            _ragdoll.GoLimp();
            var length = Distance(HumanBodyBones.Head, HumanBodyBones.RightFoot);

            for (var i = 0; i < 250; i++)
                Physics.Simulate(Step);

            var pelvis = _ragdoll.Pelvis;
            Assert.That(pelvis.y, Is.InRange(-0.5f, 0f), "Lying on the ground (its top at -0.5 m).");
            Assert.That(Distance(HumanBodyBones.Head, HumanBodyBones.RightFoot), Is.LessThan(length + 0.1f), "Not pulled apart.");
            foreach (var body in _model.Root.GetComponentsInChildren<Rigidbody>())
                Assert.That(body.linearVelocity.magnitude, Is.LessThan(0.2f), body.name + " has come to rest.");
            foreach (var hitbox in _hitboxes)
                Assert.That(hitbox.transform.position.y, Is.GreaterThan(-0.6f), hitbox.name + " is on the ground, not in it.");
        }

        float Distance(HumanBodyBones a, HumanBodyBones b) => Vector3.Distance(_model.Bone(a).position, _model.Bone(b).position);

        // The look in the T-pose its avatar was made in (arms out, straight), where its joints' bends are measured from.
        void PoseT()
        {
            var animator = _model.Animators.First(each => each.isHuman);
            var tPose = animator.avatar.humanDescription.skeleton.ToDictionary(bone => bone.name);
            foreach (var bone in _model.Root.GetComponentsInChildren<Transform>(true))
            {
                if (bone != _model.Root.transform && tPose.TryGetValue(bone.name, out var at))
                    bone.SetLocalPositionAndRotation(at.position, at.rotation);
            }
        }
    }
}
