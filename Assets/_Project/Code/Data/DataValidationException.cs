using System;
using System.Collections.Generic;
using System.Linq;

namespace Forge.Data
{
    /// <summary>
    /// Raised when plugin data or a load order is invalid. Carries every problem
    /// found, not just the first, so modders can fix them all in one pass.
    /// </summary>
    public sealed class DataValidationException : Exception
    {
        public DataValidationException(IEnumerable<string> errors, Exception innerException = null)
            : this(errors?.ToArray() ?? Array.Empty<string>(), innerException)
        {
        }

        DataValidationException(string[] errors, Exception innerException)
            : base(BuildMessage(errors), innerException)
        {
            Errors = errors;
        }

        public IReadOnlyList<string> Errors { get; }

        static string BuildMessage(string[] errors) =>
            errors.Length == 1
                ? errors[0]
                : $"{errors.Length} validation errors:{Environment.NewLine}- " +
                  string.Join(Environment.NewLine + "- ", errors);
    }
}
