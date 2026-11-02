using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Forge.Agent.ClaudeCode
{
    /// <summary>
    /// Joins arguments into one command line that the standard Windows argument parser (the C runtime's, which
    /// .NET follows on every platform) splits back into exactly the same arguments: empty ones, spaces, quotes,
    /// backslashes and newlines included.
    /// </summary>
    public static class CommandLine
    {
        static readonly char[] NeedsQuotes = { ' ', '\t', '\n', '\v', '"' };

        public static string Join(IEnumerable<string> arguments)
        {
            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));
            return string.Join(" ", arguments.Select(Quote));
        }

        public static string Quote(string argument)
        {
            if (argument == null)
                throw new ArgumentNullException(nameof(argument));
            if (argument.Length > 0 && argument.IndexOfAny(NeedsQuotes) < 0)
                return argument;

            // Backslashes are literal unless they precede a quote: then each one, and the quote, is escaped.
            var quoted = new StringBuilder(argument.Length + 2);
            quoted.Append('"');
            var backslashes = 0;
            foreach (var c in argument)
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (c == '"')
                    quoted.Append('\\', backslashes * 2 + 1);
                else
                    quoted.Append('\\', backslashes);
                quoted.Append(c);
                backslashes = 0;
            }

            // Before the closing quote, trailing backslashes are doubled so they stay literal.
            quoted.Append('\\', backslashes * 2);
            quoted.Append('"');
            return quoted.ToString();
        }
    }
}
