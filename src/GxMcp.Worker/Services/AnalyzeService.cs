using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace GxMcp.Worker.Services
{
    public class AnalyzeService
    {
        private readonly ObjectService _objectService;

        public AnalyzeService(ObjectService objectService)
        {
            _objectService = objectService;
        }

        public string Analyze(string target)
        {
            try
            {
                string xmlContent = _objectService.GetObjectXml(target);
                if (xmlContent == null) return "{\"error\": \"Object not found: " + CommandDispatcher.EscapeJsonString(target) + "\"}";

                var doc = new XmlDocument();
                doc.LoadXml(xmlContent);

                // Extract all source code from parts
                string fullCode = "";
                var partNodes = doc.GetElementsByTagName("Part");
                foreach (XmlNode pn in partNodes)
                {
                    var sourceNode = pn.SelectSingleNode("Source");
                    if (sourceNode != null)
                    {
                        fullCode += sourceNode.InnerText + "\n";
                    }
                }

                // Strip comments
                string clean = Regex.Replace(fullCode, @"(?s)/\*.*?\*/", "");
                clean = Regex.Replace(clean, @"//.*", "");

                // Extract calls
                var calls = Regex.Matches(clean, @"(?i)(?:call|udp|submit)\s*\(\s*['""]?([\w\.]+)")
                    .Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();

                // Extract tables
                var tables = Regex.Matches(clean, @"(?i)(?:for each|new)\s+([\w]+)")
                    .Cast<Match>().Select(m => m.Groups[1].Value)
                    .Where(t => t.ToLower() != "where" && t.ToLower() != "order")
                    .Distinct().ToList();

                // Semantic tags
                var tags = GetTags(target, fullCode);

                // Code insights (reviewer)
                var insights = GetInsights(fullCode);

                // Complexity Score
                int complexity = CalculateComplexity(fullCode);

                // Build JSON response
                string callsJson = "[" + string.Join(",", calls.Select(c => "\"" + CommandDispatcher.EscapeJsonString(c) + "\"")) + "]";
                string tablesJson = "[" + string.Join(",", tables.Select(t => "\"" + CommandDispatcher.EscapeJsonString(t) + "\"")) + "]";
                string tagsJson = "[" + string.Join(",", tags.Select(t => "\"" + t + "\"")) + "]";
                string insightsJson = "[" + string.Join(",", insights.Select(i => "{\"level\":\"" + i.Item1 + "\",\"message\":\"" + CommandDispatcher.EscapeJsonString(i.Item2) + "\"}")) + "]";

                return "{\"name\":\"" + CommandDispatcher.EscapeJsonString(target) + "\","
                     + "\"calls\":" + callsJson + ","
                     + "\"tables\":" + tablesJson + ","
                     + "\"tags\":" + tagsJson + ","
                     + "\"insights\":" + insightsJson + ","
                     + "\"complexity\":" + complexity + ","
                     + "\"codeLength\":" + fullCode.Length + "}";
            }
            catch (Exception ex)
            {
                return "{\"error\": \"" + CommandDispatcher.EscapeJsonString(ex.Message) + "\"}";
            }
        }

        private List<string> GetTags(string target, string code)
        {
            var tags = new List<string>();
            var rules = new Dictionary<string, string>
            {
                {"Integration", @"(?i)(httpclient|rest|json|tostring|fromjson|soap|location)"},
                {"Reporting", @"(?i)(print|output_file|pdf|report|header|footer)"},
                {"Heavy-Batch", @"(?i)(for each|commit|rollback|submit|blocking)"},
                {"Security", @"(?i)(gam|permission|encrypt|decrypt|login|authorization)"},
                {"File-System", @"(?i)(file\.|directory\.|path\.)"},
                {"Interface", @"(?i)(webpanel|form\.|control\.|event\s+'|onclick)"},
                {"Data-Logic", @"(?i)(where|order|definedby|new|delete|blocking)"},
                {"WorkWith", @"(?i)(wwp|WorkWithPlus|Grid|DynamicFilter)"}
            };

            foreach (var r in rules)
            {
                if (Regex.IsMatch(code, r.Value)) tags.Add(r.Key);
            }

            if (target.StartsWith("Prc")) tags.Add("Logic-Engine");
            if (target.StartsWith("Trn")) tags.Add("Data-Model");
            if (target.StartsWith("Wbp")) tags.Add("UI-Component");

            return tags.Distinct().ToList();
        }

        private List<Tuple<string, string>> GetInsights(string code)
        {
            var insights = new List<Tuple<string, string>>();

            if (Regex.IsMatch(code, @"(?s)for each.*?for each"))
                insights.Add(Tuple.Create("Warning", "Nested loop detected. Potential N+1 query performance risk."));

            if (Regex.IsMatch(code, @"for each") && !Regex.IsMatch(code, @"where"))
                insights.Add(Tuple.Create("Critical", "Loop without WHERE clause. High risk of Full Table Scan."));

            if (Regex.IsMatch(code, @"(?s)for each.*?commit.*?endfor"))
                insights.Add(Tuple.Create("Critical", "COMMIT inside loop. Severe performance impact."));

            if (Regex.IsMatch(code, @"https?://"))
                insights.Add(Tuple.Create("Warning", "Hardcoded URL detected. Use Location object."));

            return insights;
        }

        private int CalculateComplexity(string code)
        {
            int score = 1;
            score += Regex.Matches(code, @"\bif\b", RegexOptions.IgnoreCase).Count;
            score += Regex.Matches(code, @"\bdo\s+case\b", RegexOptions.IgnoreCase).Count;
            score += Regex.Matches(code, @"\bfor\s+each\b", RegexOptions.IgnoreCase).Count * 2;
            score += Regex.Matches(code, @"\bcall\b", RegexOptions.IgnoreCase).Count;
            return score;
        }
    }
}
