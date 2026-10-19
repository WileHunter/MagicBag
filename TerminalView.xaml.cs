using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WpfApp3.Services;
using MessageBox = HandyControl.Controls.MessageBox;

namespace WpfApp3
{
    public partial class TerminalView : UserControl
    {
        private const int MaxOutputLength = 400_000;
        private const int TrimToLength = 300_000;

        // ANSI 转义序列（颜色、光标移动等）与其余不可见控制字符
        private static readonly Regex AnsiPattern = new(
            @"\x1B\[[0-?]*[ -/]*[@-~]|\x1B\][^\x07\x1B]*(\x07|\x1B\\)|\x1B[@-Z\\-_]|[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]",
            RegexOptions.Compiled);

        /// <summary>每个会话对应的输出框及行状态。</summary>
        private sealed class SessionView
        {
            public required TextBox Box { get; init; }
            public int LastLineLength;
            public bool PendingCarriageReturn;
        }

        private readonly ObservableCollection<TerminalSession> _sessions = new();
        private readonly Dictionary<TerminalSession, SessionView> _views = new();
        private readonly DispatcherTimer _flushTimer;
        private int _sessionCounter;
        private int _historyIndex = -1;

        public TerminalView()
        {
            InitializeComponent();
            SessionList.ItemsSource = _sessions;
            _flushTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(40) };
            _flushTimer.Tick += (_, _) => FlushAll();
            _flushTimer.Start();
            UpdateState();
        }

        private TerminalSession? Current => SessionList.SelectedItem as TerminalSession;

        public int RunningCount => _sessions.Count(s => s.IsRunning);

        #region 会话

        /// <summary>
        /// 新建会话；command 不为空时立即执行。
        /// </summary>
        public TerminalSession OpenSession(string? title, string? workingDirectory, string? command = null)
        {
            _sessionCounter++;
            var session = new TerminalSession(
                string.IsNullOrWhiteSpace(title) ? $"终端 {_sessionCounter}" : title!,
                string.IsNullOrWhiteSpace(workingDirectory)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                    : workingDirectory!);

            var box = new TextBox { Style = (Style)FindResource("TermOutput") };
            box.PreviewKeyDown += OutputBox_PreviewKeyDown;
            _views[session] = new SessionView { Box = box };

            session.PropertyChanged += Session_PropertyChanged;
            session.ClearRequested += () => ClearOutput(session);
            session.CloseRequested += () => Dispatcher.BeginInvoke(() => CloseSession(session, confirm: false));

            session.Write($"MagicBag 终端 · {DateTime.Now:yyyy-MM-dd HH:mm}\n输入 cls 清屏，exit 关闭；运行中可按 Ctrl+C 停止。\n\n");

            _sessions.Add(session);
            SessionList.SelectedItem = session;
            SessionList.ScrollIntoView(session);

            if (!string.IsNullOrWhiteSpace(command))
                session.Submit(command!);

            FocusInput();
            return session;
        }

        private void CloseSession(TerminalSession session, bool confirm)
        {
            if (confirm && session.IsRunning)
            {
                var result = MessageBox.Show($"「{session.Title}」中的命令仍在运行，关闭将结束它。\n确定关闭？", "关闭终端",
                    MessageBoxButton.OKCancel, MessageBoxImage.Question);
                if (result != MessageBoxResult.OK) return;
            }

            session.Stop();
            session.PropertyChanged -= Session_PropertyChanged;

            var index = _sessions.IndexOf(session);
            _sessions.Remove(session);
            _views.Remove(session);
            if (_sessions.Count > 0 && SessionList.SelectedItem == null)
                SessionList.SelectedIndex = Math.Min(index, _sessions.Count - 1);
            UpdateState();
        }

        /// <summary>程序退出时结束所有仍在运行的命令。</summary>
        public void StopAll()
        {
            foreach (var session in _sessions)
                session.Stop();
        }

