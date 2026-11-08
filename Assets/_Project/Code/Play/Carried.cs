using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// A body being carried (<see cref="ObjectCarry"/>), Skyrim's way: held by the point it was taken at, as a hand holds
    /// it, so the rest of it hangs and swings under its own weight (a sword taken by the hilt hangs blade down; one taken
    /// by the middle balances). Every physics step a grip (a kinematic body of its own) goes toward where the carrier
    /// holds it (<see cref="Target"/>), and a joint draws the point held after it on a damped spring, free to turn and
    /// no stronger than a hand, so what is carried still knocks into things and walls stop it. It stays awake while held.
    /// Taken off, the grip goes and it falls again.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Carried : MonoBehaviour
    {
        // How the grip goes to where it is held: eased in over about this long, seconds, and at most this fast, meters a
        // second, quick enough to keep up with a turn of the head without flinging what it lifts from the ground.
        const float GripEase = 0.1f;
        const float MostSpeed = 12f;

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

        /// <summary>Takes <paramref name="body"/> up by the point <paramref name="point"/> on it (scene space).</summary>
        public static Carried Take(Rigidbody body, Vector3 point)
        {
            var carried = body.gameObject.AddComponent<Carried>();
            carried.Hold(point);
            return carried;
        }

        /// <summary>Where it is held, scene space: where the point it was taken by is drawn to.</summary>
        public Vector3 Target { get; set; }

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
        // joint ties that point to it, every turn free.
        void Hold(Vector3 point)
        {
            _held = transform.InverseTransformPoint(point);
            Target = point;
            var grip = new GameObject("Carry Grip").transform;
            grip.SetParent(transform.parent, false);
            grip.position = point;
            _grip = grip.gameObject.AddComponent<Rigidbody>();
            _grip.isKinematic = true;

            _joint = gameObject.AddComponent<ConfigurableJoint>();
            _joint.autoConfigureConnectedAnchor = false;
            _joint.connectedBody = _grip;
            _joint.anchor = _held;
            _joint.connectedAnchor = Vector3.zero;
            _joint.xMotion = _joint.yMotion = _joint.zMotion = ConfigurableJointMotion.Free;
            _joint.angularXMotion = _joint.angularYMotion = _joint.angularZMotion = ConfigurableJointMotion.Free;
            var mass = _rigidbody.mass;
            var drive = new JointDrive { positionSpring = Stiffness * mass, positionDamper = Damping * mass, maximumForce = MostForce };
            _joint.xDrive = _joint.yDrive = _joint.zDrive = drive;

            // Swinging is turning about the point held, so it is damped against how hard the body is to turn about it:
            // about its centre (the mean of its principal moments), and its mass that far from the point.
            var inertia = _rigidbody.inertiaTensor;
            var reach = Vector3.Distance(point, _rigidbody.worldCenterOfMass);
            var aboutPoint = (inertia.x + inertia.y + inertia.z) / 3f + mass * reach * reach;
            _joint.rotationDriveMode = RotationDriveMode.Slerp;
            _joint.slerpDrive = new JointDrive { positionSpring = 0f, positionDamper = SwingDamping * aboutPoint, maximumForce = float.MaxValue };
        }

        void FixedUpdate()
        {
            if (_grip != null)
                _grip.MovePosition(Vector3.SmoothDamp(_grip.position, Target, ref _gripVelocity, GripEase, MostSpeed, Time.fixedDeltaTime));
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
