using System;
using System.IO.Compression;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace GxMcp.Worker.Services
{
    public class ForgeService
    {
        private readonly BuildService _buildService;
        private readonly ObjectService _objectService;

        public ForgeService(BuildService buildService, ObjectService objectService)
        {
            _buildService = buildService;
            _objectService = objectService;
        }

        public string CreateObject(string name, string definitionJson)
        {
            try
            {
                // Parse definition (expects JSON with Attributes and Structure)
                // For simplicity, build the import XML directly
                var def = Newtonsoft.Json.Linq.JObject.Parse(definitionJson);
                var attributes = def["Attributes"] as Newtonsoft.Json.Linq.JArray;
                var structure = def["Structure"]?.ToString();

                // Phase 1: Create Attributes
                var attXml = new StringBuilder("<Attributes>");
                if (attributes != null)
                {
                    foreach (var att in attributes)
                    {
                        string attName = att["Name"]?.ToString();
                        string attType = att["Type"]?.ToString() ?? "VarChar";
                        string attLen = att["Length"]?.ToString() ?? "100";
                        attXml.Append($"<Attribute name='{attName}' description='{attName}'><Properties>");
                        attXml.Append($"<Property><Name>Name</Name><Value>{attName}</Value></Property>");
                        attXml.Append($"<Property><Name>ATTCUSTOMTYPE</Name><Value>bas:{attType}</Value></Property>");
                        attXml.Append($"<Property><Name>Length</Name><Value>{attLen}</Value></Property>");
                        attXml.Append("</Properties></Attribute>");
                    }
                }
                attXml.Append("</Attributes>");
                ImportXml(attXml.ToString(), "temp_att");

                // Phase 2: Create Transaction
                var structNodes = new StringBuilder();
                if (structure != null)
                {
                    foreach (string line in structure.Split('\n'))
                    {
                        string trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed)) continue;
                        if (trimmed.EndsWith("*"))
                        {
                            string attName = trimmed.TrimEnd('*').Trim();
                            structNodes.Append($"<Attribute key='True'>{attName}</Attribute>");
                        }
                        else
                        {
                            structNodes.Append($"<Attribute key='False'>{trimmed}</Attribute>");
                        }
                    }
                }

                // Transaction type GUID
                string trnXml = $"<Objects><Object name='{name}' type='1db606f2-af09-4cf9-a3b5-b481519d28f6'>"
                    + $"<Part type='264be5fb-1b28-4b25-a598-6ca900dd059f'>"
                    + $"<Level Name='{name}' Type='{name}' Description='{name}'>{structNodes}</Level>"
                    + "</Part>"
                    + $"<Properties><Property><Name>Name</Name><Value>{name}</Value></Property></Properties>"
                    + "</Object></Objects>";
                ImportXml(trnXml, "temp_trn");

                return _objectService.ReadObject(name);
            }
            catch (Exception ex)
            {
                return "{\"error\": \"" + CommandDispatcher.EscapeJsonString(ex.Message) + "\"}";
            }
        }

        private void ImportXml(string xmlBody, string fileName)
        {
            string kbPath = _buildService.GetKBPath();
            string gxDir = @"C:\Program Files (x86)\GeneXus\GeneXus18";
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            string xmlPath = Path.Combine(baseDir, fileName + ".xml");
            string xpzPath = Path.Combine(baseDir, fileName + ".xpz");
            string zipPath = Path.Combine(baseDir, fileName + ".zip");

            // Wrap in ExportFile if needed
            if (!xmlBody.TrimStart().StartsWith("<?xml"))
            {
                xmlBody = "<?xml version='1.0' encoding='utf-8'?><ExportFile><KMW><MajorVersion>4</MajorVersion></KMW>"
                    + xmlBody + "</ExportFile>";
            }
            File.WriteAllText(xmlPath, xmlBody, Encoding.UTF8);

            // Create XPZ
            if (File.Exists(zipPath)) File.Delete(zipPath);
            if (File.Exists(xpzPath)) File.Delete(xpzPath);
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(xmlPath, Path.GetFileName(xmlPath));
            }
            File.Move(zipPath, xpzPath);

            // Import via MSBuild
            string targetsFile = Path.Combine(baseDir, fileName + ".targets");
            string content = $@"<Project DefaultTargets='Import' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <Import Project='{gxDir}\Genexus.Tasks.targets' />
                <Target Name='Import'>
                    <OpenKnowledgeBase Directory='{kbPath}' />
                    <Import File='{xpzPath}' />
                </Target>
            </Project>";
            File.WriteAllText(targetsFile, content, Encoding.UTF8);
            _buildService.RunMSBuild(targetsFile, "Import");

            // Cleanup
            File.Delete(xmlPath);
            File.Delete(xpzPath);
            File.Delete(targetsFile);
        }
    }
}
