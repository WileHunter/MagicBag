using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using WpfApp3.Models;

namespace WpfApp3.Services
{
    /// <summary>
    /// 统一的数据目录。所有路径都基于程序所在目录，避免因“工作目录”不同而读写到不同文件。
    /// </summary>
    public static class AppPaths
    {
        public static string BaseDir => AppDomain.CurrentDomain.BaseDirectory;
        public static string ToolsFile => Path.Combine(BaseDir, "tools.json");
        public static string NotesDir => Path.Combine(BaseDir, "Notebooks");
    }

    /// <summary>
    /// tools.json 的读写。ToolsView 与 DatabaseView 共用。
    /// </summary>
    public static class ToolRepository
    {
        private static readonly JsonSerializerSettings Settings = new()
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        public static Root Load()
        {
            var path = AppPaths.ToolsFile;
            Root root;
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                root = JsonConvert.DeserializeObject<Root>(json) ?? new Root();
            }
            else
            {
                root = new Root();
            }

            root.Categories ??= new List<Category>();
            foreach (var c in root.Categories)
                Normalize(c, null);
            return root;
        }

        public static void Save(Root root)
        {
            var path = AppPaths.ToolsFile;
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(root, Settings));
            // 先写临时文件再替换，避免写入中途失败导致 tools.json 损坏
            File.Move(tmp, path, true);
        }

        public static void Normalize(Category category, Category? parent)
        {
            category.ParentCategory = parent;
            category.SubCategories ??= new List<Category>();
            category.Tools ??= new List<Tool>();
            foreach (var t in category.Tools)
                t.ParentCategory = category;
            foreach (var sub in category.SubCategories)
                Normalize(sub, category);
        }

        public static IEnumerable<Tool> AllTools(IEnumerable<Category>? categories)
        {
            if (categories == null) yield break;
            foreach (var c in categories)
            {
                if (c.Tools != null)
                    foreach (var t in c.Tools)
                        yield return t;
                foreach (var t in AllTools(c.SubCategories))
                    yield return t;
            }
        }

        public static IEnumerable<Category> AllCategories(IEnumerable<Category>? categories)
        {
            if (categories == null) yield break;
            foreach (var c in categories)
            {
                yield return c;
                foreach (var sub in AllCategories(c.SubCategories))
                    yield return sub;
            }
        }

        public static string GetPath(Category? category, string separator = " / ")
        {
            var parts = new List<string>();
            for (var c = category; c != null; c = c.ParentCategory)
                parts.Add(c.Name ?? "");
            parts.Reverse();
            return string.Join(separator, parts);
        }

        public static int NextId(IEnumerable<Category>? categories)
        {
            return AllTools(categories).Select(t => t.Id).DefaultIfEmpty(0).Max() + 1;
        }
    }

    /// <summary>
    /// 主窗口右上角的轻提示。
    /// </summary>
    public static class Toast
    {
        public const string Token = "AppMsg";
        public static void Success(string msg) => HandyControl.Controls.Growl.Success(msg, Token);
        public static void Info(string msg) => HandyControl.Controls.Growl.Info(msg, Token);
        public static void Warning(string msg) => HandyControl.Controls.Growl.Warning(msg, Token);
        public static void Error(string msg) => HandyControl.Controls.Growl.Error(msg, Token);
    }
}
