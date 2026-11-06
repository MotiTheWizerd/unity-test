namespace Forge.Data.Records
{
    /// <summary>What sort of number an <see cref="AttributeRecord"/> is.</summary>
    public enum AttributeKind
    {
        /// <summary>A plain number: carry weight, a skill, how much a sprint costs. Its value is all there is.</summary>
        Stat,

        /// <summary>A pool that is spent and refills (health, stamina, magicka): its value is the most it holds.</summary>
        Resource,
    }
}
