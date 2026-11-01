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
        static readonly string[] EngineFreeAssemblies = { "Forge.Core", "Forge.Data" };

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
    }
}
