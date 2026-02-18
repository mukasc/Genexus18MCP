using System;
using System.IO;
using System.Linq;

namespace GxMcp.Worker.Services
{
    public class ListService
    {
        private readonly BuildService _buildService;

        public ListService(BuildService buildService)
        {
            _buildService = buildService;
        }

        public string ListObjects(string filter, int limit = 100, int offset = 0)
        {
            try
            {
                string kbPath = _buildService.GetKBPath();
                string gxDir = @"C:\Program Files (x86)\GeneXus\GeneXus18";
                string tempList = Path.GetTempFileName();
                string targetsFile = Path.GetTempFileName() + ".targets";

                string xml = $@"<Project DefaultTargets='List' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <UsingTask TaskName='GetKBObjects' AssemblyFile='{gxDir}\Genexus.MSBuild.Tasks.dll' />
                    <Import Project='{gxDir}\Genexus.Tasks.targets' />
                    <Target Name='List'>
                        <OpenKnowledgeBase Directory='{kbPath}' />
                        <GetKBObjects Filter='{filter}'>
                            <Output TaskParameter='Objects' ItemName='ObjList' />
                        </GetKBObjects>
                        <WriteLinesToFile File='{tempList}' Lines='@(ObjList)' Overwrite='true' />
                    </Target>
                </Project>";

                File.WriteAllText(targetsFile, xml);
                _buildService.RunMSBuild(targetsFile, "List");

                string[] objects = new string[0];
                if (File.Exists(tempList))
                {
                    objects = File.ReadAllLines(tempList)
                        .Where(l => !string.IsNullOrWhiteSpace(l) && l.Contains(":") && !l.StartsWith("=") && !l.StartsWith("-") && !l.Contains("Task"))
                        .ToArray();
                    File.Delete(tempList);
                }
                File.Delete(targetsFile);

                int totalCount = objects.Length;
                
                // Pagination Logic
                var pagedObjects = objects.Skip(offset).Take(limit).ToArray();

                // Build JSON array
                var jsonItems = pagedObjects.Select(o => "\"" + CommandDispatcher.EscapeJsonString(o) + "\"");
                
                return "{\"total\": " + totalCount + "," +
                       "\"count\": " + pagedObjects.Length + "," +
                       "\"limit\": " + limit + "," +
                       "\"offset\": " + offset + "," + 
                       "\"objects\": [" + string.Join(",", jsonItems) + "]}";
            }
            catch (Exception ex)
            {
                return "{\"error\": \"" + CommandDispatcher.EscapeJsonString(ex.Message) + "\"}";
            }
        }
    }
}
