using Forge.Game.Cameras;
using Forge.Game.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Forge.Play
{
    /// <summary>
    /// How the player is built and moves, and which controls drive it: one asset (Settings/Player), so the feel is tuned
    /// in the Inspector, never in code. The rules read a plain <see cref="LocomotionSettings"/> made from it.
    /// </summary>
    [CreateAssetMenu(menuName = "Forge/Player Profile", fileName = "PlayerProfile")]
    public sealed class PlayerProfile : ScriptableObject
    {
        [Header("Body")]
        [SerializeField, Min(0.5f), Tooltip("Meters from the feet to the top of the head.")]
        float _height = 1.8f;

        [SerializeField, Min(0.1f), Tooltip("Meters from the body's middle to its side.")]
        float _radius = 0.3f;

        [SerializeField, Min(0.1f), Tooltip("Meters from the feet to the eyes, where the first-person camera sits.")]
        float _eyeHeight = 1.65f;

        [SerializeField, Min(0f), Tooltip("The highest step walked up without jumping (a stair, a kerb, a root).")]
        float _stepHeight = 0.4f;

        [SerializeField, Range(0f, 89f), Tooltip("Steepest ground walked up, in degrees; steeper slopes are slid down.")]
        float _slopeLimit = 50f;

        [SerializeField, Tooltip("What the player looks like: a prefab standing on its feet (the pivot), facing +Z. The placeholder mannequin until a real character replaces it.")]
        GameObject _model;

        [Header("Moving")]
        [SerializeField, Min(0.1f), Tooltip("While walking is switched on (Caps Lock), meters per second. Matches the Walk clip's stride.")]
        float _walkSpeed = 1.8f;

        [SerializeField, Min(0.1f), Tooltip("The everyday pace, meters per second.")]
        float _runSpeed = 4.5f;

        [SerializeField, Min(0.1f), Tooltip("While sprint is held, meters per second.")]
        float _sprintSpeed = 7.5f;

        [SerializeField, Min(0f), Tooltip("How high a jump lifts the feet, meters.")]
        float _jumpHeight = 1.1f;

        [SerializeField, Min(0.1f), Tooltip("Pull down in the air, meters per second squared.")]
        float _gravity = 18f;

        [SerializeField, Min(0.1f), Tooltip("How fast the pace changes on the ground, meters per second squared.")]
        float _groundAcceleration = 45f;

        [SerializeField, Min(0f), Tooltip("How much a jump or fall can be steered, meters per second squared.")]
        float _airAcceleration = 6f;

        [SerializeField, Min(0f), Tooltip("Seconds after stepping off an edge in which a jump still counts.")]
        float _coyoteTime = 0.12f;

        [SerializeField, Min(1f), Tooltip("Degrees per second the body turns toward where it goes, in third person.")]
        float _turnRate = 720f;

        [Header("First person")]
        [SerializeField, Min(0f), Tooltip("Seconds the camera takes to follow most of the way to where the head went (a stride's bob, a blow's lean); 0 rides the head exactly.")]
        float _firstPersonHeadEase = 0.1f;

        [SerializeField, Min(0.01f), Tooltip("How near the first-person camera sees, meters: near enough for the hands and what they hold.")]
        float _firstPersonNearClip = 0.05f;

        [Header("Third person")]
        [SerializeField, Tooltip("Start playing in third person (F switches either way).")]
        bool _startInThirdPerson;

        [SerializeField, Min(0.1f), Tooltip("Meters behind the shoulders the camera starts.")]
        float _cameraDistance = 3.5f;

        [SerializeField, Min(0.1f), Tooltip("The closest the wheel brings the camera; one notch nearer is first person.")]
        float _cameraMinDistance = 1.2f;

        [SerializeField, Min(0.1f), Tooltip("The farthest the wheel takes the camera.")]
        float _cameraMaxDistance = 8f;

        [SerializeField, Min(0.01f), Tooltip("Meters per wheel notch.")]
        float _cameraZoomStep = 0.6f;

        [SerializeField, Min(0f), Tooltip("Seconds the camera takes to glide most of the way to where the wheel sent it (0: it jumps).")]
        float _cameraZoomEase = 0.12f;

        [SerializeField, Min(0.1f), Tooltip("Height above the feet the camera orbits around (about the shoulders).")]
        float _cameraPivotHeight = 1.6f;

        [SerializeField, Tooltip("Meters to the right of the body the camera sits (0: straight behind).")]
        float _cameraShoulderOffset = 0.35f;

        [SerializeField, Min(0.01f), Tooltip("How far short of walls the camera stops.")]
        float _cameraRadius = 0.2f;

        [SerializeField, Min(0.1f), Tooltip("Meters per second the camera eases back out once a wall stops blocking it.")]
        float _cameraReturnSpeed = 6f;

        [Header("Interaction")]
        [SerializeField, Min(0.1f), Tooltip("How far from the eyes the use key reaches (E: take, later open, talk), meters.")]
        float _reach = 2.5f;

        [SerializeField, Min(0f), Tooltip("How near the middle of the view a thing need only be to be used, meters (0: dead on), so small things are easy to aim at.")]
        float _aimRadius = 0.1f;

        [Header("Controls")]
        [SerializeField, Tooltip("The input actions; the player reads its map (Settings/InputSystem_Actions).")]
        InputActionAsset _controls;

        [SerializeField, Tooltip("The action map the player reads: Move, Look, Sprint, Jump, ToggleWalk, ToggleView, Zoom, Interact.")]
        string _controlsMap = "Player";

        [SerializeField, Min(0.001f), Tooltip("Degrees the view turns per pixel the mouse moves.")]
        float _lookSensitivity = 0.1f;

        public float Height => _height;

        public float Radius => Mathf.Min(_radius, _height * 0.5f);

        public float EyeHeight => Mathf.Min(_eyeHeight, _height);

        public float StepHeight => Mathf.Min(_stepHeight, _height - 2f * Radius);

        public float SlopeLimit => _slopeLimit;

        /// <summary>The player's look; null leaves it unseen (first person works, third person shows nothing of it).</summary>
        public GameObject Model => _model;

        public bool StartInThirdPerson => _startInThirdPerson;

        /// <summary>How far from the eyes the use key reaches, meters.</summary>
        public float Reach => _reach;

        /// <summary>How near the middle of the view a thing need only be to be used, meters.</summary>
        public float AimRadius => _aimRadius;

        public InputActionAsset Controls => _controls;

        public string ControlsMap => _controlsMap;

        public float LookSensitivity => _lookSensitivity;

        public LocomotionSettings Locomotion() =>
            new LocomotionSettings(
                runSpeed: _runSpeed,
                sprintSpeed: _sprintSpeed,
                jumpHeight: _jumpHeight,
                gravity: _gravity,
                groundAcceleration: _groundAcceleration,
                airAcceleration: _airAcceleration,
                coyoteTime: _coyoteTime,
                turnRate: _turnRate,
                walkSpeed: _walkSpeed);

        public FirstPersonSettings FirstPerson() => new FirstPersonSettings(headEase: _firstPersonHeadEase, nearClip: _firstPersonNearClip);

        public ThirdPersonSettings ThirdPerson() =>
            new ThirdPersonSettings(
                distance: _cameraDistance,
                minDistance: _cameraMinDistance,
                maxDistance: Mathf.Max(_cameraMinDistance, _cameraMaxDistance),
                zoomStep: _cameraZoomStep,
                pivotHeight: _cameraPivotHeight,
                shoulderOffset: _cameraShoulderOffset,
                radius: _cameraRadius,
                returnSpeed: _cameraReturnSpeed,
                zoomEase: _cameraZoomEase);
    }
}
