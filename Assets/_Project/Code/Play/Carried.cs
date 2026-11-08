using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// A body being carried (<see cref="ObjectCarry"/>), Skyrim's way, one of two ways (<see cref="CarryHold"/>). Held
    /// loose, it is held by the point it was taken at, as a hand holds it, so the rest of it hangs and swings under its own
    /// weight (a sword taken by the hilt hangs blade down; one taken by the middle balances). Held steady, it is held by
    /// its middle and kept turned as it lay, turning only as the carrier turns (<see cref="Facing"/>). Every physics step
    /// a grip (a kinematic body of its own) goes toward where the carrier holds it (<see cref="Target"/>), and a joint
    /// draws the point held after it on a damped spring no stronger than a hand, so what is carried still knocks into
    /// things and walls stop it. It stays awake while held. Taken off, the grip goes and it falls again.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Carried : MonoBehaviour
    {
        // How the grip goes to where it is held: eased in over about this long, seconds, and at most this fast, meters a
        // second, quick enough to keep up with a turn of the head without flinging what it lifts from the ground.
        const float GripEase = 0.1f;
        const float MostSpeed = 12f;

        // How fast the grip of what is held steady turns after the carrier, degrees a second: a quick turn of the head
        // swings it round smoothly, not in one step.
        const float MostTurnSpeed = 540f;

        // The hold's spring and damping, per kilogram held: critically damped at 20 radians a second, so it eases in
        // rather than bounces, alike for a cup and a chest.
        const float Stiffness = 400f;
        const float Damping = 40f;

        // The hardest the hold pulls, newtons, as a hand would: a 30 kg chest is held up with some to spare and lags a turn,
        // a cup follows at once.
        const float MostForce = 800f;

        // How quickly what hangs stops swinging about the point held, per second: a sword taken by the hilt swings a time
        // or two and hangs still. A body's own angular damping can't do it, being about its centre: hung from its tip, a
        // sword feels a seventh of it.
        const float SwingDamping = 4f;

        Rigidbody _rigidbody;
        Rigidbody _grip;
        Vector3 _gripVelocity;
        ConfigurableJoint _joint;
        Vector3 _held;
        RigidbodyInterpolation _interpolation;
        float _sleepThreshold;

        /// <summary>
        /// Takes <paramref name="body"/> up <paramref name="hold"/>: loose by the point <paramref name="point"/> on it
        /// (scene space), steady by its middle, by a carrier facing <paramref name="facing"/>.
        /// </summary>
        public static Carried Take(Rigidbody body, Vector3 point, CarryHold hold, Quaternion facing)
        {
            var carried = body.gameObject.AddComponent<Carried>();
            carried.Hold(hold == CarryHold.Steady ? body.worldCenterOfMass : point, hold, facing);
            return carried;
        }

        /// <summary>How it is held.</summary>
        public CarryHold Kind { get; private set; }

        /// <summary>Where it is held, scene space: where the point it was taken by is drawn to.</summary>
        public Vector3 Target { get; set; }

        /// <summary>Which way the carrier faces, level, scene space: what is held steady turns with it.</summary>
        public Quaternion Facing { get; set; } = Quaternion.identity;

        /// <summary>How far the point it was taken by is from where it is held, meters: what a wall between keeps it from.</summary>
        public float Gap => _rigidbody != null ? Vector3.Distance(transform.TransformPoint(_held), Target) : 0f;

        /// <summary>The body.</summary>
        public Rigidbody Body => _rigidbody;

        void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _interpolation = _rigidbody.interpolation;
            _sleepThreshold = _rigidbody.sleepThreshold;
            // Seen moving every frame, it is drawn between physics steps, not stepping.
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            // Hanging still from a still hand, it would fall asleep and stop following.
            _rigidbody.sleepThreshold = 0f;
            _rigidbody.WakeUp();
        }

        // The grip starts on the point taken, beside the body in the world's frame (so a rebase moves both alike), and the
        // joint ties that point to it. Held steady, the grip faces as the carrier does, and the joint locks the body's turn
        // to the grip's as it was when taken: a spring could not, the hold's pull being far stronger, a hair off its
        // centre, than any spring that would not shake.
        void Hold(Vector3 point, CarryHold hold, Quaternion facing)
        {
            Kind = hold;
            _held = transform.InverseTransformPoint(point);
            Target = point;
            Facing = facing;
            var grip = new GameObject("Carry Grip").transform;
            grip.SetParent(transform.parent, false);
            grip.SetPositionAndRotation(point, hold == CarryHold.Steady ? facing : Quaternion.identity);
            _grip = grip.gameObject.AddComponent<Rigidbody>();
            _grip.isKinematic = true;

            _joint = gameObject.AddComponent<ConfigurableJoint>();
            _joint.autoConfigureConnectedAnchor = false;
            _joint.connectedBody = _grip;
            _joint.anchor = _held;
            _joint.connectedAnchor = Vector3.zero;
            _joint.xMotion = _joint.yMotion = _joint.zMotion = ConfigurableJointMotion.Free;
            var mass = _rigidbody.mass;
            var drive = new JointDrive { positionSpring = Stiffness * mass, positionDamper = Damping * mass, maximumForce = MostForce };
            _joint.xDrive = _joint.yDrive = _joint.zDrive = drive;
            if (hold == CarryHold.Steady)
            {
                _joint.angularXMotion = _joint.angularYMotion = _joint.angularZMotion = ConfigurableJointMotion.Locked;
                return;
            }

            // Swinging is turning about the point held, so it is damped against how hard the body is to turn about it:
            // about its centre (the mean of its principal moments), and its mass that far from the point.
            _joint.angularXMotion = _joint.angularYMotion = _joint.angularZMotion = ConfigurableJointMotion.Free;
            var inertia = _rigidbody.inertiaTensor;
            var reach = Vector3.Distance(point, _rigidbody.worldCenterOfMass);
            var aboutPoint = (inertia.x + inertia.y + inertia.z) / 3f + mass * reach * reach;
            _joint.rotationDriveMode = RotationDriveMode.Slerp;
            _joint.slerpDrive = new JointDrive { positionSpring = 0f, positionDamper = SwingDamping * aboutPoint, maximumForce = float.MaxValue };
        }

        void FixedUpdate()
        {
            if (_grip == null)
                return;
            _grip.MovePosition(Vector3.SmoothDamp(_grip.position, Target, ref _gripVelocity, GripEase, MostSpeed, Time.fixedDeltaTime));
            if (Kind == CarryHold.Steady)
                _grip.MoveRotation(Quaternion.RotateTowards(_grip.rotation, Facing, MostTurnSpeed * Time.fixedDeltaTime));
        }

        void OnDestroy()
        {
            // At once: the game ending takes the body off in this same frame, and a joint holds on to its body.
            if (_joint != null)
                DestroyImmediate(_joint);
            if (_grip != null)
                Destroy(_grip.gameObject);
            if (_rigidbody == null)
                return;
            _rigidbody.interpolation = _interpolation;
            _rigidbody.sleepThreshold = _sleepThreshold;
        }
    }
}
