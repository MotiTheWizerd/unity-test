using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Forge.Tests
{
    /// <summary>
    /// Guards the module dependency rules described in ARCHITECTURE.md.
    /// The asmdef settings enforce these at compile time; these tests make
    /// a violation loud if someone loosens an asmdef.
    /// </summary>
    public class ArchitectureTests
    {
        static readonly string[] EngineFreeAssemblies = { "Forge.Core", "Forge.Data", "Forge.Data.Json", "Forge.Editing", "Forge.Streaming", "Forge.Agent", "Forge.Game" };

        [TestCaseSource(nameof(EngineFreeAssemblies))]
        public void EngineFreeAssembly_DoesNotReferenceUnity(string assemblyName)
        {
            var unityRefs = Assembly.Load(assemblyName)
                .GetReferencedAssemblies()
                .Select(r => r.Name)
                .Where(n => n.StartsWith("UnityEngine") || n.StartsWith("UnityEditor"))
                .ToArray();

            Assert.That(unityRefs, Is.Empty,
                $"{assemblyName} must stay engine-free but references: {string.Join(", ", unityRefs)}");
        }

        [Test]
        public void Core_DoesNotReferenceOtherForgeModules()
        {
            var forgeRefs = Assembly.Load("Forge.Core")
                .GetReferencedAssemblies()
                .Select(r => r.Name)
                .Where(n => n.StartsWith("Forge."))
                .ToArray();

            Assert.That(forgeRefs, Is.Empty,
                $"Forge.Core is the bottom layer but references: {string.Join(", ", forgeRefs)}");
        }

        [Test]
        public void DataModel_DoesNotDependOnSerializationLibraries()
        {
            var references = Assembly.Load("Forge.Data")
                .GetReferencedAssemblies()
                .Select(r => r.Name)
                .ToArray();

            Assert.That(references, Has.None.StartsWith("Newtonsoft"),
                "Serialization libraries belong in format modules such as Forge.Data.Json.");
            Assert.That(references, Has.None.EqualTo("Forge.Data.Json"));
        }

        [Test]
        public void Commands_DoNotDependOnAgentProtocols()
        {
            var references = Assembly.Load("Forge.Editing")
                .GetReferencedAssemblies()
                .Select(r => r.Name)
                .ToArray();

            Assert.That(references, Has.None.StartsWith("Newtonsoft"),
                "Commands are plain C#; turning them into JSON tools is Forge.Agent's job.");
            Assert.That(references, Has.None.EqualTo("Forge.Agent"));
        }
    }
}
