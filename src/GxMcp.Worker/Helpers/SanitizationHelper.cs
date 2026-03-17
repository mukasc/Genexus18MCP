using System;
using System.Text.RegularExpressions;

namespace GxMcp.Worker.Helpers
{
    public static class SanitizationHelper
    {
        private static readonly Regex _nameRegex = new Regex(@"^[a-zA-Z0-9_\-]+$", RegexOptions.Compiled);

        /// <summary>
        /// Sanitizes a GeneXus object name or environment name for safe inclusion in XML/MSBuild tasks.
        /// GeneXus names are typically alphanumeric with underscores.
        /// </summary>
        public static string SanitizeObjectName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            
            // If it doesn't match the standard pattern, we strip everything except safe characters
            if (!_nameRegex.IsMatch(name))
            {
                Logger.Warn($"[SECURITY] Potentially unsafe object name detected and sanitized: {name}");
                return Regex.Replace(name, @"[^a-zA-Z0-9_\-]", "");
            }
            
            return name;
        }

        /// <summary>
        /// Validates if a string is a safe object name.
        /// </summary>
        public static bool IsSafeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            return _nameRegex.IsMatch(name);
        }
    }
}
