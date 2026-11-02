namespace Forge.Editing.Commands
{
    /// <summary>What kind of value a command argument holds.</summary>
    public enum ParameterKind
    {
        Boolean,
        Integer,
        Number,
        String,

        /// <summary>A nested arguments class whose own properties carry <see cref="ParamAttribute"/>s.</summary>
        Object,

        /// <summary>A list of scalars or of nested arguments classes.</summary>
        List,
    }
}
