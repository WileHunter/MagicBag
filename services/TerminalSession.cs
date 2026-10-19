using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WpfApp3.Services
{
    /// <summary>
    /// 内置终端的一个会话。每条命令用独立的 cmd /c 进程执行（输出经管道读取），
    /// cd / cls / exit 等由会话自己处理，这样“停止”可以可靠地结束整棵进程树。
    /// </summary>
    public sealed class TerminalSession : INotifyPropertyChanged
    {
        private static readonly Encoding ConsoleEncoding;
        private static readonly Regex CdPattern = new(@"^(cd|chdir)(?=$|[\s\\/.""])\s*(/d\s+)?(?<arg>.*)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DrivePattern = new(@"^[a-zA-Z]:$", RegexOptions.Compiled);

        static TerminalSession()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            try
            {
                ConsoleEncoding = Encoding.GetEncoding((int)GetOEMCP());
            }
            catch
            {
                ConsoleEncoding = Encoding.UTF8;
            }
        }

        private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
        private readonly StringBuilder _pending = new();
        private Process? _process;
        private string _title;
        private string _workingDirectory;
        private bool _isRunning;
        private string? _currentCommand;

        public TerminalSession(string title, string workingDirectory)
        {
            _title = title;
            _workingDirectory = Directory.Exists(workingDirectory)
                ? workingDirectory
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>请求清屏（cls）。</summary>
        public event Action? ClearRequested;

        /// <summary>请求关闭会话（exit）。</summary>
        public event Action? CloseRequested;

        public List<string> History { get; } = new();

        public string Title
        {
            get => _title;
            set { _title = value; OnChanged(nameof(Title)); }
        }

        public string WorkingDirectory
        {
            get => _workingDirectory;
            private set { _workingDirectory = value; OnChanged(nameof(WorkingDirectory)); }
        }

        public bool IsRunning
        {
            get => _isRunning;
            private set { _isRunning = value; OnChanged(nameof(IsRunning)); OnChanged(nameof(ToolTipText)); }
        }

        public string ToolTipText => IsRunning ? $"运行中：{_currentCommand}" : WorkingDirectory;

        /// <summary>写入一段输出（线程安全）。</summary>
        public void Write(string text)
        {
            lock (_pending) _pending.Append(text);
        }

        /// <summary>取走尚未显示的输出。</summary>
        public string? TakePending()
        {
            lock (_pending)
            {
                if (_pending.Length == 0) return null;
                var text = _pending.ToString();
                _pending.Clear();
                return text;
            }
        }

        public void DiscardPending()
        {
            lock (_pending) _pending.Clear();
        }

        /// <summary>
        /// 执行用户输入的一行。若有程序正在运行，则把这一行发送到它的标准输入。
        /// </summary>
        public void Submit(string line)
        {
            if (IsRunning)
            {
                Write(line + "\n");
                try
                {
                    _process?.StandardInput.WriteLine(line);
                }
                catch (Exception ex)
                {
                    Write($"[无法发送输入：{ex.Message}]\n");
                }
                return;
            }

            var command = line.Trim();
            Write($"{WorkingDirectory}> {command}\n");
            if (command.Length == 0) return;

            if (History.Count == 0 || History[^1] != command)
                History.Add(command);

            if (!TryBuiltin(command))
                Start(command);
        }

        public void Stop()
        {
            var process = _process;
            if (process == null) return;
            try
            {
                if (!process.HasExited)
                {
                    Write("^C\n");
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // 进程可能刚好已退出
            }
        }

        private bool TryBuiltin(string command)
        {
            // 含管道 / 重定向 / 连接符时交给 cmd 处理
            if (command.IndexOfAny(new[] { '&', '|', '<', '>' }) >= 0) return false;

            var lower = command.ToLowerInvariant();
            if (lower is "cls" or "clear")
            {
                DiscardPending();
                ClearRequested?.Invoke();
                return true;
            }
            if (lower == "exit")
            {
                CloseRequested?.Invoke();
                return true;
            }
            if (DrivePattern.IsMatch(command))
            {
                ChangeDirectory(command + "\\");
                return true;
            }

            var match = CdPattern.Match(command);
            if (!match.Success) return false;

            var arg = match.Groups["arg"].Value.Trim().Trim('"');
            if (arg.Length == 0)
                Write(WorkingDirectory + "\n\n");
            else
                ChangeDirectory(arg);
            return true;
        }

        private void ChangeDirectory(string arg)
        {
            try
            {
                var target = Path.GetFullPath(Path.Combine(WorkingDirectory, Environment.ExpandEnvironmentVariables(arg)));
                if (Directory.Exists(target))
                {
                    WorkingDirectory = target;
                    Write("\n");
                    return;
                }
            }
            catch
            {
                // 路径格式不正确，按“找不到路径”处理
            }
            Write("系统找不到指定的路径。\n\n");
        }

        private void Start(string command)
        {
            var psi = new ProcessStartInfo("cmd.exe", $"/d /s /c \"{command}\"")
            {
                WorkingDirectory = WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = ConsoleEncoding,
                StandardErrorEncoding = ConsoleEncoding,
                StandardInputEncoding = ConsoleEncoding
            };
            // 让 Python 类工具实时输出，而不是攒满缓冲区才显示
            psi.Environment["PYTHONUNBUFFERED"] = "1";

            Process process;
            try
            {
                process = Process.Start(psi) ?? throw new InvalidOperationException("进程未能启动");
            }
            catch (Exception ex)
            {
                Write($"[启动失败：{ex.Message}]\n\n");
                return;
            }

            process.StandardInput.AutoFlush = true;
            _process = process;
            _currentCommand = command;
            IsRunning = true;

            var watch = Stopwatch.StartNew();
            var pumps = Task.WhenAll(Pump(process.StandardOutput), Pump(process.StandardError));
            _ = Task.Run(async () =>
            {
                await process.WaitForExitAsync().ConfigureAwait(false);
                // 被 start 出去的子程序可能继承了管道，不无限等待
                await Task.WhenAny(pumps, Task.Delay(500)).ConfigureAwait(false);
                watch.Stop();

                int code;
                try { code = process.ExitCode; } catch { code = -1; }
                process.Dispose();

                Write($"\n[已结束 · 退出码 {code} · 用时 {FormatElapsed(watch.Elapsed)}]\n\n");
                Post(() =>
                {
                    if (_process == process) _process = null;
                    _currentCommand = null;
                    IsRunning = false;
                });
            });
        }

        private async Task Pump(StreamReader reader)
        {
            var buffer = new char[4096];
            try
            {
                int read;
                while ((read = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                    Write(new string(buffer, 0, read));
            }
            catch
            {
                // 进程被结束时管道会断开
            }
        }

        private static string FormatElapsed(TimeSpan t) =>
            t.TotalSeconds < 60 ? $"{t.TotalSeconds:0.00}s" : $"{(int)t.TotalMinutes}m{t.Seconds:00}s";

        private void Post(Action action)
        {
            if (_ui != null) _ui.Post(_ => action(), null);
            else action();
        }

        private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        [DllImport("kernel32.dll")]
        private static extern uint GetOEMCP();
    }
}
