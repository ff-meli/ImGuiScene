using ImGuiNET;
using System;
using System.Runtime.InteropServices;
using System.Text;
using static SDL2.SDL;

namespace ImGuiScene
{
    /// <summary>
    /// Currently undocumented because it is a horrible mess.
    /// A near-direct port of https://github.com/ocornut/imgui/blob/master/examples/imgui_impl_sdl.cpp
    /// </summary>
    public class ImGui_Impl_SDL : IImGuiInputHandler
    {
        private IntPtr _platformNamePtr;
        private IntPtr _iniPathPtr;
        private IntPtr _sdlWindow;
        private IntPtr[] _mouseCursors = new IntPtr[(int)ImGuiMouseCursor.COUNT];
        private bool[] _mousePressed = new bool[3];
        private ulong _lastTime;

        private delegate void SetClipboardTextDelegate(IntPtr userData, string text);
        private delegate string GetClipboardTextDelegate();

        // variables because they need to exist for the program duration without being gc'd
        private SetClipboardTextDelegate _setText;
        private GetClipboardTextDelegate _getText;


        public ImGui_Impl_SDL(IntPtr sdlWindow)
        {
            _sdlWindow = sdlWindow;

            // Setup back-end capabilities flags
            var io = ImGui.GetIO();
            // We can honor GetMouseCursor() values (optional)
            // We can honor io.WantSetMousePos requests (optional, rarely used)
            io.BackendFlags = io.BackendFlags | (ImGuiBackendFlags.HasMouseCursors | ImGuiBackendFlags.HasSetMousePos);

            // BackendPlatformName is readonly (and null) in ImGui.NET for some reason, but we can hack it via its internal pointer
            _platformNamePtr = Marshal.StringToHGlobalAnsi("imgui_impl_sdl_c#");
            unsafe
            {
                io.NativePtr->BackendPlatformName = (byte*)_platformNamePtr.ToPointer();
            }

            // Keyboard mapping. ImGui will use those indices to peek into the io.KeysDown[] array.
            io.KeyMap[(int)ImGuiKey.Tab] = (int)SDL_Scancode.SDL_SCANCODE_TAB;
            io.KeyMap[(int)ImGuiKey.LeftArrow] = (int)SDL_Scancode.SDL_SCANCODE_LEFT;
            io.KeyMap[(int)ImGuiKey.RightArrow] = (int)SDL_Scancode.SDL_SCANCODE_RIGHT;
            io.KeyMap[(int)ImGuiKey.UpArrow] = (int)SDL_Scancode.SDL_SCANCODE_UP;
            io.KeyMap[(int)ImGuiKey.DownArrow] = (int)SDL_Scancode.SDL_SCANCODE_DOWN;
            io.KeyMap[(int)ImGuiKey.PageUp] = (int)SDL_Scancode.SDL_SCANCODE_PAGEUP;
            io.KeyMap[(int)ImGuiKey.PageDown] = (int)SDL_Scancode.SDL_SCANCODE_PAGEDOWN;
            io.KeyMap[(int)ImGuiKey.Home] = (int)SDL_Scancode.SDL_SCANCODE_HOME;
            io.KeyMap[(int)ImGuiKey.End] = (int)SDL_Scancode.SDL_SCANCODE_END;
            io.KeyMap[(int)ImGuiKey.Insert] = (int)SDL_Scancode.SDL_SCANCODE_INSERT;
            io.KeyMap[(int)ImGuiKey.Delete] = (int)SDL_Scancode.SDL_SCANCODE_DELETE;
            io.KeyMap[(int)ImGuiKey.Backspace] = (int)SDL_Scancode.SDL_SCANCODE_BACKSPACE;
            io.KeyMap[(int)ImGuiKey.Space] = (int)SDL_Scancode.SDL_SCANCODE_SPACE;
            io.KeyMap[(int)ImGuiKey.Enter] = (int)SDL_Scancode.SDL_SCANCODE_RETURN;
            io.KeyMap[(int)ImGuiKey.Escape] = (int)SDL_Scancode.SDL_SCANCODE_ESCAPE;
            io.KeyMap[(int)ImGuiKey.KeyPadEnter] = (int)SDL_Scancode.SDL_SCANCODE_RETURN2;
            io.KeyMap[(int)ImGuiKey.A] = (int)SDL_Scancode.SDL_SCANCODE_A;
            io.KeyMap[(int)ImGuiKey.C] = (int)SDL_Scancode.SDL_SCANCODE_C;
            io.KeyMap[(int)ImGuiKey.V] = (int)SDL_Scancode.SDL_SCANCODE_V;
            io.KeyMap[(int)ImGuiKey.X] = (int)SDL_Scancode.SDL_SCANCODE_X;
            io.KeyMap[(int)ImGuiKey.Y] = (int)SDL_Scancode.SDL_SCANCODE_Y;
            io.KeyMap[(int)ImGuiKey.Z] = (int)SDL_Scancode.SDL_SCANCODE_Z;

            _setText = new SetClipboardTextDelegate(SetClipboardText);
            _getText = new GetClipboardTextDelegate(GetClipboardText);

            io.SetClipboardTextFn = Marshal.GetFunctionPointerForDelegate(_setText);
            io.GetClipboardTextFn = Marshal.GetFunctionPointerForDelegate(_getText);
            io.ClipboardUserData = IntPtr.Zero;

            _mouseCursors[(int)ImGuiMouseCursor.Arrow] = SDL_CreateSystemCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_ARROW);
            _mouseCursors[(int)ImGuiMouseCursor.TextInput] = SDL_CreateSystemCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_IBEAM);
            _mouseCursors[(int)ImGuiMouseCursor.ResizeAll] = SDL_CreateSystemCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZEALL);
            _mouseCursors[(int)ImGuiMouseCursor.ResizeNS] = SDL_CreateSystemCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZENS);
            _mouseCursors[(int)ImGuiMouseCursor.ResizeEW] = SDL_CreateSystemCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZEWE);
            _mouseCursors[(int)ImGuiMouseCursor.ResizeNESW] = SDL_CreateSystemCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZENESW);
            _mouseCursors[(int)ImGuiMouseCursor.ResizeNWSE] = SDL_CreateSystemCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZENWSE);
            _mouseCursors[(int)ImGuiMouseCursor.Hand] = SDL_CreateSystemCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_HAND);
            // This apparently does not exist in ImGui.NET
            // _mouseCursors[(int)ImGuiMouseCursor.NotAllowed] = SDL_CreateSystemCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_NO);

            var sysWmInfo = new SDL_SysWMinfo();
            SDL_GetVersion(out sysWmInfo.version);
            SDL_GetWindowWMInfo(_sdlWindow, ref sysWmInfo);
            io.ImeWindowHandle = sysWmInfo.info.win.window;
        }

        public void NewFrame(int width, int height)
        {
            var io = ImGui.GetIO();

            // Setup display size (every frame to accommodate for window resizing)
            //SDL_GetWindowSize(_sdlWindow, out int w, out int h);
            SDL_GL_GetDrawableSize(_sdlWindow, out int displayW, out int displayH);
            io.DisplaySize.X = width;
            io.DisplaySize.Y = height;
            if (width > 0 && height > 0)
            {
                io.DisplayFramebufferScale.X = (float)displayW / width;
                io.DisplayFramebufferScale.Y = (float)displayH / height;
            }

            // Setup time step (we don't use SDL_GetTicks() because it is using millisecond resolution)
            var frequency = SDL_GetPerformanceFrequency();
            var currentTime = SDL_GetPerformanceCounter();
            io.DeltaTime = _lastTime > 0 ? (float)((double)(currentTime - _lastTime) / frequency) : 1f / 60;
            _lastTime = currentTime;

            UpdateMousePosAndButtons();
            UpdateMouseCursor();
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

        private void UpdateMousePosAndButtons()
        {
            var io = ImGui.GetIO();

            // Set OS mouse position if requested (rarely used, only when ImGuiConfigFlags_NavEnableSetMousePos is enabled by user)
            if (io.WantSetMousePos)
            {
                SDL_WarpMouseInWindow(_sdlWindow, (int)io.MousePos.X, (int)io.MousePos.Y);
            }
            else
            {
                io.MousePos.X = float.MinValue;
                io.MousePos.Y = float.MaxValue;
            }

            var mouseButtons = SDL_GetMouseState(out int mx, out int my);
            io.MouseDown[0] = _mousePressed[0] || (mouseButtons & SDL_BUTTON(SDL_BUTTON_LEFT)) != 0;  // If a mouse press event came, always pass it as "mouse held this frame", so we don't miss click-release events that are shorter than 1 frame.
            io.MouseDown[1] = _mousePressed[1] || (mouseButtons & SDL_BUTTON(SDL_BUTTON_RIGHT)) != 0;
            io.MouseDown[2] = _mousePressed[2] || (mouseButtons & SDL_BUTTON(SDL_BUTTON_MIDDLE)) != 0;
            _mousePressed[0] = _mousePressed[1] = _mousePressed[2] = false;

            var focusedWindow = SDL_GetKeyboardFocus();
            if (_sdlWindow == focusedWindow)
            {
                // SDL_GetMouseState() gives mouse position seemingly based on the last window entered/focused(?)
                // The creation of a new windows at runtime and SDL_CaptureMouse both seems to severely mess up with that, so we retrieve that position globally.
                SDL_GetWindowPosition(focusedWindow, out int wx, out int wy);
                SDL_GetGlobalMouseState(out mx, out my);
                mx -= wx;
                my -= wy;
                io.MousePos.X = mx;
                io.MousePos.Y = my;
            }

            // SDL_CaptureMouse() let the OS know e.g. that our imgui drag outside the SDL window boundaries shouldn't e.g. trigger the OS window resize cursor.
            // The function is only supported from SDL 2.0.4 (released Jan 2016)
            SDL_CaptureMouse(ImGui.IsAnyMouseDown() ? SDL_bool.SDL_TRUE : SDL_bool.SDL_FALSE);
        }

        private void UpdateMouseCursor()
        {
            var io = ImGui.GetIO();

            if (io.ConfigFlags.HasFlag(ImGuiConfigFlags.NoMouseCursorChange))
            {
                return;
            }

            var imguiCursor = ImGui.GetMouseCursor();
            if (io.MouseDrawCursor || imguiCursor == ImGuiMouseCursor.None)
            {
                // Hide OS mouse cursor if imgui is drawing it or if it wants no cursor
                SDL_ShowCursor((int)SDL_bool.SDL_FALSE);
            }
            else
            {
                // Show OS mouse cursor
                SDL_SetCursor(_mouseCursors[(int)imguiCursor] != IntPtr.Zero ? _mouseCursors[(int)imguiCursor] : _mouseCursors[(int)ImGuiMouseCursor.Arrow]);
                SDL_ShowCursor((int)SDL_bool.SDL_TRUE);
            }
        }

        private static void SetClipboardText(IntPtr userData, string text)
        {
            // text always seems to have an extra newline, but I'll leave it for now
            SDL_SetClipboardText(text);
        }

        private static string GetClipboardText()
        {
            return SDL_GetClipboardText();
        }

        // You can read the io.WantCaptureMouse, io.WantCaptureKeyboard flags to tell if dear imgui wants to use your inputs.
        // - When io.WantCaptureMouse is true, do not dispatch mouse input data to your main application.
        // - When io.WantCaptureKeyboard is true, do not dispatch keyboard input data to your main application.
        // Generally you may always pass all inputs to dear imgui, and hide them from your application based on those two flags.
        // If you have multiple SDL events and some of them are not meant to be used by dear imgui, you may need to filter events based on their windowID field.
        internal void ProcessEvent(ref SDL_Event sdlEvent)
        {
            var io = ImGui.GetIO();

            switch (sdlEvent.type)
            {
                case SDL_EventType.SDL_MOUSEWHEEL:
                    if (sdlEvent.wheel.x > 0) io.MouseWheelH += 1f;
                    if (sdlEvent.wheel.x < 0) io.MouseWheelH -= 1f;
                    if (sdlEvent.wheel.y > 0) io.MouseWheel += 1f;
                    if (sdlEvent.wheel.y < 0) io.MouseWheel -= 1f;
                    break;

                case SDL_EventType.SDL_MOUSEBUTTONDOWN:
                    if (sdlEvent.button.button == SDL_BUTTON_LEFT) _mousePressed[0] = true;
                    if (sdlEvent.button.button == SDL_BUTTON_RIGHT) _mousePressed[1] = true;
                    if (sdlEvent.button.button == SDL_BUTTON_MIDDLE) _mousePressed[2] = true;
                    break;

                case SDL_EventType.SDL_TEXTINPUT:
                    byte[] byteCopy;
                    unsafe
                    {
                        fixed (byte* text = sdlEvent.text.text)
                        {
                            int strlen;
                            for (strlen = 0; strlen < SDL_TEXTINPUTEVENT_TEXT_SIZE; strlen++)
                            {
                                if (text[strlen] == 0x00)
                                {
                                    break;
                                }
                            }

                            byteCopy = new byte[strlen];
                            Marshal.Copy((IntPtr)text, byteCopy, 0, strlen);
                        }
                    }
                    io.AddInputCharactersUTF8(Encoding.UTF8.GetString(byteCopy));
                    break;

                case SDL_EventType.SDL_KEYDOWN:
                case SDL_EventType.SDL_KEYUP:
                    var key = sdlEvent.key.keysym.scancode;
                    io.KeysDown[(int)key] = (sdlEvent.type == SDL_EventType.SDL_KEYDOWN);
                    io.KeyShift = ((int)SDL_GetModState() & (int)SDL_Keymod.KMOD_SHIFT) != 0;
                    io.KeyCtrl = ((int)SDL_GetModState() & (int)SDL_Keymod.KMOD_CTRL) != 0;
                    io.KeyAlt = ((int)SDL_GetModState() & (int)SDL_Keymod.KMOD_ALT) != 0;
                    io.KeySuper = ((int)SDL_GetModState() & (int)SDL_Keymod.KMOD_GUI) != 0;
                    break;
            }
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
                    _setText = null;
                    _getText = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                _sdlWindow = IntPtr.Zero;

                ImGui.GetIO().SetClipboardTextFn = IntPtr.Zero;
                ImGui.GetIO().GetClipboardTextFn = IntPtr.Zero;

                // Destroy SDL mouse cursors
                foreach (var cur in _mouseCursors)
                {
                    SDL_FreeCursor(cur);
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

        ~ImGui_Impl_SDL()
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
