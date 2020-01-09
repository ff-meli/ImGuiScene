using ImGuiNET;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using Device = SharpDX.Direct3D11.Device;

namespace ImGuiScene
{
    // This class will likely eventually be unified a bit more with other scenes, but for
    // now it should be directly useable
    public sealed class RawDX11Scene : IDisposable
    {
        private Device device;
        private SwapChain swapChain;
        private DeviceContext deviceContext;
        private RenderTargetView rtv;
        private IntPtr hWnd;
        private int targetWidth;
        private int targetHeight;

        private ImGui_Impl_DX11 imguiRenderer;

        public delegate void BuildUIDelegate();

        /// <summary>
        /// User methods invoked every ImGui frame to construct custom UIs.
        /// </summary>
        public BuildUIDelegate OnBuildUI;

        public RawDX11Scene(IntPtr nativeSwapChain)
        {
            this.swapChain = new SwapChain(nativeSwapChain);
            this.device = swapChain.GetDevice<Device>();

            Initialize();
        }

        // This ctor will work fine, but it's only usefulness over using just the swapchain version
        // is that this one will allow you to pass a different device than the swapchain.GetDevice() would
        // return.  This is mostly useful for render debugging, where the real d3ddevice is hooked and
        // where we would like all our work to be done on that hooked device.
        // Because we generally will get the swapchain from the internal present() call, we are getting
        // the real d3d swapchain and not a hooked version, so GetDevice() will correspondingly return
        // the read device and not a hooked verison.
        // By passing in the hooked version explicitly here, we can mostly play nice with debug tools
        public RawDX11Scene(IntPtr nativeDevice, IntPtr nativeSwapChain)
        {
            this.device = new Device(nativeDevice);
            this.swapChain = new SwapChain(nativeSwapChain);

            Initialize();
        }

        private void Initialize()
        {
            this.deviceContext = this.device.ImmediateContext;

            using (var backbuffer = this.swapChain.GetBackBuffer<Texture2D>(0))
            {
                this.rtv = new RenderTargetView(this.device, backbuffer);
            }

            // could also do things with GetClientRect() for hWnd, not sure if that is necessary
            this.targetWidth = this.swapChain.Description.ModeDescription.Width;
            this.targetHeight = this.swapChain.Description.ModeDescription.Height;

            this.hWnd = this.swapChain.Description.OutputHandle;

            InitializeImGui();
        }

        private void InitializeImGui()
        {
            this.imguiRenderer = new ImGui_Impl_DX11();

            ImGui.CreateContext();

            ImGui_Input_Impl_Direct.Init(hWnd);
            this.imguiRenderer.Init(this.device, this.deviceContext);
        }

        public void Render()
        {
            this.deviceContext.OutputMerger.SetRenderTargets(this.rtv);

            this.imguiRenderer.NewFrame();
            // could (should?) grab size every frame, or ideally handle resize somehow (we probably crash now)
            // but as long as we pretend we don't resize, this should be fine
            ImGui_Input_Impl_Direct.NewFrame(targetWidth, targetHeight);

            ImGui.NewFrame();
                OnBuildUI?.Invoke();
            ImGui.Render();

            this.imguiRenderer.RenderDrawData(ImGui.GetDrawData());

            this.deviceContext.OutputMerger.SetRenderTargets((RenderTargetView)null);
        }

        public void TakeScreenshot(string path)
        {
            using (var backBuffer = this.swapChain.GetBackBuffer<Texture2D>(0))
            {
                Texture2DDescription desc = backBuffer.Description;
                desc.CpuAccessFlags = CpuAccessFlags.Read;
                desc.Usage = ResourceUsage.Staging;
                desc.OptionFlags = ResourceOptionFlags.None;
                desc.BindFlags = BindFlags.None;

                using (var tex = new Texture2D(this.device, desc))
                {
                    this.deviceContext.CopyResource(backBuffer, tex);
                    using (var surf = tex.QueryInterface<Surface>())
                    {
                        var map = surf.Map(SharpDX.DXGI.MapFlags.Read, out DataStream dataStream);
                        var pixelData = new byte[surf.Description.Width * surf.Description.Height * surf.Description.Format.SizeOfInBytes()];
                        var dataCounter = 0;

                        while (dataCounter < pixelData.Length)
                        {
                            //var curPixel = dataStream.Read<uint>();
                            var x = dataStream.Read<byte>();
                            var y = dataStream.Read<byte>();
                            var z = dataStream.Read<byte>();
                            var w = dataStream.Read<byte>();

                            pixelData[dataCounter++] = z;
                            pixelData[dataCounter++] = y;
                            pixelData[dataCounter++] = x;
                            pixelData[dataCounter++] = w;
                        }

                        // TODO: test this on a thread
                        var gch = GCHandle.Alloc(pixelData, GCHandleType.Pinned);
                        using (var bitmap = new Bitmap(surf.Description.Width, surf.Description.Height, map.Pitch, PixelFormat.Format32bppRgb, gch.AddrOfPinnedObject()))
                        {
                            bitmap.Save(path);
                        }

                        gch.Free();
                        surf.Unmap();
                        dataStream.Dispose();
                    }
                }
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                this.imguiRenderer.Shutdown();
                ImGui_Input_Impl_Direct.Shutdown();

                ImGui.DestroyContext();

                this.rtv.Dispose();

                // Not actually sure how sharpdx does ref management, but hopefully they
                // addref when we create our wrappers, so this should just release that count
                this.swapChain.Dispose();
                this.deviceContext.Dispose();
                this.device.Dispose();

                disposedValue = true;
            }
        }

        ~RawDX11Scene()
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
