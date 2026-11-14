using Forge.World.Assets;
using NUnit.Framework;
using UnityEngine;

namespace Forge.Tests.World
{
    public sealed class TurntablePreviewTests
    {
        // A subject may change what it wears while it is shown (the face editor puts another head on): what it took off
        // is gone without a fault, and what it put on draws only in its own render, as the rest of it does.
        [Test]
        public void Tick_DrawsWhatTheSubjectWearsNow_AsItWearsIt()
        {
            var preview = new TurntablePreview();
            try
            {
                preview.Resize(8, 8);
                GameObject subject = null;
                preview.Show("man", parent =>
                {
                    subject = new GameObject("Man");
                    subject.transform.SetParent(parent, false);
                    Part(subject, "Old head");
                    return subject;
                });
                preview.Tick(0f);

                Object.DestroyImmediate(subject.transform.Find("Old head").gameObject);
                var put = Part(subject, "New head");
                Assert.DoesNotThrow(() => preview.Tick(0f));
                Assert.That(put.forceRenderingOff, Is.True, "what it put on draws only in its own render");
            }
            finally
            {
                preview.Dispose();
            }
        }

        static Renderer Part(GameObject subject, string name)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(subject.transform, false);
            part.layer = subject.layer;
            return part.GetComponent<Renderer>();
        }
    }
}
