using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ImGuiScene
{
    class Win32
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short HIWORD(ulong val)
        {
            return (short)(val >> 16);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short LOWORD(ulong val)
        {
            return (short)(val & 0xFFFF);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short GET_XBUTTON_WPARAM(ulong val)
        {
            return HIWORD(val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short GET_WHEEL_DELTA_WPARAM(ulong val)
        {
            return HIWORD(val);
        }


        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")]
        public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);
        [DllImport("user32.dll")]
        public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);
        [DllImport("user32.dll")]
        public static extern IntPtr GetCapture();
        [DllImport("user32.dll")]
        public static extern IntPtr SetCapture(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        public static extern short GetKeyState(VirtualKey nVirtKey);
        [DllImport("user32.dll")]
        public static extern IntPtr GetCursor();
        [DllImport("user32.dll")]
        public static extern IntPtr SetCursor(IntPtr handle);
        [DllImport("user32.dll")]
        public static extern IntPtr LoadCursor(IntPtr hInstance, Cursor lpCursorName);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, WindowLongType nIndex, IntPtr dwNewLong);
        [DllImport("user32.dll")]
        public static extern long CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, ulong wParam, long lParam);
    }
}
