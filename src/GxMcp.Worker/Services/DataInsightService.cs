using System;
using System.Collections.Generic;
using System.Linq;
using Artech.Architecture.Common.Objects;
using Artech.Genexus.Common.Objects;
using Artech.Genexus.Common.Parts;
using Artech.Genexus.Common;
using Newtonsoft.Json.Linq;

namespace GxMcp.Worker.Services
{
    public class DataInsightService
    {
        private readonly KbService _kbService;
        private readonly ObjectService _objectService;

        public DataInsightService(KbService kbService, ObjectService objectService)
        {
            _kbService = kbService;
            _objectService = objectService;
        }

        public string GetDataContext(string target)
        {
            try
            {
                var obj = _objectService.FindObject(target);
                if (obj == null) return "{\"error\": \"Object not found\"}";

                var result = new JObject();
                result["name"] = obj.Name;
                result["type"] = obj.TypeDescriptor.Name;

                if (obj is Transaction trn)
                {
                    result["structure"] = GetTransactionStructure(trn);
                }
                else if (obj is Table tbl)
                {
                    result["tableInfo"] = GetTableStructure(tbl);
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                return "{\"error\": \"" + CommandDispatcher.EscapeJsonString(ex.Message) + "\"}";
            }
        }

        private JArray GetTransactionStructure(Transaction trn)
        {
            var levels = new JArray();
            foreach (var level in trn.Structure.Root.Levels)
            {
                levels.Add(ProcessLevel(level));
            }
            return levels;
        }

        private JObject ProcessLevel(TransactionLevel level)
        {
            var item = new JObject();
            item["name"] = level.Name;
            item["baseTable"] = level.AssociatedTable?.Name;
            
            var attributes = new JArray();
            foreach (var attr in level.Attributes)
            {
                attributes.Add(new JObject { ["name"] = attr.Name, ["isKey"] = attr.IsKey });
            }
            item["attributes"] = attributes;

            if (level.Levels.Count > 0)
            {
                var subLevels = new JArray();
                foreach (var sub in level.Levels) subLevels.Add(ProcessLevel(sub));
                item["subLevels"] = subLevels;
            }

            return item;
        }

        private JObject GetTableStructure(Table tbl)
        {
            var result = new JObject();
            result["name"] = tbl.Name;
            result["description"] = tbl.Description;

            var attributes = new JArray();
            foreach (var attr in tbl.TableStructure.Attributes)
            {
                var attrObj = new JObject();
                attrObj["name"] = attr.Name;
                attrObj["isKey"] = attr.IsKey;
                attrObj["type"] = attr.Type.ToString();
                attrObj["length"] = attr.Length;
                if (attr.Decimals > 0) attrObj["decimals"] = attr.Decimals;
                
                if (attr.IsFormula) attrObj["formula"] = attr.Formula.Expression;
                
                // Subtype check (SDK specific - using best effort property access)
                try {
                    if (attr.IsSubtype) {
                        attrObj["isSubtype"] = true;
                        attrObj["supertype"] = attr.Supertype?.Name;
                    }
                } catch {}

                attributes.Add(attrObj);
            }
            result["attributes"] = attributes;

            var indices = new JArray();
            foreach (var idx in tbl.TableStructure.Indices)
            {
                var idxObj = new JObject();
                idxObj["name"] = idx.Name;
                idxObj["unique"] = idx.IsUnique;
                var idxAttrs = new JArray();
                foreach (var ia in idx.Attributes) idxAttrs.Add(ia.Name);
                idxObj["attributes"] = idxAttrs;
                indices.Add(idxObj);
            }
            result["indices"] = indices;

            return result;
        }

        public JArray GetTablesUsed(KBObject obj)
        {
            var tables = new JArray();
            foreach (var reference in obj.GetReferences())
            {
                var target = _kbService.GetKB().DesignModel.Objects.Get(reference.To);
                if (target is Table tbl)
                {
                    tables.Add(new JObject { ["name"] = tbl.Name, ["description"] = tbl.Description });
                }
            }
            return tables;
        }
    }
}
