using System.Collections.Generic;
using System.Linq;
using Forge.Game.Combat;
using Forge.Play;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using BodyPart = Forge.Game.Combat.BodyPart;

namespace Forge.Tests.Play
{
    // A character is met where it is drawn: a capsule over each part of its body, fitted to its own skin and hung from the
    // part's bone (Hitboxes).
    public class HitboxesTests
    {
        const string VikingF1 = "Assets/_Project/Content/Characters/VikingF1/VikingF1.prefab";

        GameObject _body;
        CharacterModel _model;
        IReadOnlyList<Hitbox> _hitboxes;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VikingF1);
            _body = new GameObject("Body");
            _model = new CharacterModel(_body.transform, prefab);
            _hitboxes = Hitboxes.Build(_model, prefab);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_body);

        [Test]
        public void EveryPartOfHerBody_HasAHitbox_HungFromItsBone_ThatStopsNothing()
        {
            var parts = System.Enum.GetValues(typeof(BodyPart)).Cast<BodyPart>().Where(part => part != BodyPart.None);

            Assert.That(_hitboxes.Select(hitbox => hitbox.Part).Distinct(), Is.EquivalentTo(parts));
            Assert.That(_hitboxes.Count(hitbox => hitbox.Part == BodyPart.Hand), Is.EqualTo(2), "One a side.");
            foreach (var hitbox in _hitboxes)
            {
                var collider = hitbox.GetComponent<CapsuleCollider>();
                Assert.That((hitbox.gameObject.layer, collider.isTrigger, hitbox.GetComponent<Rigidbody>().isKinematic),
                    Is.EqualTo((HitboxLayer.Index, true, true)), hitbox.name);
            }

            Assert.That(Part(HumanBodyBones.Head).transform.parent, Is.EqualTo(_model.Bone(HumanBodyBones.Head)));
        }

        [Test]
        public void EachIsAsWideAsHerSkinThere()
        {
            Assert.That(Radius(Part(HumanBodyBones.Head)), Is.InRange(0.07f, 0.13f), "A head.");
            Assert.That(Radius(Part(HumanBodyBones.LeftUpperArm)), Is.InRange(0.03f, 0.08f), "An upper arm.");
            Assert.That(Radius(Part(HumanBodyBones.LeftUpperLeg)), Is.InRange(0.05f, 0.11f), "A thigh.");
        }

        [Test]
        public void AShotAtHerHeadOrChest_MeetsIt_AndOneJustBesideHer_MissesHer()
        {
            var forward = _model.Root.transform.forward;
            var side = _model.Root.transform.right;
            var head = _model.Bone(HumanBodyBones.Head).position + Vector3.up * 0.08f;
            var chest = _model.Bone(HumanBodyBones.Chest).position;
            // Her rest pose has her arms down at her sides: beside her shins nothing of her stands.
            var shin = _model.Bone(HumanBodyBones.RightLowerLeg).position;

            Assert.That(Met(head + forward * 2f, -forward), Is.EqualTo(BodyPart.Head));
            Assert.That(Met(chest + forward * 2f, -forward), Is.EqualTo(BodyPart.Chest));
            Assert.That(Met(shin + side * 0.2f + forward * 2f, -forward), Is.EqualTo(BodyPart.None),
                "20 cm past her shin: past her, though her walking capsule (0.45 m) would be met.");
        }

        Hitbox Part(HumanBodyBones bone) => _hitboxes.Single(hitbox => hitbox.transform.parent == _model.Bone(bone));

        static float Radius(Hitbox hitbox) => hitbox.GetComponent<CapsuleCollider>().radius * hitbox.transform.lossyScale.x;

        // The part a line from `from` along `way` meets first; None when it meets none.
        static BodyPart Met(Vector3 from, Vector3 way)
        {
            var hits = Physics.RaycastAll(from, way, 4f, HitboxLayer.Mask, QueryTriggerInteraction.Collide);
            return hits.Length == 0 ? BodyPart.None : Hitbox.PartOf(hits.OrderBy(hit => hit.distance).First().collider);
        }
    }
}
