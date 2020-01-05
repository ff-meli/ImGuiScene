using ImGuiNET;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ImGuiScene
{
    public static class ImGui_Input_Impl_Direct
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public static implicit operator Point(POINT point)
            {
                return new Point(point.X, point.Y);
            }
        }

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")]
        static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        const int VK_LBUTTON = 1;
        const int VK_RBUTTON = 2;
        const int VK_MBUTTON = 4;


        private static long _lastTime;
        private static IntPtr _platformNamePtr;
        private static IntPtr _hWnd;

        public static void Init(IntPtr hWnd)
        {
            _hWnd = hWnd;

            var io = ImGui.GetIO();

            _platformNamePtr = Marshal.StringToHGlobalAnsi("imgui_impl_direct_c#");
            unsafe
            {
                io.NativePtr->BackendPlatformName = (byte*)_platformNamePtr.ToPointer();
            }

            io.ImeWindowHandle = _hWnd;
        }

        public static void Shutdown()
        {
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

            UpdateMousePosAndButtons();
            //UpdateMouseCursor();
        }

        private static void UpdateMousePosAndButtons()
        {
            var io = ImGui.GetIO();

            // TODO: this should probably be done elsewhere (WM_MOUSEMOVE etc)
            // particularly because we can't block mouse input here in any way, so it all goes
            // to the game.
            // This is more just to get a basic appearance of functionality for testing things
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

            // not really accurate but & 0x80 didn't want to work
            // this won't be how we have to do it for real anyway, but at least sort of works for simple clicking
            io.MouseDown[0] = GetAsyncKeyState(VK_LBUTTON) != 0;
            io.MouseDown[1] = GetAsyncKeyState(VK_RBUTTON) != 0;
            io.MouseDown[2] = GetAsyncKeyState(VK_MBUTTON) != 0;
        }
    }
}
