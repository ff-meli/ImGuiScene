using System;
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
    public class SimpleD3D : IDisposable
    {
        private Device _device;
        public Device Device
        {
            // need an exposed variable to use as an out param for device creation
            get { return _device; }
            private set { _device = value; }
        }

        /// <summary>
        /// The device's immediateContext
        /// </summary>
        public DeviceContext Context { get; private set; }

        /// <summary>
        /// The renderer clear color used by <see cref="Clear"/>
        /// </summary>
        public RawColor4 ClearColor { get; set; }

        // no reason to expose these at the moment
        private SwapChain _swapChain;
        private RenderTargetView _backBufferView;

        /// <summary>
        /// Initialized DirectX 11 for the specified window
        /// </summary>
        /// <param name="hWnd">The windows HWND of the target window to render into</param>
        public SimpleD3D(IntPtr hWnd)
        {
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

            Device.CreateWithSwapChain(DriverType.Hardware, DeviceCreationFlags.None, desc, out _device, out _swapChain);

            // disable alt-enter fullscreen toggle, and ignore prtscn in case it does anything
            using (var factory = _swapChain.GetParent<Factory>())
            {
                factory.MakeWindowAssociation(hWnd, WindowAssociationFlags.IgnoreAll);
            }

            using (var backBuffer = Texture2D.FromSwapChain<Texture2D>(_swapChain, 0))
            {
                _backBufferView = new RenderTargetView(_device, backBuffer);
            }

            Context = _device.ImmediateContext;

            // in theory this may not always work here... but it will for any actual uses of this class
            Context.OutputMerger.SetTargets(_backBufferView);
        }

        /// <summary>
        /// Clears the render target
        /// </summary>
        public void Clear()
        {
            Context.ClearRenderTargetView(_backBufferView, ClearColor);
        }

        /// <summary>
        /// Swap render buffers to the screen.  This is currently hardcoded to occur on vsync.
        /// </summary>
        public void Present()
        {
            _swapChain.Present(1, PresentFlags.None);
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
                Context?.ClearState();
                Context?.Flush();
                Context?.Dispose();
                Context = null;

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
