using System;
using System.Runtime.InteropServices;
using static SDL2.SDL;

namespace ImGuiScene
{
    public class SimpleSDLWindow : IDisposable
    {
        #region imports
        [DllImport("user32.dll")]
        static extern uint SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);
        [DllImport("user32.dll")]
        static extern uint GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);
        #endregion

        public static uint CreateColorKey(float r, float g, float b)
        {
            return ((uint)(r * 255.0f)) | ((uint)(g * 255.0f) << 8) | ((uint)(b * 255.0f) << 16);
        }

        public delegate bool ProcessEventDelegate(ref SDL_Event sdlEvent);

        public IntPtr Window { get; private set; }
        public ProcessEventDelegate OnSDLEvent { get; set; }
        public bool WantsClose { get; set; } = false;

        public SimpleSDLWindow(string title, int xPos, int yPos, int width, int height, bool fullscreen)
        {
            if (SDL_Init(SDL_INIT_VIDEO) != 0)
            {
                throw new Exception("SDL_Init error: " + SDL_GetError());
            }

            var windowFlags = SDL_WindowFlags.SDL_WINDOW_SHOWN | SDL_WindowFlags.SDL_WINDOW_ALLOW_HIGHDPI;
            if (fullscreen)
            {
                windowFlags |= SDL_WindowFlags.SDL_WINDOW_BORDERLESS | SDL_WindowFlags.SDL_WINDOW_FULLSCREEN_DESKTOP | SDL_WindowFlags.SDL_WINDOW_ALWAYS_ON_TOP;
                // without this, clicking off the window (eg, to another monitor) will minimize and cause positioning issues
                SDL_SetHint("SDL_VIDEO_MINIMIZE_ON_FOCUS_LOSS", "0");
            }

            Window = SDL_CreateWindow(title, xPos, yPos, width, height, windowFlags);
            if (Window == IntPtr.Zero)
            {
                SDL_Quit();
                throw new Exception("Failed to create window: " + SDL_GetError());
            }
        }

        public IntPtr GetHWnd()
        {
            if (Window == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            var sysWmInfo = new SDL_SysWMinfo();
            SDL_GetVersion(out sysWmInfo.version);
            SDL_GetWindowWMInfo(Window, ref sysWmInfo);
            return sysWmInfo.info.win.window;
        }

        public void MakeWindowTransparent(uint transparentColorKey)
        {
            const int GWL_EXSTYLE = -20;
            const uint WS_EX_LAYERED = 0x80000;
            const uint LWA_COLORKEY = 1;
            const uint LWA_ALPHA = 2;

            var hWnd = GetHWnd();

            var oldFlags = GetWindowLong(hWnd, GWL_EXSTYLE);
            SetWindowLong(hWnd, GWL_EXSTYLE, oldFlags | WS_EX_LAYERED);
            SetLayeredWindowAttributes(hWnd, transparentColorKey, 0, LWA_COLORKEY);
        }

        public void ProcessEvents()
        {
            while (SDL_PollEvent(out SDL_Event sdlEvent) != 0)
            {
                OnSDLEvent?.Invoke(ref sdlEvent);

                if (sdlEvent.type == SDL_EventType.SDL_QUIT)
                {
                    WantsClose = true;
                }
                else if (sdlEvent.type == SDL_EventType.SDL_WINDOWEVENT &&
                         sdlEvent.window.windowEvent == SDL_WindowEventID.SDL_WINDOWEVENT_CLOSE &&
                         sdlEvent.window.windowID == SDL_GetWindowID(Window))
                {
                    WantsClose = true;
                }
            }
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                if (Window != IntPtr.Zero)
                {
                    SDL_DestroyWindow(Window);
                    Window = IntPtr.Zero;
                }

                SDL_Quit();

                disposedValue = true;
            }
        }

        ~SimpleSDLWindow()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
