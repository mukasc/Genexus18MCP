using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace GxMcp.Gateway.Routers
{
    public class ObjectRouter : IMcpModuleRouter
    {
        public string ModuleName => "Object";

        public object[] GetToolDefinitions()
        {
            return new object[]
            {
                new {
                    name = "genexus_read_object",
                    description = "Returns object metadata (GUID, Type, Parts). Use for structural inspection.",
                    inputSchema = new {
                        type = "object",
                        properties = new { name = new { type = "string", description = "Object name." } },
                        required = new[] { "name" }
                    }
                },
                new {
                    name = "genexus_read_source",
                    description = "Reads source code. Includes variable metadata. Supports pagination (offset/limit).",
                    inputSchema = new {
                        type = "object",
                        properties = new {
                            name = new { type = "string", description = "Object name." },
                            part = new { type = "string", description = "Part (Source, Rules, Events).", @default = "Source" },
                            offset = new { type = "integer", description = "Start line for pagination." },
                            limit = new { type = "integer", description = "Lines to read." }
                        },
                        required = new[] { "name" }
                    }
                },
                new {
                    name = "genexus_write_object",
                    description = "Updates object source code. Handles automatic variable injection.",
                    inputSchema = new {
                        type = "object",
                        properties = new {
                            name = new { type = "string", description = "Object name." },
                            part = new { type = "string", description = "Part name.", @default = "Source" },
                            code = new { type = "string", description = "Full source code to write." }
                        },
                        required = new[] { "name", "code" }
                    }
                },
                new {
                    name = "genexus_patch",
                    description = "Precise text replacement/insertion. Use unique 'context' (old_string) to target lines.",
                    inputSchema = new {
                        type = "object",
                        properties = new {
                            name = new { type = "string", description = "Object name." },
                            part = new { type = "string", description = "Part (Source, Rules, Events).", @default = "Source" },
                            operation = new { type = "string", description = "Replace, Insert_After, Append." },
                            content = new { type = "string", description = "New text." },
                            context = new { type = "string", description = "Exact 'old_string' to match." },
                            expectedCount = new { type = "integer", description = "Replacements expected. Defaults to 1.", @default = 1 }
                        },
                        required = new[] { "name", "operation", "content" }
                    }
                },
                new {
                    name = "genexus_get_variables",
                    description = "Lists all variables and their types in an object.",
                    inputSchema = new {
                        type = "object",
                        properties = new { name = new { type = "string", description = "Object name." } },
                        required = new[] { "name" }
                    }
                },
                new {
                    name = "genexus_get_attribute",
                    description = "Metadata for a GeneXus attribute (Type, Length, Table).",
                    inputSchema = new {
                        type = "object",
                        properties = new { name = new { type = "string", description = "Attribute name." } },
                        required = new[] { "name" }
                    }
                }
            };
        }

        public object ConvertToolCall(string toolName, JObject args)
        {
            switch (toolName)
            {
                case "genexus_read_object":
                    return new { module = "Read", action = "Export", target = args?["name"]?.ToString() };
                case "genexus_read_source":
                    return new { 
                        module = "Read", 
                        action = "ExtractSource", 
                        target = args?["name"]?.ToString(), 
                        part = args?["part"]?.ToString() ?? "Source",
                        offset = args?["offset"]?.ToObject<int?>(),
                        limit = args?["limit"]?.ToObject<int?>()
                    };
                case "genexus_write_object":
                    return new { module = "Write", action = args?["part"]?.ToString() ?? "Source", target = args?["name"]?.ToString(), payload = args?["code"]?.ToString() };
                case "genexus_patch":
                    return new { 
                        module = "Patch", 
                        action = "Apply", 
                        target = args?["name"]?.ToString(),
                        part = args?["part"]?.ToString() ?? "Source",
                        operation = args?["operation"]?.ToString(),
                        content = args?["content"]?.ToString(),
                        context = args?["context"]?.ToString(),
                        expectedCount = args?["expectedCount"]?.ToObject<int?>() ?? 1
                    };
                case "genexus_get_variables":
                    return new { module = "Read", action = "GetVariables", target = args?["name"]?.ToString() };
                case "genexus_get_attribute":
                    return new { module = "Read", action = "GetAttribute", target = args?["name"]?.ToString() };
                default:
                    return null;
            }
        }
    }
}
