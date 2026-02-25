using System;
using System.Collections.Generic;
using System.Linq;
using Artech.Architecture.Common.Objects;
using Artech.Genexus.Common.Parts;
using Artech.Architecture.Common.Services;
using GxMcp.Worker.Services;
using Artech.Genexus.Common.Objects;
using Artech.Common.Collections;
using Artech.Genexus.Common;

namespace GxMcp.Worker.Helpers
{
    public static class VariableInjector
    {
        private static readonly HashSet<string> StandardVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Pgmname", "Pgmdesc", "Today", "Time", "Mode", "Message", "EventName", "CtlName"
        };

        public static void InjectVariables(KBObject obj, string code, Models.SearchIndex index = null)
        {
            var variablesPart = obj.Parts.Get<VariablesPart>();
            if (variablesPart == null) return;

            var matches = System.Text.RegularExpressions.Regex.Matches(code, @"&(\w+)");
            var varNames = matches.Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .ToList();

            foreach (var varName in varNames)
            {
                if (!variablesPart.Variables.Any(v => v.Name.Equals(varName, StringComparison.OrdinalIgnoreCase)))
                {
                    global::Artech.Genexus.Common.Variable v = CreateVariable(variablesPart, varName, index);
                    if (v != null)
                    {
                        variablesPart.Variables.Add(v);
                        Logger.Info($"Injected variable: {varName} into {obj.Name}");
                    }
                }
            }
        }

        internal static global::Artech.Genexus.Common.Variable CreateVariable(VariablesPart part, string name, Models.SearchIndex index = null)
        {
            global::Artech.Genexus.Common.Variable v = new global::Artech.Genexus.Common.Variable(part);
            v.Name = name;

            // 1. Inherit from Attribute (FAST INDEX LOOKUP)
            if (index != null)
            {
                string key = "Attribute:" + name;
                if (index.Objects.TryGetValue(key, out var entry))
                {
                    if (TryParseDbType(entry.DataType, out var itype))
                    {
                        v.Type = itype;
                        v.Length = entry.Length;
                        v.Decimals = entry.Decimals;
                        Logger.Info($"Injected variable {name} inheriting from INDEXED attribute {name}");
                        return v;
                    }
                }
            }

            // Fallback (SDK lookup - only if not in index or index not provided)
            var attribute = FindAttribute(part.Model, name);
            if (attribute != null)
            {
                v.Type = attribute.Type;
                v.Length = attribute.Length;
                v.Decimals = attribute.Decimals;
                v.Signed = attribute.Signed;
                Logger.Info($"Injected variable {name} inheriting from SDK attribute {attribute.Name}");
                return v;
            }

            // 2. Naming Heuristics
            string lowerName = name.ToLower();

            // Boolean
            if (lowerName.StartsWith("is") || lowerName.StartsWith("has") || lowerName.StartsWith("flg") || 
                lowerName.Contains("ativo") || lowerName.Contains("pode") || lowerName.EndsWith("ok"))
            {
                v.Type = global::Artech.Genexus.Common.eDBType.Boolean;
                Logger.Info($"Injected Boolean variable: {name}");
                return v;
            }

            // Date / DateTime
            if (lowerName.EndsWith("data") || lowerName.EndsWith("dt") || lowerName.Contains("emissao") || lowerName.Contains("vencimento"))
            {
                v.Type = global::Artech.Genexus.Common.eDBType.DATE;
                Logger.Info($"Injected Date variable: {name}");
                return v;
            }
            if (lowerName.Contains("hora") || lowerName.Contains("timestamp") || lowerName.Contains("moment"))
            {
                v.Type = global::Artech.Genexus.Common.eDBType.DATETIME;
                Logger.Info($"Injected DateTime variable: {name}");
                return v;
            }

            // Numeric
            if (lowerName.EndsWith("id") || lowerName.EndsWith("seq") || lowerName.EndsWith("qtd") || 
                lowerName.Contains("valor") || lowerName.Contains("preco") || lowerName.Contains("total"))
            {
                v.Type = global::Artech.Genexus.Common.eDBType.NUMERIC;
                v.Length = 10;
                v.Decimals = lowerName.Contains("valor") || lowerName.Contains("preco") || lowerName.Contains("total") ? 2 : 0;
                Logger.Info($"Injected Numeric variable: {name} ({v.Length},{v.Decimals})");
                return v;
            }

            // 3. Fallback: VarChar(100)
            v.Type = global::Artech.Genexus.Common.eDBType.VARCHAR;
            v.Length = 100;
            Logger.Info($"Injected Default VarChar variable: {name}");

            return v;
        }

        public static string GetVariablesAsText(KBObject obj)
        {
            var varPart = obj.Parts.Get<VariablesPart>();
            if (varPart == null) return string.Empty;
            return GetVariablesAsText(varPart);
        }

