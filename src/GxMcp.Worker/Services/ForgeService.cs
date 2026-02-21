using System;
using System.Collections.Generic;
using Artech.Architecture.Common.Objects;
using Artech.Genexus.Common.Objects;
using Artech.Genexus.Common.Parts;
using GxMcp.Worker.Helpers;
using Newtonsoft.Json.Linq;

namespace GxMcp.Worker.Services
{
    public class ForgeService
    {
        private readonly KbService _kbService;

        public ForgeService(KbService kbService)
        {
            _kbService = kbService;
        }

        public string Scaffold(string type, string name, JObject properties)
        {
            try
            {
                var kb = _kbService.GetKB();
                Logger.Info(string.Format("Scaffolding new {0}: {1}", type, name));

                KBObject obj = null;
                if (type.Equals("Procedure", StringComparison.OrdinalIgnoreCase) || type.Equals("Prc", StringComparison.OrdinalIgnoreCase))
                {
                    obj = new Procedure(kb.DesignModel);
                }
                else if (type.Equals("Transaction", StringComparison.OrdinalIgnoreCase) || type.Equals("Trn", StringComparison.OrdinalIgnoreCase))
                {
                    obj = new Transaction(kb.DesignModel);
                }
                else
                {
                    return "{\"error\":\"Scaffold for type '" + type + "' not implemented.\"}";
                }

                obj.Name = name;
                obj.Description = properties["description"]?.ToString() ?? ("Created by MCP: " + name);
                
                // Add default logic based on properties
                if (obj is Procedure prc)
                {
                    var source = prc.Parts.Get<SourcePart>();
                    source.Source = "// Template created by MCP\r\n" + (properties["code"]?.ToString() ?? "");
                }

                obj.Save();
                Logger.Info($"Scaffold complete: {name}");
                return "{\"status\":\"Success\", \"name\":\"" + name + "\", \"guid\":\"" + obj.Guid + "\"}";
            }
            catch (Exception ex)
            {
                Logger.Error($"Scaffold Error: {ex.Message}");
                return "{\"error\":\"" + CommandDispatcher.EscapeJsonString(ex.Message) + "\"}";
            }
        }
    }
}
