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
    /// Internally this uses SDL and DirectX 11 or OpenGL 3.2.  Rendering is tied to vsync.
    /// </summary>
    public class SimpleImGuiScene : IDisposable
    {
        /// <summary>
        /// The main application container window where we do all our rendering and input processing.
        /// </summary>
        public SimpleSDLWindow Window { get; private set; }

        /// <summary>
        /// The renderer backend being used to render into this window.
        /// </summary>
        public IRenderer Renderer { get; private set; }

        /// <summary>
        /// Whether the user application has requested the system to terminate.
        /// </summary>
        public bool ShouldQuit { get; set; } = false;

        public delegate void BuildUIDelegate();

        /// <summary>
        /// User methods invoked every ImGui frame to construct custom UIs.
        /// </summary>
        public BuildUIDelegate OnBuildUI;

        /// <summary>
        /// Delegate for providing user event handler methods that want to respond to SDL_Events.
        /// This is just a convenience wrapper around <see cref="SimpleSDLWindow.OnSDLEvent"/>.
        /// </summary>
        public SimpleSDLWindow.ProcessEventDelegate OnSDLEvent
        {
            get => Window.OnSDLEvent;
            set { Window.OnSDLEvent = value; }
        }

        private List<IDisposable> _allocatedResources = new List<IDisposable>();

        /// <summary>
        /// Helper method to create a fullscreen transparent overlay that exits when pressing the specified key.
        /// </summary>
        /// <param name="rendererBackend">Which rendering backend to use.</param>
        /// <param name="closeOverlayKey">Which <see cref="SDL_Scancode"/> to listen for in order to exit the scene.  Defaults to <see cref="SDL_Scancode.SDL_SCANCODE_ESCAPE"/>.</param>
        /// <param name="transparentColor">A float[4] representing the background window color that will be masked as transparent.  Defaults to solid black.</param>
        /// <param name="enableRenderDebugging">Whether to enable debugging of the renderer internals.  This will likely greatly impact performance and is not usually recommended.</param>
        /// <returns></returns>
        public static SimpleImGuiScene CreateOverlay(RendererFactory.RendererBackend rendererBackend, SDL_Scancode closeOverlayKey = SDL_Scancode.SDL_SCANCODE_ESCAPE, float[] transparentColor = null, bool enableRenderDebugging = false)
        {
            var scene = new SimpleImGuiScene(rendererBackend, new WindowCreateInfo
            {
                Title = "ImGui Overlay",
                Fullscreen = true,
                TransparentColor = transparentColor ?? new float[] { 0, 0, 0, 0 }
            }, enableRenderDebugging);

            // Add a simple handler for the close key, so user classes don't have to bother with events in most cases
            scene.OnSDLEvent += (ref SDL_Event sdlEvent) =>
            {
                if (sdlEvent.type == SDL_EventType.SDL_KEYDOWN && sdlEvent.key.keysym.scancode == closeOverlayKey)
                {
                    scene.ShouldQuit = true;
                    return true;
                }

                return false;
            };

            return scene;
        }

        /// <summary>
        /// Creates a new window and a new renderer of the specified type, and initializes ImGUI.
        /// </summary>
        /// <param name="backend">Which rendering backend to use.</param>
        /// <param name="createInfo">Creation details for the window.</param>
        /// <param name="enableRenderDebugging">Whether to enable debugging of the renderer internals.  This will likely greatly impact performance and is not usually recommended.</param>
        public SimpleImGuiScene(RendererFactory.RendererBackend rendererBackend, WindowCreateInfo createInfo, bool enableRenderDebugging = false)
        {
            Renderer = RendererFactory.CreateRenderer(rendererBackend, enableRenderDebugging);
            Window = WindowFactory.CreateForRenderer(Renderer, createInfo);

            ImGui.CreateContext();

            ImGui_Impl_SDL.Init(Window.Window);
            Renderer.ImGui_Init();

            Window.OnSDLEvent += ImGui_Impl_SDL.ProcessEvent;
        }

        /// <summary>
        /// Loads an image from a file and creates the corresponding GPU texture.
        /// </summary>
        /// <param name="path">The filepath to the image</param>
        /// <returns>A <see cref="TextureWrap"/> associated with the loaded texture resource, containing a handle suitable for direct use in ImGui, or null on failure.</returns>
        /// <remarks>Currently any textures created by this method are managed automatically and exist until this class object is Disposed.</remarks>
        public TextureWrap LoadImage(string path)
        {
            var surface = IMG_Load(path);
            if (surface != IntPtr.Zero)
            {
                return LoadImage_Internal(surface);
            }

            return null;
        }

        /// <summary>
        /// Loads an image from a byte array of image data and creates the corresponding texture resource.
        /// </summary>
        /// <param name="imageBytes">The raw image data</param>
        /// <returns>A <see cref="TextureWrap"/> associated with the loaded texture resource, containing a handle suitable for direct use in ImGui, or null on failure.</returns>
        /// <remarks>Currently any textures created by this method are managed automatically and exist until this class object is Disposed.</remarks>
        public TextureWrap LoadImage(byte[] imageBytes)
        {
            unsafe
            {
                fixed (byte* mem = imageBytes)
                {
                    var rw = SDL_RWFromConstMem((IntPtr)mem, imageBytes.Length);
                    var surface = IMG_Load_RW(rw, 1);
                    if (surface != IntPtr.Zero)
                    {
                        return LoadImage_Internal(surface);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Internal helper to create a texture resource from an existing SDL_Surface*
        /// </summary>
        /// <param name="surface">The existing SDL_Surface* representing the image</param>
        /// <returns>A <see cref="TextureWrap"/> associated with the loaded texture resource, containing a handle suitable for direct use in ImGui, or null on failure.</returns>
        private TextureWrap LoadImage_Internal(IntPtr surface)
        {
            TextureWrap ret = null;

            unsafe
            {
                SDL_Surface* surf = (SDL_Surface*)surface;
                var bytesPerPixel = ((SDL_PixelFormat*)surf->format)->BytesPerPixel;

                var texture = Renderer.CreateTexture((void*)surf->pixels, surf->w, surf->h, bytesPerPixel);
                if (texture != null)
                {
                    _allocatedResources.Add(texture);
                    ret = texture;
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

            Renderer.ImGui_NewFrame();
            ImGui_Impl_SDL.NewFrame();

            ImGui.NewFrame();
                OnBuildUI?.Invoke();
            ImGui.Render();

            Renderer.Clear();

            Renderer.ImGui_RenderDrawData(ImGui.GetDrawData());

            Renderer.Present();
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

                Renderer.ImGui_Shutdown();
                ImGui_Impl_SDL.Shutdown();

                ImGui.DestroyContext();

                _allocatedResources.ForEach(res => res.Dispose());
                _allocatedResources.Clear();

                // Probably not necessary, but may as well be nice and try to clean up in case we used anything
                // This is safe even if nothing from the library was used
                // We also never call IMG_Load() for now since it is done automatically where needed
                IMG_Quit();

                Renderer?.Dispose();
                Renderer = null;

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