        public static string GetVariablesAsText(VariablesPart varPart)
        {
            var sb = new System.Text.StringBuilder();
            foreach (global::Artech.Genexus.Common.Variable v in varPart.Variables)
            {
                sb.AppendLine(string.Format("&{0} : {1}({2}{3})", v.Name, v.Type, v.Length, v.Decimals > 0 ? "," + v.Decimals : ""));
            }
            return sb.ToString();
        }

        public static void SetVariablesFromText(VariablesPart part, string text)
        {
            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var seenVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in lines)
            {
                // Format: &Name : Type(Length,Decimals) [Collection]
                var match = System.Text.RegularExpressions.Regex.Match(line, @"&?(\w+)\s*:\s*([\w\.\-]+)(?:\s*\(\s*(\d+)(?:\s*,\s*(\d+))?\s*\))?(?:\s+(Collection))?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string name = match.Groups[1].Value;
                    string typeStr = match.Groups[2].Value;
                    int length = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
                    int decimals = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 0;
                    bool isCollection = match.Groups[5].Success;

                    seenVars.Add(name);

                    var v = part.Variables.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (v == null)
                    {
                        v = new global::Artech.Genexus.Common.Variable(part);
                        v.Name = name;
                        part.Variables.Add(v);
                    }

                    v.IsCollection = isCollection;

                    // 1. Map string type to eDBType (including Aliases)
                    if (TryParseDbType(typeStr, out var dbType))
                    {
                        v.Type = dbType;
                        v.Length = length;
                        v.DomainBasedOn = null; 
                        v.SetPropertyValue("DataType", null); // Reset user type if it was set
                    }
                    else
                    {
                        // 2. Resolve as Domain, SDT, or BC
                        var targetObj = ResolveTypeObject(part.Model, typeStr);
                        if (targetObj != null)
                        {
                            if (targetObj is global::Artech.Genexus.Common.Objects.Domain dom)
                                v.DomainBasedOn = dom;
                            else if (targetObj.TypeDescriptor.Name.Equals("SDT", StringComparison.OrdinalIgnoreCase))
                            {
                                v.Type = global::Artech.Genexus.Common.eDBType.GX_SDT;
                                v.SetPropertyValue("DataType", targetObj.Key);
                            }
                            else if (targetObj is global::Artech.Genexus.Common.Objects.Transaction trn && trn.IsBusinessComponent)
                            {
                                v.Type = global::Artech.Genexus.Common.eDBType.GX_BUSCOMP;
                                v.SetPropertyValue("DataType", targetObj.Key);
                            }
                            Logger.Info($"Resolved variable {name} type to {targetObj.TypeDescriptor.Name}: {targetObj.Name}");
                        }
                    }
                }
            }

            // Remove variables not in the text, except standard variables
            var toRemove = part.Variables
                .Where(v => !seenVars.Contains(v.Name) && !StandardVariables.Contains(v.Name))
                .ToList();

            foreach (var v in toRemove)
            {
                part.Variables.Remove(v);
                Logger.Info($"Removed variable {v.Name} (no longer in text)");
            }
        }

        public static bool TryParseDbType(string typeStr, out global::Artech.Genexus.Common.eDBType type)
        {
            // Type Aliases mapping
            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Character", "VARCHAR" },
                { "VarChar", "VARCHAR" },
                { "Numeric", "NUMERIC" },
                { "Boolean", "Boolean" },
                { "Date", "DATE" },
                { "DateTime", "DATETIME" },
                { "Blob", "BLOB" },
                { "Image", "IMAGE" },
                { "Audio", "AUDIO" },
                { "Video", "VIDEO" },
                { "GUID", "GUID" },
                { "Geography", "GEOGRAPHY" }
            };

            if (aliases.TryGetValue(typeStr, out var mappedType))
            {
                typeStr = mappedType;
            }

            return Enum.TryParse<global::Artech.Genexus.Common.eDBType>(typeStr, true, out type);
        }

        public static KBObject ResolveTypeObject(KBModel model, string typeName)
        {
            try
            {
                foreach (var obj in model.Objects.GetByName(null, null, typeName))
                {
                    // Check for Domain
                    if (obj is global::Artech.Genexus.Common.Objects.Domain) return obj;
                    
                    // Check for SDT
                    if (obj.TypeDescriptor.Name.Equals("SDT", StringComparison.OrdinalIgnoreCase)) return obj;

                    // Check for Transaction (as BC)
                    if (obj is Transaction trn && trn.IsBusinessComponent) return obj;
                }
            }
            catch { /* Ignore model errors */ }
            return null;
        }

        private static global::Artech.Genexus.Common.Objects.Attribute FindAttribute(global::Artech.Architecture.Common.Objects.KBModel model, string name)
        {
            try
            {
                foreach (var result in model.Objects.GetByName(null, null, name))
                {
                    if (result is global::Artech.Genexus.Common.Objects.Attribute attr) return attr;
                }
            }
            catch { /* Object not found or model access error */ }
            return null;
        }
    }
}
