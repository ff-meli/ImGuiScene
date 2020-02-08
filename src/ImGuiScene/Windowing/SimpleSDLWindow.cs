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

        public delegate void ProcessEventDelegate(ref SDL_Event sdlEvent);

        /// <summary>
        /// The SDL_Window pointer for this window.
        /// </summary>
        public IntPtr Window { get; }

        /// <summary>
        /// Whether an event has closed this window.
        /// </summary>
        public bool WantsClose { get; set; } = false;

        /// <summary>
        /// Delegate for providing user event handler methods that want to respond to SDL_Events.
        /// </summary>
        public ProcessEventDelegate OnSDLEvent { get; set; }

        /// <summary>
        /// Creates a new SDL_Window with the given renderer attached.
        /// </summary>
        /// <param name="renderer">The renderer to attach to this window.</param>
        /// <param name="createInfo">The creation parameters to use when building this window.</param>
        public SimpleSDLWindow(IRenderer renderer, WindowCreateInfo createInfo)
        {
            if (SDL_Init(SDL_INIT_VIDEO) != 0)
            {
                throw new Exception("SDL_Init error: " + SDL_GetError());
            }

            InitForRenderer(renderer);

            var windowFlags = WindowCreationFlags(createInfo);
            if (createInfo.Fullscreen)
            {
                // without this, clicking off the window (eg, to another monitor) will minimize and cause positioning issues
                SDL_SetHint("SDL_VIDEO_MINIMIZE_ON_FOCUS_LOSS", "0");
            }

            Window = SDL_CreateWindow(createInfo.Title, createInfo.XPos, createInfo.YPos, createInfo.Width, createInfo.Height, windowFlags);
            if (Window == IntPtr.Zero)
            {
                SDL_Quit();
                throw new Exception("Failed to create window: " + SDL_GetError());
            }

            if (createInfo.TransparentColor != null)
            {
                var colorKey = CreateColorKey(createInfo.TransparentColor[0], createInfo.TransparentColor[1], createInfo.TransparentColor[2]);
                MakeTransparent(colorKey);
            }

            renderer.AttachToWindow(this);
        }

        protected virtual void InitForRenderer(IRenderer renderer)
        {
            // base has no extra work to do
        }

        /// <summary>
        /// Return the set of window flags necessary to create a window matching what is requested in <paramref name="createInfo"/>
        /// </summary>
        /// <param name="createInfo">The requested creation parameters for the window.</param>
        /// <returns>The full set of SDL_WindowFlags to use when creating this window.</returns>
        protected virtual SDL_WindowFlags WindowCreationFlags(WindowCreateInfo createInfo)
        {
            var flags = SDL_WindowFlags.SDL_WINDOW_ALLOW_HIGHDPI | SDL_WindowFlags.SDL_WINDOW_HIDDEN;
            if (createInfo.Fullscreen)
            {
                flags |= SDL_WindowFlags.SDL_WINDOW_BORDERLESS | SDL_WindowFlags.SDL_WINDOW_FULLSCREEN_DESKTOP | SDL_WindowFlags.SDL_WINDOW_SKIP_TASKBAR;
            }

            // Transparent windows are neat but almost certainly don't work as intended unless they are forced to remain on top of everything
            // also don't show the window in the taskbar since this is functioning as an overlay
            if (createInfo.TransparentColor != null)
            {
                flags |= SDL_WindowFlags.SDL_WINDOW_ALWAYS_ON_TOP | SDL_WindowFlags.SDL_WINDOW_SKIP_TASKBAR;
            }

            return flags;
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
        protected void MakeTransparent(uint transparentColorKey)
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

        public void Show()
        {
            SDL_ShowWindow(Window);
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
