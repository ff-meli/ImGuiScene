using ImGuiNET;
using System;
using System.Collections.Generic;
using static SDL2.SDL;
using static SDL2.SDL_image;

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

        private List<IDisposable> _allocatedResources = new List<IDisposable>();

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
        public SimpleImGuiScene(string title, int xPos = SDL_WINDOWPOS_UNDEFINED, int yPos = SDL_WINDOWPOS_UNDEFINED, int width = 0, int height = 0, bool fullscreen = false)
        {
            Window = new SimpleSDLWindow(title, xPos, yPos, width, height, fullscreen);
            D3D = new SimpleD3D(Window.GetHWnd());

            ImGui.CreateContext();

            ImGui_Impl_SDL.Init(Window.Window);
            ImGui_Impl_DX11.Init(D3D.Device, D3D.Context, false);

            Window.OnSDLEvent += ImGui_Impl_SDL.ProcessEvent;
        }

        /// <summary>
        /// Loads an image from a file and creates the corresponding DX texture
        /// </summary>
        /// <param name="path">The filepath to the image</param>
        /// <returns>The NativePointer associated with the loaded DX ShaderResourceView, suitable for direct use in ImGui, or IntPtr.Zero on failure.</returns>
        /// <remarks>Currently any textures created by this method are managed automatically and exist until this class object is Disposed.</remarks>
        public IntPtr LoadImage(string path)
        {
            var surface = IMG_Load(path);
            if (surface != null)
            {
                return LoadImage_Internal(surface);
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Loads an image from a byte array of image data and creates the corresponding DX texture
        /// </summary>
        /// <param name="imageBytes">The raw image data</param>
        /// <returns>The NativePointer associated with the loaded DX ShaderResourceView, suitable for direct use in ImGui, or IntPtr.Zero on failure.</returns>
        /// <remarks>Currently any textures created by this method are managed automatically and exist until this class object is Disposed.</remarks>
        public IntPtr LoadImage(byte[] imageBytes)
        {
            unsafe
            {
                fixed (byte* mem = imageBytes)
                {
                    var rw = SDL_RWFromConstMem((IntPtr)mem, imageBytes.Length);
                    var surface = IMG_Load_RW(rw, 1);
                    if (surface != null)
                    {
                        return LoadImage_Internal(surface);
                    }
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Internal helper to create a DX ShaderResourceView from an existing SDL_Surface*
        /// </summary>
        /// <param name="surface">The existing SDL_Surface* representing the image</param>
        /// <returns>The NativePointer associated with the loaded DX ShaderResourceView, suitable for direct use in ImGui, or IntPtr.Zero on failure.</returns>
        private IntPtr LoadImage_Internal(IntPtr surface)
        {
            IntPtr ret = IntPtr.Zero;

            unsafe
            {
                SDL_Surface* surf = (SDL_Surface*)surface;
                var bytesPerPixel = ((SDL_PixelFormat*)surf->format)->BytesPerPixel;

                var texture = D3D.CreateTexture(&surf->pixels, surf->w, surf->h, bytesPerPixel);
                if (texture != null)
                {
                    _allocatedResources.Add(texture);
                    ret = texture.NativePointer;
                }
            }

            return ret;
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

                _allocatedResources.ForEach(res => res.Dispose());
                _allocatedResources.Clear();

                // Probably not necessary, but may as well be nice and try to clean up in case we used anything
                // This is safe even if nothing from the library was used
                // We also never call IMG_Load() for now since it is done automatically where needed
                IMG_Quit();

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
