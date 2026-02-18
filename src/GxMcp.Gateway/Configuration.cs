using System;
using System.IO;
using Newtonsoft.Json;

namespace GxMcp.Gateway
{
    public class Configuration
    {
        public GeneXusConfig? GeneXus { get; set; }
        public ServerConfig? Server { get; set; }
        public LoggingConfig? Logging { get; set; }

        public static Configuration Load()
        {
            string configPath = "config.json";
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string deepSearch = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\config.json")); // bin/Debug/net8.0/../../../../ = root
            
            // Console.Error.WriteLine($"[Config Debug] CWD: {Directory.GetCurrentDirectory()}");
            // Console.Error.WriteLine($"[Config Debug] BaseDir: {baseDir}");

            // Strategy:
            // 1. Explicit copy in BaseDir (from .csproj CopyToOutput)
            if (File.Exists(Path.Combine(baseDir, "config.json")))
            {
                configPath = Path.Combine(baseDir, "config.json");
            }
            // 2. CWD (if running from root)
            else if (File.Exists("config.json")) 
            {
                configPath = Path.GetFullPath("config.json");
            }
            // 3. Upwards traversal (Development)
            else if (File.Exists(deepSearch))
            {
                configPath = deepSearch;
            }
            
            if (!File.Exists(configPath))
            {
                Console.Error.WriteLine($"[Config Failure] Could not find config.json. Searched: CWD, BaseDir, and {deepSearch}");
                throw new FileNotFoundException($"Configuration file not found at {configPath}");
            }

            // Config loaded silently to avoid MCP client error display
            string json = File.ReadAllText(configPath);
            var config = JsonConvert.DeserializeObject<Configuration>(json);
            
            if (config == null) throw new Exception("Failed to deserialize configuration");
            
            // Validate required fields
            if (config.GeneXus?.InstallationPath == null) throw new Exception("Config: GeneXus.InstallationPath is missing");
            if (config.GeneXus?.WorkerExecutable == null) throw new Exception("Config: GeneXus.WorkerExecutable is missing");
            if (config.Server == null) config.Server = new ServerConfig(); 

            return config;
        }
    }

    public class GeneXusConfig
    {
        public string? InstallationPath { get; set; }
        public string? WorkerExecutable { get; set; }
    }

    public class ServerConfig
    {
        public int HttpPort { get; set; } = 5000;
        public bool McpStdio { get; set; } = true;
    }

    public class LoggingConfig
    {
        public string? Level { get; set; }
        public string? Path { get; set; }
    }
}
