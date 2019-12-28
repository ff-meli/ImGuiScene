using ImGuiNET;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;

using Device = SharpDX.Direct3D11.Device;

namespace ImGuiScene
{
    public sealed class RawDX11Scene
    {
        private Device _device;
        private SwapChain _swapChain;
        private DeviceContext _deviceContext;

        private ImGui_Impl_DX11 _impl;

        public RawDX11Scene(IntPtr nativeDevice, IntPtr nativeSwapChain)
        {
            _device = new Device(nativeDevice);
            _swapChain = new SwapChain(nativeSwapChain);
            _deviceContext = _device.ImmediateContext;

            InitializeImGui();
        }

        public RawDX11Scene(IntPtr nativeSwapChain)
        {
            _swapChain = new SwapChain(nativeSwapChain);
            _device = _swapChain.GetDevice<Device>();
            _deviceContext = _device.ImmediateContext;

            InitializeImGui();
        }

        private void InitializeImGui()
        {
            _impl = new ImGui_Impl_DX11();

            ImGui.CreateContext();

            _impl.Init(_device, _deviceContext);
        }

        public void Render()
        {
            _impl.NewFrame();

            ImGui.NewFrame();
                ImGui.ShowDemoWindow();
            ImGui.Render();

            _impl.RenderDrawData(ImGui.GetDrawData());
        }
    }
}
