using System;
using System.Collections.Generic;
using System.Linq;
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

        [SerializeField, Min(0.1f), Tooltip("Meters from the feet to the eyes, for a look without a head; a character's own eyes are found on its head.")]
        float _eyeHeight = 1.65f;

        [SerializeField, Min(0.5f), Tooltip("Meters from the feet to the top of the head while crouched (Left Ctrl): what it passes under.")]
        float _crouchHeight = 1.0f;

        [SerializeField, Min(0f), Tooltip("The highest step walked up without jumping (a stair, a kerb, a root).")]
        float _stepHeight = 0.4f;

        [SerializeField, Range(0f, 89f), Tooltip("Steepest ground walked up, in degrees; steeper slopes are slid down.")]
        float _slopeLimit = 50f;

        [SerializeField, Tooltip("What the player looks like: a prefab standing on its feet (the pivot), facing +Z. The placeholder mannequin until a real character replaces it.")]
        GameObject _model;

        [SerializeField, Tooltip("Dev, while animation and combat are tuned: the characters the next-look key (F8) switches the player between, in order. Build Characters lists every character here.")]
        GameObject[] _looks = Array.Empty<GameObject>();

        [Header("Moving")]
        [SerializeField, Min(0.1f), Tooltip("While walking is switched on (Caps Lock), meters per second. Matches the Walk clip's stride.")]
        float _walkSpeed = 1.8f;

        [SerializeField, Min(0.1f), Tooltip("While crouched, meters per second. Matches the CrouchWalk clip's stride.")]
        float _crouchSpeed = 1.2f;

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

        [SerializeField, Min(0.05f), Tooltip("Seconds the use key is held to grab what is in sight instead of using it (a tap takes, a hold carries).")]
        float _grabHold = 0.3f;

        [SerializeField, Min(0.3f), Tooltip("How far in front of the eyes a carried thing is held, meters.")]
        float _holdDistance = 1.3f;

        [SerializeField, Min(0.1f), Tooltip("The heaviest thing the player can lift and carry, kilograms.")]
        float _mostCarriedMass = 30f;

        [SerializeField, Min(0f), Tooltip("How hard a carried thing is thrown (the attack key), newton-seconds: it flies at this over its mass...")]
        float _throwImpulse = 15f;

        [SerializeField, Min(0f), Tooltip("...up to this many meters a second.")]
        float _throwSpeed = 12f;

        [Header("Archery")]
        [SerializeField, Min(1f), Tooltip("How many times nearer the view is at a bow's full draw (1: no zoom); it closes in as the string is pulled.")]
        float _fullDrawZoom = 1.2f;

        [SerializeField, Min(0f), Tooltip("Seconds the draw's zoom takes to come most of the way in, and back out once the arrow is loosed.")]
        float _drawZoomEase = 0.15f;

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

        /// <summary>How tall the body is crouched: never above standing, nor shorter than its width.</summary>
        public float CrouchHeight => Mathf.Clamp(_crouchHeight, 2f * Radius, _height);

        public float StepHeight => Mathf.Min(_stepHeight, _height - 2f * Radius);

        public float SlopeLimit => _slopeLimit;

        /// <summary>The player's look; null leaves it unseen (first person works, third person shows nothing of it).</summary>
        public GameObject Model => _model;

        /// <summary>The characters the next-look key switches the player between (a dev key for now); may be empty.</summary>
        public IReadOnlyList<GameObject> Looks => _looks ?? Array.Empty<GameObject>();

        /// <summary>Lists <paramref name="looks"/> as the characters to switch between (used by content tools).</summary>
        public void SetLooks(IEnumerable<GameObject> looks) =>
            _looks = (looks ?? throw new ArgumentNullException(nameof(looks))).Where(look => look != null).ToArray();

        public bool StartInThirdPerson => _startInThirdPerson;

        /// <summary>How far from the eyes the use key reaches, meters.</summary>
        public float Reach => _reach;

        /// <summary>How near the middle of the view a thing need only be to be used, meters.</summary>
        public float AimRadius => _aimRadius;

        /// <summary>Seconds the use key is held to grab instead of use.</summary>
        public float GrabHold => _grabHold;

        /// <summary>How the player grabs, carries and throws loose things.</summary>
        public CarrySettings Carry() => new CarrySettings(_reach, _aimRadius, _holdDistance, _mostCarriedMass, _throwImpulse, _throwSpeed);

        /// <summary>How the view closes in as a bow is drawn.</summary>
        public DrawZoom DrawZoom() => new DrawZoom(_fullDrawZoom, _drawZoomEase);

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
                walkSpeed: _walkSpeed,
                crouchSpeed: _crouchSpeed);

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
