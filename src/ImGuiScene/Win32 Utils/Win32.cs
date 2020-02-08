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

        [StructLayout(LayoutKind.Sequential)]
        public struct CURSORINFO
        {
            public Int32 cbSize;
            public Int32 flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort HIWORD(ulong val)
        {
            // #define HIWORD(l)  ((WORD)((((DWORD_PTR)(l)) >> 16) & 0xffff))
            return (ushort)((val >> 16) & 0xFFFF);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort LOWORD(ulong val)
        {
            // #define LOWORD(l)  ((WORD)(((DWORD_PTR)(l)) & 0xffff))
            return (ushort)(val & 0xFFFF);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort GET_XBUTTON_WPARAM(ulong val)
        {
            // #define GET_XBUTTON_WPARAM(wParam)  (HIWORD(wParam))
            return HIWORD(val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short GET_WHEEL_DELTA_WPARAM(ulong val)
        {
            // #define GET_WHEEL_DELTA_WPARAM(wParam)  ((short)HIWORD(wParam))
            return (short)HIWORD(val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GET_X_LPARAM(ulong val)
        {
            // #define GET_X_LPARAM(lp)  ((int)(short)LOWORD(lp))
            return (int)(short)LOWORD(val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GET_Y_LPARAM(ulong val)
        {
            // #define GET_Y_LPARAM(lp)  ((int)(short)HIWORD(lp))
            return (int)(short)HIWORD(val);
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
        [DllImport("user32.dll")]
        public static extern int ShowCursor(bool bShow);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, WindowLongType nIndex, IntPtr dwNewLong);
        [DllImport("user32.dll")]
        public static extern long CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, ulong wParam, long lParam);

        [DllImport("user32.dll", EntryPoint = "GetCursorInfo")]
        private static extern bool GetCursorInfo_Internal(ref CURSORINFO pci);

        public static bool GetCursorInfo(out CURSORINFO pci)
        {
            pci = new CURSORINFO
            {
                cbSize = Marshal.SizeOf(typeof(CURSORINFO))
            };

            return GetCursorInfo_Internal(ref pci);
        }
    }
}
