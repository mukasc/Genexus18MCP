using System;
using System.Linq;
using System.Text;
using Artech.Architecture.Common.Objects;
using Artech.Common.Diagnostics;

namespace GxMcp.Worker.Helpers
{
    public static class PersistenceExtensions
    {
        /// <summary>
        /// Validates the object and saves it, throwing a detailed exception with SDK error messages if either fails.
        /// </summary>
        public static void EnsureSave(this KBObject obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            OutputMessages msgs = new OutputMessages();
            
            // 1. Validate
            bool isValid = obj.Validate(msgs);
            if (!isValid || msgs.HasErrors)
            {
                string errorText = ExtractErrorText(msgs);
                throw new Exception($"Validation failed for {obj.TypeDescriptor.Name} '{obj.Name}': {errorText}");
            }

            // 2. Save
            obj.Save();
            
            // Check if save introduced new errors (msgs is updated by SDK if Save fails)
            if (msgs.HasErrors)
            {
                string errorText = ExtractErrorText(msgs);
                throw new Exception($"Save failed for {obj.TypeDescriptor.Name} '{obj.Name}': {errorText}");
            }
        }

        private static string ExtractErrorText(OutputMessages msgs)
        {
            if (msgs == null) return string.Empty;
            
            // Try to get FullText or ErrorText
            if (!string.IsNullOrEmpty(msgs.ErrorText)) return msgs.ErrorText;
            if (!string.IsNullOrEmpty(msgs.FullText)) return msgs.FullText;

            // Manual concatenation if props are empty but it has messages
            var errors = msgs.OnlyMessages
                             .Where(m => m is OutputError)
                             .Select(m => m.Text)
                             .ToList();

            if (errors.Any()) return string.Join(" | ", errors);
            
            return string.Empty;
        }
    }
}
