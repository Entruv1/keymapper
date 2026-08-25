using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace KeyMapper
{
    /* ==================== 原生 Win32 API ==================== */
    internal static class Native
    {
        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        // 注意：联合体必须包含 MOUSE/KEYBOARD/HARDWARE 三个成员，
        // 否则结构体大小不匹配（x64 下必须是 40 字节），SendInput 会失败(错误87)
        [StructLayout(LayoutKind.Explicit)]
        public struct INPUTUNION
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public INPUTUNION U;
        }

        public const int WH_KEYBOARD_LL = 13;
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_SYSKEYUP = 0x0105;
        public const uint INPUT_KEYBOARD = 1;
        public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const uint KEYEVENTF_SCANCODE = 0x0008;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("kernel32.dll")]
        public static extern uint GetTickCount();

        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();
    }

    /* ==================== 键名显示 ==================== */
    internal static class KeyConsts
    {
        // .NET Framework 的 Keys 枚举缺少这三个媒体/应用键，这里补充
        public const Keys LaunchApp1 = (Keys)0xB6;   // VK_LAUNCH_APP1 我的电脑
        public const Keys LaunchApp2 = (Keys)0xB7;   // VK_LAUNCH_APP2 计算器
        public const Keys LaunchMedia = (Keys)0xB5;  // VK_LAUNCH_MEDIA_SELECT 媒体播放器
    }

    public static class KeyNames
    {
        private static readonly Dictionary<Keys, string> Map = new Dictionary<Keys, string>();

        static KeyNames()
        {
            Map.Add(Keys.Escape, "Esc");
            Map.Add(Keys.Tab, "Tab");
            Map.Add(Keys.CapsLock, "CapsLock");
            Map.Add(Keys.Space, "空格");
            Map.Add(Keys.Return, "回车");
            Map.Add(Keys.Back, "退格");
            Map.Add(Keys.Insert, "Insert");
            Map.Add(Keys.Delete, "Delete");
            Map.Add(Keys.Home, "Home");
            Map.Add(Keys.End, "End");
            Map.Add(Keys.PageUp, "PageUp（上翻页）");
            Map.Add(Keys.PageDown, "PageDown（下翻页）");
            Map.Add(Keys.Left, "←");
            Map.Add(Keys.Right, "→");
            Map.Add(Keys.Up, "↑");
            Map.Add(Keys.Down, "↓");
            Map.Add(Keys.PrintScreen, "PrtSc（截图）");
            Map.Add(Keys.Scroll, "ScrollLock");
            Map.Add(Keys.Pause, "Pause");
            Map.Add(Keys.NumLock, "NumLock");
            Map.Add(Keys.Shift, "Shift");
            Map.Add(Keys.Control, "Ctrl");
            Map.Add(Keys.Alt, "Alt");
            Map.Add(Keys.ShiftKey, "Shift");
            Map.Add(Keys.ControlKey, "Ctrl");
            Map.Add(Keys.Menu, "Alt");
            Map.Add(Keys.LShiftKey, "Shift（左）");
            Map.Add(Keys.RShiftKey, "Shift（右）");
            Map.Add(Keys.LControlKey, "Ctrl（左）");
            Map.Add(Keys.RControlKey, "Ctrl（右）");
            Map.Add(Keys.LMenu, "Alt（左）");
            Map.Add(Keys.RMenu, "Alt（右）");
            Map.Add(Keys.LWin, "Win（左）");
            Map.Add(Keys.RWin, "Win（右）");
            Map.Add(Keys.Apps, "菜单键");
            Map.Add(Keys.Sleep, "睡眠（慎用）");
            Map.Add(Keys.VolumeUp, "音量增大");
            Map.Add(Keys.VolumeDown, "音量减小");
            Map.Add(Keys.VolumeMute, "静音");
            Map.Add(Keys.MediaNextTrack, "下一曲");
            Map.Add(Keys.MediaPreviousTrack, "上一曲");
            Map.Add(Keys.MediaPlayPause, "播放/暂停");
            Map.Add(Keys.MediaStop, "停止");
            Map.Add(Keys.BrowserBack, "浏览器后退");
            Map.Add(Keys.BrowserForward, "浏览器前进");
            Map.Add(Keys.BrowserRefresh, "浏览器刷新");
            Map.Add(Keys.BrowserStop, "浏览器停止加载");
            Map.Add(Keys.BrowserSearch, "浏览器搜索");
            Map.Add(Keys.BrowserFavorites, "浏览器收藏夹");
            Map.Add(Keys.BrowserHome, "浏览器主页");
            Map.Add(Keys.LaunchMail, "电子邮件");
            Map.Add(KeyConsts.LaunchApp1, "我的电脑");
            Map.Add(KeyConsts.LaunchApp2, "计算器");
            Map.Add(KeyConsts.LaunchMedia, "媒体播放器");
            Map.Add(Keys.D0, "0"); Map.Add(Keys.D1, "1"); Map.Add(Keys.D2, "2"); Map.Add(Keys.D3, "3"); Map.Add(Keys.D4, "4");
            Map.Add(Keys.D5, "5"); Map.Add(Keys.D6, "6"); Map.Add(Keys.D7, "7"); Map.Add(Keys.D8, "8"); Map.Add(Keys.D9, "9");
            Map.Add(Keys.Oemtilde, "`~"); Map.Add(Keys.OemMinus, "-_"); Map.Add(Keys.Oemplus, "=+");
            Map.Add(Keys.OemOpenBrackets, "["); Map.Add(Keys.OemCloseBrackets, "]"); Map.Add(Keys.OemPipe, "\\|");
            Map.Add(Keys.OemSemicolon, ";:"); Map.Add(Keys.OemQuotes, "'\""); Map.Add(Keys.Oemcomma, ",<");
            Map.Add(Keys.OemPeriod, ".>"); Map.Add(Keys.OemQuestion, "/?");
            Map.Add(Keys.NumPad0, "小键盘0"); Map.Add(Keys.NumPad1, "小键盘1"); Map.Add(Keys.NumPad2, "小键盘2");
            Map.Add(Keys.NumPad3, "小键盘3"); Map.Add(Keys.NumPad4, "小键盘4"); Map.Add(Keys.NumPad5, "小键盘5");
            Map.Add(Keys.NumPad6, "小键盘6"); Map.Add(Keys.NumPad7, "小键盘7"); Map.Add(Keys.NumPad8, "小键盘8");
            Map.Add(Keys.NumPad9, "小键盘9");
            Map.Add(Keys.Decimal, "小键盘."); Map.Add(Keys.Add, "小键盘+"); Map.Add(Keys.Subtract, "小键盘-");
            Map.Add(Keys.Multiply, "小键盘*"); Map.Add(Keys.Divide, "小键盘/");
        }

        public static string GetName(Keys k)
        {
            string s;
            if (Map.TryGetValue(k, out s)) return s;
            return k.ToString();
        }
    }

    /* ==================== 程序图标（键盘） ==================== */
    public static class IconFactory
    {
        private static Icon _icon;
        public static Icon GetIcon()
        {
            if (_icon == null) _icon = CreateIcon();
            return _icon;
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private static Icon CreateIcon()
        {
            const int S = 64;
            using (Bitmap bmp = new Bitmap(S, S))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    // 蓝色渐变圆角背景
                    Rectangle bgRect = new Rectangle(1, 1, S - 2, S - 2);
                    using (GraphicsPath bgPath = RoundedRect(bgRect, S / 5))
                    using (LinearGradientBrush bgBrush = new LinearGradientBrush(bgRect,
                        Color.FromArgb(96, 165, 255), Color.FromArgb(22, 64, 168), 45f))
                    {
                        g.FillPath(bgBrush, bgPath);
                    }

                    // 白色键盘主体
                    int kx = S / 7, ky = S / 5, kw = S - kx * 2, kh = S / 2;
                    Rectangle kbRect = new Rectangle(kx, ky, kw, kh);
                    using (GraphicsPath kbPath = RoundedRect(kbRect, S / 18))
                    using (SolidBrush kbBrush = new SolidBrush(Color.FromArgb(244, 246, 250)))
                    {
                        g.FillPath(kbBrush, kbPath);
                    }

                    // 按键行 + 空格行（含一个橙色高亮键，与按键测试高亮色一致）
                    int keyH = Math.Max(2, kh / 13);
                    int gap = Math.Max(1, keyH / 3);
                    int m = Math.Max(2, S / 24);
                    int innerW = kw - m * 2;
                    int y = ky + m;

                    using (SolidBrush keyBrush = new SolidBrush(Color.FromArgb(84, 96, 118)))
                    using (SolidBrush hlBrush = new SolidBrush(Color.FromArgb(255, 170, 40)))
                    {
                        y += DrawRow(g, kx + m, y, innerW, keyH, gap, 10, keyBrush, hlBrush, false);
                        y += gap;
                        y += DrawRow(g, kx + m, y, innerW, keyH, gap, 9, keyBrush, hlBrush, false);
                        y += gap;
                        y += DrawRow(g, kx + m, y, innerW, keyH, gap, 8, keyBrush, hlBrush, false);
                        y += gap;
                        DrawRow(g, kx + m, y, innerW, keyH, gap, 0, keyBrush, hlBrush, true);
                    }
                }
                IntPtr hIcon = bmp.GetHicon();
                try
                {
                    using (Icon tmp = Icon.FromHandle(hIcon))
                    {
                        return (Icon)tmp.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
        }

        private static int DrawRow(Graphics g, int x, int y, int innerW, int keyH, int gap, int count, Brush keyBrush, Brush hlBrush, bool spaceRow)
        {
            if (spaceRow)
            {
                int hlW = Math.Max(5, innerW / 8);
                int spaceW = innerW - hlW - gap;
                using (GraphicsPath sp = RoundedRect(new Rectangle(x, y, spaceW, keyH), keyH / 2))
                {
                    g.FillPath(keyBrush, sp);
                }
                using (GraphicsPath hp = RoundedRect(new Rectangle(x + spaceW + gap, y, hlW, keyH), keyH / 2))
                {
                    g.FillPath(hlBrush, hp);
                }
            }
            else
            {
                int keyW = (innerW - gap * (count - 1)) / count;
                for (int i = 0; i < count; i++)
                {
                    using (GraphicsPath p = RoundedRect(new Rectangle(x, y, keyW, keyH), keyH / 2))
                    {
                        g.FillPath(keyBrush, p);
                    }
                    x += keyW + gap;
                }
            }
            return keyH;
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /* ==================== 映射条目 ==================== */
    [Serializable]
    public class KeyMapping
    {
        public Keys FromKey { get; set; }
        public Keys ToKey { get; set; }          // 动作类型=按键 时的目标键
        public int ActionType { get; set; }      // 0=按键 1=屏蔽 2=打开网页 3=启动程序
        public string ActionArg { get; set; }    // 网页 URL 或 程序路径

        [XmlIgnore]
        public bool IsBlock { get { return ActionType == 1; } }

        [XmlIgnore]
        public string FromName { get { return KeyNames.GetName(FromKey); } }

        [XmlIgnore]
        public string ActionName
        {
            get
            {
                switch (ActionType)
                {
                    case 1: return "屏蔽（无操作）";
                    case 2: return "打开网页：" + ActionArg;
                    case 3: return "启动程序：" + ActionArg;
                    default: return KeyNames.GetName(ToKey);
                }
            }
        }

        [XmlIgnore]
        public string TypeName
        {
            get
            {
                switch (ActionType)
                {
                    case 1: return "屏蔽";
                    case 2: return "网页";
                    case 3: return "程序";
                    default: return "按键";
                }
            }
        }
    }

    /* ==================== 键盘钩子（映射核心） ==================== */
    public class KeyboardHook : IDisposable
    {
        public event Action<Keys, bool> KeyEvent;   // (按键, 是否按下) —— 供按键测试/捕获使用

        private Native.LowLevelKeyboardProc _proc;
        private IntPtr _hookId = IntPtr.Zero;
        private readonly object _sync = new object();
        private List<KeyMapping> _mappings = new List<KeyMapping>();
        private bool _enabled = true;
        private int _injectFailures;
        private readonly List<SentInfo> _sent = new List<SentInfo>();
        private readonly HashSet<Keys> _held = new HashSet<Keys>();   // 当前按住的键（用于连发检测）

        private class SentInfo
        {
            public uint vk;
            public uint time;
        }

        public bool Enabled
        {
            get { lock (_sync) return _enabled; }
            set { lock (_sync) _enabled = value; }
        }

        public int InjectFailures
        {
            get { lock (_sync) return _injectFailures; }
        }

        public void SetMappings(List<KeyMapping> mappings)
        {
            lock (_sync) _mappings = new List<KeyMapping>(mappings);
        }

        public void Install()
        {
            if (_hookId != IntPtr.Zero) return;
            _proc = HookCallback;
            using (Process cur = Process.GetCurrentProcess())
            {
                if (cur.MainModule != null)
                {
                    _hookId = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _proc,
                        Native.GetModuleHandle(cur.MainModule.ModuleName), 0);
                }
                else
                {
                    _hookId = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _proc, IntPtr.Zero, 0);
                }
            }
            if (_hookId == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                throw new Exception("安装键盘钩子失败：" + new Win32Exception(err).Message);
            }
        }

        public void Uninstall()
        {
            if (_hookId != IntPtr.Zero)
            {
                Native.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            Uninstall();
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                Native.KBDLLHOOKSTRUCT data =
                    (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
                int msg = wParam.ToInt32();
                bool isDown = (msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN);
                bool isUp = (msg == Native.WM_KEYUP || msg == Native.WM_SYSKEYUP);

                if (isDown || isUp)
                {
                    Keys vk = (Keys)data.vkCode;

                    // 1) 自己注入的事件直接放行，避免递归映射
                    bool isOurs = false;
                    lock (_sync)
                    {
                        for (int i = 0; i < _sent.Count; i++)
                        {
                            if (_sent[i].vk == data.vkCode && WithinTicks(_sent[i].time, data.time, 40))
                            {
                                _sent.RemoveAt(i);
                                isOurs = true;
                                break;
                            }
                        }
                    }
                    if (isOurs)
                        return Native.CallNextHookEx(_hookId, nCode, wParam, lParam);

                    // 2) 报告给按键测试窗口（物理按键）
                    Action<Keys, bool> ev = KeyEvent;
                    if (ev != null) ev(vk, isDown);

                    // 3) 执行映射
                    KeyMapping mapped = null;
                    lock (_sync)
                    {
                        if (_enabled)
                        {
                            foreach (KeyMapping m in _mappings)
                            {
                                if (m.FromKey == vk) { mapped = m; break; }
                            }
                        }
                    }

                    if (mapped != null)
                    {
                        // 连发检测：网页/程序等动作只在第一次按下时触发一次
                        bool repeat = false;
                        lock (_sync)
                        {
                            if (isDown)
                            {
                                if (_held.Contains(vk)) repeat = true;
                                else _held.Add(vk);
                            }
                            else
                            {
                                _held.Remove(vk);
                            }
                        }

                        switch (mapped.ActionType)
                        {
                            case 0:   // 映射为按键
                                if (isDown) SendKey(mapped.ToKey, false);
                                else SendKey(mapped.ToKey, true);
                                break;
                            case 1:   // 屏蔽：什么都不做
                                break;
                            case 2:   // 打开网页
                                if (isDown && !repeat) Launch(mapped.ActionArg);
                                break;
                            case 3:   // 启动程序
                                if (isDown && !repeat) Launch(mapped.ActionArg);
                                break;
                        }
                        return (IntPtr)1;   // 吞掉原始按键
                    }
                }
            }
            return Native.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        /* 启动网页 / 程序（ShellExecute，不阻塞钩子线程） */
        private static void Launch(string arg)
        {
            if (string.IsNullOrEmpty(arg)) return;
            try
            {
                Process.Start(arg);
            }
            catch { }
        }

        private static bool WithinTicks(uint a, uint b, uint window)
        {
            uint diff = a > b ? a - b : b - a;
            return diff <= window;
        }

        /* 扫描码注入：媒体/浏览器/应用键用固定 E0 扫描码，普通键用 MapVirtualKey */
        private static ushort GetScanCode(Keys key, ref bool extended)
        {
            switch (key)
            {
                case Keys.VolumeMute: extended = true; return 0x20;
                case Keys.VolumeDown: extended = true; return 0x2E;
                case Keys.VolumeUp: extended = true; return 0x30;
                case Keys.MediaNextTrack: extended = true; return 0x10;
                case Keys.MediaPreviousTrack: extended = true; return 0x14;
                case Keys.MediaStop: extended = true; return 0x24;
                case Keys.MediaPlayPause: extended = true; return 0x19;
                case Keys.LaunchMail: extended = true; return 0x6C;
                case KeyConsts.LaunchApp1: extended = true; return 0x6B;
                case KeyConsts.LaunchApp2: extended = true; return 0x21;
                case KeyConsts.LaunchMedia: extended = true; return 0x22;
                case Keys.BrowserBack: extended = true; return 0x6A;
                case Keys.BrowserForward: extended = true; return 0x69;
                case Keys.BrowserRefresh: extended = true; return 0x67;
                case Keys.BrowserStop: extended = true; return 0x68;
                case Keys.BrowserSearch: extended = true; return 0x65;
                case Keys.BrowserFavorites: extended = true; return 0x66;
                case Keys.BrowserHome: extended = true; return 0x32;
                case Keys.Sleep: return 0x5F;
                case Keys.NumLock: return 0x45;
            }
            return (ushort)Native.MapVirtualKey((uint)key, 0);
        }

        private static bool IsExtendedKey(Keys k)
        {
            switch (k)
            {
                case Keys.Insert: case Keys.Delete: case Keys.Home: case Keys.End:
                case Keys.PageUp: case Keys.PageDown:
                case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down:
                case Keys.RControlKey: case Keys.RMenu:
                case Keys.LWin: case Keys.RWin: case Keys.Apps:
                case Keys.Divide:
                    return true;
                default:
                    return false;
            }
        }

        private void SendKey(Keys key, bool keyUp)
        {
            Native.INPUT input = new Native.INPUT();
            input.type = Native.INPUT_KEYBOARD;

            ushort vk = (ushort)key;
            bool extended = IsExtendedKey(key);
            ushort scan = GetScanCode(key, ref extended);

            input.U.ki.wVk = vk;
            if (scan != 0)
            {
                input.U.ki.wScan = scan;
                input.U.ki.dwFlags |= Native.KEYEVENTF_SCANCODE;
            }
            if (extended) input.U.ki.dwFlags |= Native.KEYEVENTF_EXTENDEDKEY;
            if (keyUp) input.U.ki.dwFlags |= Native.KEYEVENTF_KEYUP;

            lock (_sync) _sent.Add(new SentInfo { vk = (uint)key, time = Native.GetTickCount() });

            Native.INPUT[] arr = new Native.INPUT[1];
            arr[0] = input;
            uint r = Native.SendInput(1, arr, Marshal.SizeOf(typeof(Native.INPUT)));
            if (r == 0)
            {
                lock (_sync) _injectFailures++;
                // 兜底：改用纯 vk 方式再试一次
                Native.INPUT input2 = new Native.INPUT();
                input2.type = Native.INPUT_KEYBOARD;
                input2.U.ki.wVk = vk;
                if (extended) input2.U.ki.dwFlags |= Native.KEYEVENTF_EXTENDEDKEY;
                if (keyUp) input2.U.ki.dwFlags |= Native.KEYEVENTF_KEYUP;
                Native.INPUT[] arr2 = new Native.INPUT[1];
                arr2[0] = input2;
                Native.SendInput(1, arr2, Marshal.SizeOf(typeof(Native.INPUT)));
            }
        }
    }

    /* ==================== 屏幕模拟键盘（按键测试用） ==================== */
    public class KeyboardControl : Control
    {
        public class KeyDef
        {
            public Keys Vk;
            public string Label;
            public Rectangle Rect;
            public bool Pressed;
        }

        private const int UnitW = 40;
        private const int UnitH = 34;
        private const int Gap = 4;
        private const int RowH = UnitH + Gap;

        private readonly List<KeyDef> _keys = new List<KeyDef>();
        private readonly Dictionary<Keys, List<KeyDef>> _map = new Dictionary<Keys, List<KeyDef>>();
        private int _y;
        private Size _layoutSize;
        private Font _boldFont;

        public KeyboardControl()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(43, 48, 60);
            Font = new Font("Microsoft YaHei UI", 8.5F);
            BuildLayout();
            Size = _layoutSize;
            MinimumSize = _layoutSize;
        }

        private struct RowKey
        {
            public Keys Vk;
            public string Label;
            public double Units;
            public RowKey(Keys vk, string label, double units) { Vk = vk; Label = label; Units = units; }
        }

        private void AddRow(RowKey[] row)
        {
            int x = Gap;
            foreach (RowKey rk in row)
            {
                int w = (int)(rk.Units * UnitW);
                if (w < UnitW) w = UnitW;
                AddKey(rk.Vk, rk.Label, x, _y, w, UnitH);
                x += w + Gap;
            }
            _y += RowH;
            if (x > _layoutSize.Width) _layoutSize.Width = x;
            _layoutSize.Height = _y;
        }

        private void AddKey(Keys vk, string label, int x, int y, int w, int h)
        {
            KeyDef kd = new KeyDef();
            kd.Vk = vk;
            kd.Label = label;
            kd.Rect = new Rectangle(x, y, w, h);
            _keys.Add(kd);
            AddToMap(vk, kd);
        }

        private void AddToMap(Keys vk, KeyDef kd)
        {
            List<KeyDef> list;
            if (!_map.TryGetValue(vk, out list)) { list = new List<KeyDef>(); _map[vk] = list; }
            list.Add(kd);
        }

        private void LinkPair(Keys a, Keys b)
        {
            foreach (KeyDef kd in _keys)
            {
                if (kd.Vk == a) AddToMap(b, kd);
                if (kd.Vk == b) AddToMap(a, kd);
            }
        }

        private void BuildLayout()
        {
            _y = Gap;

            // 第 1 行
            AddRow(new RowKey[]
            {
                new RowKey(Keys.Escape, "Esc", 1.2),
                new RowKey(Keys.F1, "F1", 1.0), new RowKey(Keys.F2, "F2", 1.0),
                new RowKey(Keys.F3, "F3", 1.0), new RowKey(Keys.F4, "F4", 1.0),
                new RowKey(Keys.F5, "F5", 1.0), new RowKey(Keys.F6, "F6", 1.0),
                new RowKey(Keys.F7, "F7", 1.0), new RowKey(Keys.F8, "F8", 1.0),
                new RowKey(Keys.F9, "F9", 1.0), new RowKey(Keys.F10, "F10", 1.0),
                new RowKey(Keys.F11, "F11", 1.0), new RowKey(Keys.F12, "F12", 1.0),
                new RowKey(Keys.PrintScreen, "PrtSc", 1.0),
                new RowKey(Keys.Scroll, "ScrLk", 1.0),
                new RowKey(Keys.Pause, "Pause", 1.2)
            });

            // 第 2 行
            AddRow(new RowKey[]
            {
                new RowKey(Keys.Oemtilde, "`", 1.0),
                new RowKey(Keys.D1, "1", 1.0), new RowKey(Keys.D2, "2", 1.0),
                new RowKey(Keys.D3, "3", 1.0), new RowKey(Keys.D4, "4", 1.0),
                new RowKey(Keys.D5, "5", 1.0), new RowKey(Keys.D6, "6", 1.0),
                new RowKey(Keys.D7, "7", 1.0), new RowKey(Keys.D8, "8", 1.0),
                new RowKey(Keys.D9, "9", 1.0), new RowKey(Keys.D0, "0", 1.0),
                new RowKey(Keys.OemMinus, "-", 1.0),
                new RowKey(Keys.Oemplus, "=", 1.0),
                new RowKey(Keys.Back, "退格", 2.0),
                new RowKey(Keys.Insert, "Insert", 1.0),
                new RowKey(Keys.Home, "Home", 1.0),
                new RowKey(Keys.PageUp, "PageUp", 1.0),
                new RowKey(Keys.NumLock, "NumLk", 1.0),
                new RowKey(Keys.Divide, "/", 1.0),
                new RowKey(Keys.Multiply, "*", 1.0),
                new RowKey(Keys.Subtract, "-", 1.0)
            });

            // 第 3 行
            AddRow(new RowKey[]
            {
                new RowKey(Keys.Tab, "Tab", 1.5),
                new RowKey(Keys.Q, "Q", 1.0), new RowKey(Keys.W, "W", 1.0),
                new RowKey(Keys.E, "E", 1.0), new RowKey(Keys.R, "R", 1.0),
                new RowKey(Keys.T, "T", 1.0), new RowKey(Keys.Y, "Y", 1.0),
                new RowKey(Keys.U, "U", 1.0), new RowKey(Keys.I, "I", 1.0),
                new RowKey(Keys.O, "O", 1.0), new RowKey(Keys.P, "P", 1.0),
                new RowKey(Keys.OemOpenBrackets, "[", 1.0),
                new RowKey(Keys.OemCloseBrackets, "]", 1.0),
                new RowKey(Keys.OemPipe, "\\", 1.5),
                new RowKey(Keys.Delete, "Delete", 1.0),
                new RowKey(Keys.End, "End", 1.0),
                new RowKey(Keys.PageDown, "PageDown", 1.0),
                new RowKey(Keys.NumPad7, "7", 1.0), new RowKey(Keys.NumPad8, "8", 1.0),
                new RowKey(Keys.NumPad9, "9", 1.0),
                new RowKey(Keys.Add, "+", 1.0)
            });

            // 第 4 行
            AddRow(new RowKey[]
            {
                new RowKey(Keys.CapsLock, "CapsLock", 1.75),
                new RowKey(Keys.A, "A", 1.0), new RowKey(Keys.S, "S", 1.0),
                new RowKey(Keys.D, "D", 1.0), new RowKey(Keys.F, "F", 1.0),
                new RowKey(Keys.G, "G", 1.0), new RowKey(Keys.H, "H", 1.0),
                new RowKey(Keys.J, "J", 1.0), new RowKey(Keys.K, "K", 1.0),
                new RowKey(Keys.L, "L", 1.0),
                new RowKey(Keys.OemSemicolon, ";", 1.0),
                new RowKey(Keys.OemQuotes, "'", 1.0),
                new RowKey(Keys.Return, "Enter", 2.25),
                new RowKey(Keys.NumPad4, "4", 1.0), new RowKey(Keys.NumPad5, "5", 1.0),
                new RowKey(Keys.NumPad6, "6", 1.0)
            });

            // 第 5 行
            AddRow(new RowKey[]
            {
                new RowKey(Keys.LShiftKey, "Shift", 2.25),
                new RowKey(Keys.Z, "Z", 1.0), new RowKey(Keys.X, "X", 1.0),
                new RowKey(Keys.C, "C", 1.0), new RowKey(Keys.V, "V", 1.0),
                new RowKey(Keys.B, "B", 1.0), new RowKey(Keys.N, "N", 1.0),
                new RowKey(Keys.M, "M", 1.0),
                new RowKey(Keys.Oemcomma, ",", 1.0),
                new RowKey(Keys.OemPeriod, ".", 1.0),
                new RowKey(Keys.OemQuestion, "/", 1.0),
                new RowKey(Keys.RShiftKey, "Shift", 2.75),
                new RowKey(Keys.Up, "↑", 1.0),
                new RowKey(Keys.NumPad1, "1", 1.0), new RowKey(Keys.NumPad2, "2", 1.0),
                new RowKey(Keys.NumPad3, "3", 1.0),
                new RowKey(Keys.Return, "Enter", 2.0)
            });

            // 第 6 行
            AddRow(new RowKey[]
            {
                new RowKey(Keys.LControlKey, "Ctrl", 1.25),
                new RowKey(Keys.LWin, "Win", 1.25),
                new RowKey(Keys.LMenu, "Alt", 1.25),
                new RowKey(Keys.Space, "空格", 6.25),
                new RowKey(Keys.RMenu, "Alt", 1.25),
                new RowKey(Keys.RWin, "Win", 1.25),
                new RowKey(Keys.Apps, "菜单", 1.25),
                new RowKey(Keys.RControlKey, "Ctrl", 1.25),
                new RowKey(Keys.Left, "←", 1.0),
                new RowKey(Keys.Down, "↓", 1.0),
                new RowKey(Keys.Right, "→", 1.0),
                new RowKey(Keys.NumPad0, "0", 2.0),
                new RowKey(Keys.Decimal, ".", 1.0)
            });

            LinkPair(Keys.LShiftKey, Keys.RShiftKey);
            LinkPair(Keys.LControlKey, Keys.RControlKey);
            LinkPair(Keys.LMenu, Keys.RMenu);
            LinkPair(Keys.LWin, Keys.RWin);
        }

        public void HandleKey(Keys vk, bool down)
        {
            List<KeyDef> list;
            if (!_map.TryGetValue(vk, out list)) return;
            bool changed = false;
            foreach (KeyDef kd in list)
            {
                if (kd.Pressed != down)
                {
                    kd.Pressed = down;
                    changed = true;
                }
            }
            if (changed) Invalidate();
        }

        private static bool IsSpecial(Keys k)
        {
            switch (k)
            {
                case Keys.Escape:
                case Keys.F1: case Keys.F2: case Keys.F3: case Keys.F4:
                case Keys.F5: case Keys.F6: case Keys.F7: case Keys.F8:
                case Keys.F9: case Keys.F10: case Keys.F11: case Keys.F12:
                case Keys.PrintScreen: case Keys.Scroll: case Keys.Pause:
                case Keys.Tab: case Keys.CapsLock: case Keys.Back: case Keys.Return:
                case Keys.Insert: case Keys.Delete: case Keys.Home: case Keys.End:
                case Keys.PageUp: case Keys.PageDown:
                case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down:
                case Keys.NumLock: case Keys.Divide: case Keys.Multiply:
                case Keys.Subtract: case Keys.Add: case Keys.Decimal:
                case Keys.NumPad0: case Keys.NumPad1: case Keys.NumPad2: case Keys.NumPad3:
                case Keys.NumPad4: case Keys.NumPad5: case Keys.NumPad6: case Keys.NumPad7:
                case Keys.NumPad8: case Keys.NumPad9:
                case Keys.LShiftKey: case Keys.RShiftKey:
                case Keys.LControlKey: case Keys.RControlKey:
                case Keys.LMenu: case Keys.RMenu:
                case Keys.LWin: case Keys.RWin: case Keys.Apps:
                case Keys.Space:
                    return true;
                default:
                    return false;
            }
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            if (_boldFont == null) _boldFont = new Font(Font, FontStyle.Bold);

            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;

                foreach (KeyDef kd in _keys)
                {
                    Rectangle r = kd.Rect;
                    Color fill;
                    Color border = Color.FromArgb(25, 28, 36);
                    Color textColor = Color.White;
                    Font f = Font;

                    if (kd.Pressed)
                    {
                        fill = Color.FromArgb(255, 172, 46);
                        border = Color.FromArgb(190, 110, 8);
                        textColor = Color.Black;
                        f = _boldFont;
                    }
                    else
                    {
                        fill = IsSpecial(kd.Vk) ? Color.FromArgb(66, 74, 88) : Color.FromArgb(56, 63, 76);
                    }

                    using (GraphicsPath path = RoundedRect(r, 5))
                    using (SolidBrush brush = new SolidBrush(fill))
                    using (Pen pen = new Pen(border, 1f))
                    using (SolidBrush tb = new SolidBrush(textColor))
                    {
                        g.FillPath(brush, path);
                        g.DrawPath(pen, path);
                        g.DrawString(kd.Label, f, tb, r, sf);
                    }
                }
            }
        }
    }

    /* ==================== 按键测试窗口 ==================== */
    public class TestForm : Form
    {
        private readonly KeyboardControl _kb;
        private readonly Label _lbl;

        public TestForm()
        {
            Text = "按键测试";
            ClientSize = new Size(1000, 480);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 9F);
            Icon = IconFactory.GetIcon();
            BackColor = Color.FromArgb(43, 48, 60);

            FlowLayoutPanel bar = new FlowLayoutPanel();
            bar.Dock = DockStyle.Top;
            bar.Height = 36;
            bar.Padding = new Padding(8, 8, 8, 0);
            bar.BackColor = Color.White;

            _lbl = new Label();
            _lbl.Text = "按下键盘任意键，屏幕上对应的按键会高亮显示";
            _lbl.AutoSize = true;
            _lbl.ForeColor = Color.FromArgb(90, 90, 90);

            CheckBox chk = new CheckBox();
            chk.Text = "窗口置顶";
            chk.AutoSize = true;
            chk.Margin = new Padding(16, 0, 0, 0);
            chk.CheckedChanged += delegate { TopMost = chk.Checked; };

            bar.Controls.Add(_lbl);
            bar.Controls.Add(chk);

            Panel scroll = new Panel();
            scroll.Dock = DockStyle.Fill;
            scroll.AutoScroll = true;
            scroll.BackColor = Color.FromArgb(43, 48, 60);

            _kb = new KeyboardControl();
            scroll.Controls.Add(_kb);

            Controls.Add(scroll);
            Controls.Add(bar);
        }

        public void HandleKey(Keys vk, bool down)
        {
            if (IsDisposed) return;
            _kb.HandleKey(vk, down);
            if (down)
                _lbl.Text = "最近按键：" + KeyNames.GetName(vk) +
                            "  （虚拟键码 0x" + ((int)vk).ToString("X2") + "）";
        }
    }

    /* ==================== 添加映射对话框 ==================== */
    public class AddMappingForm : Form
    {
        public class TargetDef
        {
            public Keys Key;
            public string Name;
            public string Category;
            public TargetDef(Keys k, string name, string cat) { Key = k; Name = name; Category = cat; }
            public override string ToString() { return Category + " - " + Name; }
        }

        public KeyMapping Result;

        private readonly KeyboardHook _hook;
        private Keys _from;
        private bool _capturing;
        private TextBox _tbFrom;
        private ComboBox _cboType;
        private Panel _panelKey, _panelUrl, _panelApp, _panelBlock;
        private TextBox _tbFilter;
        private ListBox _lbTargets;
        private TextBox _tbUrl;
        private TextBox _tbApp;
        private readonly List<TargetDef> _allTargets;

        public AddMappingForm(KeyboardHook hook) : this(hook, null)
        {
        }

        public AddMappingForm(KeyboardHook hook, KeyMapping initial)
        {
            _hook = hook;
            _allTargets = BuildTargets();

            Text = initial == null ? "添加按键映射" : "编辑按键映射";
            ClientSize = new Size(560, 560);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Microsoft YaHei UI", 9F);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(14);
            root.ColumnCount = 1;
            root.RowCount = 6;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

            // ① 来源键
            Label l1 = new Label();
            l1.Text = "① 点击下方输入框，再按下要“被替换”的按键（支持全部按键）";
            l1.Dock = DockStyle.Fill;
            l1.TextAlign = ContentAlignment.MiddleLeft;

            _tbFrom = new TextBox();
            _tbFrom.Dock = DockStyle.Fill;
            _tbFrom.Font = new Font("Microsoft YaHei UI", 12F);
            _tbFrom.Text = "（点击后在此按下要映射的按键）";
            _tbFrom.ForeColor = Color.Gray;
            _tbFrom.ReadOnly = true;
            _tbFrom.Cursor = Cursors.Hand;
            _tbFrom.Click += delegate
            {
                _capturing = true;
                _tbFrom.Text = "（请按下要映射的按键…）";
                _tbFrom.ForeColor = Color.Gray;
            };

            // ② 功能类型
            Label l2 = new Label();
            l2.Text = "② 选择该按键的功能类型";
            l2.Dock = DockStyle.Fill;
            l2.TextAlign = ContentAlignment.MiddleLeft;

            _cboType = new ComboBox();
            _cboType.Dock = DockStyle.Fill;
            _cboType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboType.Items.Add("映射为按键 / 功能（如 音量+、F5）");
            _cboType.Items.Add("屏蔽此键（按下无任何反应）");
            _cboType.Items.Add("打开网页");
            _cboType.Items.Add("启动程序");
            _cboType.SelectedIndexChanged += delegate { UpdateTargetPanel(); };

            // ③ 目标区（按类型切换）
            Panel targetHost = new Panel();
            targetHost.Dock = DockStyle.Fill;

            // —— 按键 / 功能 ——
            _panelKey = new Panel();
            _panelKey.Dock = DockStyle.Fill;
            Label lk = new Label();
            lk.Text = "在下方选择目标（可直接输入关键字筛选，如：音量、F5、回车）";
            lk.Dock = DockStyle.Top;
            lk.Height = 24;
            _tbFilter = new TextBox();
            _tbFilter.Dock = DockStyle.Top;
            _tbFilter.Height = 26;
            _tbFilter.TextChanged += delegate { RefreshTargets(); };
            _lbTargets = new ListBox();
            _lbTargets.Dock = DockStyle.Fill;
            _lbTargets.IntegralHeight = false;
            _lbTargets.DoubleClick += delegate { OkClick(); };
            _panelKey.Controls.Add(_lbTargets);
            _panelKey.Controls.Add(_tbFilter);
            _panelKey.Controls.Add(lk);

            // —— 打开网页 ——
            _panelUrl = new Panel();
            _panelUrl.Dock = DockStyle.Fill;
            Label lu = new Label();
            lu.Text = "输入要打开的网址（缺少协议时会自动补全 https://）";
            lu.Dock = DockStyle.Top;
            lu.Height = 24;
            _tbUrl = new TextBox();
            _tbUrl.Dock = DockStyle.Top;
            _tbUrl.Height = 32;
            _tbUrl.Font = new Font("Microsoft YaHei UI", 11F);
            _tbUrl.Text = "https://";
            Label lu2 = new Label();
            lu2.Text = "示例：www.bing.com、https://github.com";
            lu2.Dock = DockStyle.Top;
            lu2.Height = 24;
            lu2.ForeColor = Color.Gray;
            _panelUrl.Controls.Add(lu2);
            _panelUrl.Controls.Add(_tbUrl);
            _panelUrl.Controls.Add(lu);

            // —— 启动程序 ——
            _panelApp = new Panel();
            _panelApp.Dock = DockStyle.Fill;
            Label la = new Label();
            la.Text = "选择要启动的程序（exe / 快捷方式 / 批处理）";
            la.Dock = DockStyle.Top;
            la.Height = 24;
            TableLayoutPanel appRow = new TableLayoutPanel();
            appRow.Dock = DockStyle.Top;
            appRow.Height = 34;
            appRow.ColumnCount = 2;
            appRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            appRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            _tbApp = new TextBox();
            _tbApp.Dock = DockStyle.Fill;
            Button btnBrowse = new Button();
            btnBrowse.Text = "浏览…";
            btnBrowse.Dock = DockStyle.Fill;
            btnBrowse.Click += delegate
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Title = "选择要启动的程序";
                    ofd.Filter = "程序与快捷方式 (*.exe;*.lnk;*.bat;*.cmd)|*.exe;*.lnk;*.bat;*.cmd|所有文件 (*.*)|*.*";
                    if (ofd.ShowDialog(this) == DialogResult.OK) _tbApp.Text = ofd.FileName;
                }
            };
            appRow.Controls.Add(_tbApp, 0, 0);
            appRow.Controls.Add(btnBrowse, 1, 0);
            Label la2 = new Label();
            la2.Text = "示例：C:\\Windows\\notepad.exe（也可选 .lnk 快捷方式）";
            la2.Dock = DockStyle.Top;
            la2.Height = 24;
            la2.ForeColor = Color.Gray;
            _panelApp.Controls.Add(la2);
            _panelApp.Controls.Add(appRow);
            _panelApp.Controls.Add(la);

            // —— 屏蔽 ——
            _panelBlock = new Panel();
            _panelBlock.Dock = DockStyle.Fill;
            Label lb = new Label();
            lb.Text = "按下该键后将没有任何反应。\r\n适合屏蔽容易误触的按键，如 CapsLock、Win 键等。";
            lb.Dock = DockStyle.Fill;
            lb.TextAlign = ContentAlignment.TopLeft;
            lb.ForeColor = Color.FromArgb(90, 90, 90);
            _panelBlock.Controls.Add(lb);

            targetHost.Controls.Add(_panelKey);
            targetHost.Controls.Add(_panelUrl);
            targetHost.Controls.Add(_panelApp);
            targetHost.Controls.Add(_panelBlock);

            // 按钮
            FlowLayoutPanel btnPanel = new FlowLayoutPanel();
            btnPanel.Dock = DockStyle.Fill;
            btnPanel.FlowDirection = FlowDirection.RightToLeft;
            btnPanel.Padding = new Padding(0, 10, 0, 0);

            Button btnOk = new Button();
            btnOk.Text = "确定";
            btnOk.Width = 90;
            btnOk.Height = 30;
            btnOk.Click += delegate { OkClick(); };

            Button btnCancel = new Button();
            btnCancel.Text = "取消";
            btnCancel.Width = 90;
            btnCancel.Height = 30;
            btnCancel.Margin = new Padding(8, 0, 0, 0);
            btnCancel.Click += delegate { DialogResult = DialogResult.Cancel; };

            btnPanel.Controls.Add(btnOk);
            btnPanel.Controls.Add(btnCancel);

            root.Controls.Add(l1, 0, 0);
            root.Controls.Add(_tbFrom, 0, 1);
            root.Controls.Add(l2, 0, 2);
            root.Controls.Add(_cboType, 0, 3);
            root.Controls.Add(targetHost, 0, 4);
            root.Controls.Add(btnPanel, 0, 5);

            Controls.Add(root);
            RefreshTargets();

            // 编辑模式：按原有配置预填
            if (initial != null)
            {
                _from = initial.FromKey;
                _tbFrom.Text = KeyNames.GetName(_from);
                _tbFrom.ForeColor = Color.Black;
                int t = initial.ActionType;
                if (t >= 0 && t < _cboType.Items.Count) _cboType.SelectedIndex = t;
                if (t == 0) SelectTarget(initial.ToKey);
                else if (t == 2) _tbUrl.Text = string.IsNullOrEmpty(initial.ActionArg) ? "https://" : initial.ActionArg;
                else if (t == 3) _tbApp.Text = initial.ActionArg ?? "";
            }
            else
            {
                _cboType.SelectedIndex = 0;
            }
        }

        /* 按功能类型切换目标面板 */
        private void UpdateTargetPanel()
        {
            int t = _cboType.SelectedIndex;
            if (t < 0) t = 0;
            _panelKey.Visible = (t == 0);
            _panelUrl.Visible = (t == 2);
            _panelApp.Visible = (t == 3);
            _panelBlock.Visible = (t == 1);
        }

        /* 通过全局钩子捕获来源键（普通控件收不到媒体键） */
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _hook.KeyEvent += OnHookKey;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _hook.KeyEvent -= OnHookKey;
            base.OnFormClosed(e);
        }

        private void OnHookKey(Keys vk, bool down)
        {
            if (!down || !_capturing) return;
            if (vk == Keys.Escape)   // Esc 用于取消捕获
            {
                _capturing = false;
                _tbFrom.Text = "（点击后在此按下要映射的按键）";
                _tbFrom.ForeColor = Color.Gray;
                return;
            }
            _capturing = false;
            _from = vk;
            _tbFrom.Text = KeyNames.GetName(vk);
            _tbFrom.ForeColor = Color.Black;
            _tbFilter.Focus();
        }

        private void RefreshTargets()
        {
            string kw = _tbFilter.Text.Trim().ToLower();
            _lbTargets.BeginUpdate();
            _lbTargets.Items.Clear();
            foreach (TargetDef t in _allTargets)
            {
                if (kw.Length == 0 ||
                    t.Name.ToLower().Contains(kw) ||
                    t.Category.ToLower().Contains(kw) ||
                    t.Key.ToString().ToLower().Contains(kw))
                {
                    _lbTargets.Items.Add(t);
                }
            }
            _lbTargets.EndUpdate();
            if (_lbTargets.Items.Count > 0) _lbTargets.SelectedIndex = 0;
        }

        /* 编辑时：选中列表中与 ToKey 对应的项；若不在预设列表（旧配置）则动态加入 */
        private void SelectTarget(Keys toKey)
        {
            foreach (TargetDef t in _allTargets)
            {
                if (t.Key == toKey)
                {
                    _lbTargets.SelectedItem = t;
                    return;
                }
            }
            TargetDef custom = new TargetDef(toKey, KeyNames.GetName(toKey), "自定义");
            _allTargets.Add(custom);
            RefreshTargets();
            _lbTargets.SelectedItem = custom;
        }

        private void OkClick()
        {
            if (_from == Keys.None)
            {
                MessageBox.Show(this, "请先按下要映射的按键。", "提示");
                _tbFrom.Focus();
                return;
            }
            int t = _cboType.SelectedIndex;
            if (t == 0)   // 按键 / 功能
            {
                TargetDef sel = _lbTargets.SelectedItem as TargetDef;
                if (sel == null)
                {
                    MessageBox.Show(this, "请先在列表中选择新功能 / 目标按键。", "提示");
                    return;
                }
                Result = new KeyMapping { FromKey = _from, ToKey = sel.Key, ActionType = 0, ActionArg = "" };
            }
            else if (t == 1)   // 屏蔽
            {
                Result = new KeyMapping { FromKey = _from, ToKey = Keys.None, ActionType = 1, ActionArg = "" };
            }
            else if (t == 2)   // 打开网页
            {
                string url = _tbUrl.Text.Trim();
                if (url.Length == 0 || url == "https://" || url == "http://")
                {
                    MessageBox.Show(this, "请输入要打开的网址。", "提示");
                    _tbUrl.Focus();
                    return;
                }
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                    url = "https://" + url;
                Result = new KeyMapping { FromKey = _from, ToKey = Keys.None, ActionType = 2, ActionArg = url };
            }
            else if (t == 3)   // 启动程序
            {
                string app = _tbApp.Text.Trim().Trim('"');
                if (app.Length == 0)
                {
                    MessageBox.Show(this, "请选择要启动的程序。", "提示");
                    _tbApp.Focus();
                    return;
                }
                if (!File.Exists(app) && !Directory.Exists(app))
                {
                    if (MessageBox.Show(this, "该路径不存在，仍然保存吗？", "提示",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                        return;
                }
                Result = new KeyMapping { FromKey = _from, ToKey = Keys.None, ActionType = 3, ActionArg = app };
            }
            DialogResult = DialogResult.OK;
        }

        private static List<TargetDef> BuildTargets()
        {
            List<TargetDef> list = new List<TargetDef>();
            // 媒体控制
            list.Add(new TargetDef(Keys.VolumeUp, "音量增大", "媒体控制"));
            list.Add(new TargetDef(Keys.VolumeDown, "音量减小", "媒体控制"));
            list.Add(new TargetDef(Keys.VolumeMute, "静音", "媒体控制"));
            list.Add(new TargetDef(Keys.MediaPlayPause, "播放 / 暂停", "媒体控制"));
            list.Add(new TargetDef(Keys.MediaNextTrack, "下一曲", "媒体控制"));
            list.Add(new TargetDef(Keys.MediaPreviousTrack, "上一曲", "媒体控制"));
            list.Add(new TargetDef(Keys.MediaStop, "停止", "媒体控制"));
            // 浏览器
            list.Add(new TargetDef(Keys.BrowserBack, "后退", "浏览器"));
            list.Add(new TargetDef(Keys.BrowserForward, "前进", "浏览器"));
            list.Add(new TargetDef(Keys.BrowserRefresh, "刷新", "浏览器"));
            list.Add(new TargetDef(Keys.BrowserStop, "停止加载", "浏览器"));
            list.Add(new TargetDef(Keys.BrowserSearch, "搜索", "浏览器"));
            list.Add(new TargetDef(Keys.BrowserFavorites, "收藏夹", "浏览器"));
            list.Add(new TargetDef(Keys.BrowserHome, "主页", "浏览器"));
            // 应用程序
            list.Add(new TargetDef(Keys.LaunchMail, "电子邮件", "应用程序"));
            list.Add(new TargetDef(KeyConsts.LaunchApp1, "我的电脑", "应用程序"));
            list.Add(new TargetDef(KeyConsts.LaunchApp2, "计算器", "应用程序"));
            list.Add(new TargetDef(KeyConsts.LaunchMedia, "媒体播放器", "应用程序"));
            // 特殊
            list.Add(new TargetDef(Keys.None, "屏蔽此键（无操作）", "特殊"));
            // 功能键
            for (int i = 1; i <= 24; i++)
                list.Add(new TargetDef((Keys)(0x6F + i), "F" + i, "功能键"));
            // 系统 / 编辑键
            list.Add(new TargetDef(Keys.Escape, "Esc", "系统键"));
            list.Add(new TargetDef(Keys.PrintScreen, "PrtSc（截图）", "系统键"));
            list.Add(new TargetDef(Keys.Scroll, "ScrollLock", "系统键"));
            list.Add(new TargetDef(Keys.Pause, "Pause", "系统键"));
            list.Add(new TargetDef(Keys.Insert, "Insert", "系统键"));
            list.Add(new TargetDef(Keys.Delete, "Delete", "系统键"));
            list.Add(new TargetDef(Keys.Home, "Home", "系统键"));
            list.Add(new TargetDef(Keys.End, "End", "系统键"));
            list.Add(new TargetDef(Keys.PageUp, "PageUp（上翻页）", "系统键"));
            list.Add(new TargetDef(Keys.PageDown, "PageDown（下翻页）", "系统键"));
            list.Add(new TargetDef(Keys.Left, "← 左方向", "系统键"));
            list.Add(new TargetDef(Keys.Right, "→ 右方向", "系统键"));
            list.Add(new TargetDef(Keys.Up, "↑ 上方向", "系统键"));
            list.Add(new TargetDef(Keys.Down, "↓ 下方向", "系统键"));
            list.Add(new TargetDef(Keys.Back, "退格 Backspace", "系统键"));
            list.Add(new TargetDef(Keys.Return, "回车 Enter", "系统键"));
            list.Add(new TargetDef(Keys.Tab, "Tab", "系统键"));
            list.Add(new TargetDef(Keys.CapsLock, "CapsLock 大写锁定", "系统键"));
            list.Add(new TargetDef(Keys.Space, "空格", "系统键"));
            list.Add(new TargetDef(Keys.NumLock, "NumLock 小键盘锁定", "系统键"));
            list.Add(new TargetDef(Keys.Sleep, "睡眠 Sleep（慎用）", "系统键"));
            list.Add(new TargetDef(Keys.LWin, "Win（左）", "系统键"));
            list.Add(new TargetDef(Keys.RWin, "Win（右）", "系统键"));
            list.Add(new TargetDef(Keys.Apps, "菜单键", "系统键"));
            list.Add(new TargetDef(Keys.LShiftKey, "Shift（左）", "系统键"));
            list.Add(new TargetDef(Keys.RShiftKey, "Shift（右）", "系统键"));
            list.Add(new TargetDef(Keys.LControlKey, "Ctrl（左）", "系统键"));
            list.Add(new TargetDef(Keys.RControlKey, "Ctrl（右）", "系统键"));
            list.Add(new TargetDef(Keys.LMenu, "Alt（左）", "系统键"));
            list.Add(new TargetDef(Keys.RMenu, "Alt（右）", "系统键"));
            // 小键盘
            for (int i = 0; i <= 9; i++)
                list.Add(new TargetDef((Keys)(0x60 + i), "小键盘" + i, "小键盘"));
            list.Add(new TargetDef(Keys.Decimal, "小键盘小数点", "小键盘"));
            list.Add(new TargetDef(Keys.Add, "小键盘 +", "小键盘"));
            list.Add(new TargetDef(Keys.Subtract, "小键盘 -", "小键盘"));
            list.Add(new TargetDef(Keys.Multiply, "小键盘 *", "小键盘"));
            list.Add(new TargetDef(Keys.Divide, "小键盘 /", "小键盘"));
            list.Add(new TargetDef(Keys.Return, "小键盘回车", "小键盘"));
            // 字母
            for (int i = 0; i < 26; i++)
                list.Add(new TargetDef((Keys)(0x41 + i), ((char)(0x41 + i)).ToString(), "字母键"));
            // 数字
            for (int i = 0; i <= 9; i++)
                list.Add(new TargetDef((Keys)(0x30 + i), i.ToString(), "数字键"));
            // 符号
            list.Add(new TargetDef(Keys.Oemtilde, "` ~", "符号键"));
            list.Add(new TargetDef(Keys.OemMinus, "- _", "符号键"));
            list.Add(new TargetDef(Keys.Oemplus, "= +", "符号键"));
            list.Add(new TargetDef(Keys.OemOpenBrackets, "[ {", "符号键"));
            list.Add(new TargetDef(Keys.OemCloseBrackets, "] }", "符号键"));
            list.Add(new TargetDef(Keys.OemPipe, "\\ |", "符号键"));
            list.Add(new TargetDef(Keys.OemSemicolon, "; :", "符号键"));
            list.Add(new TargetDef(Keys.OemQuotes, "' \"", "符号键"));
            list.Add(new TargetDef(Keys.Oemcomma, ", <", "符号键"));
            list.Add(new TargetDef(Keys.OemPeriod, ". >", "符号键"));
            list.Add(new TargetDef(Keys.OemQuestion, "/ ?", "符号键"));
            return list;
        }
    }

    /* ==================== 注入自检（诊断） ==================== */
    internal static class InjectSelfTest
    {
        /* 音量读取（COM） */
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorComObject { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr ppDevice);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IntPtr ppDevice);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr pActivationParams, out IntPtr ppInterface);
        }

        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            [PreserveSig] int RegisterControlChangeNotify(IntPtr pNotify);
            [PreserveSig] int UnregisterControlChangeNotify(IntPtr pNotify);
            [PreserveSig] int GetChannelCount(out uint pnChannelCount);
            [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
            [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
            [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
            [PreserveSig] int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
            [PreserveSig] int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
            [PreserveSig] int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
            [PreserveSig] int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
            [PreserveSig] int SetMute(bool bMute, ref Guid pguidEventContext);
            [PreserveSig] int GetMute(out bool pbMute);
            [PreserveSig] int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
            [PreserveSig] int VolumeStepUp(ref Guid pguidEventContext);
            [PreserveSig] int VolumeStepDown(ref Guid pguidEventContext);
            [PreserveSig] int QueryHardwareSupport(out uint pdwHardwareSupportMask);
            [PreserveSig] int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
        }

        public static string Run()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("【注入自检】");
            sb.AppendLine("说明：程序模拟按下一个无害按键(F13)和音量+键，检测系统是否收到。");
            sb.AppendLine("注意：若电脑处于锁屏状态，Windows 会禁止注入输入，请解锁后再测。");
            sb.AppendLine();

            // 1) 普通键注入检测（F13 = 0x7C，无副作用）
            bool keyDown = false;
            uint r1 = InjectKey(0x7C, false, false);
            for (int i = 0; i < 15; i++)
            {
                if ((Native.GetAsyncKeyState(0x7C) & 0x8000) != 0) { keyDown = true; break; }
                Thread.Sleep(20);
            }
            uint r2 = InjectKey(0x7C, true, false);
            sb.AppendLine("1) 注入普通键 F13：SendInput 返回 " + r1 + " / " + r2 +
                "，" + (keyDown ? "系统已识别按键按下（注入生效）" : "系统未识别（注入被拦截）"));

            // 2) 音量键注入检测（扫描码 E030）
            uint r3 = InjectKey(0xAF, false, true);
            bool volKeyDown = false;
            for (int i = 0; i < 15; i++)
            {
                if ((Native.GetAsyncKeyState(0xAF) & 0x8000) != 0) { volKeyDown = true; break; }
                Thread.Sleep(20);
            }
            uint r4 = InjectKey(0xAF, true, true);
            sb.AppendLine("2) 注入音量+键：SendInput 返回 " + r3 + " / " + r4 +
                "，" + (volKeyDown ? "系统已识别音量键按下" : "系统未识别音量键（部分系统不记录媒体键状态，属正常）"));

            // 3) 真实音量变化检测
            IAudioEndpointVolume vol = GetVolumeEndpoint();
            if (vol == null)
            {
                sb.AppendLine("3) 无法获取系统音量接口，跳过音量变化检测。");
            }
            else
            {
                float before = ReadVol(vol);
                Guid g = Guid.Empty;
                vol.SetMute(false, ref g);
                float low = Math.Min(0.30f, Math.Max(0.05f, before - 0.05f));
                vol.SetMasterVolumeLevelScalar(low, ref g);
                InjectKey(0xAF, false, true);
                InjectKey(0xAF, true, true);
                Thread.Sleep(300);
                float after = ReadVol(vol);
                vol.SetMasterVolumeLevelScalar(before, ref g);
                sb.AppendLine(string.Format("3) 真实音量变化：{0:F2} → {1:F2}，" +
                    (after > low + 0.001 ? "音量确实增大了（映射到音量键可用！）" : "音量未变化（桌面可能被锁定，或系统策略拦截）"),
                    low, after));
            }
            sb.AppendLine();
            sb.AppendLine("判定：若第1项显示“注入生效”，说明映射引擎工作正常；");
            sb.AppendLine("若被拦截：请通过托盘菜单【以管理员身份重启】，或解锁电脑后重试。");
            return sb.ToString();
        }

        private static uint InjectKey(uint vk, bool keyUp, bool mediaKey)
        {
            Native.INPUT input = new Native.INPUT();
            input.type = Native.INPUT_KEYBOARD;
            input.U.ki.wVk = (ushort)vk;
            bool extended = false;
            ushort scan = 0;
            if (mediaKey) { scan = 0x30; extended = true; }          // VolumeUp = E0 30
            else scan = (ushort)Native.MapVirtualKey(vk, 0);
            if (scan != 0) { input.U.ki.wScan = scan; input.U.ki.dwFlags |= Native.KEYEVENTF_SCANCODE; }
            if (extended) input.U.ki.dwFlags |= Native.KEYEVENTF_EXTENDEDKEY;
            if (keyUp) input.U.ki.dwFlags |= Native.KEYEVENTF_KEYUP;
            Native.INPUT[] arr = new Native.INPUT[1];
            arr[0] = input;
            return Native.SendInput(1, arr, Marshal.SizeOf(typeof(Native.INPUT)));
        }

        private static IAudioEndpointVolume GetVolumeEndpoint()
        {
            try
            {
                IMMDeviceEnumerator e = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                IntPtr dev;
                if (e.GetDefaultAudioEndpoint(0, 1, out dev) != 0) return null;
                Guid iid = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
                IntPtr pv;
                IMMDevice d = (IMMDevice)Marshal.GetObjectForIUnknown(dev);
                if (d.Activate(ref iid, 1, IntPtr.Zero, out pv) != 0) return null;
                return (IAudioEndpointVolume)Marshal.GetObjectForIUnknown(pv);
            }
            catch { return null; }
        }

        private static float ReadVol(IAudioEndpointVolume vol)
        {
            float f = -1f;
            try { vol.GetMasterVolumeLevelScalar(out f); } catch { }
            return f;
        }
    }

    /* ==================== 主窗口 ==================== */
    public class MainForm : Form
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "KeyMapper";

        private readonly KeyboardHook _hook = new KeyboardHook();
        private readonly List<KeyMapping> _mappings = new List<KeyMapping>();
        private readonly string _configPath;
        private readonly bool _startMin;

        private ListView _lv;
        private Button _btnToggle;
        private CheckBox _chkStartup;
        private Label _lblStatus;
        private NotifyIcon _tray;
        private ToolStripMenuItem _trayToggle;
        private TestForm _testForm;
        private bool _enabled = true;
        private bool _trayHintShown;

        public MainForm(bool startMin)
        {
            _startMin = startMin;
            _configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "KeyMapper", "mappings.xml");

            BuildUi();
            BuildTray();
            LoadMappings();
            _chkStartup.Checked = IsAutoStart();
            UpdateStatus();

            // 关键：把钩子收到的按键转发给按键测试窗口
            _hook.KeyEvent += OnKeyEvent;
        }

        private void OnKeyEvent(Keys vk, bool down)
        {
            if (_testForm != null && !_testForm.IsDisposed && _testForm.Visible)
                _testForm.HandleKey(vk, down);
        }

        /* ---------- 界面 ---------- */
        private void BuildUi()
        {
            Text = "按键映射助手";
            Font = new Font("Microsoft YaHei UI", 9F);
            ClientSize = new Size(920, 450);
            MinimumSize = new Size(840, 400);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = IconFactory.GetIcon();

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(10);
            root.ColumnCount = 1;
            root.RowCount = 5;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

            Label hint = new Label();
            hint.Text = "用法：【添加映射】可选择：映射为按键/功能（如 PageUp → 音量+）、打开网页、启动程序、屏蔽按键；【按键测试】实时高亮；【注入自检】检测是否被系统接收。";
            hint.Dock = DockStyle.Fill;
            hint.ForeColor = Color.FromArgb(80, 80, 80);
            hint.TextAlign = ContentAlignment.MiddleLeft;

            _lv = new ListView();
            _lv.Dock = DockStyle.Fill;
            _lv.View = View.Details;
            _lv.FullRowSelect = true;
            _lv.GridLines = true;
            _lv.HideSelection = false;
            _lv.Columns.Add("原按键", 150);
            _lv.Columns.Add("映射为 / 功能", 400);
            _lv.Columns.Add("类型", 80);
            _lv.DoubleClick += delegate { EditSelected(); };

            Button btnAdd = MakeButton("＋ 添加映射", 112);
            btnAdd.Click += delegate
            {
                using (AddMappingForm dlg = new AddMappingForm(_hook))
                {
                    bool wasEnabled = _hook.Enabled;
                    _hook.Enabled = false;   // 配置过程中暂停映射，避免干扰输入
                    if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Result != null)
                    {
                        AddOrReplace(dlg.Result);
                    }
                    _hook.Enabled = wasEnabled;
                }
            };

            Button btnDelete = MakeButton("删除选中", 96);
            btnDelete.Click += delegate { DeleteSelected(); };

            Button btnEdit = MakeButton("编辑选中", 96);
            btnEdit.Click += delegate { EditSelected(); };

            Button btnClear = MakeButton("清空全部", 96);
            btnClear.Click += delegate
            {
                if (_mappings.Count == 0) return;
                if (MessageBox.Show(this, "确定清空所有映射吗？", "确认",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _mappings.Clear();
                    _hook.SetMappings(_mappings);
                    SaveMappings();
                    RefreshList();
                    UpdateStatus();
                }
            };

            Button btnTest = MakeButton("按键测试", 96);
            btnTest.Click += delegate { OpenTest(); };

            Button btnSelfTest = MakeButton("注入自检", 96);
            btnSelfTest.Click += delegate { RunSelfTest(); };

            _btnToggle = MakeButton("停用映射", 96);
            _btnToggle.Click += delegate { ToggleEnabled(); };

            // 按钮分两组布局，保证全部完整显示不被裁剪：左=映射管理，右=工具
            TableLayoutPanel btnTable = new TableLayoutPanel();
            btnTable.Dock = DockStyle.Fill;
            btnTable.ColumnCount = 2;
            btnTable.RowCount = 1;
            btnTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            btnTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));

            FlowLayoutPanel flowManage = new FlowLayoutPanel();
            flowManage.Dock = DockStyle.Fill;
            flowManage.FlowDirection = FlowDirection.LeftToRight;
            flowManage.Margin = new Padding(0);
            flowManage.Padding = new Padding(0, 6, 0, 0);
            flowManage.Controls.AddRange(new Control[] { btnAdd, btnEdit, btnDelete, btnClear });

            FlowLayoutPanel flowTools = new FlowLayoutPanel();
            flowTools.Dock = DockStyle.Fill;
            flowTools.FlowDirection = FlowDirection.LeftToRight;
            flowTools.Margin = new Padding(0);
            flowTools.Padding = new Padding(0, 6, 0, 0);
            flowTools.Controls.AddRange(new Control[] { btnTest, btnSelfTest, _btnToggle });

            btnTable.Controls.Add(flowManage, 0, 0);
            btnTable.Controls.Add(flowTools, 1, 0);

            FlowLayoutPanel optFlow = new FlowLayoutPanel();
            optFlow.Dock = DockStyle.Fill;
            optFlow.Padding = new Padding(0, 8, 0, 0);

            _chkStartup = new CheckBox();
            _chkStartup.Text = "开机自动启动（随系统启动，后台运行）";
            _chkStartup.AutoSize = true;
            _chkStartup.CheckedChanged += delegate { SetAutoStart(_chkStartup.Checked); };
            optFlow.Controls.Add(_chkStartup);

            _lblStatus = new Label();
            _lblStatus.Dock = DockStyle.Fill;
            _lblStatus.ForeColor = Color.Gray;
            _lblStatus.TextAlign = ContentAlignment.MiddleLeft;

            root.Controls.Add(hint, 0, 0);
            root.Controls.Add(_lv, 0, 1);
            root.Controls.Add(btnTable, 0, 2);
            root.Controls.Add(optFlow, 0, 3);
            root.Controls.Add(_lblStatus, 0, 4);

            Controls.Add(root);
        }

        private static Button MakeButton(string text, int width)
        {
            Button b = new Button();
            b.Text = text;
            b.Width = width;
            b.Height = 30;
            b.Margin = new Padding(0, 0, 8, 0);
            return b;
        }

        /* ---------- 托盘 ---------- */
        private void BuildTray()
        {
            _tray = new NotifyIcon();
            _tray.Icon = IconFactory.GetIcon();
            _tray.Text = "按键映射助手";
            _tray.Visible = true;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("打开主界面", null, delegate { ShowMain(); });
            _trayToggle = new ToolStripMenuItem("停用映射");
            _trayToggle.Checked = true;
            _trayToggle.Click += delegate { ToggleEnabled(); };
            menu.Items.Add(_trayToggle);
            menu.Items.Add("以管理员身份重启", null, delegate { RelaunchAsAdmin(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { Application.Exit(); });

            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += delegate { ShowMain(); };
        }

        private void ShowMain()
        {
            Show();
            ShowInTaskbar = true;
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
        }

        private void RelaunchAsAdmin()
        {
            try
            {
                SaveMappings();
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = Application.ExecutablePath;
                psi.Verb = "runas";
                psi.UseShellExecute = true;
                psi.Arguments = "-min";
                Process.Start(psi);
                Application.Exit();
            }
            catch
            {
                MessageBox.Show(this,
                    "未能以管理员身份启动（可能已被取消）。\r\n普通模式下，对普通程序（非管理员）的映射仍然有效；\r\n只有当目标程序以管理员运行时才需要管理员权限。",
                    "提示");
            }
        }

        /* ---------- 开机自启 ---------- */
        private bool IsAutoStart()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
            {
                return k != null && k.GetValue(RunValueName) != null;
            }
        }

        private void SetAutoStart(bool enable)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (enable)
                    {
                        string exe = Application.ExecutablePath;
                        string destDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "KeyMapper");
                        string dest = Path.Combine(destDir, "KeyMapper.exe");
                        if (!string.Equals(exe, dest, StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                Directory.CreateDirectory(destDir);
                                File.Copy(exe, dest, true);
                                exe = dest;
                            }
                            catch { }
                        }
                        k.SetValue(RunValueName, "\"" + exe + "\" -min");
                        _lblStatus.Text = "已开启开机自启（程序位于 " + exe + "）";
                    }
                    else
                    {
                        k.DeleteValue(RunValueName, false);
                        _lblStatus.Text = "已关闭开机自启";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "设置开机自启失败：" + ex.Message, "提示");
            }
            UpdateStatus();
        }

        /* ---------- 配置读写 ---------- */
        private void SaveMappings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_configPath));
                using (FileStream fs = File.Create(_configPath))
                {
                    XmlSerializer ser = new XmlSerializer(typeof(List<KeyMapping>));
                    ser.Serialize(fs, _mappings);
                }
            }
            catch { }
        }

        private void LoadMappings()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    using (FileStream fs = File.OpenRead(_configPath))
                    {
                        XmlSerializer ser = new XmlSerializer(typeof(List<KeyMapping>));
                        List<KeyMapping> list = (List<KeyMapping>)ser.Deserialize(fs);
                        _mappings.Clear();
                        if (list != null) _mappings.AddRange(list);
                    }
                }
            }
            catch { }
            // 兼容旧版本配置：旧的屏蔽映射（ToKey=None 且无动作类型）迁移为屏蔽动作
            foreach (KeyMapping m in _mappings)
            {
                if (m.ActionType == 0 && m.ToKey == Keys.None) m.ActionType = 1;
                if (m.ActionArg == null) m.ActionArg = "";
            }
            _hook.SetMappings(_mappings);
            RefreshList();
        }

        private void RefreshList()
        {
            _lv.BeginUpdate();
            _lv.Items.Clear();
            foreach (KeyMapping m in _mappings)
            {
                ListViewItem item = new ListViewItem(m.FromName);
                item.SubItems.Add(m.ActionName);
                item.SubItems.Add(m.TypeName);
                _lv.Items.Add(item);
            }
            _lv.EndUpdate();
        }

        private void AddOrReplace(KeyMapping m)
        {
            for (int i = 0; i < _mappings.Count; i++)
            {
                if (_mappings[i].FromKey == m.FromKey)
                {
                    _mappings[i] = m;
                    _hook.SetMappings(_mappings);
                    SaveMappings();
                    RefreshList();
                    UpdateStatus();
                    return;
                }
            }
            _mappings.Add(m);
            _hook.SetMappings(_mappings);
            SaveMappings();
            RefreshList();
            UpdateStatus();
        }

        /* 编辑选中的映射（支持双击列表） */
        private void EditSelected()
        {
            if (_lv.SelectedItems.Count == 0)
            {
                MessageBox.Show(this, "请先在列表中选择要修改的映射。", "提示");
                return;
            }
            KeyMapping m = _mappings[_lv.SelectedItems[0].Index];
            using (AddMappingForm dlg = new AddMappingForm(_hook, m))
            {
                bool wasEnabled = _hook.Enabled;
                _hook.Enabled = false;   // 配置过程中暂停映射
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Result != null)
                {
                    AddOrReplace(dlg.Result);
                }
                _hook.Enabled = wasEnabled;
            }
        }

        private void DeleteSelected()
        {
            if (_lv.SelectedItems.Count == 0) return;
            List<Keys> toRemove = new List<Keys>();
            foreach (ListViewItem it in _lv.SelectedItems)
                toRemove.Add(_mappings[it.Index].FromKey);

            foreach (Keys k in toRemove)
            {
                for (int i = _mappings.Count - 1; i >= 0; i--)
                    if (_mappings[i].FromKey == k) _mappings.RemoveAt(i);
            }
            _hook.SetMappings(_mappings);
            SaveMappings();
            RefreshList();
            UpdateStatus();
        }

        /* ---------- 状态 ---------- */
        private void ToggleEnabled()
        {
            _enabled = !_enabled;
            _hook.Enabled = _enabled;
            _btnToggle.Text = _enabled ? "停用映射" : "启用映射";
            _trayToggle.Text = _enabled ? "停用映射" : "启用映射";
            _trayToggle.Checked = _enabled;
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            string extra = "";
            if (_hook.InjectFailures > 0)
                extra = " ｜ 注入失败 " + _hook.InjectFailures + " 次（若映射无效，请解锁屏幕或托盘菜单【以管理员身份重启】）";
            _lblStatus.Text = "共 " + _mappings.Count + " 条映射 ｜ " +
                (_enabled ? "已启用（映射生效中）" : "已停用（按键恢复原功能）") + extra;
        }

        /* ---------- 按键测试 / 注入自检 ---------- */
        private void OpenTest()
        {
            if (_testForm == null || _testForm.IsDisposed)
                _testForm = new TestForm();
            _testForm.Show();
            _testForm.Activate();
        }

        private void RunSelfTest()
        {
            bool wasEnabled = _hook.Enabled;
            _hook.Enabled = false;
            _lblStatus.Text = "正在运行注入自检…";
            string result = InjectSelfTest.Run();
            _hook.Enabled = wasEnabled;
            UpdateStatus();

            Form f = new Form();
            f.Text = "注入自检结果";
            f.ClientSize = new Size(560, 420);
            f.StartPosition = FormStartPosition.CenterParent;
            f.Font = new Font("Microsoft YaHei UI", 9F);
            f.Icon = IconFactory.GetIcon();
            TextBox tb = new TextBox();
            tb.Dock = DockStyle.Fill;
            tb.Multiline = true;
            tb.ReadOnly = true;
            tb.ScrollBars = ScrollBars.Vertical;
            tb.Text = result;
            tb.BackColor = Color.White;
            Button btn = new Button();
            btn.Text = "关闭";
            btn.Width = 90;
            btn.Height = 30;
            btn.Dock = DockStyle.Bottom;
            btn.Click += delegate { f.Close(); };
            f.Controls.Add(tb);
            f.Controls.Add(btn);
            f.ShowDialog(this);
        }

        /* ---------- 生命周期 ---------- */
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try
            {
                _hook.Install();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message + "\r\n按键映射功能不可用。", "按键映射助手",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            if (_startMin)
            {
                Hide();
                ShowInTaskbar = false;
                if (!_trayHintShown)
                {
                    _tray.ShowBalloonTip(2500, "按键映射助手",
                        "已随系统启动，在后台运行。右键托盘图标可管理。", ToolTipIcon.Info);
                    _trayHintShown = true;
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                ShowInTaskbar = false;
                if (!_trayHintShown)
                {
                    _tray.ShowBalloonTip(2000, "按键映射助手",
                        "已最小化到系统托盘，右键图标可退出。", ToolTipIcon.Info);
                    _trayHintShown = true;
                }
            }
        }

        /* ---------- 关闭行为（叉 / Alt+F4） ---------- */
        private const string BehaviorKey = @"Software\KeyMapper";
        private const string BehaviorValue = "CloseBehavior";

        private int GetCloseBehavior()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(BehaviorKey, false))
                {
                    if (k == null) return 0;
                    object v = k.GetValue(BehaviorValue);
                    int b;
                    if (v != null && int.TryParse(v.ToString(), out b)) return b;
                }
            }
            catch { }
            return 0;
        }

        private void SetCloseBehavior(int b)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(BehaviorKey))
                {
                    k.SetValue(BehaviorValue, b);
                }
            }
            catch { }
        }

        /* 返回：1=最小化到托盘 2=退出程序 0=取消 */
        private int ShowCloseChoiceDialog()
        {
            int result = 0;
            Form f = new Form();
            f.Text = "按键映射助手";
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MaximizeBox = false;
            f.MinimizeBox = false;
            f.StartPosition = FormStartPosition.CenterParent;
            f.ClientSize = new Size(400, 200);
            f.Font = new Font("Microsoft YaHei UI", 9F);
            f.Icon = IconFactory.GetIcon();

            Label lb = new Label();
            lb.Text = "关闭窗口后要做什么？\r\n（程序需在后台运行，按键映射才会生效）";
            lb.Location = new Point(16, 14);
            lb.AutoSize = true;

            CheckBox chk = new CheckBox();
            chk.Text = "记住我的选择，下次不再询问";
            chk.Location = new Point(16, 60);
            chk.AutoSize = true;

            Button btnMin = new Button();
            btnMin.Text = "最小化到托盘";
            btnMin.Size = new Size(112, 32);
            btnMin.Location = new Point(16, 96);
            btnMin.Click += delegate { result = 1; f.DialogResult = DialogResult.OK; };

            Button btnExit = new Button();
            btnExit.Text = "退出程序";
            btnExit.Size = new Size(92, 32);
            btnExit.Location = new Point(144, 96);
            btnExit.Click += delegate { result = 2; f.DialogResult = DialogResult.OK; };

            Button btnCancel = new Button();
            btnCancel.Text = "取消";
            btnCancel.Size = new Size(80, 32);
            btnCancel.Location = new Point(252, 96);
            btnCancel.Click += delegate { result = 0; f.DialogResult = DialogResult.Cancel; };
            f.CancelButton = btnCancel;

            f.Controls.Add(lb);
            f.Controls.Add(chk);
            f.Controls.Add(btnMin);
            f.Controls.Add(btnExit);
            f.Controls.Add(btnCancel);

            f.ShowDialog(this);
            if (result != 0 && chk.Checked) SetCloseBehavior(result);
            return result;
        }

        private void MinimizeToTray()
        {
            WindowState = FormWindowState.Minimized;
            Hide();
            ShowInTaskbar = false;
            if (!_trayHintShown)
            {
                _tray.ShowBalloonTip(2000, "按键映射助手",
                    "已最小化到系统托盘，右键图标可管理。", ToolTipIcon.Info);
                _trayHintShown = true;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 用户点击窗口关闭按钮 / Alt+F4 / 外部关闭请求（如任务管理器）时询问；
            // 托盘“退出”或程序自行退出（CloseReason.ApplicationExitCall）不询问
            CloseReason reason = e.CloseReason;
            bool userInitiated = (reason == CloseReason.UserClosing ||
                                  reason == CloseReason.TaskManagerClosing);
            if (userInitiated)
            {
                int behavior = GetCloseBehavior();
                if (behavior == 0)
                {
                    int choice = ShowCloseChoiceDialog();
                    if (choice == 1)      // 最小化到托盘
                    {
                        e.Cancel = true;
                        MinimizeToTray();
                        return;
                    }
                    if (choice == 2)      // 退出程序
                    {
                        // 继续执行下方清理并退出
                    }
                    else                  // 取消
                    {
                        e.Cancel = true;
                        return;
                    }
                }
                else if (behavior == 1)   // 记住的：最小化
                {
                    e.Cancel = true;
                    MinimizeToTray();
                    return;
                }
                // behavior == 2 → 记住的：直接退出
            }

            SaveMappings();
            if (_testForm != null && !_testForm.IsDisposed) _testForm.Close();
            _hook.Dispose();
            if (_tray != null) _tray.Dispose();
            base.OnFormClosing(e);
        }
    }

    /* ==================== 程序入口 ==================== */
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "KeyMapper_SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("按键映射助手已经在运行了（可查看系统托盘）。", "按键映射助手");
                    return;
                }

                Native.SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                bool startMin = Array.IndexOf(args, "-min") >= 0;
                Application.Run(new MainForm(startMin));
            }
        }
    }
}
