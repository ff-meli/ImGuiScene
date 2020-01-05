using ImGuiNET;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ImGuiScene
{
    // largely a port of https://github.com/ocornut/imgui/blob/master/examples/imgui_impl_win32.cpp, though some changes
    // and wndproc hooking
    public static class ImGui_Input_Impl_Direct
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")]
        static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);
        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);
        [DllImport("user32.dll")]
        static extern IntPtr GetCapture();
        [DllImport("user32.dll")]
        static extern IntPtr SetCapture(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        static extern short GetKeyState(int nVirtKey);
        [DllImport("user32.dll")]
        static extern IntPtr GetCursor();
        [DllImport("user32.dll")]
        public static extern IntPtr SetCursor(IntPtr handle);
        [DllImport("user32.dll")]
        static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        const int GWLP_WNDPROC = -4;

        [DllImport("user32.dll")]
        static extern long CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, ulong wParam, long lParam);

        delegate long WndProcDelegate(IntPtr hWnd, uint msg, ulong wParam, long lParam);


        private static long _lastTime;
        private static IntPtr _platformNamePtr;
        private static IntPtr _hWnd;
        private static WndProcDelegate _wndProcDelegate;
        private static IntPtr _wndProcPtr;
        private static IntPtr _oldWndProcPtr;
        // private static ImGuiMouseCursor _oldCursor;

        public static void Init(IntPtr hWnd)
        {
            _hWnd = hWnd;

            // hook wndproc
            // have to hold onto the delegate to keep it in memory for unmanaged code
            _wndProcDelegate = WndProcDetour;
            _wndProcPtr = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
            _oldWndProcPtr = SetWindowLongPtr(_hWnd, GWLP_WNDPROC, _wndProcPtr);

            var io = ImGui.GetIO();

            io.BackendFlags = io.BackendFlags | (ImGuiBackendFlags.HasMouseCursors | ImGuiBackendFlags.HasSetMousePos);

            _platformNamePtr = Marshal.StringToHGlobalAnsi("imgui_impl_win32_c#");
            unsafe
            {
                io.NativePtr->BackendPlatformName = (byte*)_platformNamePtr.ToPointer();
            }

            io.ImeWindowHandle = _hWnd;

            io.KeyMap[(int)ImGuiKey.Tab] = (int)VirtualKeys.Tab;
            io.KeyMap[(int)ImGuiKey.LeftArrow] = (int)VirtualKeys.Left;
            io.KeyMap[(int)ImGuiKey.RightArrow] = (int)VirtualKeys.Right;
            io.KeyMap[(int)ImGuiKey.UpArrow] = (int)VirtualKeys.Up;
            io.KeyMap[(int)ImGuiKey.DownArrow] = (int)VirtualKeys.Down;
            io.KeyMap[(int)ImGuiKey.PageUp] = (int)VirtualKeys.Prior;
            io.KeyMap[(int)ImGuiKey.PageDown] = (int)VirtualKeys.Next;
            io.KeyMap[(int)ImGuiKey.Home] = (int)VirtualKeys.Home;
            io.KeyMap[(int)ImGuiKey.End] = (int)VirtualKeys.End;
            io.KeyMap[(int)ImGuiKey.Insert] = (int)VirtualKeys.Insert;
            io.KeyMap[(int)ImGuiKey.Delete] = (int)VirtualKeys.Delete;
            io.KeyMap[(int)ImGuiKey.Backspace] = (int)VirtualKeys.Back;
            io.KeyMap[(int)ImGuiKey.Space] = (int)VirtualKeys.Space;
            io.KeyMap[(int)ImGuiKey.Enter] = (int)VirtualKeys.Return;
            io.KeyMap[(int)ImGuiKey.Escape] = (int)VirtualKeys.Escape;
            io.KeyMap[(int)ImGuiKey.KeyPadEnter] = (int)VirtualKeys.Return; // same keycode, lparam is different.  Not sure if this will cause dupe events or not
            io.KeyMap[(int)ImGuiKey.A] = (int)VirtualKeys.A;
            io.KeyMap[(int)ImGuiKey.C] = (int)VirtualKeys.C;
            io.KeyMap[(int)ImGuiKey.V] = (int)VirtualKeys.V;
            io.KeyMap[(int)ImGuiKey.X] = (int)VirtualKeys.X;
            io.KeyMap[(int)ImGuiKey.Y] = (int)VirtualKeys.Y;
            io.KeyMap[(int)ImGuiKey.Z] = (int)VirtualKeys.Z;
        }

        public static void Shutdown()
        {
            if (_oldWndProcPtr != IntPtr.Zero)
            {
                SetWindowLongPtr(_hWnd, GWLP_WNDPROC, _oldWndProcPtr);
            }

            if (_platformNamePtr != IntPtr.Zero)
            {
                unsafe
                {
                    ImGui.GetIO().NativePtr->BackendPlatformName = null;
                }

                Marshal.FreeHGlobal(_platformNamePtr);
                _platformNamePtr = IntPtr.Zero;
            }
        }


        public static void NewFrame(int targetWidth, int targetHeight)
        {
            var io = ImGui.GetIO();

            io.DisplaySize.X = targetWidth;
            io.DisplaySize.Y = targetHeight;
            io.DisplayFramebufferScale.X = 1f;
            io.DisplayFramebufferScale.Y = 1f;

            var frequency = Stopwatch.Frequency;
            var currentTime = Stopwatch.GetTimestamp();
            io.DeltaTime = _lastTime > 0 ? (float)((double)(currentTime - _lastTime) / frequency) : 1f / 60;
            _lastTime = currentTime;

            io.KeyCtrl = (GetKeyState((int)VirtualKeys.Control) & 0x8000) != 0;
            io.KeyShift = (GetKeyState((int)VirtualKeys.Shift) & 0x8000) != 0;
            io.KeyAlt = (GetKeyState((int)VirtualKeys.Menu) & 0x8000) != 0;
            io.KeySuper = false;

            UpdateMousePos();

            // this is what imgui's example does, but it doesn't seem to work for us
            // this could be a timing issue.. or their logic could just be wrong for many applications
            //var cursor = io.MouseDrawCursor ? ImGuiMouseCursor.None : ImGui.GetMouseCursor();
            //if (_oldCursor != cursor)
            //{
            //    _oldCursor = cursor;
            //    UpdateMouseCursor();
            //}

            // hacky attempt to make cursors work how I think they 'should'
            if (io.WantCaptureMouse || io.MouseDrawCursor)
            {
                UpdateMouseCursor();
            }
        }

        private static void UpdateMousePos()
        {
            var io = ImGui.GetIO();

            if (io.WantSetMousePos)
            {
                var pos = new POINT { X = (int)io.MousePos.X, Y = (int)io.MousePos.Y };
                ClientToScreen(_hWnd, ref pos);
                SetCursorPos(pos.X, pos.Y);
            }

            //if (HWND active_window = ::GetForegroundWindow())
            //    if (active_window == g_hWnd || ::IsChild(active_window, g_hWnd))
            if (GetCursorPos(out POINT pt) && ScreenToClient(_hWnd, ref pt))
            {
                io.MousePos.X = pt.X;
                io.MousePos.Y = pt.Y;
            }
            else
            {
                io.MousePos.X = float.MinValue;
                io.MousePos.Y = float.MinValue;
            }
        }

        private static bool UpdateMouseCursor()
        {
            var io = ImGui.GetIO();
            if (io.ConfigFlags.HasFlag(ImGuiConfigFlags.NoMouseCursorChange))
            {
                return false;
            }

            var cur = ImGui.GetMouseCursor();
            if (cur == ImGuiMouseCursor.None || io.MouseDrawCursor)
            {
                SetCursor(IntPtr.Zero);
            }
            else
            {
                var win32Cur = (int)Cursors.IDC_ARROW;
                switch (cur)
                {
                    case ImGuiMouseCursor.Arrow:
                        win32Cur = (int)Cursors.IDC_ARROW;
                        break;

                    case ImGuiMouseCursor.TextInput:
                        win32Cur = (int)Cursors.IDC_IBEAM;
                        break;

                    case ImGuiMouseCursor.ResizeAll:
                        win32Cur = (int)Cursors.IDC_SIZEALL;
                        break;

                    case ImGuiMouseCursor.ResizeEW:
                        win32Cur = (int)Cursors.IDC_SIZEWE;
                        break;

                    case ImGuiMouseCursor.ResizeNS:
                        win32Cur = (int)Cursors.IDC_SIZENS;
                        break;

                    case ImGuiMouseCursor.ResizeNESW:
                        win32Cur = (int)Cursors.IDC_SIZENESW;
                        break;

                    case ImGuiMouseCursor.ResizeNWSE:
                        win32Cur = (int)Cursors.IDC_SIZENWSE;
                        break;

                    case ImGuiMouseCursor.Hand:
                        win32Cur = (int)Cursors.IDC_HAND;
                        break;
                }

                SetCursor(LoadCursor(IntPtr.Zero, win32Cur));
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static short HIWORD(ulong val)
        {
            return (short)(val >> 16);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static short LOWORD(ulong val)
        {
            return (short)(val & 0xFFFF);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static short GET_XBUTTON_WPARAM(ulong val)
        {
            return HIWORD(val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static short GET_WHEEL_DELTA_WPARAM(ulong val)
        {
            return HIWORD(val);
        }

        const int WHEEL_DELTA = 120;
        const int HTCLIENT = 1;

        private static long WndProcDetour(IntPtr hWnd, uint msg, ulong wParam, long lParam)
        {
            if (hWnd == _hWnd && ImGui.GetCurrentContext() != IntPtr.Zero && (ImGui.GetIO().WantCaptureMouse || ImGui.GetIO().WantCaptureKeyboard))
            {
                var io = ImGui.GetIO();
                var wmsg = (WindowsMessage)msg;

                switch (wmsg)
                {
                    case WindowsMessage.WM_LBUTTONDOWN:
                    case WindowsMessage.WM_LBUTTONDBLCLK:
                    case WindowsMessage.WM_RBUTTONDOWN:
                    case WindowsMessage.WM_RBUTTONDBLCLK:
                    case WindowsMessage.WM_MBUTTONDOWN:
                    case WindowsMessage.WM_MBUTTONDBLCLK:
                    case WindowsMessage.WM_XBUTTONDOWN:
                    case WindowsMessage.WM_XBUTTONDBLCLK:
                        if (io.WantCaptureMouse)
                        {
                            var button = 0;
                            if (wmsg == WindowsMessage.WM_LBUTTONDOWN || wmsg == WindowsMessage.WM_LBUTTONDBLCLK)
                            {
                                button = 0;
                            }
                            else if (wmsg == WindowsMessage.WM_RBUTTONDOWN || wmsg == WindowsMessage.WM_RBUTTONDBLCLK)
                            {
                                button = 1;
                            }
                            else if (wmsg == WindowsMessage.WM_MBUTTONDOWN || wmsg == WindowsMessage.WM_MBUTTONDBLCLK)
                            {
                                button = 2;
                            }
                            else if (wmsg == WindowsMessage.WM_XBUTTONDOWN || wmsg == WindowsMessage.WM_XBUTTONDBLCLK)
                            {
                                // XBUTTON1 == 3
                                button = GET_XBUTTON_WPARAM(wParam) == 1 ? 3 : 4;
                            }

                            if (!ImGui.IsAnyMouseDown() && GetCapture() == IntPtr.Zero)
                            {
                                SetCapture(hWnd);
                            }
                            io.MouseDown[button] = true;
                            return 0;
                        }
                        break;

                    case WindowsMessage.WM_LBUTTONUP:
                    case WindowsMessage.WM_RBUTTONUP:
                    case WindowsMessage.WM_MBUTTONUP:
                    case WindowsMessage.WM_XBUTTONUP:
                        if (io.WantCaptureMouse)
                        {
                            var button = 0;
                            if (wmsg == WindowsMessage.WM_LBUTTONUP)
                            {
                                button = 0;
                            }
                            else if (wmsg == WindowsMessage.WM_RBUTTONUP)
                            {
                                button = 1;
                            }
                            else if (wmsg == WindowsMessage.WM_MBUTTONUP)
                            {
                                button = 2;
                            }
                            else if (wmsg == WindowsMessage.WM_XBUTTONUP)
                            {
                                // XBUTTON1 == 3
                                button = GET_XBUTTON_WPARAM(wParam) == 1 ? 3 : 4;
                            }

                            if (!ImGui.IsAnyMouseDown() && GetCapture() == hWnd)
                            {
                                ReleaseCapture();
                            }
                            io.MouseDown[button] = false;
                            return 0;
                        }
                        break;

                    case WindowsMessage.WM_MOUSEWHEEL:
                        if (io.WantCaptureMouse)
                        {
                            io.MouseWheel += (float)GET_WHEEL_DELTA_WPARAM(wParam) / (float)WHEEL_DELTA;
                            return 0;
                        }
                        break;

                    case WindowsMessage.WM_MOUSEHWHEEL:
                        if (io.WantCaptureMouse)
                        {
                            io.MouseWheelH += (float)GET_WHEEL_DELTA_WPARAM(wParam) / (float)WHEEL_DELTA;
                            return 0;
                        }
                        break;

                    case WindowsMessage.WM_KEYDOWN:
                    case WindowsMessage.WM_SYSKEYDOWN:
                        if (io.WantCaptureKeyboard)
                        {
                            if (wParam < 256)
                            {
                                io.KeysDown[(int)wParam] = true;
                            }
                            return 0;
                        }
                        break;

                    case WindowsMessage.WM_KEYUP:
                    case WindowsMessage.WM_SYSKEYUP:
                        if (io.WantCaptureKeyboard)
                        {
                            if (wParam < 256)
                            {
                                io.KeysDown[(int)wParam] = false;
                            }
                            return 0;
                        }
                        break;

                    case WindowsMessage.WM_CHAR:
                        if (io.WantCaptureKeyboard)
                        {
                            io.AddInputCharacter((uint)wParam);
                            return 0;
                        }
                        break;

                    // this never seemed to work reasonably
                    //case WindowsMessage.WM_SETCURSOR:
                    //    if (LOWORD((ulong)lParam) == HTCLIENT && UpdateMouseCursor())
                    //    {
                    //        return 0;
                    //    }
                    //    break;

                    default:
                        break;
                }
            }

            return CallWindowProc(_oldWndProcPtr, hWnd, msg, wParam, lParam);
        }
    }
}
