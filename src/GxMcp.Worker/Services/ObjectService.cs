using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using Artech.Architecture.Common.Objects;
using Artech.Genexus.Common.Objects;
using Artech.Genexus.Common.Parts;
using Newtonsoft.Json.Linq;
using GxMcp.Worker.Helpers;
using GxMcp.Worker.Models;

namespace GxMcp.Worker.Services
{
    public class ObjectService
    {
        private readonly KbService _kbService;
        private readonly BuildService _buildService;
        private DataInsightService _dataInsightService;
        private UIService _uiService;

        public ObjectService(KbService kbService, BuildService buildService)
        {
            _kbService = kbService;
            _buildService = buildService;
        }

        public void SetDataInsightService(DataInsightService ds) { _dataInsightService = ds; }
        public void SetUIService(UIService ui) { _uiService = ui; }
        public KbService GetKbService() { return _kbService; }

        public SearchIndex GetIndex() { return _kbService.GetIndexCache().GetIndex(); }

        public KBObject FindObject(string target)
        {
            var sw = Stopwatch.StartNew();
            var kb = _kbService.GetKB();
            string typePart = null;
            string namePart = target;

            if (target.Contains(":"))
            {
                var parts = target.Split(':');
                typePart = parts[0];
                namePart = parts[1];
            }

            foreach (KBObject obj in kb.DesignModel.Objects.GetByName(null, null, namePart))
            {
                if (typePart == null || string.Equals(obj.TypeDescriptor.Name, typePart, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Debug(string.Format("FindObject '{0}' SUCCESS in {1}ms", target, sw.ElapsedMilliseconds));
                    return obj;
                }
            }
            return null;
        }

        public string ReadObject(string target)
        {
            var obj = FindObject(target);
            if (obj == null) return "{\"error\": \"Object not found\"}";

            var parts = new JArray();
            foreach (KBObjectPart p in obj.Parts)
            {
                parts.Add(new JObject { 
                    ["name"] = p.Type.ToString(), 
                    ["guid"] = p.Type.ToString() 
                });
            }

            return new JObject { 
                ["name"] = obj.Name, 
                ["type"] = obj.TypeDescriptor.Name,
                ["parts"] = parts
            }.ToString();
        }

        public string ReadObjectSource(string target, string partName, int? offset = null, int? limit = null)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var obj = FindObject(target);
                if (obj == null) return "{\"error\": \"Object not found\"}";

                Guid partGuid = MapLogicalPartToGuid(obj.TypeDescriptor.Name, partName);
                
                KBObjectPart part = null;
                foreach (KBObjectPart p in obj.Parts)
                {
                    if (p.Type == partGuid) { part = p; break; }
                }

                if (part == null) return "{\"error\": \"Part '" + partName + "' not found.\"}";

                JObject result = new JObject();
                if (part is ISource)
                {
                    ISource sourcePart = (ISource)part;
                    string content = sourcePart.Source;
                    
                    if (offset.HasValue || limit.HasValue)
                    {
                        string[] lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                        int start = offset ?? 0;
                        int count = limit ?? (lines.Length - start);
                        
                        if (start < 0) start = 0;
                        if (start >= lines.Length) count = 0;
                        else if (start + count > lines.Length) count = lines.Length - start;

                        string paginatedContent = string.Join(Environment.NewLine, lines.Skip(start).Take(count));
                        result["source"] = paginatedContent;
                        result["offset"] = start;
                        result["limit"] = count;
                        result["totalLines"] = lines.Length;

                        AddVariableMetadata(obj, paginatedContent, result);
                        Logger.Info(string.Format("ReadSource (Paginated {0}-{1}) SUCCESS", start, start+count));
                    }
                    else
                    {
                        result["source"] = content;
                        AddVariableMetadata(obj, content, result);
                        Logger.Info("ReadSource (Full Text) SUCCESS");
                    }
                }
                else
                {
                    result["xml"] = part.SerializeToXml();
                }

                if (_dataInsightService != null) result["dataContext"] = JObject.Parse(_dataInsightService.GetDataContext(target));
                if (_uiService != null) result["uiContext"] = JObject.Parse(_uiService.GetUIContext(target));

                return result.ToString();
            }
            catch (Exception ex)
            {
                return "{\"error\": \"" + CommandDispatcher.EscapeJsonString(ex.Message) + "\"}";
            }
        }

        private void AddVariableMetadata(KBObject obj, string source, JObject result)
        {
            try
            {
                var variables = new JArray();
                VariablesPart varPart = obj.Parts.Get<VariablesPart>();
                if (varPart == null) return;

                foreach (global::Artech.Genexus.Common.Variable v in varPart.Variables)
                {
                    if (source.Contains("&" + v.Name))
                    {
                        variables.Add(new JObject {
                            ["name"] = v.Name,
                            ["type"] = v.Type.ToString(),
                            ["length"] = v.Length,
                            ["decimals"] = v.Decimals
                        });
                    }
                }
                if (variables.Count > 0) result["variables"] = variables;
            }
            catch { }
        }

        public static Guid GetPartGuid(string name)
        {
            if (string.IsNullOrEmpty(name)) return Guid.Empty;
            switch (name.ToLower())
            {
                case "source": return Guid.Parse("00000000-0000-0000-0000-000000000001");
                case "rules": return Guid.Parse("00000000-0000-0000-0000-000000000002");
                case "events": return Guid.Parse("00000000-0000-0000-0000-000000000003");
                default: return Guid.Empty;
            }
        }

        private Guid MapLogicalPartToGuid(string objType, string partName)
        {
            string p = partName.ToLower();
            if (objType.Equals("Procedure", StringComparison.OrdinalIgnoreCase))
            {
                if (p == "source") return Guid.Parse("c5f0ef88-9ef8-4218-bf76-915024b3c48f");
                if (p == "rules") return Guid.Parse("00000000-0000-0000-0000-000000000002");
            }
            if (objType.Equals("Transaction", StringComparison.OrdinalIgnoreCase))
            {
                if (p == "structure") return Guid.Parse("00000000-0000-0000-0000-000000000001");
                if (p == "rules") return Guid.Parse("00000000-0000-0000-0000-000000000002");
                if (p == "events") return Guid.Parse("00000000-0000-0000-0000-000000000003");
            }
            return GetPartGuid(partName);
        }
    }
}
