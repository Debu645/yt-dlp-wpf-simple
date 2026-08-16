using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace YtDlpHelper;

public partial class App : Application
{
    private bool _isLightTheme;
    private DispatcherTimer? _themeTimer;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _isLightTheme = ShouldUseLightTheme();
        ApplyTheme(_isLightTheme);

        _themeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };

        _themeTimer.Tick += ThemeTimer_Tick;
        _themeTimer.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _themeTimer?.Stop();
        base.OnExit(e);
    }

    private void ThemeTimer_Tick(object? sender, EventArgs e)
    {
        var currentSystemTheme = ShouldUseLightTheme();

        if (currentSystemTheme != _isLightTheme)
        {
            _isLightTheme = currentSystemTheme;
            ApplyTheme(currentSystemTheme);
        }
    }

    private static bool ShouldUseLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            if (key?.GetValue("AppsUseLightTheme") is int value)
            {
                return value != 0;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private void ApplyTheme(bool isLight)
    {
        if (isLight)
        {
            SetColor("BackgroundBrush", 246, 247, 250);
            SetColor("SurfaceBrush", 255, 255, 255);
            SetColor("SurfaceAltBrush", 240, 241, 245);
            SetColor("ForegroundBrush", 30, 30, 35);
            SetColor("MutedForegroundBrush", 110, 110, 120);
            SetColor("TextBoxBackgroundBrush", 255, 255, 255);
            SetColor("ButtonBackgroundBrush", 235, 236, 240);
            SetColor("ButtonHoverBrush", 225, 226, 232);
            SetColor("ButtonPressedBrush", 210, 211, 218);
            SetColor("BorderBrush", 210, 211, 218);
            SetColor("AccentBrush", 79, 140, 255);
            SetColor("AccentHoverBrush", 107, 160, 255);
            SetColor("AccentPressedBrush", 58, 116, 224);
            SetColor("DangerBrush", 229, 72, 77);
            SetColor("DangerHoverBrush", 240, 90, 95);
            SetColor("SuccessBrush", 70, 167, 88);
            SetColor("OutputBackgroundBrush", 250, 250, 252);
        }
        else
        {
            SetColor("BackgroundBrush", 27, 27, 31);
            SetColor("SurfaceBrush", 35, 35, 41);
            SetColor("SurfaceAltBrush", 42, 42, 50);
            SetColor("ForegroundBrush", 240, 240, 242);
            SetColor("MutedForegroundBrush", 154, 154, 165);
            SetColor("TextBoxBackgroundBrush", 46, 46, 54);
            SetColor("ButtonBackgroundBrush", 58, 58, 68);
            SetColor("ButtonHoverBrush", 74, 74, 86);
            SetColor("ButtonPressedBrush", 46, 46, 54);
            SetColor("BorderBrush", 63, 63, 74);
            SetColor("AccentBrush", 79, 140, 255);
            SetColor("AccentHoverBrush", 107, 160, 255);
            SetColor("AccentPressedBrush", 58, 116, 224);
            SetColor("DangerBrush", 229, 72, 77);
            SetColor("DangerHoverBrush", 240, 90, 95);
            SetColor("SuccessBrush", 70, 167, 88);
            SetColor("OutputBackgroundBrush", 20, 20, 24);
        }
    }

    private void SetColor(string resourceKey, byte r, byte g, byte b)
    {
        Current.Resources[resourceKey] = new SolidColorBrush(Color.FromRgb(r, g, b));
    }
}