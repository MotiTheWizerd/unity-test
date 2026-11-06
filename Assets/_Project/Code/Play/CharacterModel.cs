using System;
using Forge.Game.Motion;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// A character's look riding on its body: the character prefab (Content/Characters) standing on the body's feet,
    /// turned to the body's heading and animated as the body moves (<see cref="HumanoidAnimation"/>). The body is the
    /// collider, so the model's own colliders go, and the model shares the body's layer. Whoever has a body uses it: the
    /// player (<see cref="PlayerAvatar"/>), and characters of the world later. A model without animations just rides along.
    /// </summary>
    public sealed class CharacterModel : IDisposable
    {
        readonly HumanoidAnimation _animation;

        /// <param name="body">The body object the model rides on.</param>
        public CharacterModel(Transform body, GameObject prefab)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));

            Root = UnityEngine.Object.Instantiate(prefab, body, false);
            Root.name = prefab.name;
            foreach (var collider in Root.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.Destroy(collider);
            SetLayer(Root.transform, body.gameObject.layer);
            Renderers = Root.GetComponentsInChildren<Renderer>(true);
            Animators = Root.GetComponentsInChildren<Animator>(true);
            _animation = HumanoidAnimation.On(Root);
        }

        /// <summary>The model's object, a child of the body.</summary>
        public GameObject Root { get; }

        public Renderer[] Renderers { get; }

        public Animator[] Animators { get; }

        /// <summary>Turns the model to <paramref name="heading"/> (compass degrees).</summary>
        public void Face(float heading) => Root.transform.localRotation = Quaternion.Euler(0f, heading, 0f);

        /// <summary>Shows how the body moved over the last <paramref name="deltaTime"/> seconds (standing, walking, a jump).</summary>
        public void Animate(BodyMotion motion, float deltaTime) => _animation?.Show(motion, deltaTime);

        public void Dispose()
        {
            if (Root != null)
                UnityEngine.Object.Destroy(Root);
        }

        static void SetLayer(Transform transform, int layer)
        {
            transform.gameObject.layer = layer;
            foreach (Transform child in transform)
                SetLayer(child, layer);
        }
    }
}
