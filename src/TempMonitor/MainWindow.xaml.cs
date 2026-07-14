using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace TempMonitor;

public partial class MainWindow : Window
{
    private readonly Settings _settings = Settings.Load();
    private SensorReader? _sensors;
    private WinForms.NotifyIcon? _trayIcon;
    private CancellationTokenSource? _pollCts;

    private static readonly Brush CoolBrush = new SolidColorBrush(Color.FromRgb(0x7C, 0xD9, 0x92));
    private static readonly Brush WarmBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0xC5, 0x6A));
    private static readonly Brush HotBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0x6A, 0x6A));

    public MainWindow()
    {
        InitializeComponent();
        Opacity = _settings.Opacity;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionWindow();
        BuildTrayIcon();
        BuildContextMenu();
        if (_settings.ClickThrough) SetClickThrough(true);

        // Opening the sensor library loads its kernel driver - do it off the
        // UI thread so the overlay appears instantly.
        _pollCts = new CancellationTokenSource();
        _ = Task.Run(() => PollLoopAsync(_pollCts.Token));
    }

    private void PositionWindow()
    {
        var area = SystemParameters.WorkArea;
        if (!double.IsNaN(_settings.Left) && !double.IsNaN(_settings.Top) &&
            _settings.Left < SystemParameters.VirtualScreenWidth &&
            _settings.Top < SystemParameters.VirtualScreenHeight)
        {
            Left = _settings.Left;
            Top = _settings.Top;
        }
        else
        {
            // Default: top-right corner of the primary monitor.
            Left = area.Right - 190;
            Top = area.Top + 12;
        }
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        try
        {
            _sensors = new SensorReader();
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() =>
                CpuTempText.Text = "ERR");
            System.Diagnostics.Debug.WriteLine($"Sensor init failed: {ex}");
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            var stats = _sensors.Read();
            await Dispatcher.InvokeAsync(() => UpdateUi(stats));
            try { await Task.Delay(_settings.UpdateIntervalMs, ct); }
            catch (TaskCanceledException) { break; }
        }
    }

    private void UpdateUi(HardwareStats s)
    {
        CpuTempText.Text = Fmt(s.CpuTemp, "°");
        CpuTempText.Foreground = TempBrush(s.CpuTemp);
        CpuLoadText.Text = Fmt(s.CpuLoad, "%");
        CpuPowerText.Text = Fmt(s.CpuPower, "W");

        GpuTempText.Text = Fmt(s.GpuTemp, "°");
        GpuTempText.Foreground = TempBrush(s.GpuTemp);
        GpuLoadText.Text = Fmt(s.GpuLoad, "%");
        GpuPowerText.Text = Fmt(s.GpuPower, "W");

        if (_trayIcon is not null)
            _trayIcon.Text = $"CPU {Fmt(s.CpuTemp, "°")} {Fmt(s.CpuPower, "W")} | GPU {Fmt(s.GpuTemp, "°")} {Fmt(s.GpuPower, "W")}";
    }

    private static string Fmt(float? v, string unit) =>
        v is null ? $"--{unit}" : $"{v:0}{unit}";

    private static Brush TempBrush(float? temp) => temp switch
    {
        null => Brushes.Gray,
        < 60 => CoolBrush,
        < 80 => WarmBrush,
        _ => HotBrush,
    };

    // ----- interaction -------------------------------------------------

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
            _settings.Left = Left;
            _settings.Top = Top;
            _settings.Save();
        }
    }

    private void BuildContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var clickThrough = new System.Windows.Controls.MenuItem
        {
            Header = "Click-through (disable via tray icon)",
            IsCheckable = true,
            IsChecked = _settings.ClickThrough,
        };
        clickThrough.Click += (_, _) => ToggleClickThrough();
        menu.Items.Add(clickThrough);

        var exit = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exit.Click += (_, _) => Close();
        menu.Items.Add(exit);

        ContextMenu = menu;
    }

    private void BuildTrayIcon()
    {
        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Visible = true,
            Text = "Temperature Intensity Monitor",
        };

        var menu = new WinForms.ContextMenuStrip();
        var clickThroughItem = new WinForms.ToolStripMenuItem("Click-through")
        {
            Checked = _settings.ClickThrough,
            CheckOnClick = true,
        };
        clickThroughItem.Click += (_, _) => ToggleClickThrough(clickThroughItem.Checked);
        menu.Items.Add(clickThroughItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Close());
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => { Visibility = Visibility.Visible; Activate(); };
    }

    /// <summary>Tiny generated thermometer glyph so the repo needs no binary assets.</summary>
    private static Drawing.Icon CreateTrayIcon()
    {
        using var bmp = new Drawing.Bitmap(16, 16);
        using (var g = Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var stem = new Drawing.Pen(Drawing.Color.White, 3);
            g.DrawLine(stem, 8, 2, 8, 9);
            g.FillEllipse(Drawing.Brushes.OrangeRed, 4, 8, 8, 8);
        }
        return Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    // ----- click-through (WS_EX_TRANSPARENT) ----------------------------

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x20;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    private void ToggleClickThrough(bool? state = null)
    {
        _settings.ClickThrough = state ?? !_settings.ClickThrough;
        SetClickThrough(_settings.ClickThrough);
        _settings.Save();
    }

    private void SetClickThrough(bool enabled)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        int style = GetWindowLong(hwnd, GWL_EXSTYLE);
        style = enabled ? style | WS_EX_TRANSPARENT : style & ~WS_EX_TRANSPARENT;
        SetWindowLong(hwnd, GWL_EXSTYLE, style);
    }

    // ----- teardown ------------------------------------------------------

    private void OnClosed(object? sender, EventArgs e)
    {
        _pollCts?.Cancel();
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        _sensors?.Dispose();
        _settings.Save();
    }
}
