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
    public sealed class RawDX11Scene : IDisposable
    {
        public Device _device;
        private SwapChain _swapChain;
        private DeviceContext _deviceContext;
        private RenderTargetView rtv;
        private IntPtr hWnd;
        private int targetWidth;
        private int targetHeight;

        private ImGui_Impl_DX11 _impl;

        public delegate void BuildUIDelegate();

        /// <summary>
        /// User methods invoked every ImGui frame to construct custom UIs.
        /// </summary>
        public BuildUIDelegate OnBuildUI;

        public RawDX11Scene(IntPtr nativeSwapChain)
        {
            _swapChain = new SwapChain(nativeSwapChain);
            _device = _swapChain.GetDevice<Device>();

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
            _device = new Device(nativeDevice);
            _swapChain = new SwapChain(nativeSwapChain);

            Initialize();
        }

        private void Initialize()
        {
            _deviceContext = _device.ImmediateContext;

            using (var bb = _swapChain.GetBackBuffer<Texture2D>(0))
            {
                rtv = new RenderTargetView(_device, bb);
            }

            // could also do things with GetClientRect() for hWnd, not sure if that is necessary
            targetWidth = _swapChain.Description.ModeDescription.Width;
            targetHeight = _swapChain.Description.ModeDescription.Height;

            hWnd = _swapChain.Description.OutputHandle;

            InitializeImGui();
        }

        public void Dispose()
        {
            _impl.Shutdown();
            ImGui_Input_Impl_Direct.Shutdown();

            ImGui.DestroyContext();

            rtv.Dispose();
        }

        private void InitializeImGui()
        {
            _impl = new ImGui_Impl_DX11();

            ImGui.CreateContext();

            ImGui_Input_Impl_Direct.Init(hWnd);
            _impl.Init(_device, _deviceContext);
        }

        public void Render()
        {
            _deviceContext.OutputMerger.SetRenderTargets(rtv);

            _impl.NewFrame();
            // could (should?) grab size every frame, or ideally handle resize somehow (we probably crash now)
            // but as long as we pretend we don't resize, this should be fine
            ImGui_Input_Impl_Direct.NewFrame(targetWidth, targetHeight);

            ImGui.NewFrame();
            OnBuildUI?.Invoke();
            ImGui.Render();

            _impl.RenderDrawData(ImGui.GetDrawData());

            _deviceContext.OutputMerger.SetRenderTargets((RenderTargetView)null);
        }

        public void SS(string path)
        {
            using (var backBuffer = _swapChain.GetBackBuffer<Texture2D>(0))
            {
                Texture2DDescription desc = backBuffer.Description;
                desc.CpuAccessFlags = CpuAccessFlags.Read;
                desc.Usage = ResourceUsage.Staging;
                desc.OptionFlags = ResourceOptionFlags.None;
                desc.BindFlags = BindFlags.None;

                using (var tex = new Texture2D(backBuffer.Device, desc))
                {
                    backBuffer.Device.ImmediateContext.CopyResource(backBuffer, tex);
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
    }
}
