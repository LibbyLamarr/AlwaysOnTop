using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("AlwaysOnTop Utility")]
[assembly: AssemblyDescription("System Tray utility to keep selected applications Always on Top")]
[assembly: AssemblyCompany("AlwaysOnTop")]
[assembly: AssemblyProduct("AlwaysOnTop Utility")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

namespace AlwaysOnTopUtility
{
    public class PinnedAppConfig
    {
        public string ProcessName { get; set; }
        public string ExecutablePath { get; set; }
        public string LastKnownTitle { get; set; }
        public string AddedAt { get; set; }
    }

    public class AppSettings
    {
        public List<PinnedAppConfig> PinnedApps { get; set; }
        public bool HotkeyEnabled { get; set; }
        public bool StartMinimized { get; set; }
        public string Theme { get; set; }

        public AppSettings()
        {
            PinnedApps = new List<PinnedAppConfig>();
            HotkeyEnabled = true;
            StartMinimized = false;
            Theme = "System";
        }
    }

    public class WindowEntry
    {
        public IntPtr Handle { get; set; }
        public uint ProcessId { get; set; }
        public string ProcessName { get; set; }
        public string ExecutablePath { get; set; }
        public string Title { get; set; }
        public bool IsTopMost { get; set; }
        public bool IsStandardAppWindow { get; set; }
    }

    public class ProcessEntry
    {
        public uint ProcessId { get; set; }
        public string ProcessName { get; set; }
        public string ExecutablePath { get; set; }
        public string WindowSummary { get; set; }
        public int VisibleWindowCount { get; set; }
    }

    internal static class NativeMethods
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        public delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        public static long GetWindowLong(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8)
                return GetWindowLongPtr64(hWnd, nIndex).ToInt64();
            return GetWindowLongPtr32(hWnd, nIndex).ToInt64();
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool GetTokenInformation(
            IntPtr TokenHandle,
            int TokenInformationClass,
            out int TokenInformation,
            int TokenInformationLength,
            out int ReturnLength);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ChangeWindowMessageFilter(uint message, uint dwFlag);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        // Cursor APIs for global Crosshair cursor during Window Picker Mode
        [DllImport("user32.dll")]
        public static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

        [DllImport("user32.dll")]
        public static extern IntPtr CopyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        public static extern bool SetSystemCursor(IntPtr hcur, uint id);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        public static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        [DllImport("uxtheme.dll", EntryPoint = "#135", SetLastError = true)]
        public static extern int SetPreferredAppMode(int preferredAppMode);

        [DllImport("uxtheme.dll", EntryPoint = "#133", SetLastError = true)]
        public static extern bool AllowDarkModeForWindow(IntPtr hWnd, bool allow);

        [DllImport("uxtheme.dll", EntryPoint = "#136", SetLastError = true)]
        public static extern void FlushMenuThemes();

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        public static readonly IntPtr HWND_TOP = new IntPtr(0);
        public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_FRAMECHANGED = 0x0020;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const int SW_RESTORE = 9;
        public const int LVM_GETHEADER = 0x101F;
        public const int WM_PAINT = 0x000F;
        public const int WM_ERASEBKGND = 0x0014;
        public const int WM_THEMECHANGED = 0x031A;

        public const int GWL_EXSTYLE = -20;
        public const long WS_EX_TOPMOST = 0x00000008L;
        public const long WS_EX_TRANSPARENT = 0x00000020L;
        public const long WS_EX_TOOLWINDOW = 0x00000080L;
        public const long WS_EX_APPWINDOW = 0x00040000L;
        public const long WS_EX_LAYERED = 0x00080000L;
        public const long WS_EX_NOACTIVATE = 0x08000000L;

        public const uint GW_OWNER = 4;
        public const uint GA_ROOT = 2;
        public const int DWMWA_CLOAKED = 14;
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        public const uint TOKEN_QUERY = 0x0008;
        public const int TokenElevation = 20;

        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_NOREPEAT = 0x4000;
        public const uint VK_T = 0x54;

        public const int WH_KEYBOARD_LL = 13;
        public const int WH_MOUSE_LL = 14;
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_MOUSEMOVE = 0x0200;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int VK_ESCAPE = 0x1B;

        public const int IDC_CROSS = 32515;
        public const uint SPI_SETCURSORS = 0x0057;
        public static readonly uint[] SystemCursorIds = new uint[]
        {
            32512, // OCR_NORMAL
            32513, // OCR_IBEAM
            32514, // OCR_WAIT
            32515, // OCR_CROSS
            32516, // OCR_UP
            32642, // OCR_SIZENWSE
            32643, // OCR_SIZENESW
            32644, // OCR_SIZEWE
            32645, // OCR_SIZENS
            32646, // OCR_SIZEALL
            32648, // OCR_NO
            32649, // OCR_HAND
            32650  // OCR_APPSTARTING
        };
    }

    /// <summary>
    /// Non-interactive floating HUD banner shown at the top of the screen while Window Picker Mode is active.
    /// Click-through (WS_EX_TRANSPARENT | WS_EX_NOACTIVATE) so it never blocks mouse clicks.
    /// </summary>
    internal class PickerHudForm : Form
    {
        private readonly Label _titleLabel;
        private readonly Label _targetLabel;

        public PickerHudForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true;
            this.Size = new Size(660, 62);
            this.BackColor = Color.FromArgb(15, 23, 42);

            Rectangle screen = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(
                screen.Left + (screen.Width - this.Width) / 2,
                screen.Top + 18);

            _titleLabel = new Label
            {
                Text = "🎯 WINDOW PICKER ACTIVE (+) — Click any open window to keep it Always on Top",
                ForeColor = Color.FromArgb(96, 165, 250),
                Font = new Font("Segoe UI Semibold", 9.75f, FontStyle.Bold),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 30
            };

            _targetLabel = new Label
            {
                Text = "Click on an empty desktop area (or press Esc / Right-Click) to cancel",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };

            this.Controls.Add(_targetLabel);
            this.Controls.Add(_titleLabel);
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= (int)(NativeMethods.WS_EX_TOPMOST |
                                    NativeMethods.WS_EX_TOOLWINDOW |
                                    NativeMethods.WS_EX_NOACTIVATE |
                                    NativeMethods.WS_EX_TRANSPARENT |
                                    NativeMethods.WS_EX_LAYERED);
                return cp;
            }
        }

        public void UpdateTargetText(string text, bool isCancelArea)
        {
            if (this.IsDisposed)
                return;
            _targetLabel.Text = text;
            _targetLabel.ForeColor = isCancelArea
                ? Color.FromArgb(251, 191, 36)
                : Color.FromArgb(134, 239, 172);
        }
    }

    internal class ThemedMenuColorTable : ProfessionalColorTable
    {
        private readonly bool _dark;

        public ThemedMenuColorTable(bool dark)
        {
            _dark = dark;
            this.UseSystemColors = false;
        }

        public override Color ToolStripDropDownBackground
        {
            get { return _dark ? Color.FromArgb(30, 41, 59) : Color.White; }
        }
        public override Color ImageMarginGradientBegin
        {
            get { return _dark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(248, 250, 252); }
        }
        public override Color ImageMarginGradientMiddle
        {
            get { return _dark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(248, 250, 252); }
        }
        public override Color ImageMarginGradientEnd
        {
            get { return _dark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(248, 250, 252); }
        }
        public override Color MenuBorder
        {
            get { return _dark ? Color.FromArgb(71, 85, 105) : Color.FromArgb(203, 213, 225); }
        }
        public override Color MenuItemBorder
        {
            get { return _dark ? Color.FromArgb(96, 165, 250) : Color.FromArgb(147, 197, 253); }
        }
        public override Color MenuItemSelected
        {
            get { return _dark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(239, 246, 255); }
        }
        public override Color MenuItemSelectedGradientBegin
        {
            get { return _dark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(239, 246, 255); }
        }
        public override Color MenuItemSelectedGradientEnd
        {
            get { return _dark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(239, 246, 255); }
        }
        public override Color SeparatorDark
        {
            get { return _dark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240); }
        }
        public override Color SeparatorLight
        {
            get { return _dark ? Color.FromArgb(30, 41, 59) : Color.White; }
        }
        public override Color CheckBackground
        {
            get { return _dark ? Color.FromArgb(37, 99, 235) : Color.FromArgb(219, 234, 254); }
        }
        public override Color CheckSelectedBackground
        {
            get { return _dark ? Color.FromArgb(59, 130, 246) : Color.FromArgb(191, 219, 254); }
        }
    }

    internal class ThemedListView : ListView
    {
        private bool _isDarkTheme = false;
        private HeaderNativeWindow _headerWindow;
        private Font _headerFont = new Font("Segoe UI Semibold", 8.75f, FontStyle.Bold);

        public bool IsDarkTheme
        {
            get { return _isDarkTheme; }
            set
            {
                _isDarkTheme = value;
                ApplyNativeScrollbarAndHeaderTheme();
                this.Invalidate(true);
            }
        }

        public ThemedListView()
        {
            this.DoubleBuffered = true;
            this.OwnerDraw = true;
            this.GridLines = false;
            this.FullRowSelect = true;
            this.HideSelection = false;
            this.View = View.Details;
            this.BorderStyle = BorderStyle.None;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            AttachHeaderSubsystem();
            ApplyNativeScrollbarAndHeaderTheme();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (_headerWindow != null)
            {
                _headerWindow.ReleaseHandle();
                _headerWindow = null;
            }
            base.OnHandleDestroyed(e);
        }

        private void AttachHeaderSubsystem()
        {
            if (!this.IsHandleCreated)
                return;
            IntPtr hHeader = NativeMethods.SendMessage(this.Handle, NativeMethods.LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
            if (hHeader != IntPtr.Zero)
            {
                if (_headerWindow != null)
                {
                    _headerWindow.ReleaseHandle();
                }
                _headerWindow = new HeaderNativeWindow(this, hHeader);
            }
        }

        public void ApplyNativeScrollbarAndHeaderTheme()
        {
            if (!this.IsHandleCreated)
                return;
            try
            {
                NativeMethods.SetPreferredAppMode(_isDarkTheme ? 2 : 3);
                NativeMethods.AllowDarkModeForWindow(this.Handle, _isDarkTheme);
                NativeMethods.SetWindowTheme(this.Handle, _isDarkTheme ? "DarkMode_Explorer" : "Explorer", null);

                IntPtr hHeader = NativeMethods.SendMessage(this.Handle, NativeMethods.LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
                if (hHeader != IntPtr.Zero)
                {
                    NativeMethods.AllowDarkModeForWindow(hHeader, _isDarkTheme);
                    NativeMethods.SetWindowTheme(hHeader, _isDarkTheme ? "DarkMode_ItemsView" : "Explorer", null);
                }

                NativeMethods.FlushMenuThemes();
                NativeMethods.SendMessage(this.Handle, NativeMethods.WM_THEMECHANGED, IntPtr.Zero, IntPtr.Zero);
                NativeMethods.SetWindowPos(
                    this.Handle,
                    IntPtr.Zero,
                    0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE |
                    NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE |
                    NativeMethods.SWP_FRAMECHANGED);
            }
            catch { }
        }

        protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
        {
            Color headerBack = _isDarkTheme ? Color.FromArgb(22, 33, 51) : Color.FromArgb(241, 245, 249);
            Color headerFore = _isDarkTheme ? Color.FromArgb(203, 213, 225) : Color.FromArgb(51, 65, 85);
            Color borderCol = _isDarkTheme ? Color.FromArgb(51, 65, 85) : Color.FromArgb(203, 213, 225);

            using (SolidBrush bgBrush = new SolidBrush(headerBack))
            {
                e.Graphics.FillRectangle(bgBrush, e.Bounds);
            }
            using (Pen borderPen = new Pen(borderCol))
            {
                e.Graphics.DrawLine(borderPen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                e.Graphics.DrawLine(borderPen, e.Bounds.Right - 1, e.Bounds.Top + 3, e.Bounds.Right - 1, e.Bounds.Bottom - 4);
            }

            Rectangle textRect = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 10), e.Bounds.Height);
            TextRenderer.DrawText(
                e.Graphics,
                e.Header.Text,
                _headerFont,
                textRect,
                headerFore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }

        protected override void OnDrawItem(DrawListViewItemEventArgs e)
        {
            // SubItems are drawn in OnDrawSubItem
        }

        protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
        {
            bool isSelected = e.Item.Selected;
            Color rowBack = isSelected
                ? (_isDarkTheme ? Color.FromArgb(30, 58, 138) : Color.FromArgb(219, 234, 254))
                : (_isDarkTheme ? Color.FromArgb(15, 23, 42) : Color.White);
            Color gridCol = _isDarkTheme ? Color.FromArgb(30, 41, 59) : Color.FromArgb(226, 232, 240);

            using (SolidBrush bgBrush = new SolidBrush(rowBack))
            {
                e.Graphics.FillRectangle(bgBrush, e.Bounds);
            }
            using (Pen gridPen = new Pen(gridCol))
            {
                e.Graphics.DrawLine(gridPen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                e.Graphics.DrawLine(gridPen, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom);
            }

            int textOffsetX = 6;
            if (e.ColumnIndex == 0 && this.SmallImageList != null &&
                e.Item.ImageIndex >= 0 && e.Item.ImageIndex < this.SmallImageList.Images.Count)
            {
                int iconSize = this.SmallImageList.ImageSize.Width;
                int iconY = e.Bounds.Y + Math.Max(0, (e.Bounds.Height - iconSize) / 2);
                this.SmallImageList.Draw(e.Graphics, e.Bounds.X + 5, iconY, e.Item.ImageIndex);
                textOffsetX = iconSize + 10;
            }

            Color textCol = isSelected
                ? (_isDarkTheme ? Color.White : Color.FromArgb(15, 23, 42))
                : e.Item.ForeColor;

            Rectangle textRect = new Rectangle(
                e.Bounds.X + textOffsetX,
                e.Bounds.Y,
                Math.Max(0, e.Bounds.Width - textOffsetX - 4),
                e.Bounds.Height);

            TextRenderer.DrawText(
                e.Graphics,
                e.SubItem.Text,
                this.Font,
                textRect,
                textCol,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }

        private class HeaderNativeWindow : NativeWindow
        {
            private readonly ThemedListView _owner;

            public HeaderNativeWindow(ThemedListView owner, IntPtr hHeader)
            {
                _owner = owner;
                this.AssignHandle(hHeader);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == NativeMethods.WM_ERASEBKGND)
                {
                    try
                    {
                        NativeMethods.RECT rc;
                        if (NativeMethods.GetWindowRect(this.Handle, out rc))
                        {
                            int w = Math.Max(1, rc.Right - rc.Left);
                            int h = Math.Max(1, rc.Bottom - rc.Top);
                            Color headerBack = _owner._isDarkTheme ? Color.FromArgb(22, 33, 51) : Color.FromArgb(241, 245, 249);
                            using (Graphics g = Graphics.FromHdc(m.WParam))
                            using (SolidBrush br = new SolidBrush(headerBack))
                            {
                                g.FillRectangle(br, 0, 0, w, h);
                            }
                            m.Result = (IntPtr)1;
                            return;
                        }
                    }
                    catch { }
                }

                base.WndProc(ref m);

                if (m.Msg == NativeMethods.WM_PAINT)
                {
                    try
                    {
                        NativeMethods.RECT rc;
                        if (NativeMethods.GetWindowRect(this.Handle, out rc))
                        {
                            int w = Math.Max(1, rc.Right - rc.Left);
                            int h = Math.Max(1, rc.Bottom - rc.Top);
                            int colsWidth = 0;
                            foreach (ColumnHeader col in _owner.Columns)
                            {
                                colsWidth += col.Width;
                            }
                            if (colsWidth < w)
                            {
                                Color headerBack = _owner._isDarkTheme ? Color.FromArgb(22, 33, 51) : Color.FromArgb(241, 245, 249);
                                Color borderCol = _owner._isDarkTheme ? Color.FromArgb(51, 65, 85) : Color.FromArgb(203, 213, 225);
                                using (Graphics g = Graphics.FromHwnd(this.Handle))
                                {
                                    using (SolidBrush br = new SolidBrush(headerBack))
                                    {
                                        g.FillRectangle(br, colsWidth, 0, w - colsWidth, h);
                                    }
                                    using (Pen pen = new Pen(borderCol))
                                    {
                                        g.DrawLine(pen, colsWidth, h - 1, w, h - 1);
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }
    }

    public class MainForm : Form
    {
        private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunRegistryValueName = "AlwaysOnTopUtility";
        private const int HotkeyId = 9001;

        public static readonly uint WM_SHOW_MANAGER = NativeMethods.RegisterWindowMessage("AlwaysOnTop_ShowManager_Message");
        public static readonly uint WM_QUIT_UTILITY = NativeMethods.RegisterWindowMessage("AlwaysOnTop_QuitUtility_Message");
        public const string ShowManagerEventName = @"Local\AlwaysOnTopUtility_ShowManager_Event";
        public const string QuitUtilityEventName = @"Local\AlwaysOnTopUtility_QuitUtility_Event";

        private readonly string _settingsFilePath;
        private EventWaitHandle _showManagerEvent;
        private EventWaitHandle _quitUtilityEvent;
        private readonly uint _currentProcessId;
        private readonly bool _launchedFromWindowsStartup;
        private readonly bool _isRunningAsAdmin;
        private bool _initialVisibilityHandled = false;
        private bool _isDarkThemeEffective = false;

        private AppSettings _settings = new AppSettings();
        private readonly HashSet<IntPtr> _pinnedHandles = new HashSet<IntPtr>();
        private readonly HashSet<IntPtr> _explicitPickedHandles = new HashSet<IntPtr>();
        private readonly Dictionary<string, int> _iconIndexCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private ToolStripMenuItem _trayPinnedCountItem;
        private ToolStripMenuItem _trayThemeMenuItem;
        private ToolStripMenuItem _themeSystemItem;
        private ToolStripMenuItem _themeLightItem;
        private ToolStripMenuItem _themeDarkItem;

        private System.Windows.Forms.Timer _enforceTimer;
        private ImageList _appImageList;
        private SplitContainer _mainVerticalSplit;
        private SplitContainer _topHorizontalSplit;

        // Panels & Themeable Labels
        private Panel _headerPanel;
        private Panel _footerPanel;
        private Panel _appPanel;
        private Panel _procPanel;
        private Panel _section2Panel;
        private Panel _appSearchBorderPanel;
        private Panel _procSearchBorderPanel;
        private Panel _runningListBorderPanel;
        private Panel _processListBorderPanel;
        private Panel _pinnedListBorderPanel;
        private Label _appTitle;
        private Label _procTitle;
        private Label _sec2Title;
        private Label _appFilterLbl;
        private Label _procFilterLbl;
        private Label _sec2Hint;

        // Section 1A Controls (Running Applications)
        private TextBox _appSearchBox;
        private ThemedListView _runningListView;
        private Button _btnPinSelectedApp;
        private Button _btnPickWindow;
        private Button _btnRefreshApps;
        private Label _lblRunningAppsCount;

        // Section 1B Controls (Running Windows Processes)
        private TextBox _procSearchBox;
        private ThemedListView _processListView;
        private Button _btnPinSelectedProcess;
        private Button _btnRefreshProcesses;
        private Label _lblProcessesCount;

        // Section 2 Controls (Pinned Applications)
        private ThemedListView _pinnedListView;
        private Button _btnUnpinSelected;
        private Button _btnUnpinAll;
        private Label _lblPinnedCount;

        // Footer Controls
        private CheckBox _chkStartWithWindows;
        private CheckBox _chkStartMinimized;
        private Button _btnHideToTray;
        private Button _btnQuitUtility;
        private Button _btnElevateAdmin;
        private Action _relayoutTopBars;

        // Window Picker State
        private bool _isPickingWindow = false;
        private bool _crosshairCursorApplied = false;
        private IntPtr _mouseHookHandle = IntPtr.Zero;
        private IntPtr _keyboardHookHandle = IntPtr.Zero;
        private NativeMethods.LowLevelProc _mouseHookDelegate;
        private NativeMethods.LowLevelProc _keyboardHookDelegate;
        private PickerHudForm _pickerHud;

        private bool _isExitingCompletely = false;
        private bool _suppressCheckboxEvents = false;
        private bool _splittersInitialized = false;
        private Icon _appIcon;

        public MainForm(bool launchedFromWindowsStartup)
        {
            _launchedFromWindowsStartup = launchedFromWindowsStartup;
            _currentProcessId = (uint)Process.GetCurrentProcess().Id;
            _isRunningAsAdmin = Program.IsCurrentProcessAdmin();
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _settingsFilePath = Path.Combine(baseDir, "AlwaysOnTop.settings.json");
            try
            {
                string oldPath = Path.Combine(baseDir, "AlwaysOnTop.old.exe");
                if (File.Exists(oldPath)) File.Delete(oldPath);
            }
            catch { }

            if (WM_SHOW_MANAGER != 0)
            {
                // Allow non-elevated secondary launches to signal this window even when running as Administrator
                try { NativeMethods.ChangeWindowMessageFilter(WM_SHOW_MANAGER, 1); } catch { }
            }
            if (WM_QUIT_UTILITY != 0)
            {
                try { NativeMethods.ChangeWindowMessageFilter(WM_QUIT_UTILITY, 1); } catch { }
            }

            AppDomain.CurrentDomain.ProcessExit += (s, e) => RestoreNormalSystemCursors();
            Application.ApplicationExit += (s, e) => RestoreNormalSystemCursors();
            SystemEvents.UserPreferenceChanged += OnSystemUserPreferenceChanged;

            LoadAppIcon();
            LoadSettings();
            InitializeComponent();
            InitializeTrayIcon();
            SyncSettingsCheckboxesUI();
            ApplyTheme();

            // Immediately enforce Always on Top on any remembered apps currently running
            EnforceTopmostAndRefresh(true, true);
            RefreshProcessListUI();

            try
            {
                _showManagerEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowManagerEventName);
                _quitUtilityEvent = new EventWaitHandle(false, EventResetMode.AutoReset, QuitUtilityEventName);
            }
            catch { }

            // Periodic timer (every 1000ms) to re-assert topmost and detect newly opened apps
            _enforceTimer = new System.Windows.Forms.Timer();
            _enforceTimer.Interval = 1000;
            _enforceTimer.Tick += (s, e) =>
            {
                if (_quitUtilityEvent != null && _quitUtilityEvent.WaitOne(0))
                {
                    ExitApplicationCompletely();
                    return;
                }
                if (_showManagerEvent != null && _showManagerEvent.WaitOne(0))
                {
                    ShowPopupFromTray();
                }
                EnforceTopmostAndRefresh(false, false);
            };
            _enforceTimer.Start();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterGlobalHotkey();
            ApplyWindowTitleBarDarkMode(_isDarkThemeEffective);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            InitializeSplitterPositions();
        }

        protected override void SetVisibleCore(bool value)
        {
            if (!_initialVisibilityHandled)
            {
                _initialVisibilityHandled = true;

                // Start minimized if "Start minimised to System Tray" is enabled or if launched by Windows Startup (--tray)
                bool startMinimized = _settings.StartMinimized || _launchedFromWindowsStartup;
                if (startMinimized)
                {
                    if (!IsHandleCreated)
                    {
                        CreateHandle();
                    }
                    base.SetVisibleCore(false);
                    return;
                }
            }

            base.SetVisibleCore(value);

            if (value && !_splittersInitialized)
            {
                InitializeSplitterPositions();
            }
        }

        private void InitializeSplitterPositions()
        {
            try
            {
                if (_mainVerticalSplit != null && _mainVerticalSplit.Height > 200)
                {
                    _mainVerticalSplit.SplitterDistance = (int)(_mainVerticalSplit.Height * 0.56);
                }
                if (_topHorizontalSplit != null && _topHorizontalSplit.Width > 300)
                {
                    _topHorizontalSplit.SplitterDistance = (int)(_topHorizontalSplit.Width * 0.50);
                }
                if (_relayoutTopBars != null)
                {
                    _relayoutTopBars();
                }
                _splittersInitialized = true;
            }
            catch { }
        }

        private void LoadAppIcon()
        {
            try
            {
                string exePath = Application.ExecutablePath;
                _appIcon = Icon.ExtractAssociatedIcon(exePath);
            }
            catch
            {
                _appIcon = SystemIcons.Application;
            }
            if (_appIcon == null)
                _appIcon = SystemIcons.Application;
        }

        private void RegisterGlobalHotkey()
        {
            try
            {
                NativeMethods.RegisterHotKey(
                    this.Handle,
                    HotkeyId,
                    NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT,
                    NativeMethods.VK_T);
            }
            catch { }
        }

        private void UnregisterGlobalHotkey()
        {
            try
            {
                if (IsHandleCreated)
                    NativeMethods.UnregisterHotKey(this.Handle, HotkeyId);
            }
            catch { }
        }

        private void InitializeComponent()
        {
            this.Text = _isRunningAsAdmin
                ? "AlwaysOnTop — Application & Process Pin Manager (Administrator)"
                : "AlwaysOnTop — Application & Process Pin Manager";
            this.Icon = _appIcon;

            Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
            int minW = Math.Min(1100, workArea.Width - 20);
            int minH = Math.Min(680, workArea.Height - 20);
            int initW = Math.Min(1260, workArea.Width - 40);
            int initH = Math.Min(780, workArea.Height - 40);

            this.MinimumSize = new Size(minW, minH);
            this.Size = new Size(Math.Max(minW, initW), Math.Max(minH, initH));
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(241, 245, 249);
            this.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;

            _appImageList = new ImageList
            {
                ImageSize = new Size(20, 20),
                ColorDepth = ColorDepth.Depth32Bit
            };
            _appImageList.Images.Add("default", _appIcon);

            // =========================================================================
            // TOP HEADER BANNER (with Pick Window on Screen Button)
            // =========================================================================
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 66,
                BackColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(16, 10, 16, 10)
            };

            Label titleLabel = new Label
            {
                Text = "AlwaysOnTop Manager",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(16, 10)
            };

            Label subtitleLabel = new Label
            {
                Text = "Select by Running App, Windows Process, or Window Picker  •  Global Shortcut: Ctrl + Alt + T",
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 8.75f, FontStyle.Regular),
                AutoSize = true,
                Location = new Point(18, 36)
            };

            _btnPickWindow = new Button
            {
                Text = "🎯 Pick Window on Screen",
                Size = new Size(225, 38),
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnPickWindow.FlatAppearance.BorderSize = 0;
            _btnPickWindow.Click += (s, e) => StartWindowPickerMode();

            if (!_isRunningAsAdmin)
            {
                _btnElevateAdmin = new Button
                {
                    Text = "🛡️ Run as Administrator",
                    Size = new Size(195, 38),
                    BackColor = Color.FromArgb(59, 130, 246),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                _btnElevateAdmin.FlatAppearance.BorderSize = 0;
                _btnElevateAdmin.Click += (s, e) => RestartAsAdministrator();
                _headerPanel.Controls.Add(_btnElevateAdmin);
            }

            Action layoutHeader = () =>
            {
                int w = Math.Max(600, _headerPanel.ClientSize.Width);
                _btnPickWindow.Width = Math.Max(225, _btnPickWindow.PreferredSize.Width + 16);
                _btnPickWindow.Location = new Point(w - _btnPickWindow.Width - 16, 14);
                if (_btnElevateAdmin != null)
                {
                    _btnElevateAdmin.Width = Math.Max(195, _btnElevateAdmin.PreferredSize.Width + 16);
                    _btnElevateAdmin.Location = new Point(_btnPickWindow.Left - _btnElevateAdmin.Width - 10, 14);
                }
            };
            _headerPanel.Resize += (s, e) => layoutHeader();

            _headerPanel.Controls.Add(titleLabel);
            _headerPanel.Controls.Add(subtitleLabel);
            _headerPanel.Controls.Add(_btnPickWindow);
            layoutHeader();

            // =========================================================================
            // BOTTOM FOOTER BAR (Single Horizontal Row: Checkboxes + Minimize / Quit)
            // =========================================================================
            _footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = Color.FromArgb(226, 232, 240),
                Padding = new Padding(16, 6, 16, 6)
            };

            _chkStartWithWindows = new CheckBox
            {
                Text = "Start automatically on Windows start",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Cursor = Cursors.Hand
            };
            _chkStartWithWindows.CheckedChanged += OnStartupWithWindowsChanged;

            _chkStartMinimized = new CheckBox
            {
                Text = "Start minimised to System Tray",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Cursor = Cursors.Hand
            };
            _chkStartMinimized.CheckedChanged += OnStartMinimizedChanged;

            _btnHideToTray = new Button
            {
                Text = "Minimize to Tray",
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30, 41, 59),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            _btnHideToTray.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnHideToTray.Click += (s, e) => HidePopupToTray();

            _btnQuitUtility = new Button
            {
                Text = "Quit Utility",
                BackColor = Color.FromArgb(220, 38, 38),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnQuitUtility.FlatAppearance.BorderSize = 0;
            _btnQuitUtility.Click += (s, e) => ExitApplicationCompletely();

            Action layoutFooter = () =>
            {
                int w = Math.Max(600, _footerPanel.ClientSize.Width);
                int h = Math.Max(44, _footerPanel.ClientSize.Height);

                // Measure both buttons' text and size both buttons identically based on the longest text
                // with a slight border where left/right border equals top/bottom border around the text
                Size szHide = TextRenderer.MeasureText(
                    _btnHideToTray.Text,
                    _btnHideToTray.Font,
                    Size.Empty,
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                Size szQuit = TextRenderer.MeasureText(
                    _btnQuitUtility.Text,
                    _btnQuitUtility.Font,
                    Size.Empty,
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

                int maxTextW = Math.Max(szHide.Width, szQuit.Width);
                int maxTextH = Math.Max(szHide.Height, szQuit.Height);
                int borderPad = 7; // equal slight border on top, bottom, left, and right of the longest text
                int uniformBtnW = maxTextW + (borderPad * 2);
                int uniformBtnH = maxTextH + (borderPad * 2);

                _btnHideToTray.Size = new Size(uniformBtnW, uniformBtnH);
                _btnQuitUtility.Size = new Size(uniformBtnW, uniformBtnH);

                int btnY = (h - uniformBtnH) / 2;
                _btnQuitUtility.Location = new Point(w - _btnQuitUtility.Width - 16, btnY);
                _btnHideToTray.Location = new Point(_btnQuitUtility.Left - _btnHideToTray.Width - 10, btnY);

                int chkH = Math.Max(20, _chkStartWithWindows.PreferredSize.Height);
                int chkY = (h - chkH) / 2;
                _chkStartWithWindows.Location = new Point(16, chkY);
                _chkStartMinimized.Location = new Point(_chkStartWithWindows.Left + _chkStartWithWindows.PreferredSize.Width + 24, chkY);
            };
            _footerPanel.Resize += (s, e) => layoutFooter();

            _footerPanel.Controls.Add(_chkStartWithWindows);
            _footerPanel.Controls.Add(_chkStartMinimized);
            _footerPanel.Controls.Add(_btnHideToTray);
            _footerPanel.Controls.Add(_btnQuitUtility);
            layoutFooter();

            // =========================================================================
            // MAIN SPLIT CONTAINER (Top: Apps + Processes side-by-side; Bottom: Pinned)
            // =========================================================================
            _mainVerticalSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 8,
                BackColor = Color.FromArgb(203, 213, 225),
                Padding = new Padding(12, 10, 12, 10)
            };

            _topHorizontalSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 8,
                BackColor = Color.FromArgb(203, 213, 225)
            };

            // -------------------------------------------------------------------------
            // TOP-LEFT PANEL: 1. Running Applications (Visible Windows)
            // -------------------------------------------------------------------------
            _appPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(10)
            };

            Panel appTopBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36
            };

            _appTitle = new Label
            {
                Text = "1. Running Applications",
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                AutoSize = true,
                Location = new Point(0, 6)
            };

            _lblRunningAppsCount = new Label
            {
                Text = "(0)",
                Font = new Font("Segoe UI", 8.75f, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                Location = new Point(180, 8)
            };

            _appFilterLbl = new Label
            {
                Text = "Filter:",
                Font = new Font("Segoe UI", 8.75f, FontStyle.Regular),
                ForeColor = Color.FromArgb(71, 85, 105),
                AutoSize = true
            };

            _appSearchBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.75f, FontStyle.Regular)
            };
            _appSearchBox.TextChanged += (s, e) => RefreshListsUI(null);

            Panel appSearchInner = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(5, 3, 5, 2)
            };
            appSearchInner.Controls.Add(_appSearchBox);

            _appSearchBorderPanel = new Panel
            {
                Size = new Size(144, 24),
                Padding = new Padding(1),
                BackColor = Color.FromArgb(203, 213, 225)
            };
            _appSearchBorderPanel.Controls.Add(appSearchInner);

            Action layoutAppTop = () =>
            {
                _lblRunningAppsCount.Location = new Point(_appTitle.Left + _appTitle.PreferredWidth + 6, 8);
                int w = Math.Max(420, appTopBar.ClientSize.Width);
                _appSearchBorderPanel.Location = new Point(w - _appSearchBorderPanel.Width - 2, 4);
                _appFilterLbl.Location = new Point(_appSearchBorderPanel.Left - _appFilterLbl.PreferredWidth - 6, 8);
            };
            appTopBar.Resize += (s, e) => layoutAppTop();

            appTopBar.Controls.Add(_appTitle);
            appTopBar.Controls.Add(_lblRunningAppsCount);
            appTopBar.Controls.Add(_appFilterLbl);
            appTopBar.Controls.Add(_appSearchBorderPanel);
            layoutAppTop();

            Panel appBottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                Padding = new Padding(0, 6, 0, 0)
            };

            _btnPinSelectedApp = new Button
            {
                Text = "📌 Pin Selected App",
                Size = new Size(180, 32),
                Location = new Point(0, 6),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnPinSelectedApp.FlatAppearance.BorderSize = 0;
            _btnPinSelectedApp.Click += (s, e) => PinSelectedRunningApps();

            _btnRefreshApps = new Button
            {
                Text = "↻ Refresh",
                Size = new Size(110, 32),
                Location = new Point(190, 6),
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(51, 65, 85),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.75f, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            _btnRefreshApps.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnRefreshApps.Click += (s, e) => EnforceTopmostAndRefresh(true, false);

            appBottomBar.Controls.Add(_btnPinSelectedApp);
            appBottomBar.Controls.Add(_btnRefreshApps);

            _runningListView = CreateStyledListView(out _runningListBorderPanel);
            _runningListView.Columns.Add("Application", 150);
            _runningListView.Columns.Add("Window Title", 330);
            _runningListView.Columns.Add("PID", 70);
            _runningListView.DoubleClick += (s, e) => PinSelectedRunningApps();

            _appPanel.Controls.Add(_runningListBorderPanel);
            _appPanel.Controls.Add(appBottomBar);
            _appPanel.Controls.Add(appTopBar);

            // -------------------------------------------------------------------------
            // TOP-RIGHT PANEL: Running Windows Processes (All Processes)
            // -------------------------------------------------------------------------
            _procPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(10)
            };

            Panel procTopBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36
            };

            _procTitle = new Label
            {
                Text = "Running Windows Processes",
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                AutoSize = true,
                Location = new Point(0, 6)
            };

            _lblProcessesCount = new Label
            {
                Text = "(0)",
                Font = new Font("Segoe UI", 8.75f, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                Location = new Point(205, 8)
            };

            _procFilterLbl = new Label
            {
                Text = "Filter:",
                Font = new Font("Segoe UI", 8.75f, FontStyle.Regular),
                ForeColor = Color.FromArgb(71, 85, 105),
                AutoSize = true
            };

            _procSearchBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.75f, FontStyle.Regular)
            };
            _procSearchBox.TextChanged += (s, e) => RefreshProcessListUI();

            Panel procSearchInner = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(5, 3, 5, 2)
            };
            procSearchInner.Controls.Add(_procSearchBox);

            _procSearchBorderPanel = new Panel
            {
                Size = new Size(144, 24),
                Padding = new Padding(1),
                BackColor = Color.FromArgb(203, 213, 225)
            };
            _procSearchBorderPanel.Controls.Add(procSearchInner);

            Action layoutProcTop = () =>
            {
                _lblProcessesCount.Location = new Point(_procTitle.Left + _procTitle.PreferredWidth + 6, 8);
                int w = Math.Max(420, procTopBar.ClientSize.Width);
                _procSearchBorderPanel.Location = new Point(w - _procSearchBorderPanel.Width - 2, 4);
                _procFilterLbl.Location = new Point(_procSearchBorderPanel.Left - _procFilterLbl.PreferredWidth - 6, 8);
            };
            procTopBar.Resize += (s, e) => layoutProcTop();

            procTopBar.Controls.Add(_procTitle);
            procTopBar.Controls.Add(_lblProcessesCount);
            procTopBar.Controls.Add(_procFilterLbl);
            procTopBar.Controls.Add(_procSearchBorderPanel);
            layoutProcTop();

            Panel procBottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                Padding = new Padding(0, 6, 0, 0)
            };

            _btnPinSelectedProcess = new Button
            {
                Text = "📌 Pin Selected Process",
                Size = new Size(190, 32),
                Location = new Point(0, 6),
                BackColor = Color.FromArgb(79, 70, 229),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnPinSelectedProcess.FlatAppearance.BorderSize = 0;
            _btnPinSelectedProcess.Click += (s, e) => PinSelectedProcesses();

            _btnRefreshProcesses = new Button
            {
                Text = "↻ Refresh Processes",
                Size = new Size(150, 32),
                Location = new Point(200, 6),
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(51, 65, 85),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.75f, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            _btnRefreshProcesses.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnRefreshProcesses.Click += (s, e) => RefreshProcessListUI();

            procBottomBar.Controls.Add(_btnPinSelectedProcess);
            procBottomBar.Controls.Add(_btnRefreshProcesses);

            _processListView = CreateStyledListView(out _processListBorderPanel);
            _processListView.Columns.Add("Process Name", 170);
            _processListView.Columns.Add("PID", 70);
            _processListView.Columns.Add("Window / Process Status", 310);
            _processListView.DoubleClick += (s, e) => PinSelectedProcesses();

            _procPanel.Controls.Add(_processListBorderPanel);
            _procPanel.Controls.Add(procBottomBar);
            _procPanel.Controls.Add(procTopBar);

            _topHorizontalSplit.Panel1.Controls.Add(_appPanel);
            _topHorizontalSplit.Panel2.Controls.Add(_procPanel);

            // -------------------------------------------------------------------------
            // BOTTOM PANEL: 2. Always on Top Applications (Pinned & Remembered)
            // -------------------------------------------------------------------------
            _section2Panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(10)
            };

            Panel sec2TopBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32
            };

            _sec2Title = new Label
            {
                Text = "2. Always on Top Applications (Pinned & Remembered)",
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(22, 101, 52),
                AutoSize = true,
                Location = new Point(0, 4)
            };

            _lblPinnedCount = new Label
            {
                Text = "(0 pinned)",
                Font = new Font("Segoe UI", 8.75f, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                Location = new Point(365, 6)
            };

            Action layoutSec2Top = () =>
            {
                _lblPinnedCount.Location = new Point(_sec2Title.Left + _sec2Title.PreferredWidth + 8, 6);
            };
            sec2TopBar.Resize += (s, e) => layoutSec2Top();

            sec2TopBar.Controls.Add(_sec2Title);
            sec2TopBar.Controls.Add(_lblPinnedCount);
            layoutSec2Top();

            Panel sec2BottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                Padding = new Padding(0, 6, 0, 0)
            };

            _btnUnpinSelected = new Button
            {
                Text = "✕ Remove Selected from Always on Top",
                Size = new Size(275, 32),
                Location = new Point(0, 6),
                BackColor = Color.FromArgb(245, 158, 11),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnUnpinSelected.FlatAppearance.BorderSize = 0;
            _btnUnpinSelected.Click += (s, e) => UnpinSelectedPinnedApps();

            _btnUnpinAll = new Button
            {
                Text = "Remove All from Always on Top",
                Size = new Size(215, 32),
                Location = new Point(285, 6),
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(51, 65, 85),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.75f, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            _btnUnpinAll.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnUnpinAll.Click += (s, e) => UnpinAllPinnedApps();

            _sec2Hint = new Label
            {
                Text = "Tip: Double-click any pinned item to remove it from Always on Top.",
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                Location = new Point(512, 12)
            };

            sec2BottomBar.Controls.Add(_btnUnpinSelected);
            sec2BottomBar.Controls.Add(_btnUnpinAll);
            sec2BottomBar.Controls.Add(_sec2Hint);

            _pinnedListView = CreateStyledListView(out _pinnedListBorderPanel);
            _pinnedListView.Columns.Add("Application / Process", 230);
            _pinnedListView.Columns.Add("Current Window / Remembered Title", 550);
            _pinnedListView.Columns.Add("Status", 250);
            _pinnedListView.DoubleClick += (s, e) => UnpinSelectedPinnedApps();

            _section2Panel.Controls.Add(_pinnedListBorderPanel);
            _section2Panel.Controls.Add(sec2BottomBar);
            _section2Panel.Controls.Add(sec2TopBar);

            _mainVerticalSplit.Panel1.Controls.Add(_topHorizontalSplit);
            _mainVerticalSplit.Panel2.Controls.Add(_section2Panel);

            this.Controls.Add(_mainVerticalSplit);
            this.Controls.Add(_footerPanel);
            this.Controls.Add(_headerPanel);

            _relayoutTopBars = () =>
            {
                layoutHeader();
                layoutFooter();
                layoutAppTop();
                layoutProcTop();
                layoutSec2Top();
            };

            this.FormClosing += OnMainFormClosing;
        }

        private ThemedListView CreateStyledListView(out Panel borderPanel)
        {
            ThemedListView lv = new ThemedListView
            {
                Dock = DockStyle.Fill,
                MultiSelect = true,
                SmallImageList = _appImageList,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30, 41, 59),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            borderPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(1),
                BackColor = Color.FromArgb(203, 213, 225)
            };
            borderPanel.Controls.Add(lv);

            return lv;
        }

        // =====================================================================
        // System Tray Context Menu & Theme Support
        // =====================================================================

        private void InitializeTrayIcon()
        {
            _trayMenu = new ContextMenuStrip
            {
                Font = new Font("Segoe UI", 9.25f, FontStyle.Regular)
            };

            ToolStripMenuItem openItem = new ToolStripMenuItem("Open AlwaysOnTop Manager...")
            {
                Font = new Font("Segoe UI Semibold", 9.25f, FontStyle.Bold)
            };
            openItem.Click += (s, e) => ShowPopupFromTray();

            _trayPinnedCountItem = new ToolStripMenuItem("Pinned Apps: 0")
            {
                Enabled = false
            };

            // Theme submenu: System, Light, Dark
            _trayThemeMenuItem = new ToolStripMenuItem("Theme");
            _themeSystemItem = new ToolStripMenuItem("System");
            _themeSystemItem.Click += (s, e) => SetThemeMode("System");

            _themeLightItem = new ToolStripMenuItem("Light");
            _themeLightItem.Click += (s, e) => SetThemeMode("Light");

            _themeDarkItem = new ToolStripMenuItem("Dark");
            _themeDarkItem.Click += (s, e) => SetThemeMode("Dark");

            _trayThemeMenuItem.DropDownItems.Add(_themeSystemItem);
            _trayThemeMenuItem.DropDownItems.Add(_themeLightItem);
            _trayThemeMenuItem.DropDownItems.Add(_themeDarkItem);

            // Quit Utility uses the same text colour as the other context menu items
            ToolStripMenuItem quitItem = new ToolStripMenuItem("Quit Utility");
            quitItem.Click += (s, e) => ExitApplicationCompletely();

            _trayMenu.Items.Add(openItem);
            _trayMenu.Items.Add(_trayPinnedCountItem);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(_trayThemeMenuItem);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(quitItem);

            _trayIcon = new NotifyIcon
            {
                Icon = _appIcon,
                Text = "AlwaysOnTop Utility — Click to open manager (Ctrl+Alt+T)",
                ContextMenuStrip = _trayMenu,
                Visible = true
            };

            _trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ShowPopupFromTray();
                }
            };
            _trayIcon.DoubleClick += (s, e) => ShowPopupFromTray();
            _trayIcon.BalloonTipClicked += (s, e) => ShowPopupFromTray();
        }

        private void OnSystemUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (string.Equals(_settings.Theme, "System", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(_settings.Theme))
            {
                if (this.IsHandleCreated)
                {
                    this.BeginInvoke(new Action(() => ApplyTheme()));
                }
            }
        }

        private void SetThemeMode(string themeMode)
        {
            _settings.Theme = themeMode;
            SaveSettings();
            ApplyTheme();
        }

        private static bool IsWindowsSystemDarkMode()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("AppsUseLightTheme");
                        if (val != null)
                        {
                            int lightVal = Convert.ToInt32(val);
                            return lightVal == 0;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        private void ApplyWindowTitleBarDarkMode(bool isDark)
        {
            if (!this.IsHandleCreated)
                return;
            try
            {
                int flag = isDark ? 1 : 0;
                NativeMethods.DwmSetWindowAttribute(this.Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref flag, sizeof(int));
                NativeMethods.DwmSetWindowAttribute(this.Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref flag, sizeof(int));
            }
            catch { }
        }

        private void ApplyTheme()
        {
            string mode = _settings.Theme;
            if (string.IsNullOrEmpty(mode))
                mode = "System";

            bool isSystem = string.Equals(mode, "System", StringComparison.OrdinalIgnoreCase);
            bool isDark = string.Equals(mode, "Dark", StringComparison.OrdinalIgnoreCase) ||
                          (isSystem && IsWindowsSystemDarkMode());
            bool isLight = !isSystem && !isDark;

            _isDarkThemeEffective = isDark;

            if (_themeSystemItem != null) _themeSystemItem.Checked = isSystem;
            if (_themeLightItem != null) _themeLightItem.Checked = isLight;
            if (_themeDarkItem != null) _themeDarkItem.Checked = string.Equals(mode, "Dark", StringComparison.OrdinalIgnoreCase);

            ApplyWindowTitleBarDarkMode(isDark);

            // Palette definitions for Light and Dark themes
            Color formBack = isDark ? Color.FromArgb(15, 23, 42) : Color.FromArgb(241, 245, 249);
            Color headerBack = isDark ? Color.FromArgb(9, 14, 26) : Color.FromArgb(30, 41, 59);
            Color footerBack = isDark ? Color.FromArgb(15, 23, 42) : Color.FromArgb(226, 232, 240);
            Color splitBack = isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(203, 213, 225);
            Color cardBack = isDark ? Color.FromArgb(30, 41, 59) : Color.White;
            Color listBack = isDark ? Color.FromArgb(15, 23, 42) : Color.White;
            Color inputBack = isDark ? Color.FromArgb(15, 23, 42) : Color.White;

            Color primaryText = isDark ? Color.FromArgb(241, 245, 249) : Color.FromArgb(15, 23, 42);
            Color secondaryText = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);
            Color filterText = isDark ? Color.FromArgb(203, 213, 225) : Color.FromArgb(71, 85, 105);
            Color sec2TitleColor = isDark ? Color.FromArgb(74, 222, 128) : Color.FromArgb(22, 101, 52);

            Color secBtnBack = isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(241, 245, 249);
            Color secBtnText = isDark ? Color.FromArgb(226, 232, 240) : Color.FromArgb(51, 65, 85);
            Color secBtnBorder = isDark ? Color.FromArgb(71, 85, 105) : Color.FromArgb(203, 213, 225);

            Color hideBtnBack = isDark ? Color.FromArgb(30, 41, 59) : Color.White;
            Color hideBtnText = isDark ? Color.FromArgb(226, 232, 240) : Color.FromArgb(30, 41, 59);

            this.BackColor = formBack;
            if (_headerPanel != null) _headerPanel.BackColor = headerBack;
            if (_footerPanel != null) _footerPanel.BackColor = footerBack;
            if (_mainVerticalSplit != null) _mainVerticalSplit.BackColor = splitBack;
            if (_topHorizontalSplit != null) _topHorizontalSplit.BackColor = splitBack;

            if (_appPanel != null) _appPanel.BackColor = cardBack;
            if (_procPanel != null) _procPanel.BackColor = cardBack;
            if (_section2Panel != null) _section2Panel.BackColor = cardBack;

            if (_appTitle != null) _appTitle.ForeColor = primaryText;
            if (_procTitle != null) _procTitle.ForeColor = primaryText;
            if (_sec2Title != null) _sec2Title.ForeColor = sec2TitleColor;

            if (_lblRunningAppsCount != null) _lblRunningAppsCount.ForeColor = secondaryText;
            if (_lblProcessesCount != null) _lblProcessesCount.ForeColor = secondaryText;
            if (_lblPinnedCount != null) _lblPinnedCount.ForeColor = secondaryText;
            if (_sec2Hint != null) _sec2Hint.ForeColor = secondaryText;

            if (_appFilterLbl != null) _appFilterLbl.ForeColor = filterText;
            if (_procFilterLbl != null) _procFilterLbl.ForeColor = filterText;

            if (_appSearchBox != null)
            {
                _appSearchBox.BackColor = inputBack;
                _appSearchBox.ForeColor = primaryText;
                if (_appSearchBox.Parent != null)
                    _appSearchBox.Parent.BackColor = inputBack;
            }
            if (_procSearchBox != null)
            {
                _procSearchBox.BackColor = inputBack;
                _procSearchBox.ForeColor = primaryText;
                if (_procSearchBox.Parent != null)
                    _procSearchBox.Parent.BackColor = inputBack;
            }
            if (_appSearchBorderPanel != null) _appSearchBorderPanel.BackColor = secBtnBorder;
            if (_procSearchBorderPanel != null) _procSearchBorderPanel.BackColor = secBtnBorder;

            Color listBorderCol = isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(203, 213, 225);
            if (_runningListBorderPanel != null) _runningListBorderPanel.BackColor = listBorderCol;
            if (_processListBorderPanel != null) _processListBorderPanel.BackColor = listBorderCol;
            if (_pinnedListBorderPanel != null) _pinnedListBorderPanel.BackColor = listBorderCol;

            if (_runningListView != null)
            {
                _runningListView.BackColor = listBack;
                _runningListView.ForeColor = primaryText;
                _runningListView.IsDarkTheme = isDark;
            }
            if (_processListView != null)
            {
                _processListView.BackColor = listBack;
                _processListView.ForeColor = primaryText;
                _processListView.IsDarkTheme = isDark;
            }
            if (_pinnedListView != null)
            {
                _pinnedListView.BackColor = listBack;
                _pinnedListView.ForeColor = primaryText;
                _pinnedListView.IsDarkTheme = isDark;
            }

            if (_btnRefreshApps != null)
            {
                _btnRefreshApps.BackColor = secBtnBack;
                _btnRefreshApps.ForeColor = secBtnText;
                _btnRefreshApps.FlatAppearance.BorderColor = secBtnBorder;
            }
            if (_btnRefreshProcesses != null)
            {
                _btnRefreshProcesses.BackColor = secBtnBack;
                _btnRefreshProcesses.ForeColor = secBtnText;
                _btnRefreshProcesses.FlatAppearance.BorderColor = secBtnBorder;
            }
            if (_btnUnpinAll != null)
            {
                _btnUnpinAll.BackColor = secBtnBack;
                _btnUnpinAll.ForeColor = secBtnText;
                _btnUnpinAll.FlatAppearance.BorderColor = secBtnBorder;
            }
            if (_btnHideToTray != null)
            {
                _btnHideToTray.BackColor = hideBtnBack;
                _btnHideToTray.ForeColor = hideBtnText;
                _btnHideToTray.FlatAppearance.BorderColor = secBtnBorder;
            }

            if (_chkStartWithWindows != null) _chkStartWithWindows.ForeColor = primaryText;
            if (_chkStartMinimized != null) _chkStartMinimized.ForeColor = primaryText;

            // Theme the System Tray ContextMenuStrip & submenu items (all items share the same text colour)
            if (_trayMenu != null)
            {
                Color menuBack = isDark ? Color.FromArgb(30, 41, 59) : Color.White;
                Color menuFore = isDark ? Color.FromArgb(241, 245, 249) : Color.FromArgb(30, 41, 59);
                _trayMenu.Renderer = new ToolStripProfessionalRenderer(new ThemedMenuColorTable(isDark));
                _trayMenu.BackColor = menuBack;
                _trayMenu.ForeColor = menuFore;

                foreach (ToolStripItem item in _trayMenu.Items)
                {
                    item.ForeColor = menuFore;
                    ToolStripMenuItem menuItem = item as ToolStripMenuItem;
                    if (menuItem != null && menuItem.HasDropDownItems)
                    {
                        menuItem.DropDown.BackColor = menuBack;
                        menuItem.DropDown.ForeColor = menuFore;
                        foreach (ToolStripItem subItem in menuItem.DropDownItems)
                        {
                            subItem.ForeColor = menuFore;
                        }
                    }
                }
            }

            RefreshListsUI(null);
            RefreshProcessListUI();
        }

        public void ShowPopupFromTray()
        {
            SyncSettingsCheckboxesUI();
            EnforceTopmostAndRefresh(true, false);
            RefreshProcessListUI();

            if (!this.Visible)
            {
                this.Show();
                if (!_splittersInitialized)
                {
                    InitializeSplitterPositions();
                }
            }
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.WindowState = FormWindowState.Normal;
            }
            this.Activate();
            this.BringToFront();
        }

        private void HidePopupToTray()
        {
            this.Hide();
        }

        private void OnMainFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_isExitingCompletely && e.CloseReason == CloseReason.UserClosing)
            {
                // Keep running in the System Tray when the popup window is closed
                e.Cancel = true;
                HidePopupToTray();
            }
        }

        private void ExitApplicationCompletely()
        {
            StopWindowPickerMode(false);
            RestoreNormalSystemCursors();
            SystemEvents.UserPreferenceChanged -= OnSystemUserPreferenceChanged;

            _isExitingCompletely = true;
            if (_enforceTimer != null)
            {
                _enforceTimer.Stop();
            }
            UnregisterGlobalHotkey();

            // Unpin all currently pinned windows before exiting so nothing stays stuck on top
            UnpinAllActiveHandlesOnly();

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            Application.Exit();
        }

        private void RestartAsAdministrator()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    Arguments = "--relaunch-admin",
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);
                ExitApplicationCompletely();
            }
            catch
            {
                // User cancelled UAC prompt
            }
        }

        // =====================================================================
        // Window Picker Tool (Global Crosshair Cursor + Mouse / Keyboard Hook)
        // =====================================================================

        private void ApplyCrosshairSystemCursors()
        {
            try
            {
                IntPtr hCross = NativeMethods.LoadCursor(IntPtr.Zero, NativeMethods.IDC_CROSS);
                if (hCross != IntPtr.Zero)
                {
                    foreach (uint id in NativeMethods.SystemCursorIds)
                    {
                        IntPtr copy = NativeMethods.CopyIcon(hCross);
                        if (copy != IntPtr.Zero)
                        {
                            NativeMethods.SetSystemCursor(copy, id);
                        }
                    }
                    _crosshairCursorApplied = true;
                }
            }
            catch { }
        }

        private void RestoreNormalSystemCursors()
        {
            if (!_crosshairCursorApplied)
                return;
            try
            {
                NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETCURSORS, 0, IntPtr.Zero, 0);
                _crosshairCursorApplied = false;
            }
            catch { }
        }

        private void StartWindowPickerMode()
        {
            if (_isPickingWindow)
                return;

            _isPickingWindow = true;

            // Hide main window temporarily so any window behind it can be clicked
            this.Hide();

            _pickerHud = new PickerHudForm();
            _pickerHud.Show();

            // Change mouse pointer globally to a Crosshair whilst picking a window
            ApplyCrosshairSystemCursors();

            _mouseHookDelegate = MouseHookCallback;
            _keyboardHookDelegate = KeyboardHookCallback;

            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                IntPtr hMod = NativeMethods.GetModuleHandle(curModule.ModuleName);
                _mouseHookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseHookDelegate, hMod, 0);
                _keyboardHookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyboardHookDelegate, hMod, 0);
            }
        }

        private void StopWindowPickerMode(bool restoreMainForm)
        {
            if (!_isPickingWindow)
            {
                RestoreNormalSystemCursors();
                return;
            }

            _isPickingWindow = false;

            if (_mouseHookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_mouseHookHandle);
                _mouseHookHandle = IntPtr.Zero;
            }
            if (_keyboardHookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_keyboardHookHandle);
                _keyboardHookHandle = IntPtr.Zero;
            }

            RestoreNormalSystemCursors();

            if (_pickerHud != null)
            {
                try
                {
                    _pickerHud.Close();
                    _pickerHud.Dispose();
                }
                catch { }
                _pickerHud = null;
            }

            if (restoreMainForm && !_isExitingCompletely)
            {
                ShowPopupFromTray();
            }
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _isPickingWindow && wParam == (IntPtr)NativeMethods.WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode == NativeMethods.VK_ESCAPE)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        StopWindowPickerMode(true);
                    }));
                    return (IntPtr)1;
                }
            }
            return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _isPickingWindow)
            {
                int msg = wParam.ToInt32();
                NativeMethods.MSLLHOOKSTRUCT hookStruct =
                    (NativeMethods.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(NativeMethods.MSLLHOOKSTRUCT));

                if (msg == NativeMethods.WM_MOUSEMOVE)
                {
                    NativeMethods.POINT pt = hookStruct.pt;
                    this.BeginInvoke(new Action(() => UpdatePickerHoverPreview(pt)));
                }
                else if (msg == NativeMethods.WM_LBUTTONDOWN)
                {
                    NativeMethods.POINT pt = hookStruct.pt;
                    this.BeginInvoke(new Action(() => CompleteWindowPickAtPoint(pt)));
                    // Swallow the click so the underlying window is not inadvertently clicked
                    return (IntPtr)1;
                }
                else if (msg == NativeMethods.WM_RBUTTONDOWN)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        StopWindowPickerMode(true);
                    }));
                    return (IntPtr)1;
                }
            }
            return NativeMethods.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }

        private void UpdatePickerHoverPreview(NativeMethods.POINT pt)
        {
            if (!_isPickingWindow || _pickerHud == null)
                return;

            IntPtr rootHwnd;
            bool isEmptyArea;
            WindowEntry entry = ResolvePickedWindowAtPoint(pt, out rootHwnd, out isEmptyArea);

            if (isEmptyArea || entry == null)
            {
                _pickerHud.UpdateTargetText("Empty Desktop / System Area — Click here to cancel picker", true);
            }
            else
            {
                string titleText = string.IsNullOrEmpty(entry.Title) ? "(Untitled Window)" : entry.Title;
                _pickerHud.UpdateTargetText(
                    string.Format("Click to Pin: {0}  —  \"{1}\" (PID {2})", entry.ProcessName, titleText, entry.ProcessId),
                    false);
            }
        }

        private void CompleteWindowPickAtPoint(NativeMethods.POINT pt)
        {
            IntPtr rootHwnd;
            bool isEmptyArea;
            WindowEntry entry = ResolvePickedWindowAtPoint(pt, out rootHwnd, out isEmptyArea);

            StopWindowPickerMode(true);

            if (isEmptyArea || entry == null || rootHwnd == IntPtr.Zero)
            {
                // User clicked on an empty area (not on an application window): cancel picker tool
                return;
            }

            // Pin this specific window handle AND register its process in PinnedApps
            _explicitPickedHandles.Add(rootHwnd);
            AddOrUpdatePinnedApp(entry, true);

            if (CheckAndOfferAdminElevationForEntries(new uint[] { entry.ProcessId }))
                return;

            EnforceTopmostAndRefresh(true, false);
            RefreshProcessListUI();
        }

        private WindowEntry ResolvePickedWindowAtPoint(NativeMethods.POINT pt, out IntPtr rootHwnd, out bool isEmptyArea)
        {
            rootHwnd = IntPtr.Zero;
            isEmptyArea = true;

            IntPtr hitHwnd = NativeMethods.WindowFromPoint(pt);
            if (hitHwnd == IntPtr.Zero)
                return null;

            string hitClass = GetWindowClassName(hitHwnd);
            if (IsDesktopOrShellEmptyClass(hitClass))
                return null;

            rootHwnd = NativeMethods.GetAncestor(hitHwnd, NativeMethods.GA_ROOT);
            if (rootHwnd == IntPtr.Zero)
                rootHwnd = hitHwnd;

            if (IsHandleCreated && rootHwnd == this.Handle)
                return null;
            if (_pickerHud != null && _pickerHud.IsHandleCreated && rootHwnd == _pickerHud.Handle)
                return null;

            string rootClass = GetWindowClassName(rootHwnd);
            if (IsDesktopOrShellEmptyClass(rootClass))
                return null;

            uint pid;
            NativeMethods.GetWindowThreadProcessId(rootHwnd, out pid);
            if (pid == 0 || pid == _currentProcessId)
                return null;

            // Inspect with relaxed rules so even problematic/borderless/tool windows can be picked!
            WindowEntry entry = InspectWindow(rootHwnd, true);
            if (entry == null)
                return null;

            // Check if this is explorer.exe's desktop wallpaper surface (FolderView / Program Manager)
            if (string.Equals(entry.ProcessName, "explorer", StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(entry.Title, "Program Manager", StringComparison.OrdinalIgnoreCase) ||
                 string.IsNullOrEmpty(entry.Title) ||
                 rootClass == "Progman" || rootClass == "WorkerW"))
            {
                return null;
            }

            isEmptyArea = false;
            return entry;
        }

        private static bool IsDesktopOrShellEmptyClass(string className)
        {
            if (string.IsNullOrEmpty(className))
                return true;

            return className == "Progman" ||
                   className == "WorkerW" ||
                   className == "SHELLDLL_DefView" ||
                   className == "SysListView32" ||
                   className == "Shell_TrayWnd" ||
                   className == "Shell_SecondaryTrayWnd" ||
                   className == "MSTaskSwWClass" ||
                   className == "MSTaskListWClass" ||
                   className == "ReBarWindow32" ||
                   className == "TrayNotifyWnd" ||
                   className == "UAC_InputIndicatorOverlayWnd" ||
                   className == "UAC Input Indicator";
        }

        private static string GetWindowClassName(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return "";
            StringBuilder sb = new StringBuilder(256);
            NativeMethods.GetClassName(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        // =====================================================================
        // Global Hotkey & Window Messages
        // =====================================================================

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                ToggleForegroundWindowPin();
                return;
            }
            if (WM_SHOW_MANAGER != 0 && m.Msg == WM_SHOW_MANAGER)
            {
                ShowPopupFromTray();
                return;
            }
            if (WM_QUIT_UTILITY != 0 && m.Msg == WM_QUIT_UTILITY)
            {
                ExitApplicationCompletely();
                return;
            }
            base.WndProc(ref m);
        }

        private void ToggleForegroundWindowPin()
        {
            IntPtr fgHwnd = NativeMethods.GetForegroundWindow();
            if (fgHwnd == IntPtr.Zero || (IsHandleCreated && fgHwnd == this.Handle))
                return;

            WindowEntry entry = InspectWindow(fgHwnd, true);
            if (entry == null || string.IsNullOrWhiteSpace(entry.ProcessName))
                return;

            PinnedAppConfig existing = FindPinnedConfig(entry.ProcessName);
            if (existing != null)
            {
                RemovePinnedApp(existing.ProcessName);
                ShowTrayNotification("Removed from Always on Top", string.Format("{0} ({1}) is no longer pinned.", entry.ProcessName, entry.Title));
            }
            else
            {
                AddOrUpdatePinnedApp(entry, true);
                EnforceTopmostAndRefresh(true, false);
                RefreshProcessListUI();
                ShowTrayNotification("Pinned to Always on Top", string.Format("{0} ({1}) will stay on top.", entry.ProcessName, entry.Title));
            }
        }

        private void ShowTrayNotification(string title, string message)
        {
            try
            {
                if (_trayIcon != null)
                {
                    _trayIcon.ShowBalloonTip(2500, title, message, ToolTipIcon.Info);
                }
            }
            catch { }
        }

        // =====================================================================
        // Pin / Unpin Operations (From Apps List, Processes List, or Picker)
        // =====================================================================

        private void PinSelectedRunningApps()
        {
            if (_runningListView.SelectedItems.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "Please select one or more running applications from the Running Applications list first.",
                    "No Application Selected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            List<WindowEntry> selectedEntries = new List<WindowEntry>();
            foreach (ListViewItem item in _runningListView.SelectedItems)
            {
                WindowEntry entry = item.Tag as WindowEntry;
                if (entry != null)
                {
                    selectedEntries.Add(entry);
                }
            }

            if (CheckAndOfferAdminElevationForEntries(selectedEntries.Select(e => e.ProcessId)))
                return;

            foreach (WindowEntry entry in selectedEntries)
            {
                AddOrUpdatePinnedApp(entry, true);
            }

            SaveSettings();
            EnforceTopmostAndRefresh(true, false);
            RefreshProcessListUI();
        }

        private void PinSelectedProcesses()
        {
            if (_processListView.SelectedItems.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "Please select one or more processes from the Running Windows Processes list first.",
                    "No Process Selected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            List<ProcessEntry> selectedProcs = new List<ProcessEntry>();
            foreach (ListViewItem item in _processListView.SelectedItems)
            {
                ProcessEntry proc = item.Tag as ProcessEntry;
                if (proc != null && !string.IsNullOrEmpty(proc.ProcessName))
                {
                    selectedProcs.Add(proc);
                }
            }

            foreach (ProcessEntry proc in selectedProcs)
            {
                PinnedAppConfig existing = FindPinnedConfig(proc.ProcessName);
                if (existing == null)
                {
                    existing = new PinnedAppConfig
                    {
                        ProcessName = proc.ProcessName,
                        ExecutablePath = proc.ExecutablePath ?? "",
                        LastKnownTitle = proc.WindowSummary ?? "",
                        AddedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    };
                    _settings.PinnedApps.Add(existing);
                }
                else
                {
                    if (!string.IsNullOrEmpty(proc.ExecutablePath))
                        existing.ExecutablePath = proc.ExecutablePath;
                }
            }

            SaveSettings();

            if (CheckAndOfferAdminElevationForEntries(selectedProcs.Select(p => p.ProcessId)))
                return;

            // Immediately bring all matching windows of the newly pinned process(es) to the front of topmost band
            List<WindowEntry> allWindows = EnumerateAllCandidateWindows();
            foreach (WindowEntry win in allWindows)
            {
                if (selectedProcs.Any(sp => IsProcessNameMatch(sp.ProcessName, win.ProcessName)))
                {
                    ForceWindowTopMost(win.Handle, true, true);
                    _pinnedHandles.Add(win.Handle);
                }
            }

            EnforceTopmostAndRefresh(true, false);
            RefreshProcessListUI();
        }

        private bool CheckAndOfferAdminElevationForEntries(IEnumerable<uint> pids)
        {
            if (_isRunningAsAdmin)
                return false;

            bool anyTargetElevated = false;
            foreach (uint pid in pids)
            {
                if (IsTargetProcessElevated(pid))
                {
                    anyTargetElevated = true;
                    break;
                }
            }

            if (anyTargetElevated)
            {
                SaveSettings();
                DialogResult dr = MessageBox.Show(
                    this,
                    "The selected application is running with Administrator privileges.\n\n" +
                    "Windows blocks non-elevated utilities from keeping Administrator windows (such as Hikvision iVMS-4200 or Administrator Notepad++) Always on Top.\n\n" +
                    "Would you like to restart AlwaysOnTop as Administrator now so it can pin this window?",
                    "Administrator Privileges Required",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (dr == DialogResult.Yes)
                {
                    RestartAsAdministrator();
                    return true;
                }
            }
            return false;
        }

        private void UnpinSelectedPinnedApps()
        {
            if (_pinnedListView.SelectedItems.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "Please select one or more pinned applications from Section 2 to remove.",
                    "No Pinned Application Selected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            List<string> toRemove = new List<string>();
            foreach (ListViewItem item in _pinnedListView.SelectedItems)
            {
                PinnedAppConfig cfg = item.Tag as PinnedAppConfig;
                if (cfg != null && !string.IsNullOrEmpty(cfg.ProcessName))
                {
                    toRemove.Add(cfg.ProcessName);
                }
            }

            foreach (string procName in toRemove)
            {
                RemovePinnedApp(procName);
            }
            RefreshProcessListUI();
        }

        private void UnpinAllPinnedApps()
        {
            if (_settings.PinnedApps.Count == 0)
                return;

            List<string> allNames = _settings.PinnedApps.Select(p => p.ProcessName).ToList();
            foreach (string procName in allNames)
            {
                UnpinWindowsOfProcess(procName);
            }

            foreach (IntPtr hWnd in _explicitPickedHandles.ToList())
            {
                if (NativeMethods.IsWindow(hWnd))
                    ForceWindowTopMost(hWnd, false, false);
            }
            _explicitPickedHandles.Clear();

            _settings.PinnedApps.Clear();
            SaveSettings();
            EnforceTopmostAndRefresh(true, false);
            RefreshProcessListUI();
        }

        private void AddOrUpdatePinnedApp(WindowEntry entry, bool bringToFrontNow)
        {
            PinnedAppConfig existing = FindPinnedConfig(entry.ProcessName);
            if (existing == null)
            {
                existing = new PinnedAppConfig
                {
                    ProcessName = entry.ProcessName,
                    ExecutablePath = entry.ExecutablePath ?? "",
                    LastKnownTitle = entry.Title ?? "",
                    AddedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                _settings.PinnedApps.Add(existing);
            }
            else
            {
                if (!string.IsNullOrEmpty(entry.ExecutablePath))
                    existing.ExecutablePath = entry.ExecutablePath;
                if (!string.IsNullOrEmpty(entry.Title))
                    existing.LastKnownTitle = entry.Title;
            }

            if (entry.Handle != IntPtr.Zero)
            {
                ForceWindowTopMost(entry.Handle, true, bringToFrontNow);
                _pinnedHandles.Add(entry.Handle);
            }
            SaveSettings();
        }

        private void RemovePinnedApp(string processName)
        {
            UnpinWindowsOfProcess(processName);
            _settings.PinnedApps.RemoveAll(p => IsProcessNameMatch(p.ProcessName, processName));
            SaveSettings();
            EnforceTopmostAndRefresh(true, false);
        }

        private void UnpinWindowsOfProcess(string processName)
        {
            List<WindowEntry> allWindows = EnumerateAllCandidateWindows();
            foreach (WindowEntry win in allWindows)
            {
                if (IsProcessNameMatch(processName, win.ProcessName))
                {
                    ForceWindowTopMost(win.Handle, false, false);
                    _pinnedHandles.Remove(win.Handle);
                    _explicitPickedHandles.Remove(win.Handle);
                }
            }
        }

        private void UnpinAllActiveHandlesOnly()
        {
            List<WindowEntry> allWindows = EnumerateAllCandidateWindows();
            foreach (WindowEntry win in allWindows)
            {
                if (FindPinnedConfig(win.ProcessName) != null ||
                    _pinnedHandles.Contains(win.Handle) ||
                    _explicitPickedHandles.Contains(win.Handle))
                {
                    ForceWindowTopMost(win.Handle, false, false);
                }
            }

            foreach (IntPtr hWnd in _pinnedHandles.Concat(_explicitPickedHandles).ToList())
            {
                if (NativeMethods.IsWindow(hWnd))
                {
                    ForceWindowTopMost(hWnd, false, false);
                }
            }
            _pinnedHandles.Clear();
            _explicitPickedHandles.Clear();
        }

        private PinnedAppConfig FindPinnedConfig(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return null;

            // 1. Exact process name match
            PinnedAppConfig exact = _settings.PinnedApps.FirstOrDefault(p =>
                string.Equals(p.ProcessName, processName, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
                return exact;

            // 2. Multi-process suite match (e.g., Hikvision iVMS-4200.Framework.C / iVMS-4200.Video.C / iVMS-4200.*)
            return _settings.PinnedApps.FirstOrDefault(p => IsProcessNameMatch(p.ProcessName, processName));
        }

        private static bool IsProcessNameMatch(string configuredName, string actualProcessName)
        {
            if (string.IsNullOrEmpty(configuredName) || string.IsNullOrEmpty(actualProcessName))
                return false;

            if (string.Equals(configuredName, actualProcessName, StringComparison.OrdinalIgnoreCase))
                return true;

            string prefixA = GetMultiProcessSuitePrefix(configuredName);
            string prefixB = GetMultiProcessSuitePrefix(actualProcessName);
            if (!string.IsNullOrEmpty(prefixA) && string.Equals(prefixA, prefixB, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static string GetMultiProcessSuitePrefix(string procName)
        {
            if (string.IsNullOrEmpty(procName))
                return "";
            int dotIdx = procName.IndexOf('.');
            if (dotIdx >= 4)
            {
                return procName.Substring(0, dotIdx);
            }
            return "";
        }

        /// <summary>
        /// Forces a window to be TopMost (or removes TopMost), with thread-input attachment fallback
        /// for stubborn windows (e.g. Qt / Notepad++ / iVMS-4200) and optional bring-to-front on initial pin.
        /// </summary>
        private static bool ForceWindowTopMost(IntPtr hWnd, bool topMost, bool bringToFront)
        {
            if (hWnd == IntPtr.Zero || !NativeMethods.IsWindow(hWnd))
                return false;

            if (topMost && bringToFront && NativeMethods.IsIconic(hWnd))
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
            }

            uint flags = NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE;
            if (!bringToFront)
            {
                flags |= NativeMethods.SWP_NOACTIVATE;
            }
            else
            {
                flags |= NativeMethods.SWP_SHOWWINDOW;
            }

            bool ok = NativeMethods.SetWindowPos(
                hWnd,
                topMost ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_NOTOPMOST,
                0, 0, 0, 0,
                flags);

            long afterEx = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
            bool isNowTopMost = (afterEx & NativeMethods.WS_EX_TOPMOST) != 0;

            // Fallback: if the window resisted HWND_TOPMOST or needs to be brought above other topmost windows
            if (topMost && (!isNowTopMost || bringToFront))
            {
                uint targetPid;
                uint targetThreadId = NativeMethods.GetWindowThreadProcessId(hWnd, out targetPid);
                uint currentThreadId = NativeMethods.GetCurrentThreadId();
                bool attached = false;

                try
                {
                    if (targetThreadId != 0 && targetThreadId != currentThreadId)
                    {
                        attached = NativeMethods.AttachThreadInput(currentThreadId, targetThreadId, true);
                    }

                    NativeMethods.SetWindowPos(
                        hWnd,
                        NativeMethods.HWND_TOPMOST,
                        0, 0, 0, 0,
                        NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE |
                        (bringToFront ? NativeMethods.SWP_SHOWWINDOW : NativeMethods.SWP_NOACTIVATE));

                    if (bringToFront)
                    {
                        NativeMethods.BringWindowToTop(hWnd);
                    }
                }
                catch { }
                finally
                {
                    if (attached)
                    {
                        NativeMethods.AttachThreadInput(currentThreadId, targetThreadId, false);
                    }
                }
            }

            return ok;
        }

        // =====================================================================
        // Window Enumeration, Process Enumeration & Periodic Enforcement
        // =====================================================================

        private string _lastUiSignature = "";

        private void EnforceTopmostAndRefresh(bool forceUiUpdate, bool forceReassertAll)
        {
            // EnumWindows returns windows in top-to-bottom (front-to-back) Z-order.
            List<WindowEntry> allCandidateWindows = EnumerateAllCandidateWindows();
            bool settingsMetadataUpdated = false;
            int activePinnedWindowsCount = 0;

            // Check if any non-pinned visible window is currently sitting ABOVE a pinned window in Z-order
            bool seenPinnedWindow = false;
            bool nonPinnedAbovePinned = false;
            foreach (WindowEntry win in allCandidateWindows)
            {
                bool isPinned = FindPinnedConfig(win.ProcessName) != null || _explicitPickedHandles.Contains(win.Handle);
                if (isPinned)
                {
                    seenPinnedWindow = true;
                }
                else if (!seenPinnedWindow && win.IsStandardAppWindow)
                {
                    // A non-pinned standard window was enumerated BEFORE a pinned window (meaning it's above a pinned window)
                    nonPinnedAbovePinned = true;
                }
            }

            // Iterate in REVERSE order (back-to-front) so that when multiple windows are pinned
            // (e.g., HDHomeRunView + Notepad++ + iVMS-4200), the frontmost pinned window stays in front
            // and background pinned windows NEVER leapfrog over the pinned window the user is working in!
            for (int i = allCandidateWindows.Count - 1; i >= 0; i--)
            {
                WindowEntry win = allCandidateWindows[i];
                PinnedAppConfig cfg = FindPinnedConfig(win.ProcessName);
                bool isExplicitlyPicked = _explicitPickedHandles.Contains(win.Handle);

                if (cfg != null || isExplicitlyPicked)
                {
                    // Only call SetWindowPos(HWND_TOPMOST) if:
                    // 1. The window does not currently have WS_EX_TOPMOST (!win.IsTopMost), OR
                    // 2. A non-pinned window got above a pinned window, OR
                    // 3. forceReassertAll was requested.
                    if (!win.IsTopMost || nonPinnedAbovePinned || forceReassertAll)
                    {
                        ForceWindowTopMost(win.Handle, true, false);
                    }

                    long curEx = NativeMethods.GetWindowLong(win.Handle, NativeMethods.GWL_EXSTYLE);
                    win.IsTopMost = (curEx & NativeMethods.WS_EX_TOPMOST) != 0;
                    _pinnedHandles.Add(win.Handle);
                    activePinnedWindowsCount++;

                    if (cfg != null)
                    {
                        if (!string.IsNullOrEmpty(win.Title) && cfg.LastKnownTitle != win.Title)
                        {
                            cfg.LastKnownTitle = win.Title;
                            settingsMetadataUpdated = true;
                        }
                        if (!string.IsNullOrEmpty(win.ExecutablePath) && cfg.ExecutablePath != win.ExecutablePath)
                        {
                            cfg.ExecutablePath = win.ExecutablePath;
                            settingsMetadataUpdated = true;
                        }
                    }
                }
            }

            _pinnedHandles.RemoveWhere(h => !NativeMethods.IsWindow(h));
            _explicitPickedHandles.RemoveWhere(h => !NativeMethods.IsWindow(h));

            if (settingsMetadataUpdated)
            {
                SaveSettings();
            }

            if (_trayPinnedCountItem != null)
            {
                _trayPinnedCountItem.Text = string.Format(
                    "Pinned Apps: {0} ({1} active window{2})",
                    _settings.PinnedApps.Count,
                    activePinnedWindowsCount,
                    activePinnedWindowsCount == 1 ? "" : "s");
            }

            if (this.Visible || forceUiUpdate)
            {
                string sig = BuildUiSignature(allCandidateWindows);
                if (forceUiUpdate || sig != _lastUiSignature)
                {
                    _lastUiSignature = sig;
                    RefreshListsUI(allCandidateWindows);
                }
            }
        }

        private string BuildUiSignature(List<WindowEntry> windows)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var w in windows.OrderBy(x => x.Handle.ToInt64()))
            {
                sb.Append(w.Handle.ToInt64()).Append(':')
                  .Append(w.ProcessName).Append(':')
                  .Append(w.Title).Append(':')
                  .Append(w.IsTopMost ? '1' : '0').Append(';');
            }
            sb.Append("|P|");
            foreach (var p in _settings.PinnedApps)
            {
                sb.Append(p.ProcessName).Append(':').Append(p.LastKnownTitle).Append(';');
            }
            return sb.ToString();
        }

        private void RefreshListsUI(List<WindowEntry> preloadedWindows)
        {
            List<WindowEntry> allWindows = preloadedWindows ?? EnumerateAllCandidateWindows();
            string filter = (_appSearchBox != null && _appSearchBox.Text != null) ? _appSearchBox.Text.Trim() : "";

            // Preserve current selections
            HashSet<string> selectedRunningProcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ListViewItem item in _runningListView.SelectedItems)
            {
                WindowEntry we = item.Tag as WindowEntry;
                if (we != null)
                    selectedRunningProcs.Add(we.ProcessName + "|" + we.Handle.ToInt64());
            }

            HashSet<string> selectedPinnedProcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ListViewItem item in _pinnedListView.SelectedItems)
            {
                PinnedAppConfig pc = item.Tag as PinnedAppConfig;
                if (pc != null)
                    selectedPinnedProcs.Add(pc.ProcessName);
            }

            // 1A. Update Running Applications (standard visible windows not already pinned)
            _runningListView.BeginUpdate();
            _runningListView.Items.Clear();

            int runningShownCount = 0;
            foreach (WindowEntry win in allWindows
                .Where(w => w.IsStandardAppWindow)
                .OrderBy(w => w.ProcessName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(w => w.Title))
            {
                if (FindPinnedConfig(win.ProcessName) != null)
                    continue;

                if (!string.IsNullOrEmpty(filter))
                {
                    bool match = (win.ProcessName != null && win.ProcessName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                 (win.Title != null && win.Title.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!match)
                        continue;
                }

                int imgIdx = GetOrLoadIconIndex(win.ProcessName, win.ExecutablePath);
                ListViewItem lvi = new ListViewItem(win.ProcessName, imgIdx);
                lvi.SubItems.Add(win.Title);
                lvi.SubItems.Add(win.ProcessId.ToString());
                lvi.Tag = win;
                lvi.ForeColor = _isDarkThemeEffective
                    ? Color.FromArgb(241, 245, 249)
                    : Color.FromArgb(30, 41, 59);

                if (selectedRunningProcs.Contains(win.ProcessName + "|" + win.Handle.ToInt64()))
                {
                    lvi.Selected = true;
                }

                _runningListView.Items.Add(lvi);
                runningShownCount++;
            }
            _runningListView.EndUpdate();
            _lblRunningAppsCount.Text = string.Format("({0})", runningShownCount);

            // 2. Update Pinned Applications (Always on Top)
            _pinnedListView.BeginUpdate();
            _pinnedListView.Items.Clear();

            int activePinnedApps = 0;
            foreach (PinnedAppConfig cfg in _settings.PinnedApps.OrderBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase))
            {
                List<WindowEntry> matchingWindows = allWindows
                    .Where(w => IsProcessNameMatch(cfg.ProcessName, w.ProcessName))
                    .ToList();

                string exePath = cfg.ExecutablePath;
                if (matchingWindows.Count > 0 && !string.IsNullOrEmpty(matchingWindows[0].ExecutablePath))
                    exePath = matchingWindows[0].ExecutablePath;

                int imgIdx = GetOrLoadIconIndex(cfg.ProcessName, exePath);
                ListViewItem lvi = new ListViewItem(cfg.ProcessName, imgIdx);

                if (matchingWindows.Count > 0)
                {
                    activePinnedApps++;
                    List<string> nonEmptyTitles = matchingWindows
                        .Select(w => string.IsNullOrEmpty(w.Title) ? string.Format("({0} Window)", w.ProcessName) : w.Title)
                        .Distinct()
                        .ToList();
                    string titles = string.Join("  |  ", nonEmptyTitles);
                    lvi.SubItems.Add(titles);

                    bool anyBlockedByAdmin = !_isRunningAsAdmin && matchingWindows.Any(w => !w.IsTopMost && IsTargetProcessElevated(w.ProcessId));
                    if (anyBlockedByAdmin)
                    {
                        lvi.SubItems.Add("Needs Admin Privileges (Click 'Run as Administrator')");
                        lvi.ForeColor = _isDarkThemeEffective
                            ? Color.FromArgb(248, 113, 113)
                            : Color.FromArgb(185, 28, 28);
                    }
                    else
                    {
                        lvi.SubItems.Add(matchingWindows.Count == 1
                            ? "Pinned & Active (1 window)"
                            : string.Format("Pinned & Active ({0} windows)", matchingWindows.Count));
                        lvi.ForeColor = _isDarkThemeEffective
                            ? Color.FromArgb(74, 222, 128)
                            : Color.FromArgb(22, 101, 52);
                    }
                }
                else
                {
                    bool isProcRunning = IsProcessCurrentlyRunning(cfg.ProcessName);
                    string rememberedTitle = string.IsNullOrEmpty(cfg.LastKnownTitle)
                        ? (isProcRunning ? "(Process running — monitoring for windows)" : "(Not currently running)")
                        : string.Format("{0} ({1})", cfg.LastKnownTitle, isProcRunning ? "Process active" : "Closed");
                    lvi.SubItems.Add(rememberedTitle);
                    lvi.SubItems.Add(isProcRunning
                        ? "Pinned Process (Monitoring windows)"
                        : "Remembered (Auto-pins on start)");
                    lvi.ForeColor = isProcRunning
                        ? (_isDarkThemeEffective ? Color.FromArgb(96, 165, 250) : Color.FromArgb(37, 99, 235))
                        : (_isDarkThemeEffective ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139));
                }

                lvi.Tag = cfg;
                if (selectedPinnedProcs.Contains(cfg.ProcessName))
                {
                    lvi.Selected = true;
                }

                _pinnedListView.Items.Add(lvi);
            }
            _pinnedListView.EndUpdate();
            _lblPinnedCount.Text = string.Format("({0} total, {1} with active windows)", _settings.PinnedApps.Count, activePinnedApps);
        }

        private void RefreshProcessListUI()
        {
            if (_processListView == null)
                return;

            string filter = (_procSearchBox != null && _procSearchBox.Text != null) ? _procSearchBox.Text.Trim() : "";

            HashSet<string> selectedProcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ListViewItem item in _processListView.SelectedItems)
            {
                ProcessEntry pe = item.Tag as ProcessEntry;
                if (pe != null)
                    selectedProcs.Add(pe.ProcessName);
            }

            List<WindowEntry> allCandidateWindows = EnumerateAllCandidateWindows();
            Dictionary<string, List<WindowEntry>> windowsByProc = new Dictionary<string, List<WindowEntry>>(StringComparer.OrdinalIgnoreCase);
            foreach (WindowEntry w in allCandidateWindows)
            {
                if (string.IsNullOrEmpty(w.ProcessName))
                    continue;
                List<WindowEntry> list;
                if (!windowsByProc.TryGetValue(w.ProcessName, out list))
                {
                    list = new List<WindowEntry>();
                    windowsByProc[w.ProcessName] = list;
                }
                list.Add(w);
            }

            Dictionary<string, ProcessEntry> procMap = new Dictionary<string, ProcessEntry>(StringComparer.OrdinalIgnoreCase);

            Process[] allProcs = Process.GetProcesses();
            foreach (Process p in allProcs)
            {
                try
                {
                    uint pid = (uint)p.Id;
                    if (pid == 0 || pid == 4 || pid == _currentProcessId)
                        continue;

                    string procName = p.ProcessName;
                    if (string.IsNullOrEmpty(procName))
                        continue;

                    // Skip already-pinned processes so they appear in Section 2
                    if (FindPinnedConfig(procName) != null)
                        continue;

                    string exePath = GetProcessExecutablePath(pid);
                    string mainTitle = "";

                    List<WindowEntry> winList;
                    windowsByProc.TryGetValue(procName, out winList);
                    int winCount = winList != null ? winList.Count : 0;

                    if (winList != null && winList.Count > 0)
                    {
                        WindowEntry firstTitled = winList.FirstOrDefault(w => !string.IsNullOrEmpty(w.Title));
                        if (firstTitled != null)
                            mainTitle = firstTitled.Title;
                    }

                    ProcessEntry existing;
                    if (!procMap.TryGetValue(procName, out existing))
                    {
                        string summary;
                        if (!string.IsNullOrEmpty(mainTitle))
                            summary = mainTitle;
                        else if (winCount > 0)
                            summary = string.Format("{0} window(s) detected", winCount);
                        else
                            summary = "Background / Service Process";

                        procMap[procName] = new ProcessEntry
                        {
                            ProcessId = pid,
                            ProcessName = procName,
                            ExecutablePath = exePath,
                            WindowSummary = summary,
                            VisibleWindowCount = winCount
                        };
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(existing.ExecutablePath) && !string.IsNullOrEmpty(exePath))
                            existing.ExecutablePath = exePath;
                        if (!string.IsNullOrEmpty(mainTitle) && existing.WindowSummary.StartsWith("Background"))
                            existing.WindowSummary = mainTitle;
                        if (winCount > existing.VisibleWindowCount)
                            existing.VisibleWindowCount = winCount;
                    }
                }
                catch { }
                finally
                {
                    p.Dispose();
                }
            }

            _processListView.BeginUpdate();
            _processListView.Items.Clear();

            int shownCount = 0;
            foreach (ProcessEntry pe in procMap.Values
                .OrderByDescending(x => x.VisibleWindowCount > 0 ? 1 : 0)
                .ThenBy(x => x.ProcessName, StringComparer.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(filter))
                {
                    bool match = (pe.ProcessName != null && pe.ProcessName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                 (pe.WindowSummary != null && pe.WindowSummary.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                 pe.ProcessId.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!match)
                        continue;
                }

                int imgIdx = GetOrLoadIconIndex(pe.ProcessName, pe.ExecutablePath);
                ListViewItem lvi = new ListViewItem(pe.ProcessName, imgIdx);
                lvi.SubItems.Add(pe.ProcessId.ToString());
                lvi.SubItems.Add(pe.WindowSummary);
                lvi.Tag = pe;

                if (pe.VisibleWindowCount == 0)
                {
                    lvi.ForeColor = _isDarkThemeEffective
                        ? Color.FromArgb(148, 163, 184)
                        : Color.FromArgb(100, 116, 139);
                }
                else
                {
                    lvi.ForeColor = _isDarkThemeEffective
                        ? Color.FromArgb(241, 245, 249)
                        : Color.FromArgb(30, 41, 59);
                }

                if (selectedProcs.Contains(pe.ProcessName))
                {
                    lvi.Selected = true;
                }

                _processListView.Items.Add(lvi);
                shownCount++;
            }

            _processListView.EndUpdate();
            if (_lblProcessesCount != null)
            {
                _lblProcessesCount.Text = string.Format("({0})", shownCount);
            }
        }

        private static bool IsProcessCurrentlyRunning(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return false;
            try
            {
                Process[] procs = Process.GetProcessesByName(processName);
                bool running = procs != null && procs.Length > 0;
                if (procs != null)
                {
                    foreach (Process p in procs)
                        p.Dispose();
                }
                if (running)
                    return true;

                // Also check multi-process suite prefix (e.g., iVMS-4200.*)
                string prefix = GetMultiProcessSuitePrefix(processName);
                if (!string.IsNullOrEmpty(prefix))
                {
                    Process[] all = Process.GetProcesses();
                    foreach (Process p in all)
                    {
                        try
                        {
                            if (IsProcessNameMatch(processName, p.ProcessName))
                                running = true;
                        }
                        catch { }
                        finally
                        {
                            p.Dispose();
                        }
                    }
                }
                return running;
            }
            catch
            {
                return false;
            }
        }

        private int GetOrLoadIconIndex(string processName, string exePath)
        {
            string key = !string.IsNullOrEmpty(exePath) ? exePath : (processName ?? "default");
            int cachedIdx;
            if (_iconIndexCache.TryGetValue(key, out cachedIdx))
                return cachedIdx;

            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                try
                {
                    Icon ico = Icon.ExtractAssociatedIcon(exePath);
                    if (ico != null)
                    {
                        _appImageList.Images.Add(key, ico);
                        int idx = _appImageList.Images.Count - 1;
                        _iconIndexCache[key] = idx;
                        return idx;
                    }
                }
                catch { }
            }
            return 0;
        }

        private List<WindowEntry> EnumerateAllCandidateWindows()
        {
            List<WindowEntry> results = new List<WindowEntry>();

            NativeMethods.EnumWindows((hWnd, lParam) =>
            {
                WindowEntry entry = InspectWindow(hWnd, true);
                if (entry != null)
                {
                    results.Add(entry);
                }
                return true;
            }, IntPtr.Zero);

            return results;
        }

        private WindowEntry InspectWindow(IntPtr hWnd, bool includeNonStandardWindows)
        {
            if (hWnd == IntPtr.Zero)
                return null;
            if (IsHandleCreated && hWnd == this.Handle)
                return null;
            if (_pickerHud != null && _pickerHud.IsHandleCreated && hWnd == _pickerHud.Handle)
                return null;

            if (!NativeMethods.IsWindowVisible(hWnd))
                return null;

            // Exclude Windows 10/11 cloaked background windows
            int cloaked;
            if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAKED, out cloaked, sizeof(int)) == 0)
            {
                if (cloaked != 0)
                    return null;
            }

            // Exclude zero-size or microscopic invisible helper windows
            NativeMethods.RECT rect;
            if (NativeMethods.GetWindowRect(hWnd, out rect))
            {
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;
                if (width < 16 || height < 16)
                    return null;
            }

            string className = GetWindowClassName(hWnd);
            if (IsDesktopOrShellEmptyClass(className) || className == "Windows.UI.Core.CoreWindow")
            {
                return null;
            }

            long exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
            bool isAppWindow = (exStyle & NativeMethods.WS_EX_APPWINDOW) != 0;
            bool isToolWindow = (exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0;
            bool isNoActivate = (exStyle & NativeMethods.WS_EX_NOACTIVATE) != 0;
            IntPtr owner = NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER);
            bool hasVisibleOwner = owner != IntPtr.Zero && NativeMethods.IsWindowVisible(owner);

            int titleLen = NativeMethods.GetWindowTextLength(hWnd);
            string title = "";
            if (titleLen > 0)
            {
                StringBuilder titleSb = new StringBuilder(titleLen + 1);
                NativeMethods.GetWindowText(hWnd, titleSb, titleSb.Capacity);
                title = titleSb.ToString().Trim();
            }

            // Many Qt / Delphi / C++ apps (like Hikvision iVMS-4200) have an invisible root owner window
            bool isStandardApp = !string.IsNullOrEmpty(title) &&
                                 ((!isToolWindow && !isNoActivate && !hasVisibleOwner) || isAppWindow);

            if (!includeNonStandardWindows && !isStandardApp)
                return null;

            uint pid;
            NativeMethods.GetWindowThreadProcessId(hWnd, out pid);
            if (pid == 0 || pid == _currentProcessId)
                return null;

            string exePath = GetProcessExecutablePath(pid);
            string procName = GetProcessShortName(pid, exePath);

            // Resolve real UWP process inside ApplicationFrameHost windows (e.g., Calculator, HDHomeRunView)
            if (string.Equals(procName, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
            {
                uint realChildPid = 0;
                NativeMethods.EnumChildWindows(hWnd, (childHwnd, lParam) =>
                {
                    uint childPid;
                    NativeMethods.GetWindowThreadProcessId(childHwnd, out childPid);
                    if (childPid != 0 && childPid != pid)
                    {
                        realChildPid = childPid;
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);

                if (realChildPid != 0)
                {
                    pid = realChildPid;
                    exePath = GetProcessExecutablePath(pid);
                    procName = GetProcessShortName(pid, exePath);
                }
            }

            if (string.IsNullOrEmpty(procName) ||
                string.Equals(procName, "TextInputHost", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(procName, "SearchHost", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(procName, "StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(procName, "ShellExperienceHost", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            bool isTopMost = (exStyle & NativeMethods.WS_EX_TOPMOST) != 0;

            return new WindowEntry
            {
                Handle = hWnd,
                ProcessId = pid,
                ProcessName = procName,
                ExecutablePath = exePath,
                Title = title,
                IsTopMost = isTopMost,
                IsStandardAppWindow = isStandardApp
            };
        }

        public static bool IsTargetProcessElevated(uint pid)
        {
            if (pid == 0)
                return false;

            IntPtr hProc = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProc == IntPtr.Zero)
                return false;

            try
            {
                IntPtr hToken;
                if (NativeMethods.OpenProcessToken(hProc, NativeMethods.TOKEN_QUERY, out hToken))
                {
                    try
                    {
                        int elevation = 0;
                        int returnLen = 0;
                        if (NativeMethods.GetTokenInformation(hToken, NativeMethods.TokenElevation, out elevation, sizeof(int), out returnLen))
                        {
                            return elevation != 0;
                        }
                    }
                    finally
                    {
                        NativeMethods.CloseHandle(hToken);
                    }
                }
            }
            catch { }
            finally
            {
                NativeMethods.CloseHandle(hProc);
            }
            return false;
        }

        private static string GetProcessExecutablePath(uint pid)
        {
            IntPtr hProc = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProc == IntPtr.Zero)
                return "";

            try
            {
                int capacity = 1024;
                StringBuilder sb = new StringBuilder(capacity);
                if (NativeMethods.QueryFullProcessImageName(hProc, 0, sb, ref capacity))
                {
                    return sb.ToString();
                }
            }
            catch { }
            finally
            {
                NativeMethods.CloseHandle(hProc);
            }
            return "";
        }

        private static string GetProcessShortName(uint pid, string exePath)
        {
            if (!string.IsNullOrEmpty(exePath))
            {
                try
                {
                    return Path.GetFileNameWithoutExtension(exePath);
                }
                catch { }
            }
            try
            {
                using (Process p = Process.GetProcessById((int)pid))
                {
                    return p.ProcessName;
                }
            }
            catch
            {
                return "";
            }
        }

        // =====================================================================
        // Windows Startup & Start Minimized Settings
        // =====================================================================

        private bool IsStartWithWindowsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, false))
                {
                    if (key == null)
                        return false;
                    object val = key.GetValue(RunRegistryValueName);
                    return val != null && !string.IsNullOrWhiteSpace(val.ToString());
                }
            }
            catch
            {
                return false;
            }
        }

        private void SetStartWithWindows(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, true))
                {
                    if (key == null)
                        return;

                    if (enable)
                    {
                        string exePath = Application.ExecutablePath;
                        key.SetValue(RunRegistryValueName, string.Format("\"{0}\" --tray", exePath));
                    }
                    else
                    {
                        if (key.GetValue(RunRegistryValueName) != null)
                            key.DeleteValue(RunRegistryValueName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    string.Format("Could not update Windows Startup registry setting:\n{0}", ex.Message),
                    "Startup Setting Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void OnStartupWithWindowsChanged(object sender, EventArgs e)
        {
            if (_suppressCheckboxEvents)
                return;
            SetStartWithWindows(_chkStartWithWindows.Checked);
            SyncSettingsCheckboxesUI();
        }

        private void OnStartMinimizedChanged(object sender, EventArgs e)
        {
            if (_suppressCheckboxEvents)
                return;
            _settings.StartMinimized = _chkStartMinimized.Checked;
            SaveSettings();
        }

        private void SyncSettingsCheckboxesUI()
        {
            _suppressCheckboxEvents = true;
            if (_chkStartWithWindows != null)
                _chkStartWithWindows.Checked = IsStartWithWindowsEnabled();
            if (_chkStartMinimized != null)
                _chkStartMinimized.Checked = _settings.StartMinimized;
            _suppressCheckboxEvents = false;
        }

        // =====================================================================
        // Settings Load / Save
        // =====================================================================

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath, Encoding.UTF8);
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    AppSettings loaded = serializer.Deserialize<AppSettings>(json);
                    if (loaded != null)
                    {
                        if (loaded.PinnedApps == null)
                            loaded.PinnedApps = new List<PinnedAppConfig>();
                        if (string.IsNullOrEmpty(loaded.Theme))
                            loaded.Theme = "System";
                        _settings = loaded;
                    }
                }
            }
            catch
            {
                _settings = new AppSettings();
            }
        }

        private void SaveSettings()
        {
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(_settings);
                File.WriteAllText(_settingsFilePath, json, Encoding.UTF8);
            }
            catch { }
        }
    }

    internal static class Program
    {
        public static bool IsCurrentProcessAdmin()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        [STAThread]
        private static void Main(string[] args)
        {
            bool quitRequested = args != null && args.Any(a => string.Equals(a, "--quit", StringComparison.OrdinalIgnoreCase));
            if (quitRequested)
            {
                if (MainForm.WM_QUIT_UTILITY != 0)
                {
                    NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST, MainForm.WM_QUIT_UTILITY, IntPtr.Zero, IntPtr.Zero);
                }
                return;
            }

            bool launchedFromStartup = args != null && args.Any(a => string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase));
            bool isRelaunchAdmin = args != null && args.Any(a => string.Equals(a, "--relaunch-admin", StringComparison.OrdinalIgnoreCase));
            string oldExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AlwaysOnTop.old.exe");
            bool isUpgradingOldBinary = File.Exists(oldExePath);

            if (isUpgradingOldBinary)
            {
                try
                {
                    int myPid = Process.GetCurrentProcess().Id;
                    foreach (string pName in new string[] { "AlwaysOnTop", "AlwaysOnTop.old" })
                    {
                        foreach (Process p in Process.GetProcessesByName(pName))
                        {
                            try
                            {
                                if (p.Id != myPid)
                                {
                                    p.Kill();
                                    p.WaitForExit(1000);
                                }
                            }
                            catch { }
                            finally
                            {
                                p.Dispose();
                            }
                        }
                    }
                    File.Delete(oldExePath);
                }
                catch { }
            }

            // If an instance is already running (and we are not in the middle of elevating or upgrading), signal it to show the manager window
            if (!isRelaunchAdmin && !isUpgradingOldBinary)
            {
                try
                {
                    using (Mutex existing = Mutex.OpenExisting(@"Local\AlwaysOnTopUtility_SingleInstance_Mutex"))
                    {
                        try
                        {
                            using (EventWaitHandle ev = EventWaitHandle.OpenExisting(MainForm.ShowManagerEventName))
                            {
                                ev.Set();
                            }
                        }
                        catch { }
                        if (MainForm.WM_SHOW_MANAGER != 0)
                        {
                            NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST, MainForm.WM_SHOW_MANAGER, IntPtr.Zero, IntPtr.Zero);
                        }
                        return;
                    }
                }
                catch (WaitHandleCannotBeOpenedException)
                {
                    // No existing instance running yet
                }
                catch { }
            }

            bool createdNew;
            using (Mutex mutex = new Mutex(true, @"Local\AlwaysOnTopUtility_SingleInstance_Mutex", out createdNew))
            {
                if (!createdNew && isRelaunchAdmin)
                {
                    try
                    {
                        int myPid = Process.GetCurrentProcess().Id;
                        foreach (string pName in new string[] { "AlwaysOnTop", "AlwaysOnTop.old" })
                        {
                            foreach (Process p in Process.GetProcessesByName(pName))
                            {
                                try
                                {
                                    if (p.Id != myPid)
                                        p.Kill();
                                }
                                catch { }
                                finally
                                {
                                    p.Dispose();
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (!createdNew)
                {
                    // Wait briefly in case previous instance is just exiting
                    try { createdNew = mutex.WaitOne(1500); } catch { }
                }

                if (!createdNew)
                {
                    if (MainForm.WM_SHOW_MANAGER != 0)
                    {
                        NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST, MainForm.WM_SHOW_MANAGER, IntPtr.Zero, IntPtr.Zero);
                    }
                    return;
                }

                NativeMethods.SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm(launchedFromStartup));
            }
        }
    }
}
