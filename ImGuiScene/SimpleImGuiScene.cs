using ImGuiNET;
using System;

namespace ImGuiScene
{
    /// <summary>
    /// Simple class to wrap everything necessary to use ImGui inside a window.
    /// Currently this always creates a new window rather than take ownership of an existing one.
    /// 
    /// Internally this uses SDL and DirectX 11.  Rendering is tied to vsync.
    /// </summary>
    public class SimpleImGuiScene : IDisposable
    {
        public SimpleSDLWindow Window { get; private set; }
        public SimpleD3D D3D { get; private set; }

        /// <summary>
        /// Whether the user application has requested the system to terminate
        /// </summary>
        public bool ShouldQuit { get; set; } = false;

        public delegate void BuildUIDelegate();

        /// <summary>
        /// User methods invoked every ImGui frame to construct custom UIs
        /// </summary>
        public BuildUIDelegate OnBuildUI;

        /// <summary>
        /// Constructs a new window, initializes DX11 inside it and bootstraps ImGui.
        /// </summary>
        /// <remarks>Fullscreen windows are borderless windowed with "always on top" behavior.  Be sure to add a way to close the window as the X will not be visible.</remarks>
        /// <param name="title">The window's title.  Note that this is hidden for fullscreen windows.</param>
        /// <param name="xPos">X position of the window.  Largely irrelevant for fullscreen.</param>
        /// <param name="yPos">Y position of the window.  Largely irrelevant for fullscreen.</param>
        /// <param name="width">Width of the window.  Unused for fullscreen.</param>
        /// <param name="height">Height of the window.  Unused for fullscreen.</param>
        /// <param name="fullscreen">Whether the window should be fullscreen.  Fullscreen windows are borderless windowed with "Always on top" behavior.</param>
        public SimpleImGuiScene(string title, int xPos = SDL2.SDL.SDL_WINDOWPOS_UNDEFINED, int yPos = SDL2.SDL.SDL_WINDOWPOS_UNDEFINED, int width = 0, int height = 0, bool fullscreen = false)
        {
            Window = new SimpleSDLWindow(title, xPos, yPos, width, height, fullscreen);
            D3D = new SimpleD3D(Window.GetHWnd());

            ImGui.CreateContext();

            ImGui_Impl_SDL.Init(Window.Window);
            ImGui_Impl_DX11.Init(D3D.Device, D3D.Context, false);

            Window.OnSDLEvent += ImGui_Impl_SDL.ProcessEvent;
        }

        /// <summary>
        /// Performs a single-frame update of ImGui and renders it to the window.
        /// This method does not check any quit conditions.
        /// </summary>
        public void Update()
        {
            Window.ProcessEvents();

            ImGui_Impl_DX11.NewFrame();
            ImGui_Impl_SDL.NewFrame();

            ImGui.NewFrame();
                OnBuildUI?.Invoke();
            ImGui.Render();

            D3D.Clear();

            ImGui_Impl_DX11.RenderDrawData(ImGui.GetDrawData());

            D3D.Present();
        }

        /// <summary>
        /// Simple method to run the scene in a loop until the window is closed or the application
        /// requests an exit (via <see cref="ShouldQuit"/>)
        /// </summary>
        public void Run()
        {
            // For now we consider the window closing to be a quit request
            // while ShouldQuit is used for external/application close requests
            while (!Window.WantsClose && !ShouldQuit)
            {
                Update();
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
                ImGui_Impl_DX11.Shutdown();
                ImGui_Impl_SDL.Shutdown();

                ImGui.DestroyContext();

                D3D?.Dispose();
                D3D = null;

                Window?.Dispose();
                Window = null;

                disposedValue = true;
            }
        }

        ~SimpleImGuiScene()
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
