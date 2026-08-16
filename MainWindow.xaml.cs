using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace YtDlpHelper;

public partial class MainWindow : Window
{
    private CancellationTokenSource? _cts;

    private static readonly string SettingsFilePath =
        Path.Combine(
            Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory,
            "yt-dlp-helper-settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public MainWindow()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void MainWindow_Closing(object sender, CancelEventArgs e)
    {
        SaveSettings();
    }

    private void RootGrid_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // 点击窗口空白区域时，清除键盘焦点，让输入框的高亮边框消失
        if (e.OriginalSource is not System.Windows.Controls.TextBox)
        {
            Keyboard.ClearFocus();
        }
    }

    private async void ListFormatsButton_Click(object sender, RoutedEventArgs e)
    {
        var url = UrlTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show(
                "请先填写视频地址 URL。\n\nyt-dlp -F 需要一个目标地址，否则无法列出格式。",
                "提示",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var arguments = new List<string>
        {
            "-F"
        };

        AddCommonArguments(arguments);
        arguments.Add(url);

        await RunYtDlpAsync(arguments);
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        var url = UrlTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show(
                "请先填写视频地址 URL。",
                "提示",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var arguments = new List<string>();

        var format = BuildFormatSelection();
        if (!string.IsNullOrWhiteSpace(format))
        {
            arguments.Add("-f");
            arguments.Add(format);
        }

        AddCommonArguments(arguments);
        arguments.Add(url);

        await RunYtDlpAsync(arguments);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
    }

    private string BuildFormatSelection()
    {
        var video = VideoFormatIdTextBox.Text.Trim();
        var audio = AudioFormatIdTextBox.Text.Trim();

        if (!string.IsNullOrWhiteSpace(video) && !string.IsNullOrWhiteSpace(audio))
        {
            return $"{video}+{audio}";
        }

        if (!string.IsNullOrWhiteSpace(video))
        {
            return video;
        }

        if (!string.IsNullOrWhiteSpace(audio))
        {
            return audio;
        }

        return string.Empty;
    }

    private void AddCommonArguments(List<string> arguments)
    {
        // 强制 yt-dlp 以 UTF-8 输出，避免日文等非 ANSI 字符在管道模式下被替换成乱码
        arguments.Add("--encoding");
        arguments.Add("utf-8");

        var proxy = ProxyTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(proxy))
        {
            arguments.Add("--proxy");
            arguments.Add(proxy);
        }

        var cookies = CookiesTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(cookies))
        {
            arguments.Add("--cookies");
            arguments.Add(cookies);
        }

        var downloadDir = DownloadDirTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(downloadDir))
        {
            arguments.Add("--paths");
            arguments.Add(downloadDir);
        }

        var extra = ExtraArgsTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(extra))
        {
            arguments.AddRange(SplitCommandLine(extra));
        }
    }

    private async Task RunYtDlpAsync(IReadOnlyList<string> arguments)
    {
        SaveSettings();

        var exe = YtDlpPathTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(exe))
        {
            exe = "yt-dlp.exe";
        }

        SetBusy(true);

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        AppendOutput("");
        AppendOutput($">>> 启动命令：{FormatCommandLine(exe, arguments)}");

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = ShowWindowCheckBox.IsChecked != true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = AppContext.BaseDirectory
        };

        foreach (var argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        using var process = new Process
        {
            StartInfo = psi,
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data is not null)
            {
                Dispatcher.InvokeAsync(() => AppendOutput(e.Data));
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data is not null)
            {
                Dispatcher.InvokeAsync(() => AppendOutput("[stderr] " + e.Data));
            }
        };

        try
        {
            if (!process.Start())
            {
                AppendOutput("无法启动 yt-dlp 进程。");
                return;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(token);

            AppendOutput($"<<< 进程退出，退出码：{process.ExitCode}");
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }

            AppendOutput("<<< 已取消。");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            AppendOutput($"无法启动 yt-dlp：{ex.Message}");
            AppendOutput("请检查 yt-dlp.exe 路径是否正确，或确认 yt-dlp.exe 已加入 PATH。");
        }
        catch (Exception ex)
        {
            AppendOutput($"发生错误：{ex.Message}");
        }
        finally
        {
            SetBusy(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void SetBusy(bool busy)
    {
        ListFormatsButton.IsEnabled = !busy;
        DownloadButton.IsEnabled = !busy;
        CancelButton.IsEnabled = busy;
    }

    private void AppendOutput(string text)
    {
        OutputTextBox.AppendText(text + Environment.NewLine);
        OutputTextBox.ScrollToEnd();
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return;
            }

            var json = File.ReadAllText(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);

            if (settings is null)
            {
                return;
            }

            YtDlpPathTextBox.Text = string.IsNullOrWhiteSpace(settings.YtDlpPath)
                ? "yt-dlp.exe"
                : settings.YtDlpPath;

            DownloadDirTextBox.Text = settings.DownloadDir ?? string.Empty;
            ProxyTextBox.Text = settings.Proxy ?? string.Empty;
            CookiesTextBox.Text = settings.Cookies ?? string.Empty;
            ExtraArgsTextBox.Text = settings.ExtraArgs ?? string.Empty;
            ShowWindowCheckBox.IsChecked = settings.ShowWindow;
        }
        catch
        {
        }
    }

    private void SaveSettings()
    {
        try
        {
            var settings = new AppSettings
            {
                YtDlpPath = YtDlpPathTextBox.Text.Trim(),
                DownloadDir = DownloadDirTextBox.Text.Trim(),
                Proxy = ProxyTextBox.Text.Trim(),
                Cookies = CookiesTextBox.Text.Trim(),
                ExtraArgs = ExtraArgsTextBox.Text.Trim(),
                ShowWindow = ShowWindowCheckBox.IsChecked == true
            };

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
        }
    }

    private static IEnumerable<string> SplitCommandLine(string input)
    {
        var result = new List<string>();
        var current = new StringBuilder();

        bool inDoubleQuotes = false;
        bool inSingleQuotes = false;
        bool hasToken = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (c == '"' && !inSingleQuotes)
            {
                inDoubleQuotes = !inDoubleQuotes;
                hasToken = true;
                continue;
            }

            if (c == '\'' && !inDoubleQuotes)
            {
                inSingleQuotes = !inSingleQuotes;
                hasToken = true;
                continue;
            }

            if (char.IsWhiteSpace(c) && !inDoubleQuotes && !inSingleQuotes)
            {
                if (current.Length > 0 || hasToken)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    hasToken = false;
                }
            }
            else
            {
                current.Append(c);
                hasToken = true;
            }
        }

        if (current.Length > 0 || hasToken)
        {
            result.Add(current.ToString());
        }

        return result;
    }

    private static string FormatCommandLine(string fileName, IReadOnlyList<string> args)
    {
        return $"{QuoteArgument(fileName)} {string.Join(" ", args.Select(QuoteArgument))}";
    }

    private static string QuoteArgument(string argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            return "\"\"";
        }

        if (argument.Contains(' ') || argument.Contains('\t') || argument.Contains('"'))
        {
            return $"\"{argument.Replace("\"", "\\\"")}\"";
        }

        return argument;
    }

    private sealed class AppSettings
    {
        public string YtDlpPath { get; set; } = "yt-dlp.exe";
        public string DownloadDir { get; set; } = string.Empty;
        public string Proxy { get; set; } = string.Empty;
        public string Cookies { get; set; } = string.Empty;
        public string ExtraArgs { get; set; } = string.Empty;
        public bool ShowWindow { get; set; }
    }
}