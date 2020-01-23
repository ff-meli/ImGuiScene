using System;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;

using Device = SharpDX.Direct3D11.Device;

namespace ImGuiScene
{
    /// <summary>
    /// A simple wrapper for a minimal DirectX 11 renderer.  Consumers of this class will need to implement all actual pipeline and render logic externally.
    /// </summary>
    public class SimpleD3D : IRenderer
    {
        /// <summary>
        /// The type (API/version) of this renderer
        /// </summary>
        public RendererFactory.RendererBackend Type => RendererFactory.RendererBackend.DirectX11;

        private RawColor4 _clearColor;
        /// <summary>
        /// The renderer clear color used by <see cref="Clear"/>
        /// </summary>
        public System.Numerics.Vector4 ClearColor
        {
            get
            {
                return new System.Numerics.Vector4(_clearColor.R, _clearColor.G, _clearColor.B, _clearColor.A);
            }
            set
            {
                _clearColor = new RawColor4(value.X, value.Y, value.Z, value.W);
            }
        }

        private int _vsyncFlag = 1;
        /// <summary>
        /// Whether or not the renderer should sync presentation to the monitor's refresh rate.
        /// </summary>
        public bool Vsync
        {
            get => _vsyncFlag == 1;
            set
            {
                _vsyncFlag = value ? 1 : 0;
            }
        }

        /// <summary>
        /// Whether this renderer was created with debuggable state.
        /// </summary>
        public bool Debuggable { get; }

        private Device _device;
        private DeviceContext _deviceContext;
        private SwapChain _swapChain;
        private RenderTargetView _backBufferView;
        private ImGui_Impl_DX11 _backend = new ImGui_Impl_DX11();

        internal SimpleD3D(bool enableDebugging)
        {
            Debuggable = enableDebugging;
        }

        /// <summary>
        /// Initialize DirectX 11 for the specified window.
        /// </summary>
        /// <param name="sdlWindow">The SimpleSDLWindow to render into</param>
        public void AttachToWindow(SimpleSDLWindow sdlWindow)
        {
            // DX seems to need this to happen before context creation
            sdlWindow.Show();

            var hWnd = sdlWindow.GetHWnd();

            var desc = new SwapChainDescription
            {
                BufferCount = 1,
                ModeDescription = new ModeDescription
                {
                    Format = Format.R8G8B8A8_UNorm,
                    Width = 0,
                    Height = 0,
                    RefreshRate = new Rational(60, 1)
                },
                Usage = Usage.RenderTargetOutput,
                OutputHandle = hWnd,
                SampleDescription = new SampleDescription
                {
                    Count = 1,
                    Quality = 0
                },
                SwapEffect = SwapEffect.Discard,
                IsWindowed = true
            };

            Device.CreateWithSwapChain(DriverType.Hardware, Debuggable ? DeviceCreationFlags.Debug : DeviceCreationFlags.None, desc, out _device, out _swapChain);

            // disable alt-enter fullscreen toggle, and ignore prtscn in case it does anything
            using (var factory = _swapChain.GetParent<Factory>())
            {
                factory.MakeWindowAssociation(hWnd, WindowAssociationFlags.IgnoreAll);
            }

            using (var backBuffer = Texture2D.FromSwapChain<Texture2D>(_swapChain, 0))
            {
                _backBufferView = new RenderTargetView(_device, backBuffer);
            }

            _deviceContext = _device.ImmediateContext;

            // in theory this may not always work here... but it will for any actual uses of this class
            _deviceContext.OutputMerger.SetTargets(_backBufferView);
        }

        /// <summary>
        /// Clears the render target
        /// </summary>
        public void Clear()
        {
            _deviceContext.ClearRenderTargetView(_backBufferView, _clearColor);
        }

        /// <summary>
        /// Swap render buffers to the screen.  This is currently hardcoded to occur on vsync.
        /// </summary>
        public void Present()
        {
            _swapChain.Present(_vsyncFlag, PresentFlags.None);
        }

        /// <summary>
        /// Helper method to create a shader resource view from raw image data.
        /// </summary>
        /// <param name="pixelData">A pointer to the raw pixel data</param>
        /// <param name="width">The width of the image</param>
        /// <param name="height">The height of the image</param>
        /// <param name="bytesPerPixel">The bytes per pixel of the image, used for stride calculations</param>
        /// <returns>The wrapped ShaderResourceView created for the image, null on failure.</returns>
        /// <remarks>The ShaderResourceView created by this method is not managed, and it is up to calling code to invoke Dispose() when done</remarks>
        public unsafe TextureWrap CreateTexture(void* pixelData, int width, int height, int bytesPerPixel)
        {
            ShaderResourceView resView = null;

            var texDesc = new Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.R8G8B8A8_UNorm,    // TODO - support other formats?
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Immutable,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            };

            using (var texture = new Texture2D(_device, texDesc, new DataRectangle(new IntPtr(pixelData), width * bytesPerPixel)))
            {
                resView = new ShaderResourceView(_device, texture, new ShaderResourceViewDescription
                {
                    Format = texDesc.Format,
                    Dimension = ShaderResourceViewDimension.Texture2D,
                    Texture2D = { MipLevels = texDesc.MipLevels }
                });
            }

            // no sampler for now because the ImGui implementation we copied doesn't allow for changing it

            return new D3DTextureWrap(resView, width, height);
        }

        #region ImGui forwarding
        public void ImGui_Init()
        {
            _backend.Init(_device, _deviceContext);
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
                _deviceContext?.ClearState();
                _deviceContext?.Flush();
                _deviceContext?.Dispose();
                _deviceContext = null;

                _backBufferView?.Dispose();
                _backBufferView = null;

                _swapChain?.Dispose();
                _swapChain = null;

                _device?.Dispose();
                _device = null;

                disposedValue = true;
            }
        }

        ~SimpleD3D()
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
