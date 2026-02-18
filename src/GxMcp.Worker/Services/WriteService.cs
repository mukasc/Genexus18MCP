using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections;
using Artech.Architecture.Common.Objects;
using Artech.Architecture.Common.Packages;
using GxMcp.Worker.Helpers;

namespace GxMcp.Worker.Services
{
    public class WriteService
    {
        private readonly ObjectService _objectService;
        private readonly BuildService _buildService;
        private readonly KbService _kbService;

        public WriteService(ObjectService objectService, BuildService buildService, KbService kbService)
        {
            _objectService = objectService;
            _buildService = buildService;
            _kbService = kbService;
        }

        public string WriteObject(string target, string partName, string newCode)
        {
            try
            {
                Logger.Info($"[WriteService] Native write to: {target} (Part: {partName})");
                
                var kb = _kbService.GetKB();
                string namePart = target.Contains(":") ? target.Split(':')[1] : target;

                KBObject obj = FindObject(kb, namePart);
                if (obj == null) return "{\"error\": \"Object not found: " + target + "\"}";

                Guid partType = GetPartTypeGuid(partName);
                if (partType == Guid.Empty) return "{\"error\": \"Invalid part type\"}";

                KBObjectPart part = obj.Parts[partType];
                if (part == null) return "{\"error\": \"Part not found\"}";

                // 1. Try setting 'Source' property (Standard for ProcedurePart, EventsPart, etc.)
                var sourceProp = part.GetType().GetProperty("Source", BindingFlags.Public | BindingFlags.Instance);
                if (sourceProp != null && sourceProp.CanWrite)
                {
                    sourceProp.SetValue(part, newCode, null);
                    Logger.Info("[WriteService] Updated via Part.Source property.");
                }
                else
                {
                    // 2. Fallback to Element.InnerXml (for some specific parts)
                    var elementProp = part.GetType().GetProperty("Element");
                    var element = elementProp?.GetValue(part, null);
                    if (element != null)
                    {
                        var innerXmlProp = element.GetType().GetProperty("InnerXml");
                        innerXmlProp?.SetValue(element, newCode, null);
                        Logger.Info("[WriteService] Updated via Element.InnerXml.");
                    }
                }

                // 3. Special Case: WorkWithPlus Source
                // If WWP is present, it often uses a custom part. Let's look for any part that might be WWP.
                foreach (KBObjectPart p in obj.Parts)
                {
                    string typeName = p.GetType().Name;
                    if (typeName.Contains("WorkWithPlus") || typeName.Contains("DVelop"))
                    {
                        var wwpSourceProp = p.GetType().GetProperty("Source");
                        wwpSourceProp?.SetValue(p, newCode, null);
                        Logger.Info($"[WriteService] Also updated suspected WWP part: {typeName}");
                    }
                }

                obj.Save();
                _objectService.Invalidate(target);

                return "{\"status\": \"Success\", \"object\": \"" + target + "\"}";
            }
            catch (Exception ex)
            {
                Logger.Error($"[WriteService Error] {ex.Message}");
                return "{\"error\": \"" + CommandDispatcher.EscapeJsonString(ex.Message) + "\"}";
            }
        }

        private KBObject FindObject(KnowledgeBase kb, string name)
        {
            foreach (KBObject kbo in kb.DesignModel.Objects)
            {
                if (string.Equals(kbo.Name, name, StringComparison.OrdinalIgnoreCase))
                    return kbo;
            }
            return null;
        }

        private Guid GetPartTypeGuid(string partName)
        {
            switch (partName?.ToLower())
            {
                case "source": return new Guid("528d1c06-a9c2-420d-bd35-21dca83f12ff");
                case "rules": return new Guid("9b0a32a3-de6d-4be1-a4dd-1b85d3741534");
                case "events": return new Guid("c414ed00-8cc4-4f44-8820-4baf93547173");
                default: return Guid.Empty;
            }
        }
    }
}
