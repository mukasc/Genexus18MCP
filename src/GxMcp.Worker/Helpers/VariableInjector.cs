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
        public static void InjectVariables(KBObject obj, string code)
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
                    global::Artech.Genexus.Common.Variable v = CreateVariable(variablesPart, varName);
                    if (v != null)
                    {
                        variablesPart.Variables.Add(v);
                        Logger.Info($"Injected variable: {varName} into {obj.Name}");
                    }
                }
            }
        }

        private static global::Artech.Genexus.Common.Variable CreateVariable(VariablesPart part, string name)
        {
            global::Artech.Genexus.Common.Variable v = new global::Artech.Genexus.Common.Variable(part);
            v.Name = name;

            // 1. Inherit from Attribute (Priority)
            var attribute = FindAttribute(part.Model, name);
            if (attribute != null)
            {
                v.Type = attribute.Type;
                v.Length = attribute.Length;
                v.Decimals = attribute.Decimals;
                v.Signed = attribute.Signed;
                Logger.Info($"Injected variable {name} inheriting from attribute {attribute.Name}");
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
