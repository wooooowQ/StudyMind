using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;

namespace StudyMind.App;

internal sealed class TrayIconController : IDisposable
{
    private const uint NotifyIconId = 41;
    private const uint CallbackMessage = 0x8000 + 41;
    private const uint NimAdd = 0x00000000;
    private const uint NimDelete = 0x00000002;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint WmRButtonUp = 0x0205;
    private const uint WmLButtonDblClk = 0x0203;
    private const uint MfString = 0x00000000;
    private const uint MfSeparator = 0x00000800;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCmd = 0x0100;
    private const uint TpmNonotify = 0x0080;
    private const int IdiApplication = 32512;

    private readonly IntPtr _hWnd;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly Action _showNote;
    private readonly Action _hideNote;
    private readonly Action _showMainWindow;
    private readonly Action _exitApp;
    private readonly SubclassProc _subclassProc;
    private bool _disposed;

    public TrayIconController(
        IntPtr hWnd,
        DispatcherQueue dispatcherQueue,
        Action showNote,
        Action hideNote,
        Action showMainWindow,
        Action exitApp)
    {
        _hWnd = hWnd;
        _dispatcherQueue = dispatcherQueue;
        _showNote = showNote;
        _hideNote = hideNote;
        _showMainWindow = showMainWindow;
        _exitApp = exitApp;
        _subclassProc = WindowSubclassProc;

        SetWindowSubclass(_hWnd, _subclassProc, new UIntPtr(1), IntPtr.Zero);
        AddIcon();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DeleteIcon();
        RemoveWindowSubclass(_hWnd, _subclassProc, new UIntPtr(1));
    }

    private void AddIcon()
    {
        var data = CreateNotifyIconData();
        data.uFlags = NifMessage | NifIcon | NifTip;
        data.uCallbackMessage = CallbackMessage;
        data.hIcon = LoadIcon(IntPtr.Zero, new IntPtr(IdiApplication));
        data.szTip = "StudyMind 便签";
        Shell_NotifyIcon(NimAdd, ref data);
    }

    private void DeleteIcon()
    {
        var data = CreateNotifyIconData();
        Shell_NotifyIcon(NimDelete, ref data);
    }

    private NotifyIconData CreateNotifyIconData() =>
        new()
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
            hWnd = _hWnd,
            uID = NotifyIconId,
            szTip = ""
        };

    private IntPtr WindowSubclassProc(
        IntPtr hWnd,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        UIntPtr subclassId,
        IntPtr refData)
    {
        if (message == CallbackMessage)
        {
            var mouseMessage = unchecked((uint)lParam.ToInt64());
            if (mouseMessage == WmLButtonDblClk)
            {
                Enqueue(_showNote);
                return IntPtr.Zero;
            }

            if (mouseMessage == WmRButtonUp)
            {
                ShowContextMenu();
                return IntPtr.Zero;
            }
        }

        return DefSubclassProc(hWnd, message, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero)
        {
            return;
        }

        try
        {
            AppendMenu(menu, MfString, 1, "显示便签");
            AppendMenu(menu, MfString, 2, "隐藏便签");
            AppendMenu(menu, MfSeparator, 0, null);
            AppendMenu(menu, MfString, 3, "显示主窗口");
            AppendMenu(menu, MfString, 4, "退出 StudyMind");

            GetCursorPos(out var point);
            SetForegroundWindow(_hWnd);
            var command = TrackPopupMenu(
                menu,
                TpmRightButton | TpmReturnCmd | TpmNonotify,
                point.X,
                point.Y,
                0,
                _hWnd,
                IntPtr.Zero);

            switch (command)
            {
                case 1:
                    Enqueue(_showNote);
                    break;
                case 2:
                    Enqueue(_hideNote);
                    break;
                case 3:
                    Enqueue(_showMainWindow);
                    break;
                case 4:
                    Enqueue(_exitApp);
                    break;
            }
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private void Enqueue(Action action)
    {
        _dispatcherQueue.TryEnqueue(() => action());
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    private delegate IntPtr SubclassProc(
        IntPtr hWnd,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        UIntPtr subclassId,
        IntPtr refData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NotifyIconData lpData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(
        IntPtr hWnd,
        SubclassProc pfnSubclass,
        UIntPtr uIdSubclass,
        IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(
        IntPtr hWnd,
        SubclassProc pfnSubclass,
        UIntPtr uIdSubclass);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(
        IntPtr hWnd,
        uint uMsg,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out Point lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint TrackPopupMenu(
        IntPtr hMenu,
        uint uFlags,
        int x,
        int y,
        int nReserved,
        IntPtr hWnd,
        IntPtr prcRect);
}
