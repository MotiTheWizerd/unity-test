using System;
using Forge.Editing.Commands;

namespace Forge.Editing.Building
{
    /// <summary>Turns an edit's refusal (an <see cref="ArgumentException"/> that says what would work) into a command refusal for the caller.</summary>
    static class Refusal
    {
        public static T Of<T>(Func<T> edit)
        {
            try
            {
                return edit();
            }
            catch (ArgumentException ex)
            {
                throw new CommandException(Sentence(ex));
            }
        }

        // The message without the parameter name the runtime appends (on a new line, or in parentheses).
        static string Sentence(ArgumentException ex)
        {
            var message = ex.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];
            var suffix = $" (Parameter '{ex.ParamName}')";
            return ex.ParamName != null && message.EndsWith(suffix, StringComparison.Ordinal) ? message.Substring(0, message.Length - suffix.Length) : message;
        }
    }
}