        private void Session_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender == Current && e.PropertyName is nameof(TerminalSession.IsRunning) or nameof(TerminalSession.WorkingDirectory))
                UpdateState();
        }

        private void SessionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _historyIndex = -1;
            UpdateState();
            if (Current != null && _views.TryGetValue(Current, out var view))
                Dispatcher.BeginInvoke(view.Box.ScrollToEnd, DispatcherPriority.Background);
        }

        private void UpdateState()
        {
            var session = Current;
            var hasSession = session != null;

            OutputHost.Content = hasSession && _views.TryGetValue(session!, out var view) ? view.Box : null;
            EmptyState.Visibility = hasSession ? Visibility.Collapsed : Visibility.Visible;
            InputBar.IsEnabled = hasSession;
            StopButton.IsEnabled = session?.IsRunning == true;

            if (session == null)
            {
                PromptText.Text = "";
                PromptText.ToolTip = null;
            }
            else if (session.IsRunning)
            {
                PromptText.Text = "›";
                PromptText.ToolTip = "程序运行中：输入内容将发送给它";
            }
            else
            {
                PromptText.Text = session.WorkingDirectory + ">";
                PromptText.ToolTip = session.WorkingDirectory;
            }
        }

        private void TerminalView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!IsVisible) return;
            if (_sessions.Count == 0)
                OpenSession(null, null);
            else
                FocusInput();
        }

        private void FocusInput() =>
            Dispatcher.BeginInvoke(() => { if (IsVisible) CommandInput.Focus(); }, DispatcherPriority.Input);

        #endregion

        #region 输出

        private void FlushAll()
        {
            foreach (var (session, view) in _views)
            {
                var text = session.TakePending();
                if (text != null)
                    Append(view, text);
            }
        }

        private static void Append(SessionView view, string raw)
        {
            var box = view.Box;

            // 统一换行；单独的 \r 表示回到行首（进度条），用新内容覆盖当前行
            var text = raw;
            if (view.PendingCarriageReturn) text = "\r" + text;
            view.PendingCarriageReturn = text.EndsWith('\r');
            if (view.PendingCarriageReturn) text = text[..^1];
            text = text.Replace("\r\n", "\n");
            text = AnsiPattern.Replace(text, "");

            var parts = text.Split('\r');
            var sb = new StringBuilder(parts[0]);
            var removeFromBox = 0;
            for (var i = 1; i < parts.Length; i++)
            {
                var newline = LastIndexOf(sb, '\n');
                if (newline >= 0)
                    sb.Length = newline + 1;
                else
                {
                    sb.Clear();
                    removeFromBox = view.LastLineLength;
                }
                sb.Append(parts[i]);
            }
            var chunk = sb.ToString();
            if (chunk.Length == 0 && removeFromBox == 0) return;

            var atBottom = box.ExtentHeight - box.VerticalOffset - box.ViewportHeight < 4;

            if (removeFromBox > 0)
            {
                var length = box.Text.Length;
                removeFromBox = Math.Min(removeFromBox, length);
                box.Select(length - removeFromBox, removeFromBox);
                box.SelectedText = "";
            }
            box.AppendText(chunk);

            var lastNewline = chunk.LastIndexOf('\n');
            view.LastLineLength = lastNewline >= 0
                ? chunk.Length - lastNewline - 1
                : view.LastLineLength - removeFromBox + chunk.Length;

            if (box.Text.Length > MaxOutputLength)
            {
                var all = box.Text;
                var start = all.IndexOf('\n', all.Length - TrimToLength);
                box.Text = all[(start >= 0 ? start + 1 : all.Length - TrimToLength)..];
                atBottom = true;
            }

            if (atBottom) box.ScrollToEnd();
        }

        private static int LastIndexOf(StringBuilder sb, char ch)
        {
            for (var i = sb.Length - 1; i >= 0; i--)
                if (sb[i] == ch) return i;
            return -1;
        }

        private void ClearOutput(TerminalSession session)
        {
            if (!_views.TryGetValue(session, out var view)) return;
            session.DiscardPending();
            view.Box.Clear();
            view.LastLineLength = 0;
            view.PendingCarriageReturn = false;
        }

        #endregion

        #region 输入

        private void Submit()
        {
            var session = Current;
            if (session == null) return;

            var line = CommandInput.Text;
            CommandInput.Clear();
            _historyIndex = -1;

            // 用户执行命令时总是滚到底部
            FlushAll();
            if (_views.TryGetValue(session, out var view)) view.Box.ScrollToEnd();

            session.Submit(line);
            UpdateState();
        }

        private void CommandInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var session = Current;
            if (session == null) return;
            var ctrl = Keyboard.Modifiers == ModifierKeys.Control;

            switch (e.Key)
            {
                case Key.Enter:
                    Submit();
                    e.Handled = true;
                    break;

                case Key.Up when !session.IsRunning:
                case Key.Down when !session.IsRunning:
                    var history = session.History;
                    if (history.Count == 0) break;
                    if (e.Key == Key.Up)
                        _historyIndex = _historyIndex < 0 ? history.Count - 1 : Math.Max(0, _historyIndex - 1);
                    else if (_historyIndex >= 0)
                        _historyIndex = _historyIndex + 1 >= history.Count ? -1 : _historyIndex + 1;
                    CommandInput.Text = _historyIndex >= 0 ? history[_historyIndex] : "";
                    CommandInput.CaretIndex = CommandInput.Text.Length;
                    e.Handled = true;
                    break;

                case Key.Escape:
                    CommandInput.Clear();
                    _historyIndex = -1;
                    e.Handled = true;
                    break;

                case Key.C when ctrl && CommandInput.SelectionLength == 0 && session.IsRunning:
                    session.Stop();
                    e.Handled = true;
                    break;

                case Key.L when ctrl:
                    ClearOutput(session);
                    e.Handled = true;
                    break;
            }
        }

        // 在输出区直接打字时，把焦点交给输入框
        private void OutputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            if (ctrl && e.Key == Key.L && Current != null)
            {
                ClearOutput(Current);
                e.Handled = true;
                return;
            }
            if (ctrl && e.Key == Key.C && ((TextBox)sender).SelectionLength == 0 && Current?.IsRunning == true)
            {
                Current.Stop();
                e.Handled = true;
                return;
            }
            if (!ctrl && (e.Key is >= Key.A and <= Key.Z or >= Key.D0 and <= Key.D9 or Key.Enter or Key.Space))
                CommandInput.Focus();
        }

        private void Run_Click(object sender, RoutedEventArgs e)
        {
            Submit();
            CommandInput.Focus();
        }

        #endregion

        #region 工具栏

        private void NewSession_Click(object sender, RoutedEventArgs e) =>
            OpenSession(null, Current?.WorkingDirectory);

        private void CloseSession_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is TerminalSession session)
                CloseSession(session, confirm: true);
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            Current?.Stop();
            CommandInput.Focus();
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (Current != null) ClearOutput(Current);
            CommandInput.Focus();
        }

        private void CopyAll_Click(object sender, RoutedEventArgs e)
        {
            if (Current == null || !_views.TryGetValue(Current, out var view)) return;
            try
            {
                Clipboard.SetText(view.Box.Text);
                Toast.Success("已复制全部输出");
            }
            catch (Exception ex)
            {
                Toast.Error($"复制失败：{ex.Message}");
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (Current == null) return;
            try
            {
                Process.Start(new ProcessStartInfo { FileName = Current.WorkingDirectory, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Toast.Error($"无法打开目录：{ex.Message}");
            }
        }

        private void OpenSystem_Click(object sender, RoutedEventArgs e)
        {
            if (Current == null) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/k",
                    WorkingDirectory = Current.WorkingDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Toast.Error($"无法打开系统终端：{ex.Message}");
            }
        }

        #endregion
    }
}
