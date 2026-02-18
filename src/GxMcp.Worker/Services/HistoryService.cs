using System;
using System.IO;
using System.Linq;
using System.Text;

namespace GxMcp.Worker.Services
{
    public class HistoryService
    {
        private readonly ObjectService _objectService;
        private readonly WriteService _writeService;

        public HistoryService(ObjectService objectService, WriteService writeService)
        {
            _objectService = objectService;
            _writeService = writeService;
        }

        public string Execute(string target, string action)
        {
            string histDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".history");
            if (!Directory.Exists(histDir)) Directory.CreateDirectory(histDir);

            try
            {
                switch (action?.ToLower())
                {
                    case "save":
                        return SaveSnapshot(target, histDir);
                    case "restore":
                        return RestoreSnapshot(target, histDir);
                    default:
                        return "{\"error\": \"Unknown action: " + action + ". Use Save or Restore.\"}";
                }
            }
            catch (Exception ex)
            {
                return "{\"error\": \"" + CommandDispatcher.EscapeJsonString(ex.Message) + "\"}";
            }
        }

        private string SaveSnapshot(string target, string histDir)
        {
            string xmlContent = _objectService.GetObjectXml(target);
            if (xmlContent == null) return "{\"error\": \"Object not found\"}";

            // Extract source code from XML
            var doc = new System.Xml.XmlDocument();
            doc.LoadXml(xmlContent);
            string code = "";
            var parts = doc.GetElementsByTagName("Part");
            foreach (System.Xml.XmlNode p in parts)
            {
                var src = p.SelectSingleNode("Source");
                if (src != null) code += src.InnerText + "\n";
            }

            string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string safeName = target.Replace(":", "_");
            string filePath = Path.Combine(histDir, $"{safeName}_{ts}.txt");
            File.WriteAllText(filePath, code, Encoding.UTF8);

            long size = new FileInfo(filePath).Length;

            return "{\"status\": \"Snapshot saved\", \"file\": \"" + CommandDispatcher.EscapeJsonString(Path.GetFileName(filePath)) + "\", \"timestamp\": \"" + ts + "\", \"size\": " + size + "}";
        }

        private string RestoreSnapshot(string target, string histDir)
        {
            string safeName = target.Replace(":", "_");
            var files = Directory.GetFiles(histDir, $"{safeName}_*.txt")
                .OrderByDescending(f => f)
                .ToArray();

            if (files.Length == 0)
                return "{\"error\": \"No snapshots found for " + CommandDispatcher.EscapeJsonString(target) + "\"}";

            string lastFile = files.First();
            string code = File.ReadAllText(lastFile, Encoding.UTF8);

            return _writeService.WriteObject(target, "Source", code);
        }
    }
}
