using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace GxMcp.Gateway.Routers
{
    public class SearchRouter : IMcpModuleRouter
    {
        public string ModuleName => "Search";

        public object[] GetToolDefinitions()
        {
            return new object[]
            {
                new {
                    name = "genexus_list_objects",
                    description = "List objects by Name, Type, or Description. Returns signatures and snippets.",
                    inputSchema = new {
                        type = "object",
                        properties = new {
                            filter = new { type = "string", description = "Search term (e.g. 'Customer')." },
                            limit = new { type = "integer", description = "Max results.", @default = 50 }
                        }
                    }
                },
                new {
                    name = "genexus_search",
                    description = "Search for references (usedby:Name), connections, and source snippets.",
                    inputSchema = new {
                        type = "object",
                        properties = new {
                            query = new { type = "string", description = "Query or 'usedby:Name'." }
                        },
                        required = new[] { "query" }
                    }
                }
            };
        }

        public object ConvertToolCall(string toolName, JObject args)
        {
            switch (toolName)
            {
                case "genexus_list_objects":
                case "genexus_search":
                    string q = args?["query"]?.ToString() ?? args?["filter"]?.ToString() ?? "";
                    return new { module = "Search", action = "Query", target = q, limit = args?["limit"]?.ToObject<int?>() ?? 50 };
                default:
                    return null;
            }
        }
    }
}
