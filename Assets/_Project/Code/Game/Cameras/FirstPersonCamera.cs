using System.Numerics;
using Forge.Core.Geometry;
using Forge.Game.Player;

namespace Forge.Game.Cameras
{
    /// <summary>
    /// Seeing through the player's eyes, looking along its look. The camera rides the eyes of the player's look as it
    /// animates (<see cref="Head"/>), eased (<see cref="FirstPersonSettings.HeadEase"/>) in the body's own frame, so a stride
    /// or a blow moves the view a little and steadily while walking and turning never lag; without a look it sits at
    /// <see cref="PlayerCharacter.Eyes"/>. The look turns only with the mouse, never with the head.
    /// </summary>
    public sealed class FirstPersonCamera : ICameraMode
    {
        readonly FirstPersonSettings _settings;
        Vector3? _eyes;

        public FirstPersonCamera(FirstPersonSettings settings = null)
        {
            _settings = settings ?? FirstPersonSettings.Default;
        }

        public FirstPersonSettings Settings => _settings;

        public BodyFacing Facing => BodyFacing.Look;

        public bool InsideHead => true;

        public float? NearClip => _settings.NearClip;

        /// <summary>Where the player's look puts its eyes, which the camera rides; null: at the player's eye height.</summary>
        public IEyePoint Head { get; set; }

        /// <summary>Starts framing afresh: the camera goes straight to the eyes, with no easing from before.</summary>
        public void Begin() => _eyes = null;

        public ViewPose Frame(PlayerCharacter player, float deltaTime)
        {
            var eyes = Head != null && Head.TryGetEyes(out var head) ? head : new Vector3(0f, player.EyeHeight, 0f);
            _eyes = _eyes.HasValue ? _eyes.Value + (eyes - _eyes.Value) * Glide.Share(deltaTime, _settings.HeadEase) : eyes;
            var body = Quaternion.CreateFromYawPitchRoll(player.BodyHeading * Compass.DegreesToRadians, 0f, 0f);
            return new ViewPose(player.Feet + Vector3.Transform(_eyes.Value, body), Looking(player.Look));
        }

        /// <summary>
        /// The rotation of a look: turned to its heading around +Y, then tilted by its pitch (a turn around +X points the
        /// view down, so looking up is the negative turn).
        /// </summary>
        public static Quaternion Looking(PlayerLook look) =>
            Quaternion.CreateFromYawPitchRoll(look.Heading * Compass.DegreesToRadians, -look.Pitch * Compass.DegreesToRadians, 0f);
    }
}
