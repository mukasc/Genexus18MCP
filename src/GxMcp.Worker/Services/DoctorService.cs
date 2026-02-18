using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GxMcp.Worker.Services
{
    public class DoctorService
    {
        public string Diagnose(string logPath)
        {
            try
            {
                // Try provided path, then default
                if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath))
                {
                    logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "msbuild.log");
                }

                if (!File.Exists(logPath))
                    return "{\"status\": \"No build log found. Run a build first.\"}";

                string[] logContent = File.ReadAllLines(logPath);
                var errors = logContent
                    .Where(l => Regex.IsMatch(l, @"error\s*:\s*(spc\w+|rpg\w+)", RegexOptions.IgnoreCase))
                    .ToArray();

                if (errors.Length == 0)
                    return "{\"status\": \"Healthy. No specification errors found.\"}";

                // Diagnose each error
                var diagnoses = errors.Select(err =>
                {
                    var match = Regex.Match(err, @"(spc\w+|rpg\w+)", RegexOptions.IgnoreCase);
                    string code = match.Success ? match.Groups[1].Value : "unknown";
                    string prescription = GetPrescription(code);
                    string severity = GetSeverity(code);
                    return "{\"code\":\"" + code + "\",\"severity\":\"" + severity + "\",\"line\":\"" + CommandDispatcher.EscapeJsonString(err.Trim()) + "\",\"prescription\":\"" + CommandDispatcher.EscapeJsonString(prescription) + "\"}";
                });

                return "{\"errorCount\":" + errors.Length + ",\"diagnoses\":[" + string.Join(",", diagnoses) + "]}";
            }
            catch (Exception ex)
            {
                return "{\"error\": \"" + CommandDispatcher.EscapeJsonString(ex.Message) + "\"}";
            }
        }

        private string GetPrescription(string code)
        {
            switch (code.ToLower())
            {
                case "spc0096": return "Variable not defined. Use genexus_write_object to add it, or genexus_refactor with CleanVars.";
                case "spc0055": return "Type mismatch. Check variable type vs attribute type.";
                case "spc0001": return "Syntax error. Check for unclosed blocks (if/endif, for each/endfor).";
                case "spc0017": return "Attribute not found. Verify attribute name spelling or create it with genexus_create_object.";
                default: return "Consult GeneXus Wiki for error code " + code;
            }
        }
        private string GetSeverity(string code)
        {
            switch (code.ToLower())
            {
                case "spc0001": return "Critical"; // Syntax
                case "spc0096": return "High";     // Undefined var
                case "spc0055": return "High";     // Type mismatch
                case "spc0017": return "High";     // Missing attribute
                default: return "Medium";
            }
        }
    }
}
