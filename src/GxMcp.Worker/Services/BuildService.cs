using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using GxMcp.Worker.Helpers;
using Artech.Architecture.Common.Objects;
using Artech.Genexus.Common.Objects;
using Artech.Genexus.Common.Services;
using Artech.Architecture.Common.Services;
using Artech.Udm.Framework;

namespace GxMcp.Worker.Services
{
    public class BuildService
    {
        private string _msbuildPath;
        private string _gxDir;
        private KbService _kbService;

        public BuildService()
        {
            _gxDir = Environment.GetEnvironmentVariable("GX_PROGRAM_DIR") ?? @"C:\Program Files (x86)\GeneXus\GeneXus18";

            string[] searchPaths = new[] {
                @"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
                @"C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
                Path.Combine(_gxDir, "MSBuild.exe"),
                @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"
            };

            foreach (var p in searchPaths) { if (File.Exists(p)) { _msbuildPath = p; break; } }
        }

        public void SetKbService(KbService kbService) { _kbService = kbService; }
        public KbService KbService => _kbService;

        public string Build(string action, string target)
        {
            try
            {
                var kb = _kbService?.GetKB();
                if (kb != null && action.Equals("Build", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Info("Attempting Native SDK Build for: " + target);
                    IBuildService buildService = null;
                    try
                    {
                        var model = kb.DesignModel.Environment.TargetModel;
                        var method = model.GetType().GetMethod("GetService", new Type[] { typeof(Type) });
                        if (method != null) buildService = method.Invoke(model, new object[] { typeof(IBuildService) }) as IBuildService;
                    }
                    catch { }

                    if (buildService != null)
                    {
                        KBObject obj = kb.DesignModel.Objects.Get(null, new QualifiedName(SanitizationHelper.SanitizeObjectName(target)));
                        if (obj != null)
                        {
                            Logger.Info($"Executing Native BuildWithTheseOnly for {obj.Name} ({obj.Guid})");
                            buildService.BuildWithTheseOnly(new List<EntityKey> { obj.Key });
                            return Newtonsoft.Json.JsonConvert.SerializeObject(new {
                                status = "Success",
                                message = $"Native Build iniciado para '{SanitizationHelper.SanitizeObjectName(target)}'."
                            });
                        }
                        else
                        {
                            Logger.Warn($"Object '{target}' not found for Native Build, falling back to MSBuild.");
                        }
                    }
                    else
                    {
                        Logger.Warn("IBuildService not available via reflection, falling back to MSBuild.");
                    }
                }
            }
            catch (Exception ex) { Logger.Warn("Native Build failed: " + ex.Message); }

            return BuildWithMSBuild(action, target);
        }

        private string BuildWithMSBuild(string action, string target)
        {
            try
            {
                // Wait for KB initialization if needed
                if (_kbService != null)
                {
                    int waitAttempts = 0;
                    while (_kbService.IsInitializing && waitAttempts < 15)
                    {
                        System.Threading.Thread.Sleep(1000);
                        waitAttempts++;
                    }
                }

                // Pre-flight checks with clear error messages
                if (string.IsNullOrEmpty(_msbuildPath) || !File.Exists(_msbuildPath))
                    return Err("MSBuild.exe não encontrado. Instale o Visual Studio Build Tools ou verifique a instalação do GeneXus.");

                string kbPath = GetKBPath();
                if (string.IsNullOrEmpty(kbPath))
                    return Err("Caminho da KB não configurado (GX_KB_PATH). Verifique as configurações do worker.");

                string tasksFile = Path.Combine(_gxDir, "Genexus.Tasks.targets");
                if (!File.Exists(tasksFile))
                    return Err($"Arquivo 'Genexus.Tasks.targets' não encontrado em: {_gxDir}. Verifique a instalação do GeneXus.");

                // Build MSBuild project file
                string tempFile = Path.Combine(Path.GetTempPath(), "GxBuild_" + Guid.NewGuid().ToString().Substring(0, 8) + ".msbuild");
                var sb = new StringBuilder();
                sb.AppendLine("<Project DefaultTargets=\"Execute\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">");
                sb.AppendLine($"  <Import Project=\"{tasksFile}\" />");
                sb.AppendLine("  <Target Name=\"Execute\">");
                sb.AppendLine($"    <OpenKnowledgeBase Directory=\"{kbPath}\" />");

                if (action.Equals("Build", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(target))
                    sb.AppendLine($"    <BuildOne BuildCalled=\"true\" ObjectName=\"{SanitizationHelper.SanitizeObjectName(target)}\" ForceRebuild=\"false\" />");
                else if (action.Equals("RebuildAll", StringComparison.OrdinalIgnoreCase))
                    sb.AppendLine("    <BuildAll ForceRebuild=\"true\" />");
                else if (action.Equals("Reorg", StringComparison.OrdinalIgnoreCase))
                    sb.AppendLine("    <CheckAndInstallDatabase />");
                else
                    sb.AppendLine("    <BuildAll />");

                sb.AppendLine("    <CloseKnowledgeBase />");
                sb.AppendLine("  </Target></Project>");

                File.WriteAllText(tempFile, sb.ToString(), Encoding.UTF8);
                Logger.Info($"[BUILD] Action={action}, Target={target}, MSBuild={_msbuildPath}, KB={kbPath}");

                var stdoutLines = new System.Collections.Concurrent.ConcurrentBag<string>();
                var stderrLines = new System.Collections.Concurrent.ConcurrentBag<string>();

                var startInfo = new ProcessStartInfo
                {
                    FileName = _msbuildPath,
                    Arguments = $"/nologo /v:m /nodeReuse:false \"{tempFile}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = _gxDir
                };

                using (var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true })
                {
                    process.OutputDataReceived += (s, e) => { if (e.Data != null) stdoutLines.Add(e.Data); };
                    process.ErrorDataReceived += (s, e) => { if (e.Data != null) stderrLines.Add(e.Data); };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // Timeout: BuildOne = 3 min, RebuildAll = 15 min, others = 5 min
                    int timeoutMs = action.Equals("RebuildAll", StringComparison.OrdinalIgnoreCase) ? 900000
                                  : action.Equals("Build", StringComparison.OrdinalIgnoreCase) ? 180000
                                  : 300000;

                    bool finished = process.WaitForExit(timeoutMs);
                    try { File.Delete(tempFile); } catch { }

                    if (!finished)
                    {
                        try { process.Kill(); } catch { }
                        return Err($"Build cancelado após timeout de {timeoutMs / 1000}s. O processo MSBuild foi encerrado.",
                                   string.Join("\n", stdoutLines.Take(30)));
                    }

                    var allLines = stdoutLines.Concat(stderrLines).Where(l => l != null).ToList();

                    // Extract meaningful error/warning lines
                    var errorLines = allLines
                        .Where(l => l.Contains("error :") || l.Contains(": error") ||
                                    l.Contains("ERRO") || l.Contains("falhou") ||
                                    l.Contains("FATAL") || l.Contains("unavailable") ||
                                    l.Contains("authentication") || l.Contains("GXaccount") ||
                                    l.Contains("Exception") || l.Contains("inválido") ||
                                    l.Contains("não encontrado") || l.Contains("failure"))
                        .Select(l => l.Trim())
                        .Where(l => l.Length > 5) // Skip too short lines
                        .Distinct()
                        .ToList();

                    // If no explicit error lines found but build failed, take the last 20 lines as context
                    if (errorLines.Count == 0 && process.ExitCode != 0)
                    {
                        errorLines = allLines.Skip(Math.Max(0, allLines.Count - 20)).ToList();
                    }

                    var outputSummary = string.Join("\n", allLines.Where(l => l.Trim().Length > 0).Take(100));

                    if (process.ExitCode == 0)
                    {
                        Logger.Info($"[BUILD] SUCCESS: {action}/{target}");
                        return Newtonsoft.Json.JsonConvert.SerializeObject(new {
                            status = "Success",
                            message = $"Build concluído com sucesso" + (string.IsNullOrEmpty(target) ? "." : $" para '{target}'."),
                            output = outputSummary
                        });
                    }
                    else
                    {
                        string errorDetail = errorLines.Count > 0
                            ? string.Join("\n", errorLines)
                            : (outputSummary.Length > 0 ? outputSummary : "Nenhuma mensagem de erro disponível do MSBuild. Verifique os logs do worker.");

                        Logger.Error($"[BUILD] FAILED (exit={process.ExitCode}) {action}/{target}: {errorDetail}");
                        return Err(errorDetail, outputSummary);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[BUILD] Exception {action}/{target}: {ex.Message}");
                return Err(ex.Message);
            }
        }

        private static string Err(string error, string output = "")
            => Newtonsoft.Json.JsonConvert.SerializeObject(new { status = "Error", error, output });

        public string GetKBPath()
        {
            return Environment.GetEnvironmentVariable("GX_KB_PATH") ?? "";
        }
    }
}
