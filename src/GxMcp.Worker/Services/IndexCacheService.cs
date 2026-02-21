using System;
using System.IO;
using GxMcp.Worker.Models;
using GxMcp.Worker.Helpers;

namespace GxMcp.Worker.Services
{
    public class IndexCacheService
    {
        private SearchIndex _index;
        private string _indexPath;
        private readonly BuildService _buildService;
        private bool _initialized = false;

        public IndexCacheService(BuildService buildService)
        {
            _buildService = buildService;
            // Default path if not initialized with a KB
            _indexPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "search_index.json");
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;
            try
            {
                string kbPath = _buildService.GetKBPath();
                if (!string.IsNullOrEmpty(kbPath))
                {
                    Initialize(kbPath);
                }
            }
            catch { /* Ignore, will use default path */ }
        }

        public void Initialize(string kbPath)
        {
            if (string.IsNullOrEmpty(kbPath)) return;
            if (kbPath.EndsWith(".gxw", StringComparison.OrdinalIgnoreCase)) kbPath = Path.GetDirectoryName(kbPath);

            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string cacheDir = Path.Combine(localAppData, "GxMcp", "Cache");
                if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);

                // Create a unique filename based on the KB path hash
                string hash = GetHash(kbPath);
                _indexPath = Path.Combine(cacheDir, $"index_{hash}.json");
                _initialized = true;
                
                Logger.Info($"IndexCache initialized for KB: {kbPath} -> {_indexPath}");
            }
            catch (Exception ex)
            {
                Logger.Error($"IndexCache Init Error: {ex.Message}");
            }
        }

        private string GetHash(string input)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input.ToLower().Trim()));
                return BitConverter.ToString(bytes).Replace("-", "").Substring(0, 16);
            }
        }

        public SearchIndex GetIndex()
        {
            EnsureInitialized();
            if (_index != null) return _index;

            try
            {
                if (File.Exists(_indexPath))
                {
                    string json = File.ReadAllText(_indexPath);
                    _index = SearchIndex.FromJson(json);
                    Logger.Info($"Index loaded from disk. Objects: {_index?.Objects.Count ?? 0}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load index: {ex.Message}");
            }

            return _index;
        }

        public void UpdateIndex(SearchIndex index)
        {
            EnsureInitialized();
            _index = index;
            try
            {
                string dir = Path.GetDirectoryName(_indexPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_indexPath, _index.ToJson());
                Logger.Info("Index updated in memory and written to disk.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save index: {ex.Message}");
            }
        }

        public void Clear()
        {
            _index = null;
        }

        public void UpdateEntry(global::Artech.Architecture.Common.Objects.KBObject obj)
        {
            if (_index == null) GetIndex(); // Load if not already in memory

            var entry = new SearchIndex.IndexEntry
            {
                Name = obj.Name,
                Type = obj.TypeDescriptor.Name,
                Description = obj.Description
            };

            // Enrichment for Attributes
            if (obj is global::Artech.Genexus.Common.Objects.Attribute attr)
            {
                entry.DataType = attr.Type.ToString();
                entry.Length = attr.Length;
                entry.Decimals = attr.Decimals;
            }

            string key = string.Format("{0}:{1}", entry.Type, entry.Name);
            if (_index.Objects.ContainsKey(key))
                _index.Objects[key] = entry;
            else
                _index.Objects.Add(key, entry);

            UpdateIndex(_index); // Persist changes
            Logger.Info(string.Format("Incremental cache update for {0}", key));
        }

        public void RemoveEntry(string type, string name)
        {
            if (_index == null) return;
            string key = string.Format("{0}:{1}", type, name);
            if (_index.Objects.Remove(key))
            {
                UpdateIndex(_index);
                Logger.Info(string.Format("Removed {0} from cache", key));
            }
        }
    }
}
