using System;
using System.IO;
using GxMcp.Worker.Models;

namespace GxMcp.Worker.Services
{
    public class IndexCacheService
    {
        private SearchIndex _index;
        private readonly string _indexPath;

        public IndexCacheService()
        {
            _indexPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "search_index.json");
        }

        public SearchIndex GetIndex()
        {
            if (_index != null) return _index;

            try
            {
                if (File.Exists(_indexPath))
                {
                    string json = File.ReadAllText(_indexPath);
                    _index = SearchIndex.FromJson(json);
                    Console.Error.WriteLine($"[IndexCacheService] Index loaded from disk. Objects: {_index?.Objects.Count ?? 0}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[IndexCacheService Error] Failed to load index: {ex.Message}");
            }

            return _index;
        }

        public void UpdateIndex(SearchIndex index)
        {
            _index = index;
            try
            {
                string dir = Path.GetDirectoryName(_indexPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_indexPath, _index.ToJson());
                Console.Error.WriteLine($"[IndexCacheService] Index updated in memory and written to disk.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[IndexCacheService Error] Failed to save index: {ex.Message}");
            }
        }

        public void Clear()
        {
            _index = null;
        }
    }
}
