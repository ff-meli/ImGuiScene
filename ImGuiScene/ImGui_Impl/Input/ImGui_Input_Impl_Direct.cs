using ImGuiNET;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace ImGuiScene
{
    // largely a port of https://github.com/ocornut/imgui/blob/master/examples/imgui_impl_win32.cpp, though some changes
    // and wndproc hooking
    public class ImGui_Input_Impl_Direct : IImGuiInputHandler
    {
        delegate long WndProcDelegate(IntPtr hWnd, uint msg, ulong wParam, long lParam);

        private long _lastTime;
        private IntPtr _platformNamePtr;
        private IntPtr _iniPathPtr;
        private IntPtr _hWnd;
        private WndProcDelegate _wndProcDelegate;
        private IntPtr _wndProcPtr;
        private IntPtr _oldWndProcPtr;
        // private ImGuiMouseCursor _oldCursor = ImGuiMouseCursor.None;
        private IntPtr[] _cursors;

        public ImGui_Input_Impl_Direct(IntPtr hWnd)
        {
            _hWnd = hWnd;

            // hook wndproc
            // have to hold onto the delegate to keep it in memory for unmanaged code
            _wndProcDelegate = WndProcDetour;
            _wndProcPtr = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
            _oldWndProcPtr = Win32.SetWindowLongPtr(_hWnd, WindowLongType.GWL_WNDPROC, _wndProcPtr);

            var io = ImGui.GetIO();

            io.BackendFlags = io.BackendFlags | (ImGuiBackendFlags.HasMouseCursors | ImGuiBackendFlags.HasSetMousePos);

            _platformNamePtr = Marshal.StringToHGlobalAnsi("imgui_impl_win32_c#");
            unsafe
            {
                io.NativePtr->BackendPlatformName = (byte*)_platformNamePtr.ToPointer();
            }

            io.ImeWindowHandle = _hWnd;

            io.KeyMap[(int)ImGuiKey.Tab] = (int)VirtualKey.Tab;
            io.KeyMap[(int)ImGuiKey.LeftArrow] = (int)VirtualKey.Left;
            io.KeyMap[(int)ImGuiKey.RightArrow] = (int)VirtualKey.Right;
            io.KeyMap[(int)ImGuiKey.UpArrow] = (int)VirtualKey.Up;
            io.KeyMap[(int)ImGuiKey.DownArrow] = (int)VirtualKey.Down;
            io.KeyMap[(int)ImGuiKey.PageUp] = (int)VirtualKey.Prior;
            io.KeyMap[(int)ImGuiKey.PageDown] = (int)VirtualKey.Next;
            io.KeyMap[(int)ImGuiKey.Home] = (int)VirtualKey.Home;
            io.KeyMap[(int)ImGuiKey.End] = (int)VirtualKey.End;
            io.KeyMap[(int)ImGuiKey.Insert] = (int)VirtualKey.Insert;
            io.KeyMap[(int)ImGuiKey.Delete] = (int)VirtualKey.Delete;
            io.KeyMap[(int)ImGuiKey.Backspace] = (int)VirtualKey.Back;
            io.KeyMap[(int)ImGuiKey.Space] = (int)VirtualKey.Space;
            io.KeyMap[(int)ImGuiKey.Enter] = (int)VirtualKey.Return;
            io.KeyMap[(int)ImGuiKey.Escape] = (int)VirtualKey.Escape;
            io.KeyMap[(int)ImGuiKey.KeyPadEnter] = (int)VirtualKey.Return; // same keycode, lparam is different.  Not sure if this will cause dupe events or not
            io.KeyMap[(int)ImGuiKey.A] = (int)VirtualKey.A;
            io.KeyMap[(int)ImGuiKey.C] = (int)VirtualKey.C;
            io.KeyMap[(int)ImGuiKey.V] = (int)VirtualKey.V;
            io.KeyMap[(int)ImGuiKey.X] = (int)VirtualKey.X;
            io.KeyMap[(int)ImGuiKey.Y] = (int)VirtualKey.Y;
            io.KeyMap[(int)ImGuiKey.Z] = (int)VirtualKey.Z;

            _cursors = new IntPtr[8];
            _cursors[(int)ImGuiMouseCursor.Arrow] = Win32.LoadCursor(IntPtr.Zero, Cursor.IDC_ARROW);
            _cursors[(int)ImGuiMouseCursor.TextInput] = Win32.LoadCursor(IntPtr.Zero, Cursor.IDC_IBEAM);
            _cursors[(int)ImGuiMouseCursor.ResizeAll] = Win32.LoadCursor(IntPtr.Zero, Cursor.IDC_SIZEALL);
            _cursors[(int)ImGuiMouseCursor.ResizeEW] = Win32.LoadCursor(IntPtr.Zero, Cursor.IDC_SIZEWE);
            _cursors[(int)ImGuiMouseCursor.ResizeNS] = Win32.LoadCursor(IntPtr.Zero, Cursor.IDC_SIZENS);
            _cursors[(int)ImGuiMouseCursor.ResizeNESW] = Win32.LoadCursor(IntPtr.Zero, Cursor.IDC_SIZENESW);
            _cursors[(int)ImGuiMouseCursor.ResizeNWSE] = Win32.LoadCursor(IntPtr.Zero, Cursor.IDC_SIZENWSE);
            _cursors[(int)ImGuiMouseCursor.Hand] = Win32.LoadCursor(IntPtr.Zero, Cursor.IDC_HAND);
        }

        public bool IsImGuiCursor(IntPtr hCursor)
        {
            return _cursors?.Contains(hCursor) ?? false;
        }

        public void NewFrame(int targetWidth, int targetHeight)
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

            io.KeyCtrl = (Win32.GetKeyState(VirtualKey.Control) & 0x8000) != 0;
            io.KeyShift = (Win32.GetKeyState(VirtualKey.Shift) & 0x8000) != 0;
            io.KeyAlt = (Win32.GetKeyState(VirtualKey.Menu) & 0x8000) != 0;
            io.KeySuper = false;

            UpdateMousePos();

            // TODO: need to figure out some way to unify all this
            // The bottom case works(?) if the caller hooks SetCursor, but otherwise causes fps issues
            // The top case more or less works if we use ImGui's software cursor (and ideally hide the
            // game's hardware cursor)
            // It would be nice if hooking WM_SETCURSOR worked as it 'should' so that external hooking
            // wasn't necessary

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

            // From ImGui's FAQ:
            // Note: Text input widget releases focus on "Return KeyDown", so the subsequent "Return KeyUp" event
            // that your application receive will typically have io.WantCaptureKeyboard == false. Depending on your
            // application logic it may or not be inconvenient.
            //
            // With how the local wndproc works, this causes the key up event to be missed when exiting ImGui text entry
            // (eg, from hitting enter or escape.  There may be other ways as well)
            // This then causes the key to appear 'stuck' down, which breaks subsequent attempts to use the input field.
            // This is something of a brute force fix that basically makes key up events irrelevant
            // Holding a key will send repeated key down events and (re)set these where appropriate, so this should be ok.
            if (!io.WantTextInput)
            {
                for (int i = 0; i < io.KeysDown.Count; i++)
                {
                    io.KeysDown[i] = false;
                }
            }
        }

        public void SetIniPath(string iniPath)
        {
            // TODO: error/messaging when trying to set after first render?
            if (iniPath != null)
            {
                if (_iniPathPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_iniPathPtr);
                }

                _iniPathPtr = Marshal.StringToHGlobalAnsi(iniPath);
                unsafe
                {
                    ImGui.GetIO().NativePtr->IniFilename = (byte*)_iniPathPtr.ToPointer();
                }
            }
        }

        private void UpdateMousePos()
        {
            var io = ImGui.GetIO();

            if (io.WantSetMousePos)
            {
                var pos = new Win32.POINT { X = (int)io.MousePos.X, Y = (int)io.MousePos.Y };
                Win32.ClientToScreen(_hWnd, ref pos);
                Win32.SetCursorPos(pos.X, pos.Y);
            }

            //if (HWND active_window = ::GetForegroundWindow())
            //    if (active_window == g_hWnd || ::IsChild(active_window, g_hWnd))
            if (Win32.GetCursorPos(out Win32.POINT pt) && Win32.ScreenToClient(_hWnd, ref pt))
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

        private bool UpdateMouseCursor()
        {
            var io = ImGui.GetIO();
            if (io.ConfigFlags.HasFlag(ImGuiConfigFlags.NoMouseCursorChange))
            {
                return false;
            }

            var cur = ImGui.GetMouseCursor();
            if (cur == ImGuiMouseCursor.None || io.MouseDrawCursor)
            {
                Win32.SetCursor(IntPtr.Zero);
            }
            else
            {
                Win32.SetCursor(_cursors[(int)cur]);
            }

            return true;
        }

        private long WndProcDetour(IntPtr hWnd, uint msg, ulong wParam, long lParam)
        {
            if (hWnd == _hWnd && ImGui.GetCurrentContext() != IntPtr.Zero && (ImGui.GetIO().WantCaptureMouse || ImGui.GetIO().WantTextInput))
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
                                button = Win32.GET_XBUTTON_WPARAM(wParam) == Win32Constants.XBUTTON1 ? 3 : 4;
                            }

                            if (!ImGui.IsAnyMouseDown() && Win32.GetCapture() == IntPtr.Zero)
                            {
                                Win32.SetCapture(hWnd);
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
                                button = Win32.GET_XBUTTON_WPARAM(wParam) == Win32Constants.XBUTTON1 ? 3 : 4;
                            }

                            if (!ImGui.IsAnyMouseDown() && Win32.GetCapture() == hWnd)
                            {
                                Win32.ReleaseCapture();
                            }
                            io.MouseDown[button] = false;
                            return 0;
                        }
                        break;

                    case WindowsMessage.WM_MOUSEWHEEL:
                        if (io.WantCaptureMouse)
                        {
                            io.MouseWheel += (float)Win32.GET_WHEEL_DELTA_WPARAM(wParam) / (float)Win32Constants.WHEEL_DELTA;
                            return 0;
                        }
                        break;

                    case WindowsMessage.WM_MOUSEHWHEEL:
                        if (io.WantCaptureMouse)
                        {
                            io.MouseWheelH += (float)Win32.GET_WHEEL_DELTA_WPARAM(wParam) / (float)Win32Constants.WHEEL_DELTA;
                            return 0;
                        }
                        break;

                    case WindowsMessage.WM_KEYDOWN:
                    case WindowsMessage.WM_SYSKEYDOWN:
                        if (io.WantTextInput)
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
                        if (io.WantTextInput)
                        {
                            if (wParam < 256)
                            {
                                io.KeysDown[(int)wParam] = false;
                            }
                            return 0;
                        }
                        break;

                    case WindowsMessage.WM_CHAR:
                        if (io.WantTextInput)
                        {
                            io.AddInputCharacter((uint)wParam);
                            return 0;
                        }
                        break;

                    // this never seemed to work reasonably, but I'll leave it for now
                    case WindowsMessage.WM_SETCURSOR:
                        if (io.WantCaptureMouse)
                        {
                            if (Win32.LOWORD((ulong)lParam) == Win32Constants.HTCLIENT && UpdateMouseCursor())
                            {
                                // this message returns 1 to block further processing
                                // because consistency is no fun
                                return 1;
                            }
                        }
                        break;

                    default:
                        break;
                }
            }

            return Win32.CallWindowProc(_oldWndProcPtr, hWnd, msg, wParam, lParam);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    _cursors = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                if (_oldWndProcPtr != IntPtr.Zero)
                {
                    Win32.SetWindowLongPtr(_hWnd, WindowLongType.GWL_WNDPROC, _oldWndProcPtr);
                    _oldWndProcPtr = IntPtr.Zero;
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

                if (_iniPathPtr != IntPtr.Zero)
                {
                    unsafe
                    {
                        ImGui.GetIO().NativePtr->IniFilename = null;
                    }

                    Marshal.FreeHGlobal(_iniPathPtr);
                    _iniPathPtr = IntPtr.Zero;
                }

                disposedValue = true;
            }
        }

        ~ImGui_Input_Impl_Direct()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
