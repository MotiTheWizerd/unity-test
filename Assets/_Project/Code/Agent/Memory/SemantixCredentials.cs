using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Forge.Agent.Memory
{
    /// <summary>
    /// The Semantix login on this machine. Signing in to the Semantix app writes the user's token to
    /// <c>~/.semantix/USER/credentials.json</c>; everything else that talks to the server as the user only reads it.
    /// </summary>
    public static class SemantixCredentials
    {
        public static string DefaultPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".semantix", "USER", "credentials.json");

        /// <summary>The bearer token in <paramref name="path"/>; throws <see cref="SemantixException"/> saying what to do when there is none.</summary>
        public static string ReadToken(string path)
        {
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                throw new SemantixException("not signed in to Semantix (sign in with the Semantix app first)");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new SemantixException($"couldn't read the Semantix login ({ex.Message})");
            }

            try
            {
                var token = (string)JObject.Parse(text)["token"];
                if (string.IsNullOrWhiteSpace(token))
                    throw new SemantixException("the Semantix login has no token (sign in again with the Semantix app)");
                return token.Trim();
            }
            catch (JsonException)
            {
                throw new SemantixException("the Semantix login file is damaged (sign in again with the Semantix app)");
            }
        }
    }
}
