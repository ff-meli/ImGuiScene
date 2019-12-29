using ImGuiNET;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;

using Device = SharpDX.Direct3D11.Device;

namespace ImGuiScene
{
    public sealed class RawDX11Scene : IDisposable
    {
        private Device _device;
        private SwapChain _swapChain;
        private DeviceContext _deviceContext;

#if false
        Format _backBufferFormat;
        private Texture2D _backBuffer;
        private Texture2D _backBufferResolved;
        private Texture2D _backBufferTexture;
        private RenderTargetView _backBufferRTV0;
        private RenderTargetView _backBufferRTV1;
        private RenderTargetView _backBufferRTV2;
        private ShaderResourceView _backbufferTextureSrv0;
        private ShaderResourceView _backbufferTextureSrv1;
        private VertexShader _copyVertexShader;
        private PixelShader _copyPixelShader;
        private SamplerState _copySamplerState;
        private RasterizerState _effectRasterizerState;
#endif

        private ImGui_Impl_DX11 _impl;

        //public RawDX11Scene(IntPtr nativeDevice, IntPtr nativeSwapChain)
        //{
        //    _device = new Device(nativeDevice);
        //    _swapChain = new SwapChain(nativeSwapChain);
        //    _deviceContext = _device.ImmediateContext;

        //    InitializeImGui();
        //}

        public RawDX11Scene(IntPtr nativeSwapChain)
        {
            _swapChain = new SwapChain(nativeSwapChain);
            _device = _swapChain.GetDevice<Device>();
            _deviceContext = _device.ImmediateContext;

#if false
            _backBuffer = _swapChain.GetBackBuffer<Texture2D>(0);
            _backBufferFormat = _backBuffer.Description.Format;

            var texDesc = new Texture2DDescription
            {
                Width = _backBuffer.Description.Width,
                Height = _backBuffer.Description.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = FormatUtils.MakeTypeless(_backBufferFormat),
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget
            };

            _backBufferResolved = new Texture2D(_device, texDesc);
            _backBufferRTV2 = new RenderTargetView(_device, _backBuffer);

            texDesc.BindFlags = BindFlags.ShaderResource;
            _backBufferTexture = new Texture2D(_device, texDesc);

            var srvDesc = new ShaderResourceViewDescription
            {
                Format = FormatUtils.MakeNormal(texDesc.Format),
                Dimension = ShaderResourceViewDimension.Texture2D,
                Texture2D = { MipLevels = texDesc.MipLevels }
            };
            _backbufferTextureSrv0 = new ShaderResourceView(_device, _backBufferTexture, srvDesc);
            srvDesc.Format = FormatUtils.MakeSrgb(texDesc.Format);
            _backbufferTextureSrv1 = new ShaderResourceView(_device, _backBufferTexture, srvDesc);

            var rtvDesc = new RenderTargetViewDescription
            {
                Format = FormatUtils.MakeNormal(texDesc.Format),
                Dimension = RenderTargetViewDimension.Texture2D
            };
            _backBufferRTV0 = new RenderTargetView(_device, _backBufferResolved, rtvDesc);
            rtvDesc.Format = FormatUtils.MakeSrgb(texDesc.Format);
            _backBufferRTV1 = new RenderTargetView(_device, _backBufferResolved, rtvDesc);

            byte[] shaderData;

            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("fullscreen-vs.hlsl.bytes"))
            {
                shaderData = new byte[stream.Length];
                stream.Read(shaderData, 0, shaderData.Length);
            }
            _copyVertexShader = new VertexShader(_device, shaderData);

            using (var stream = assembly.GetManifestResourceStream("copy-ps.hlsl.bytes"))
            {
                shaderData = new byte[stream.Length];
                stream.Read(shaderData, 0, shaderData.Length);
            }
            _copyPixelShader = new PixelShader(_device, shaderData);

            _copySamplerState = new SamplerState(_device, new SamplerStateDescription
            {
                Filter = Filter.MinMagMipPoint,
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp
            });

            _effectRasterizerState = new RasterizerState(_device, new RasterizerStateDescription
            {
                FillMode = FillMode.Solid,
                CullMode = CullMode.None,
                IsDepthClipEnabled = true
            });
#endif

            InitializeImGui();
        }

        public void Dispose()
        {
            _impl.Shutdown();
            ImGui.DestroyContext();

            //_backBufferRTV0.Dispose();
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

            //_deviceContext.OutputMerger.SetRenderTargets(_backBufferRTV0);

            ImGui.GetIO().DisplaySize.X = 1920;
            ImGui.GetIO().DisplaySize.Y = 1080;
            ImGui.GetIO().DisplayFramebufferScale.X = 1f;
            ImGui.GetIO().DisplayFramebufferScale.Y = 1f;
            ImGui.GetIO().DeltaTime = 1f / 60;

            ImGui.NewFrame();
            ImGui.ShowDemoWindow();
            ImGui.Render();

            _impl.RenderDrawData(ImGui.GetDrawData());

#if false
            ShaderResourceView srv = (_backBufferFormat == FormatUtils.MakeSrgb(_backBufferFormat)) ? _backbufferTextureSrv1 : _backbufferTextureSrv0;

            _deviceContext.CopyResource(_backBufferResolved, _backBufferTexture);

            _deviceContext.InputAssembler.InputLayout = null;
            _deviceContext.InputAssembler.SetVertexBuffers(0, null);
            _deviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            _deviceContext.VertexShader.Set(_copyVertexShader);
            _deviceContext.GeometryShader.Set(null);
            _deviceContext.HullShader.Set(null);
            _deviceContext.DomainShader.Set(null);
            _deviceContext.PixelShader.Set(_copyPixelShader);
            _deviceContext.PixelShader.SetSampler(0, _copySamplerState);
            _deviceContext.PixelShader.SetShaderResource(0, srv);
            _deviceContext.Rasterizer.State = _effectRasterizerState;
            _deviceContext.Rasterizer.SetViewport(0, 0, 1920, 1080);
            _deviceContext.OutputMerger.BlendState = null;
            _deviceContext.OutputMerger.DepthStencilState = null;
            _deviceContext.OutputMerger.SetRenderTargets(_backBufferRTV2);

            _deviceContext.Draw(3, 0);
#endif
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
