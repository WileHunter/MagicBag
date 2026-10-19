using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfApp3.Models;

namespace WpfApp3.Services
{
    /// <summary>
    /// 从工具的可执行文件中提取图标。EXE 工具取 RUN 指向的文件；
    /// 命令行工具若命令第一个词是工具目录下的 exe，也取它的图标。
    /// 提取在后台线程进行，完成后通过 <see cref="Tool"/> 的 Icon 属性变更通知界面；取不到时为 null。
    /// </summary>
    public static class ToolIconProvider
    {
        private const int IconSize = 64;

        // key → 图标（null 表示该工具没有可用图标）
        private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
        // 正在排队的 key → 等待结果的工具
        private static readonly Dictionary<string, List<Tool>> Pending = new(StringComparer.OrdinalIgnoreCase);
        private static readonly BlockingCollection<(string Key, Tool Tool, int Generation)> Queue = new();
        private static readonly object Sync = new();
        private static Dispatcher? _dispatcher;
        private static Thread? _worker;
        private static int _generation;

        /// <summary>立即返回已缓存的图标；尚未提取时返回 null 并在后台排队，完成后触发 tool 的 Icon 变更通知。</summary>
        public static ImageSource? Get(Tool? tool)
        {
            if (tool == null) return null;
            var key = KeyOf(tool);
            if (Cache.TryGetValue(key, out var icon)) return icon;

            lock (Sync)
            {
                if (Cache.TryGetValue(key, out icon)) return icon;
                if (Pending.TryGetValue(key, out var waiting))
                {
                    if (!waiting.Contains(tool)) waiting.Add(tool);
                    return null;
                }
                Pending[key] = new List<Tool> { tool };
                EnsureWorker();
                Queue.Add((key, tool, _generation));
            }
            return null;
        }

        /// <summary>数据变更后清空缓存，以便重新提取。</summary>
        public static void Clear()
        {
            lock (Sync)
            {
                _generation++;
                Cache.Clear();
                Pending.Clear();
            }
        }

        private static string KeyOf(Tool tool) =>
            $"{tool.Cmd}|{tool.ToolPath}|{(tool.Cmd == 1 ? tool.RUN : tool.Commond)}";

        private static void EnsureWorker()
        {
            if (_worker != null) return;
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            _worker = new Thread(Work) { IsBackground = true, Name = "ToolIconProvider", Priority = ThreadPriority.BelowNormal };
            _worker.SetApartmentState(ApartmentState.STA); // SHGetFileInfo 需要 COM
            _worker.Start();
        }

        private static void Work()
        {
            foreach (var (key, tool, generation) in Queue.GetConsumingEnumerable())
            {
                if (generation != Volatile.Read(ref _generation)) continue;

                ImageSource? icon = null;
                var path = ResolveFile(tool);
                if (path != null) icon = Extract(path);

                List<Tool>? waiting;
                lock (Sync)
                {
                    if (generation != _generation) continue;
                    Cache[key] = icon;
                    Pending.Remove(key, out waiting);
                }
                if (icon == null || waiting == null) continue;

                _dispatcher?.BeginInvoke(DispatcherPriority.Background, () =>
                {
                    foreach (var t in waiting) t.NotifyIconChanged();
                });
            }
        }

        private static string? ResolveFile(Tool tool)
        {
            try
            {
                if (tool.Cmd == 1)
                {
                    if (string.IsNullOrWhiteSpace(tool.RUN)) return null;
                    var exe = Path.IsPathRooted(tool.RUN) ? tool.RUN : Path.Combine(tool.ToolPath ?? "", tool.RUN);
                    return File.Exists(exe) ? exe : null;
                }

                if (string.IsNullOrWhiteSpace(tool.ToolPath)) return null;
                var first = (tool.Commond ?? "").Split(';').Select(c => c.Trim()).FirstOrDefault(c => c.Length > 0);
                if (first == null) return null;

                var token = first.StartsWith('"')
                    ? first[1..].Split('"')[0]
                    : first.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                if (token.Length == 0) return null;

                var candidate = Path.Combine(tool.ToolPath, token);
                if (!candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) candidate += ".exe";
                return File.Exists(candidate) ? candidate : null;
            }
            catch
            {
                return null;
            }
        }

        private static ImageSource? Extract(string path)
        {
            var handle = IntPtr.Zero;
            try
            {
                var handles = new IntPtr[1];
                var ids = new uint[1];
                // exe / dll / ico 直接按指定尺寸取，画质更好
                if (PrivateExtractIcons(path, 0, IconSize, IconSize, handles, ids, 1, 0) > 0 && handles[0] != IntPtr.Zero)
                {
                    handle = handles[0];
                }
                else if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    // 其他文件（bat、jar 等）取系统关联图标；没有图标的 exe 保留默认图块，比通用程序图标更好认
                    var info = new SHFILEINFO();
                    if (SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON) != IntPtr.Zero)
                        handle = info.hIcon;
                }
                if (handle == IntPtr.Zero) return null;

                var source = Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (handle != IntPtr.Zero) DestroyIcon(handle);
            }
        }

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_LARGEICON = 0x0;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint PrivateExtractIcons(string file, int index, int cx, int cy, IntPtr[] icons, uint[] ids, uint count, uint flags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string path, uint attributes, ref SHFILEINFO info, uint size, uint flags);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);
    }
}
