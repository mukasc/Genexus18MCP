using System;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace GxMcp.Worker.Services
{
    public class RefactorService
    {
        private readonly ObjectService _objectService;
        private readonly BuildService _buildService;
        private const string VARIABLES_GUID = "e4c4ade7-53f0-4a56-bdfd-843735b66f47";
        private const string SOURCE_GUID = "528d1c06-a9c2-420d-bd35-21dca83f12ff";

        public RefactorService(ObjectService objectService, BuildService buildService)
        {
            _objectService = objectService;
            _buildService = buildService;
        }

        public string Refactor(string target, string action)
        {
            if (action?.ToLower() != "cleanvars")
                return "{\"error\": \"Unknown refactor action: " + action + ". Use CleanVars.\"}";

            try
            {
                string xmlContent = _objectService.GetObjectXml(target);
                if (xmlContent == null) return "{\"error\": \"Object not found\"}";

                var doc = new XmlDocument();
                doc.LoadXml(xmlContent);

                // Get full source code
                string fullCode = "";
                var partNodes = doc.GetElementsByTagName("Part");
                foreach (XmlNode pn in partNodes)
                {
                    if (pn.Attributes?["type"]?.Value == SOURCE_GUID)
                    {
                        var src = pn.SelectSingleNode("Source");
                        if (src != null) fullCode += src.InnerText;
                    }
                }

                // Find variables part
                XmlNode varPart = null;
                foreach (XmlNode pn in partNodes)
                {
                    if (pn.Attributes?["type"]?.Value == VARIABLES_GUID)
                    {
                        varPart = pn;
                        break;
                    }
                }

                if (varPart == null) return "{\"status\": \"No variables part found\"}";

                // Find unused variables
                var removed = 0;
                var varNodes = varPart.SelectNodes("Variable");
                if (varNodes != null)
                {
                    var toRemove = new System.Collections.Generic.List<XmlNode>();
                    foreach (XmlNode v in varNodes)
                    {
                        string varName = v.Attributes?["Name"]?.Value;
                        if (varName == null) continue;
                        if (!Regex.IsMatch(fullCode, @"&" + Regex.Escape(varName) + @"\b"))
                        {
                            toRemove.Add(v);
                        }
                    }
                    foreach (var v in toRemove)
                    {
                        varPart.RemoveChild(v);
                        removed++;
                    }
                }

                if (removed == 0) return "{\"status\": \"No unused variables found\"}";

                // Re-import the cleaned XML
                string tempXml = System.IO.Path.GetTempFileName() + ".xml";
                doc.Save(tempXml);

                string xpzPath = tempXml.Replace(".xml", ".xpz");
                string zipPath = tempXml.Replace(".xml", ".zip");
                using (var archive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create))
                {
                    archive.CreateEntryFromFile(tempXml, System.IO.Path.GetFileName(tempXml));
                }
                System.IO.File.Move(zipPath, xpzPath);

                string targetsFile = System.IO.Path.GetTempFileName() + ".targets";
                string kbPath = _buildService.GetKBPath();
                string gxDir = @"C:\Program Files (x86)\GeneXus\GeneXus18";
                string content = $@"<Project DefaultTargets='Import' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <Import Project='{gxDir}\Genexus.Tasks.targets' />
                    <Target Name='Import'>
                        <OpenKnowledgeBase Directory='{kbPath}' />
                        <Import File='{xpzPath}' />
                    </Target>
                </Project>";
                System.IO.File.WriteAllText(targetsFile, content);
                _buildService.RunMSBuild(targetsFile, "Import");

                System.IO.File.Delete(tempXml);
                System.IO.File.Delete(xpzPath);
                System.IO.File.Delete(targetsFile);

                return "{\"status\": \"Removed " + removed + " unused variables from " + CommandDispatcher.EscapeJsonString(target) + "\"}";
            }
            catch (Exception ex)
            {
                return "{\"error\": \"" + CommandDispatcher.EscapeJsonString(ex.Message) + "\"}";
            }
        }
    }
}
