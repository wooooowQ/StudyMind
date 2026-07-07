using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using StudyMind.App.ViewModels;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT.Interop;
using VirtualKey = Windows.System.VirtualKey;

namespace StudyMind.App;

public sealed partial class CyberNoteWindow : Window
{
    private const int WmNcLButtonDown = 0x00A1;
    private const int HtCaption = 0x0002;
    private const int NoteCornerRadiusDip = 15;
    private const int DefaultDpi = 96;

    private readonly Action _showMainWindow;
    private readonly IntPtr _hWnd;
    private readonly CyberNoteSettings _settings;
    private readonly DispatcherQueueTimer _saveSettingsTimer;
    private readonly AppWindow? _appWindow;
    private bool _isClosingForAppExit;
    private bool _hasPlacedWindow;

    public CyberNoteWindow(MainViewModel study, Action showMainWindow)
    {
        InitializeComponent();

        _showMainWindow = showMainWindow;
        _hWnd = WindowNative.GetWindowHandle(this);
        _settings = CyberNoteSettingsStore.Load();
        _saveSettingsTimer = DispatcherQueue.CreateTimer();
        _saveSettingsTimer.Interval = TimeSpan.FromMilliseconds(500);
        _saveSettingsTimer.Tick += SaveSettingsTimer_Tick;
        Root.DataContext = new CyberNoteViewModel(study, _settings, SaveSettings);
        Title = "StudyMind 便签";

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(DragHandle);

        _appWindow = GetAppWindow();
        ConfigureCompanionWindow();
    }

    public void ShowNote()
    {
        if (!_hasPlacedWindow)
        {
            if (!RestoreSavedPlacement())
            {
                PositionNearWorkAreaCorner();
            }
        }
        else
        {
            EnsureWindowInsideWorkArea();
        }

        _appWindow?.Show();
        ApplyRoundedWindowRegion();
        Activate();
        Root.Focus(FocusState.Programmatic);
    }

    public void HideNote()
    {
        SaveSettings();
        _appWindow?.Hide();
    }

    public void CloseForAppExit()
    {
        SaveSettings();
        _isClosingForAppExit = true;
        Close();
    }

    private AppWindow? GetAppWindow()
    {
        var windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
        return AppWindow.GetFromWindowId(windowId);
    }

    private void ConfigureCompanionWindow()
    {
        if (_appWindow is null)
        {
            return;
        }

        _appWindow.Resize(new SizeInt32(_settings.Width, _settings.Height));
        _appWindow.IsShownInSwitchers = false;

        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        ApplyRoundedWindowRegion();
        _appWindow.Changed += AppWindow_Changed;
        _appWindow.Closing += AppWindow_Closing;
    }

    private bool RestoreSavedPlacement()
    {
        if (_appWindow is null || !_settings.HasSavedPlacement)
        {
            return false;
        }

        _appWindow.Move(new PointInt32(_settings.X!.Value, _settings.Y!.Value));
        _hasPlacedWindow = true;
        EnsureWindowInsideWorkArea();
        return true;
    }

    private void PositionNearWorkAreaCorner()
    {
        if (_appWindow is null)
        {
            return;
        }

        var area = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
        var workArea = area.WorkArea;
        var size = _appWindow.Size;
        var x = workArea.X + workArea.Width - size.Width - 20;
        var y = workArea.Y + workArea.Height - size.Height - 20;
        _appWindow.Move(new PointInt32(Math.Max(workArea.X, x), Math.Max(workArea.Y, y)));
        _hasPlacedWindow = true;
    }

    private void EnsureWindowInsideWorkArea()
    {
        if (_appWindow is null)
        {
            return;
        }

        var area = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
        var workArea = area.WorkArea;
        var size = _appWindow.Size;
        var position = _appWindow.Position;
        var maxX = Math.Max(workArea.X, workArea.X + workArea.Width - size.Width);
        var maxY = Math.Max(workArea.Y, workArea.Y + workArea.Height - size.Height);
        var x = Math.Clamp(position.X, workArea.X, maxX);
        var y = Math.Clamp(position.Y, workArea.Y, maxY);

        if (x != position.X || y != position.Y)
        {
            _appWindow.Move(new PointInt32(x, y));
        }
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidSizeChange)
        {
            ApplyRoundedWindowRegion();
        }

        if (args.DidPositionChange || args.DidSizeChange)
        {
            _hasPlacedWindow = true;
            QueueSaveSettings();
        }
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isClosingForAppExit)
        {
            return;
        }

        args.Cancel = true;
        HideNote();
    }

    private void QueueSaveSettings()
    {
        _saveSettingsTimer.Stop();
        _saveSettingsTimer.Start();
    }

    private void SaveSettingsTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        SaveSettings();
    }

    private void SaveSettings()
    {
        CapturePlacementSettings();
        CyberNoteSettingsStore.Save(_settings);
    }

    private void CapturePlacementSettings()
    {
        if (_appWindow is null)
        {
            return;
        }

        var position = _appWindow.Position;
        _settings.Width = CyberNoteSettings.DefaultWidth;
        _settings.Height = CyberNoteSettings.DefaultHeight;
        _settings.X = position.X;
        _settings.Y = position.Y;
    }

    private void ApplyRoundedWindowRegion()
    {
        if (!GetWindowRect(_hWnd, out var rect))
        {
            return;
        }

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var dpiScale = GetDpiForWindow(_hWnd) / (double)DefaultDpi;
        var cornerDiameter = Math.Max(1, (int)Math.Round(NoteCornerRadiusDip * dpiScale * 2));
        var region = CreateRoundRectRgn(0, 0, width, height, cornerDiameter, cornerDiameter);
        if (region == IntPtr.Zero)
        {
            return;
        }

        if (SetWindowRgn(_hWnd, region, true) == 0)
        {
            DeleteObject(region);
        }
    }

    private void Hide_Click(object sender, RoutedEventArgs e)
    {
        HideNote();
    }

    private void OpenMain_Click(object sender, RoutedEventArgs e)
    {
        _showMainWindow();
    }

    private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.OriginalSource is TextBox)
        {
            return;
        }

        if (Root.DataContext is not CyberNoteViewModel viewModel)
        {
            return;
        }

        if (e.Key == VirtualKey.Left)
        {
            viewModel.PreviousPage();
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Right)
        {
            viewModel.NextPage();
            e.Handled = true;
        }
    }

    private void DragHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pointer = e.GetCurrentPoint(DragHandle);
        if (!pointer.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _hasPlacedWindow = true;
        e.Handled = true;
        ReleaseCapture();
        SendMessage(_hWnd, WmNcLButtonDown, new IntPtr(HtCaption), IntPtr.Zero);
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out WindowRect lpRect);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int widthEllipse, int heightEllipse);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct WindowRect
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;
    }
}
