using System.Linq;
using Forge.Play.Hud;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Forge.Tests.Play
{
    public sealed class FaceEditorScreenTests
    {
        [Test]
        public void Parts_AreUniquelyNamed_EachWithUniquelyNamedControls()
        {
            var parts = FaceEditorScreen.Parts;
            Assert.That(parts.Select(p => p.Name), Is.Unique);
            foreach (var part in parts)
            {
                Assert.That(part.Controls, Is.Not.Empty, part.Name);
                Assert.That(part.Controls.Select(c => c.Name), Is.Unique, part.Name);
            }
        }

        [Test]
        public void Screen_ShowsTheChosenPartsControls_StartingInTheMiddle()
        {
            var parent = new VisualElement();
            var screen = new FaceEditorScreen(parent, null);
            try
            {
                Assert.That(screen.Chosen, Is.EqualTo("Head"));
                screen.Choose("Nose");
                screen.Tick(0f);

                Assert.That(screen.ShownControls, Is.EqualTo(FaceEditorScreen.Parts.Single(p => p.Name == "Nose").Controls.Length));
                Assert.That(screen.Value("Nose", "Width"), Is.EqualTo(FaceEditorScreen.Middle));
                Assert.That(screen.Value("Hair", "Style"), Is.EqualTo(0));
            }
            finally
            {
                screen.Dispose();
            }

            Assert.That(parent.childCount, Is.Zero);
        }
    }
}
