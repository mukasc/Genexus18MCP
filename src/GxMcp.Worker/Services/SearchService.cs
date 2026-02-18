using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace GxMcp.Worker.Services
{
    public class SearchService
    {
        private class SearchResult
        {
            public string File { get; set; }
            public string Content { get; set; }
            public int Score { get; set; }
        }

        public string Search(string query)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string cacheDir = Path.Combine(baseDir, "cache");

                if (!Directory.Exists(cacheDir))
                    return "{\"status\": \"No cache directory found. Run genexus_analyze first to populate cache.\"}";

                var files = Directory.GetFiles(cacheDir, "*.xml");
                if (files.Length == 0)
                    return "{\"status\": \"Cache is empty. Run genexus_analyze to populate.\"}";

                string[] terms = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var matches = new List<SearchResult>();

                foreach (var file in files)
                {
                    string content = File.ReadAllText(file);
                    int score = terms.Count(t => content.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);

                    if (score > 0)
                    {
                        matches.Add(new SearchResult { File = file, Content = content, Score = score });
                    }
                }

                var topMatches = matches.OrderByDescending(m => m.Score).Take(5).ToList();

                if (topMatches.Any())
                {
                    var results = new System.Collections.Generic.List<string>();
                    foreach (var m in topMatches)
                    {
                        string name = Path.GetFileNameWithoutExtension(m.File).Replace("_", ":");
                        string content = m.Content;
                        
                        // Extract source part for context
                        string sourceSnippet = "";
                        int srcIdx = content.IndexOf("<![CDATA[");
                        if (srcIdx >= 0)
                        {
                            int srcEnd = content.IndexOf("]]>", srcIdx);
                            if (srcEnd > srcIdx)
                            {
                                sourceSnippet = content.Substring(srcIdx + 9, Math.Min(srcEnd - srcIdx - 9, 500)); // Limit to 500 chars
                            }
                        }
                        
                        // Clean snippet
                        sourceSnippet = CommandDispatcher.EscapeJsonString(sourceSnippet.Replace("\r", "").Replace("\n", " ").Substring(0, Math.Min(sourceSnippet.Length, 200)) + "...");

                        results.Add("{\"name\":\"" + CommandDispatcher.EscapeJsonString(name) + "\",\"score\":" + m.Score + ",\"snippet\":\"" + sourceSnippet + "\"}");
                    }

                    return "{\"count\": " + topMatches.Count + ", \"results\": [" + string.Join(",", results) + "]}";
                }

                return "{\"count\": 0, \"results\": []}";
            }
            catch (Exception ex)
            {
                return "{\"error\": \"" + CommandDispatcher.EscapeJsonString(ex.Message) + "\"}";
            }
        }
    }
}
