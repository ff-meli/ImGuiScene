using System;
using System.Runtime.InteropServices;
using static SDL2.SDL;

namespace ImGuiScene
{
    /// <summary>
    /// A very basic SDL wrapper to handle creating a window and processing SDL events.
    /// </summary>
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

        /// <summary>
        /// Creates a color key for use as a mask for <see cref="MakeTransparent(uint)"/>
        /// </summary>
        /// <param name="r">The red component of the mask color (0-1)</param>
        /// <param name="g">The green component of the mask color (0-1)</param>
        /// <param name="b">The blue component of the mask color (0-1)</param>
        /// <returns></returns>
        public static uint CreateColorKey(float r, float g, float b)
        {
            return ((uint)(r * 255.0f)) | ((uint)(g * 255.0f) << 8) | ((uint)(b * 255.0f) << 16);
        }

        public delegate bool ProcessEventDelegate(ref SDL_Event sdlEvent);

        /// <summary>
        /// The SDL_Window pointer for this window.
        /// </summary>
        public IntPtr Window { get; private set; }

        /// <summary>
        /// Whether an event has closed this window.
        /// </summary>
        public bool WantsClose { get; set; } = false;

        /// <summary>
        /// Delegate for providing user event handler methods that want to respond to SDL_Events
        /// </summary>
        public ProcessEventDelegate OnSDLEvent { get; set; }

        /// <summary>
        /// Initializes SDL and constructs a new window.
        /// </summary>
        /// <remarks>Fullscreen windows are borderless windowed with "always on top" behavior.  Be sure to add a way to close the window as the X will not be visible.</remarks>
        /// <param name="title">The window's title.  Note that this is hidden for fullscreen windows.</param>
        /// <param name="xPos">X position of the window.  Largely irrelevant for fullscreen.</param>
        /// <param name="yPos">Y position of the window.  Largely irrelevant for fullscreen.</param>
        /// <param name="width">Width of the window.  Unused for fullscreen.</param>
        /// <param name="height">Height of the window.  Unused for fullscreen.</param>
        /// <param name="fullscreen">Whether the window should be fullscreen.  Fullscreen windows are borderless windowed with "Always on top" behavior.</param>
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

        /// <summary>
        /// Gets the HWND of this window for interop with Windows methods.
        /// </summary>
        /// <returns>This window's HWND</returns>
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

        /// <summary>
        /// Converts this to a layered window and makes any region that matches <paramref name="transparentColorKey"/> fully transparent.
        /// Transparent regions behave as if they are not present, and can be clicked through etc.
        /// </summary>
        /// <seealso cref="CreateColorKey(float, float, float)"/>
        /// <param name="transparentColorKey"></param>
        public void MakeTransparent(uint transparentColorKey)
        {
            // yes these could be enums
            const int GWL_EXSTYLE = -20;
            const uint WS_EX_LAYERED = 0x80000;
            const uint LWA_COLORKEY = 1;
            // const uint LWA_ALPHA = 2;    // left for reference but unused

            var hWnd = GetHWnd();

            var oldFlags = GetWindowLong(hWnd, GWL_EXSTYLE);
            SetWindowLong(hWnd, GWL_EXSTYLE, oldFlags | WS_EX_LAYERED);
            SetLayeredWindowAttributes(hWnd, transparentColorKey, 0, LWA_COLORKEY);
        }

        /// <summary>
        /// Basic SDL event loop to consume all events and handle window closure.
        /// User handlers from <see cref="OnSDLEvent"/> are invoked for every event.
        /// </summary>
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
