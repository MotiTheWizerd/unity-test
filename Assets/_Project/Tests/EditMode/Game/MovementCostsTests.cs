using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
using Forge.Game.Attributes;
using Forge.Game.Motion;
using Forge.Game.Player;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class MovementCostsTests
    {
        const float Frame = 1f / 60f;

        // Endless flat ground at height 0.
        sealed class FlatGround : IPlayerMotor
        {
            public Vector3 Position { get; private set; }

            public MotorContact Move(Vector3 displacement)
            {
                var next = Position + displacement;
                var grounded = next.Y <= 0f;
                if (grounded)
                    next.Y = 0f;
                Position = next;
                return new MotorContact(grounded, false);
            }

            public void Teleport(Vector3 feet) => Position = feet;
        }

        // The base game's moving attributes: 100 health and stamina, stamina back 10 a second after 1.5 s, sprint 10 a
        // second, a jump 10, falls safe to 4 m and 10 health a meter past it.
        public static AttributeSet CoreSet() => AttributeSet.From(new[]
        {
            new AttributeRecord(CoreAttributes.Health, "Health", AttributeKind.Resource, 100f),
            new AttributeRecord(CoreAttributes.Stamina, "Stamina", AttributeKind.Resource, 100f,
                regenRate: CoreAttributes.StaminaRegen, regenDelay: CoreAttributes.StaminaRegenDelay),
            Stat(CoreAttributes.StaminaRegen, 10f),
            Stat(CoreAttributes.StaminaRegenDelay, 1.5f),
            Stat(CoreAttributes.SprintCost, 10f),
            Stat(CoreAttributes.JumpCost, 10f),
            Stat(CoreAttributes.FallSafeHeight, 4f),
            Stat(CoreAttributes.FallDamage, 10f),
        });

        static AttributeRecord Stat(RecordId id, float value) => new AttributeRecord(id, id.Name, AttributeKind.Stat, value);

        static PlayerCharacter Player(out ActorAttributes attributes, float height = 0f)
        {
            attributes = new ActorAttributes(CoreSet());
            var player = new PlayerCharacter(new FlatGround(), LocomotionSettings.Default, 1.65f, attributes);
            player.PlaceAt(new Vector3(0f, height, 0f), 0f);
            player.Tick(PlayerCommands.None, Frame);
            return player;
        }

        static void Run(PlayerCharacter player, PlayerCommands commands, float seconds)
        {
            for (var t = 0f; t < seconds; t += Frame)
                player.Tick(commands, Frame);
        }

        static PlayerCommands Forward(bool sprint = false, bool jump = false) =>
            new PlayerCommands(new Vector2(0f, 1f), Vector2.Zero, sprint, jump);

        static float GroundSpeed(PlayerCharacter player) =>
            new Vector2(player.Locomotion.Velocity.X, player.Locomotion.Velocity.Z).Length();

        [Test]
        public void Sprinting_SpendsStamina_RunningDoesNot()
        {
            var player = Player(out var attributes);

            Run(player, Forward(), 2f);
            Assert.That(attributes.Current(CoreAttributes.Stamina), Is.EqualTo(100f));

            Run(player, Forward(sprint: true), 3f);
            Assert.That(attributes.Current(CoreAttributes.Stamina), Is.EqualTo(70f).Within(0.5f));
        }

        [Test]
        public void SprintingStill_CostsNothing()
        {
            var player = Player(out var attributes);

            Run(player, new PlayerCommands(Vector2.Zero, Vector2.Zero, sprint: true, jump: false), 2f);

            Assert.That(attributes.Current(CoreAttributes.Stamina), Is.EqualTo(100f));
        }

        [Test]
        public void RunningOut_LeavesItWalking_UntilAQuarterIsBack()
        {
            var player = Player(out var attributes);

            Run(player, Forward(sprint: true), 11f);
            Assert.That(player.Costs.Winded, Is.True);
            Assert.That(GroundSpeed(player), Is.EqualTo(LocomotionSettings.Default.WalkSpeed).Within(1e-3f), "Too tired to run, sprint held or not.");

            // Still holding sprint, it walks: the stamina comes back after the delay, and at a quarter it runs again.
            Run(player, Forward(sprint: true), 1.5f + 2.6f);
            Assert.That(player.Costs.Winded, Is.False);
            Run(player, Forward(sprint: true), 0.5f);
            Assert.That(GroundSpeed(player), Is.EqualTo(LocomotionSettings.Default.SprintSpeed).Within(1e-3f));
        }

        [Test]
        public void AJump_SpendsItsCost_AndNeedsIt()
        {
            var player = Player(out var attributes);

            player.Tick(Forward(jump: true), Frame);
            Assert.That(attributes.Current(CoreAttributes.Stamina), Is.EqualTo(90f));
            Run(player, PlayerCommands.None, 1f);

            attributes.Drain(CoreAttributes.Stamina, 85f);
            var shortOf = new System.Collections.Generic.List<RecordId>();
            player.Costs.ShortOf += shortOf.Add;
            player.Tick(Forward(jump: true), Frame);
            Assert.That(player.Locomotion.Jumped, Is.False, "Not enough stamina to jump.");
            Assert.That(shortOf, Is.EqualTo(new[] { CoreAttributes.Stamina }), "Told, so the HUD can flash.");
        }

        [Test]
        public void AFall_PastTheSafeHeight_CostsHealth_PerMeter()
        {
            var player = Player(out var attributes, height: 10f);

            Run(player, PlayerCommands.None, 3f);

            Assert.That(attributes.Current(CoreAttributes.Health), Is.EqualTo(100f - (10f - 4f) * 10f).Within(0.5f));
        }

        [Test]
        public void ASmallFall_OrAJump_CostsNoHealth()
        {
            var player = Player(out var attributes, height: 3f);
            Run(player, PlayerCommands.None, 2f);

            player.Tick(Forward(jump: true), Frame);
            Run(player, Forward(), 2f);

            Assert.That(attributes.Current(CoreAttributes.Health), Is.EqualTo(100f));
        }

        [Test]
        public void TheCosts_AreAttributes_ThatModifiersChange()
        {
            var player = Player(out var attributes);
            attributes.Add(new ModifierSource("item:boots"), AttributeModifier.Multiply(CoreAttributes.SprintCost, 0.5f));

            Run(player, Forward(sprint: true), 4f);

            Assert.That(attributes.Current(CoreAttributes.Stamina), Is.EqualTo(80f).Within(0.5f));
        }

        [Test]
        public void ARebase_MidFall_KeepsTheFall()
        {
            // The world shifts everything down 100 m under a falling body: the top it fell from moves with it.
            var tracker = new FallTracker();
            tracker.Step(false, 10f);
            tracker.Shift(-100f);
            Assert.That(tracker.Step(true, -100f), Is.EqualTo(10f).Within(1e-4f));
        }

        [Test]
        public void TheRules_NeedTheirAttributes() =>
            Assert.Throws<DataValidationException>(() => new MovementCosts(new ActorAttributes(AttributeSet.From(new[] { Stat(CoreAttributes.JumpCost, 1f) }))));
    }
}
