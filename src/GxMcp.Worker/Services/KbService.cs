using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Artech.Architecture.Common.Objects;
using GxMcp.Worker.Helpers;

namespace GxMcp.Worker.Services
{
    public class KbService
    {
        private static KnowledgeBase _kb;
        private readonly BuildService _buildService;

        public KbService(BuildService buildService)
        {
            _buildService = buildService;
        }

        public KnowledgeBase GetKB() { EnsureKbOpen(); return _kb; }

        public void Reload()
        {
            if (_kb != null) { try { _kb.Close(); } catch { } _kb = null; GC.Collect(); }
            EnsureKbOpen();
        }

        private void EnsureKbOpen()
        {
            if (_kb != null) return;
            string gxPath = @"C:\Program Files (x86)\GeneXus\GeneXus18";
            
            try {
                string kbPath = _buildService.GetKBPath();
                if (string.IsNullOrEmpty(kbPath)) throw new Exception("KBPath not configured in config.json");

                Logger.Info($"[KbService] Opening KB: {kbPath}");
                string oldDir = Directory.GetCurrentDirectory();
                try {
                    Directory.SetCurrentDirectory(gxPath);
                    _kb = KnowledgeBase.Open(new KnowledgeBase.OpenOptions(kbPath));
                } finally { Directory.SetCurrentDirectory(oldDir); }

                if (_kb == null) throw new Exception("KnowledgeBase.Open returned null.");
                Logger.Info("[KbService] KB Opened Successfully.");
            } catch (Exception ex) { 
                Logger.Error($"[KbService] SDK Error: {ex.Message}"); 
                throw new Exception($"Failed to connect to GeneXus KB: {ex.Message}");
            }
        }

        public string IndexPrefix(string prefix)
        {
            try
            {
                var kb = GetKB();
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "search_index.json");
                var index = File.Exists(path) ? Models.SearchIndex.FromJson(File.ReadAllText(path)) : new Models.SearchIndex();

                int count = 0;
                foreach (KBObject kbo in kb.DesignModel.Objects)
                {
                    try {
                        if (kbo == null || !kbo.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                        string n = kbo.Name;
                        string t = GetPrefix(kbo);
                        string k = t + ":" + n;
                        index.Objects[k] = new Models.SearchIndex.IndexEntry {
                            Name = k, Type = t, Description = kbo.Description,
                            Tags = new List<string>{t}, Keywords = new List<string>{t, n},
                            Calls = new List<string>(), CalledBy = new List<string>(),
                            BusinessDomain = "Protocolo"
                        };
                        count++;
                    } catch { continue; }
                }
                index.LastUpdated = DateTime.Now;
                File.WriteAllText(path, index.ToJson());
                return "{\"status\":\"Success\", \"indexed\":" + count + "}";
            } catch (Exception ex) { return "{\"error\":\"" + CommandDispatcher.EscapeJsonString(ex.Message) + "\"}"; }
        }

        public string BulkIndex()
        {
            try {
                var kb = GetKB();
                var index = new Models.SearchIndex();
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "search_index.json");
                if (!Directory.Exists(Path.GetDirectoryName(path))) Directory.CreateDirectory(Path.GetDirectoryName(path));

                int count = 0;
                foreach (KBObject kbo in kb.DesignModel.Objects) {
                    try {
                        if (kbo == null) continue;
                        string n = kbo.Name;
                        string t = GetPrefix(kbo);
                        string k = t + ":" + n;
                        index.Objects[k] = new Models.SearchIndex.IndexEntry {
                            Name = k, Type = t, Description = kbo.Description,
                            Tags = new List<string>{t}, Keywords = new List<string>{t, n},
                            Calls = new List<string>(), CalledBy = new List<string>()
                        };
                        count++;
                    } catch { continue; }
                }
                index.LastUpdated = DateTime.Now;
                File.WriteAllText(path, index.ToJson());
                return "{\"status\":\"Success\", \"indexed\":" + count + "}";
            } catch (Exception ex) { return "{\"error\":\"" + CommandDispatcher.EscapeJsonString(ex.Message) + "\"}"; }
        }

        private string GetPrefix(KBObject obj)
        {
            if (obj == null) return "Obj";
            if (obj is Artech.Genexus.Common.Objects.Transaction) return "Trn";
            if (obj is Artech.Genexus.Common.Objects.Procedure) return "Prc";
            if (obj is Artech.Genexus.Common.Objects.WebPanel) return "Wbp";
            if (obj is Artech.Genexus.Common.Objects.Attribute) return "Att";
            if (obj is Artech.Genexus.Common.Objects.Table) return "Tbl";
            return obj.GetType().Name;
        }
    }
}
