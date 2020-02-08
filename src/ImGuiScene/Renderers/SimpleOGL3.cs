using OpenGL;
using System;
using System.Numerics;
using static SDL2.SDL;

namespace ImGuiScene
{
    /// <summary>
    /// A simple wrapper for a minimal OpenGL 3.2 renderer.  Consumers of this class will need to implement all actual pipeline and render logic externally.
    /// </summary>
    public class SimpleOGL3 : IRenderer
    {
        public int ContextMajorVersion => 3;
        public int ContextMinorVersion => 2;

        /// <summary>
        /// The type (API/version) of this renderer
        /// </summary>
        public RendererFactory.RendererBackend Type => RendererFactory.RendererBackend.OpenGL3;

        private Vector4 _clearColor;
        /// <summary>
        /// The clear color used by <see cref="Clear"/>
        /// </summary>
        public Vector4 ClearColor {
            get => _clearColor;
            set
            {
                _clearColor = value;
                Gl.ClearColor(value.X, value.Y, value.Z, value.W);
            }
        }

        private bool _vsync = true;
        /// <summary>
        /// Whether or not the renderer should sync presentation to the monitor's refresh rate.
        /// </summary>
        public bool Vsync
        {
            get => _vsync;
            set
            {
                _vsync = value;
                if (_glContext != IntPtr.Zero)
                {
                    SDL_GL_SetSwapInterval(_vsync ? 1 : 0);
                }
            }
        }

        /// <summary>
        /// Whether this renderer was created with debuggable state.
        /// </summary>
        public bool Debuggable { get; }

        private ImGui_Impl_OpenGL3 _backend = new ImGui_Impl_OpenGL3();
        private IntPtr _glContext;
        private DeviceContext _deviceContext;
        private SimpleSDLWindow _window;
        private Gl.DebugProc _debugProc;

        // This isn't really a great place to do this
        internal struct WindowBufferConfig
        {
            public int RedBits;
            public int GreenBits;
            public int BlueBits;
            public int AlphaBits;
            public int DepthBits;
            public int StencilBits;
        }
        // entirely hardcoded for now
        internal WindowBufferConfig WindowBufferParams => new WindowBufferConfig
        {
            RedBits = 8,
            GreenBits = 8,
            BlueBits = 8,
            AlphaBits = 8,
            DepthBits = 24,
            StencilBits = 8
        };

        internal SimpleOGL3(bool enableDebugging = false)
        {
            Debuggable = enableDebugging;
        }

        /// <summary>
        /// Initialize OpenGL for the specified window.
        /// </summary>
        /// <param name="sdlWindow">The SimpleSDLWindow to render into</param>
        public void AttachToWindow(SimpleSDLWindow sdlWindow)
        {
            _window = sdlWindow;

            _glContext = SDL_GL_CreateContext(sdlWindow.Window);
            SDL_GL_MakeCurrent(sdlWindow.Window, _glContext);
            SDL_GL_SetSwapInterval(Vsync ? 1 : 0);

            // because duplicating logic is always exciting
            // This doesn't (shouldn't?) create an actual additional gl context, but is necessary for OpenGl.NET Gl.* commands to work
            _deviceContext = DeviceContext.Create(IntPtr.Zero, sdlWindow.GetHWnd());
            _deviceContext.MakeCurrent(_glContext);

            if (Debuggable)
            {
                Gl.Enable((EnableCap)37600);    // GL_DEBUG_OUTPUT
                Gl.Enable((EnableCap)33346);    // GL_DEBUG_OUTPUT_SYNCHRONOUS

                _debugProc = GL_DebugCallback;
                Gl.DebugMessageCallback(_debugProc, IntPtr.Zero);
                // ALL THE THINGS!
                Gl.DebugMessageControl(DebugSource.DontCare, DebugType.DontCare, DebugSeverity.DontCare, new uint[] { }, true);
            }

            // We defer this until everything is set up to avoid flicker/whiteout
            sdlWindow.Show();
        }

        /// <summary>
        /// Clear the render view.
        /// </summary>
        public void Clear()
        {
            Gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
        }

        /// <summary>
        /// Swap the render buffer to the screen.
        /// </summary>
        public void Present()
        {
            // TODO: figure out if these differ at all
            SDL_GL_SwapWindow(_window.Window);
            //_deviceContext.SwapBuffers();
        }

        // <summary>
        /// Helper method to create a shader resource view from raw image data.
        /// </summary>
        /// <param name="pixelData">A pointer to the raw pixel data</param>
        /// <param name="width">The width of the image</param>
        /// <param name="height">The height of the image</param>
        /// <param name="bytesPerPixel">The bytes per pixel of the image, used for stride calculations</param>
        /// <returns>The wrapped gl texture id created for the image, null on failure.</returns>
        /// <remarks>The gl texture created by this method is not managed, and it is up to calling code to invoke Dispose() when done</remarks>
        public unsafe TextureWrap CreateTexture(void* pixelData, int width, int height, int bytesPerPixel)
        {
            Gl.GetInteger(GetPName.TextureBinding2d, out int lastTexture);

            var texture = Gl.GenTexture();
            Gl.BindTexture(TextureTarget.Texture2d, texture);
            Gl.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMinFilter, TextureMinFilter.Linear);
            Gl.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, TextureMagFilter.Linear);
            Gl.TexImage2D(TextureTarget.Texture2d, 0, InternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, new IntPtr(pixelData));

            Gl.BindTexture(TextureTarget.Texture2d, (uint)lastTexture);

            return new GLTextureWrap(texture, width, height);
        }

        private void GL_DebugCallback(DebugSource source, DebugType type, uint id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
        {
            //string msg = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(message);
            //Console.WriteLine(msg);
            // Could do something with this but it's more for breakpointing and looking into
            // should probably have a user-provided callback
        }

        #region ImGui forwarding
        public void ImGui_Init()
        {
            _backend.Init();
        }

        public void ImGui_Shutdown()
        {
            _backend.Shutdown();
        }

        public void ImGui_NewFrame()
        {
            _backend.NewFrame();
        }

        public void ImGui_RenderDrawData(ImGuiNET.ImDrawDataPtr drawData)
        {
            _backend.RenderDrawData(drawData);
        }
        #endregion

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

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
                _deviceContext?.Dispose();
                _deviceContext = null;

                SDL_GL_DeleteContext(_glContext);
                _glContext = IntPtr.Zero;

                disposedValue = true;
            }
        }

        ~SimpleOGL3()
        {
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

    /// <summary>
    /// OpenGL 3 Implementation of <see cref="TextureWrap"/>.
    /// Provides a simple wrapped view of the disposeable resource as well as the handle for ImGui.
    /// </summary>
    public class GLTextureWrap : TextureWrap
    {
        public IntPtr ImGuiHandle { get; }
        public int Width { get; }
        public int Height { get; }

        public GLTextureWrap(uint texture, int width, int height)
        {
            ImGuiHandle = (IntPtr)texture;
            Width = width;
            Height = height;
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
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                var textureId = (uint)ImGuiHandle;
                if (textureId != 0)
                {
                    Gl.DeleteTextures(textureId);
                }

                disposedValue = true;
            }
        }

        ~GLTextureWrap()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
