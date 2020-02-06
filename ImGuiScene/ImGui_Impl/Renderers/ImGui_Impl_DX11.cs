using ImGuiNET;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace ImGuiScene
{
    /// <summary>
    /// Currently undocumented because it is a horrible mess.
    /// A near-direct port of https://github.com/ocornut/imgui/blob/master/examples/imgui_impl_dx11.cpp
    /// State backup follows the general layout of imgui's sample (which is a mess), but has been rather
    /// expanded to cover the vast majority of render state, following the example here
    /// https://github.com/GPUOpen-LibrariesAndSDKs/CrossfireAPI11/blob/master/amd_lib/src/AMD_SaveRestoreState.cpp
    /// Would be nice to organize it better, but it seems to work
    /// </summary>
    public class ImGui_Impl_DX11 : IImGuiRenderer
    {
        private IntPtr _renderNamePtr;
        private Device _device;
        private DeviceContext _deviceContext;
        private ShaderResourceView _fontResourceView;
        private SamplerState _fontSampler;
        private VertexShader _vertexShader;
        private PixelShader _pixelShader;
        private InputLayout _inputLayout;
        private Buffer _vertexConstantBuffer;
        private BlendState _blendState;
        private RasterizerState _rasterizerState;
        private DepthStencilState _depthStencilState;
        private Buffer _vertexBuffer;
        private Buffer _indexBuffer;
        private int _vertexBufferSize;
        private int _indexBufferSize;
        private VertexBufferBinding _vertexBinding;
        // so we don't make a temporary object every frame
        private RawColor4 _blendColor = new RawColor4(0, 0, 0, 0);

        private struct StateBackup
        {
            // IA
            public InputLayout InputLayout;
            public PrimitiveTopology PrimitiveTopology;
            public Buffer IndexBuffer;
            public Format IndexBufferFormat;
            public int IndexBufferOffset;
            public Buffer[] VertexBuffers;
            public int[] VertexBufferStrides;
            public int[] VertexBufferOffsets;

            // RS
            public RasterizerState RS;
            public Rectangle[] ScissorRects;
            public RawViewportF[] Viewports;
            
            // OM
            public BlendState BlendState;
            public RawColor4 BlendFactor;
            public int SampleMask;
            public DepthStencilState DepthStencilState;
            public int DepthStencilRef;
            public DepthStencilView DepthStencilView;
            public RenderTargetView[] RenderTargetViews;

            // VS
            public VertexShader VS;
            public Buffer[] VSConstantBuffers;
            public SamplerState[] VSSamplers;
            public ShaderResourceView[] VSResourceViews;

            // HS
            public HullShader HS;
            public Buffer[] HSConstantBuffers;
            public SamplerState[] HSSamplers;
            public ShaderResourceView[] HSResourceViews;

            // DS
            public DomainShader DS;
            public Buffer[] DSConstantBuffers;
            public SamplerState[] DSSamplers;
            public ShaderResourceView[] DSResourceViews;

            // GS
            public GeometryShader GS;
            public Buffer[] GSConstantBuffers;
            public SamplerState[] GSSamplers;
            public ShaderResourceView[] GSResourceViews;

            // PS
            public PixelShader PS;
            public Buffer[] PSConstantBuffers;
            public SamplerState[] PSSamplers;
            public ShaderResourceView[] PSResourceViews;

            public ComputeShader CS;
            public Buffer[] CSConstantBuffers;
            public SamplerState[] CSSamplers;
            public ShaderResourceView[] CSResourceViews;
            public UnorderedAccessView[] CSUAVs;
        }

        private StateBackup BackupRenderState()
        {
            var backup = new StateBackup();
            backup.ScissorRects = new Rectangle[16];    // I couldn't find D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE as a SharpDX enum
            backup.Viewports = new RawViewportF[16];
            backup.VertexBuffers = new Buffer[InputAssemblerStage.VertexInputResourceSlotCount];
            backup.VertexBufferStrides = new int[InputAssemblerStage.VertexInputResourceSlotCount];
            backup.VertexBufferOffsets = new int[InputAssemblerStage.VertexInputResourceSlotCount];

            // IA
            backup.InputLayout = _deviceContext.InputAssembler.InputLayout;
            _deviceContext.InputAssembler.GetIndexBuffer(out backup.IndexBuffer, out backup.IndexBufferFormat, out backup.IndexBufferOffset);
            backup.PrimitiveTopology = _deviceContext.InputAssembler.PrimitiveTopology;
            _deviceContext.InputAssembler.GetVertexBuffers(0, InputAssemblerStage.VertexInputResourceSlotCount, backup.VertexBuffers, backup.VertexBufferStrides, backup.VertexBufferOffsets);

            // RS
            backup.RS = _deviceContext.Rasterizer.State;
            _deviceContext.Rasterizer.GetScissorRectangles<Rectangle>(backup.ScissorRects);
            _deviceContext.Rasterizer.GetViewports<RawViewportF>(backup.Viewports);

            // OM
            backup.BlendState = _deviceContext.OutputMerger.GetBlendState(out backup.BlendFactor, out backup.SampleMask);
            backup.DepthStencilState = _deviceContext.OutputMerger.GetDepthStencilState(out backup.DepthStencilRef);
            backup.RenderTargetViews = _deviceContext.OutputMerger.GetRenderTargets(OutputMergerStage.SimultaneousRenderTargetCount, out backup.DepthStencilView);

            // VS
            backup.VS = _deviceContext.VertexShader.Get();
            backup.VSSamplers = _deviceContext.VertexShader.GetSamplers(0, CommonShaderStage.SamplerSlotCount);
            backup.VSConstantBuffers = _deviceContext.VertexShader.GetConstantBuffers(0, CommonShaderStage.ConstantBufferApiSlotCount);
            backup.VSResourceViews = _deviceContext.VertexShader.GetShaderResources(0, CommonShaderStage.InputResourceSlotCount);

            // HS
            backup.HS = _deviceContext.HullShader.Get();
            backup.HSSamplers = _deviceContext.HullShader.GetSamplers(0, CommonShaderStage.SamplerSlotCount);
            backup.HSConstantBuffers = _deviceContext.HullShader.GetConstantBuffers(0, CommonShaderStage.ConstantBufferApiSlotCount);
            backup.HSResourceViews = _deviceContext.HullShader.GetShaderResources(0, CommonShaderStage.InputResourceSlotCount);

            // DS
            backup.DS = _deviceContext.DomainShader.Get();
            backup.DSSamplers = _deviceContext.DomainShader.GetSamplers(0, CommonShaderStage.SamplerSlotCount);
            backup.DSConstantBuffers = _deviceContext.DomainShader.GetConstantBuffers(0, CommonShaderStage.ConstantBufferApiSlotCount);
            backup.DSResourceViews = _deviceContext.DomainShader.GetShaderResources(0, CommonShaderStage.InputResourceSlotCount);

            // GS
            backup.GS = _deviceContext.GeometryShader.Get();
            backup.GSSamplers = _deviceContext.GeometryShader.GetSamplers(0, CommonShaderStage.SamplerSlotCount);
            backup.GSConstantBuffers = _deviceContext.GeometryShader.GetConstantBuffers(0, CommonShaderStage.ConstantBufferApiSlotCount);
            backup.GSResourceViews = _deviceContext.GeometryShader.GetShaderResources(0, CommonShaderStage.InputResourceSlotCount);

            // PS
            backup.PS = _deviceContext.PixelShader.Get();
            backup.PSSamplers = _deviceContext.PixelShader.GetSamplers(0, CommonShaderStage.SamplerSlotCount);
            backup.PSConstantBuffers = _deviceContext.PixelShader.GetConstantBuffers(0, CommonShaderStage.ConstantBufferApiSlotCount);
            backup.PSResourceViews = _deviceContext.PixelShader.GetShaderResources(0, CommonShaderStage.InputResourceSlotCount);
            
            // CS
            backup.CS = _deviceContext.ComputeShader.Get();
            backup.CSSamplers = _deviceContext.ComputeShader.GetSamplers(0, CommonShaderStage.SamplerSlotCount);
            backup.CSConstantBuffers = _deviceContext.ComputeShader.GetConstantBuffers(0, CommonShaderStage.ConstantBufferApiSlotCount);
            backup.CSResourceViews = _deviceContext.ComputeShader.GetShaderResources(0, CommonShaderStage.InputResourceSlotCount);
            backup.CSUAVs = _deviceContext.ComputeShader.GetUnorderedAccessViews(0, ComputeShaderStage.UnorderedAccessViewSlotCount);   // should be register count and not slot, but the value is correct

            return backup;
        }

        private void RestoreRenderState(ref StateBackup oldState)
        {
            // IA
            _deviceContext.InputAssembler.InputLayout = oldState.InputLayout;
            _deviceContext.InputAssembler.SetIndexBuffer(oldState.IndexBuffer, oldState.IndexBufferFormat, oldState.IndexBufferOffset);
            _deviceContext.InputAssembler.PrimitiveTopology = oldState.PrimitiveTopology;
            _deviceContext.InputAssembler.SetVertexBuffers(0, oldState.VertexBuffers, oldState.VertexBufferStrides, oldState.VertexBufferOffsets);

            // RS
            _deviceContext.Rasterizer.State = oldState.RS;
            _deviceContext.Rasterizer.SetScissorRectangles(oldState.ScissorRects);
            _deviceContext.Rasterizer.SetViewports(oldState.Viewports, oldState.Viewports.Length);

            // OM
            _deviceContext.OutputMerger.SetBlendState(oldState.BlendState, oldState.BlendFactor, oldState.SampleMask);
            _deviceContext.OutputMerger.SetDepthStencilState(oldState.DepthStencilState, oldState.DepthStencilRef);
            _deviceContext.OutputMerger.SetRenderTargets(oldState.DepthStencilView, oldState.RenderTargetViews);

            // VS
            _deviceContext.VertexShader.Set(oldState.VS);
            _deviceContext.VertexShader.SetSamplers(0, oldState.VSSamplers);
            _deviceContext.VertexShader.SetConstantBuffers(0, oldState.VSConstantBuffers);
            _deviceContext.VertexShader.SetShaderResources(0, oldState.VSResourceViews);

            // HS
            _deviceContext.HullShader.Set(oldState.HS);
            _deviceContext.HullShader.SetSamplers(0, oldState.HSSamplers);
            _deviceContext.HullShader.SetConstantBuffers(0, oldState.HSConstantBuffers);
            _deviceContext.HullShader.SetShaderResources(0, oldState.HSResourceViews);

            // DS
            _deviceContext.DomainShader.Set(oldState.DS);
            _deviceContext.DomainShader.SetSamplers(0, oldState.DSSamplers);
            _deviceContext.DomainShader.SetConstantBuffers(0, oldState.DSConstantBuffers);
            _deviceContext.DomainShader.SetShaderResources(0, oldState.DSResourceViews);

            // GS
            _deviceContext.GeometryShader.Set(oldState.GS);
            _deviceContext.GeometryShader.SetSamplers(0, oldState.GSSamplers);
            _deviceContext.GeometryShader.SetConstantBuffers(0, oldState.GSConstantBuffers);
            _deviceContext.GeometryShader.SetShaderResources(0, oldState.GSResourceViews);

            // PS
            _deviceContext.PixelShader.Set(oldState.PS);
            _deviceContext.PixelShader.SetSamplers(0, oldState.PSSamplers);
            _deviceContext.PixelShader.SetConstantBuffers(0, oldState.PSConstantBuffers);
            _deviceContext.PixelShader.SetShaderResources(0, oldState.PSResourceViews);

            // CS
            _deviceContext.ComputeShader.Set(oldState.CS);
            _deviceContext.ComputeShader.SetSamplers(0, oldState.CSSamplers);
            _deviceContext.ComputeShader.SetConstantBuffers(0, oldState.CSConstantBuffers);
            _deviceContext.ComputeShader.SetShaderResources(0, oldState.CSResourceViews);
            _deviceContext.ComputeShader.SetUnorderedAccessViews(0, oldState.CSUAVs);
        }

        public void SetupRenderState(ImDrawDataPtr drawData)
        {
            // Setup viewport
            _deviceContext.Rasterizer.SetViewport(0, 0, drawData.DisplaySize.X, drawData.DisplaySize.Y);

            // Setup shader and vertex buffers
            _deviceContext.InputAssembler.InputLayout = _inputLayout;
            _vertexBinding.Buffer = _vertexBuffer;
            _deviceContext.InputAssembler.SetVertexBuffers(0, _vertexBinding);
            _deviceContext.InputAssembler.SetIndexBuffer(_indexBuffer, Format.R16_UInt, 0);
            _deviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            _deviceContext.VertexShader.Set(_vertexShader);
            _deviceContext.VertexShader.SetConstantBuffer(0, _vertexConstantBuffer);
            _deviceContext.PixelShader.Set(_pixelShader);
            _deviceContext.PixelShader.SetSampler(0, _fontSampler);
            _deviceContext.GeometryShader.Set(null);
            _deviceContext.HullShader.Set(null);
            _deviceContext.DomainShader.Set(null);
            _deviceContext.ComputeShader.Set(null);

            // Setup blend state
            _deviceContext.OutputMerger.BlendState = _blendState;
            _deviceContext.OutputMerger.BlendFactor = _blendColor;
            _deviceContext.OutputMerger.DepthStencilState = _depthStencilState;
            _deviceContext.Rasterizer.State = _rasterizerState;
        }

        public void RenderDrawData(ImDrawDataPtr drawData)
        {
            // Avoid rendering when minimized
            if (drawData.DisplaySize.X <= 0 || drawData.DisplaySize.Y <= 0)
            {
                return;
            }

            if (!drawData.Valid || drawData.CmdListsCount == 0)
            {
                return;
            }

            //drawData.ScaleClipRects(ImGui.GetIO().DisplayFramebufferScale);

            // Create and grow vertex/index buffers if needed
            if (_vertexBuffer == null || _vertexBufferSize < drawData.TotalVtxCount)
            {
                _vertexBuffer?.Dispose();
                _vertexBufferSize = drawData.TotalVtxCount + 5000;

                _vertexBuffer = new Buffer(_device, new BufferDescription
                {
                    Usage = ResourceUsage.Dynamic,
                    SizeInBytes = Unsafe.SizeOf<ImDrawVert>() * _vertexBufferSize,
                    BindFlags = BindFlags.VertexBuffer,
                    CpuAccessFlags = CpuAccessFlags.Write,
                    OptionFlags = ResourceOptionFlags.None
                });

                // (Re)make this here rather than every frame
                _vertexBinding = new VertexBufferBinding
                {
                    Buffer = _vertexBuffer,
                    Stride = Unsafe.SizeOf<ImDrawVert>(),
                    Offset = 0
                };
            }

            if (_indexBuffer == null || _indexBufferSize < drawData.TotalIdxCount)
            {
                _indexBuffer?.Dispose();
                _indexBufferSize = drawData.TotalIdxCount + 10000;

                _indexBuffer = new Buffer(_device, new BufferDescription
                {
                    Usage = ResourceUsage.Dynamic,
                    SizeInBytes = sizeof(ushort) * _indexBufferSize,    // ImGui.NET doesn't provide an ImDrawIdx, but their sample uses ushort
                    BindFlags = BindFlags.IndexBuffer,
                    CpuAccessFlags = CpuAccessFlags.Write
                });
            }

            // Upload vertex/index data into a single contiguous GPU buffer
            int vertexOffset = 0, indexOffset = 0;
            var vertexData = _deviceContext.MapSubresource(_vertexBuffer, 0, MapMode.WriteDiscard, MapFlags.None).DataPointer;
            var indexData = _deviceContext.MapSubresource(_indexBuffer, 0, MapMode.WriteDiscard, MapFlags.None).DataPointer;

            for (int n = 0; n < drawData.CmdListsCount; n++)
            {
                var cmdList = drawData.CmdListsRange[n];
                unsafe
                {
                    System.Buffer.MemoryCopy(cmdList.VtxBuffer.Data.ToPointer(),
                                                (ImDrawVert*)vertexData + vertexOffset,
                                                Unsafe.SizeOf<ImDrawVert>() * _vertexBufferSize,
                                                Unsafe.SizeOf<ImDrawVert>() * cmdList.VtxBuffer.Size);

                    System.Buffer.MemoryCopy(cmdList.IdxBuffer.Data.ToPointer(),
                                                (ushort*)indexData + indexOffset,
                                                sizeof(ushort) * _indexBufferSize,
                                                sizeof(ushort) * cmdList.IdxBuffer.Size);

                    vertexOffset += cmdList.VtxBuffer.Size;
                    indexOffset += cmdList.IdxBuffer.Size;
                }
            }
            _deviceContext.UnmapSubresource(_vertexBuffer, 0);
            _deviceContext.UnmapSubresource(_indexBuffer, 0);

            // Setup orthographic projection matrix into our constant buffer
            // Our visible imgui space lies from drawData.DisplayPos (top left) to drawData.DisplayPos+drawData.DisplaySize (bottom right). DisplayPos is (0,0) for single viewport apps.
            var L = drawData.DisplayPos.X;
            var R = drawData.DisplayPos.X + drawData.DisplaySize.X;
            var T = drawData.DisplayPos.Y;
            var B = drawData.DisplayPos.Y + drawData.DisplaySize.Y;
            var mvp = new float[]
            {
                2f/(R-L),     0,              0,      0,
                0,            2f/(T-B),       0,      0,
                0,            0,              0.5f,   0,
                (R+L)/(L-R),  (T+B)/(B-T),    0.5f,   1f
            };

            var constantBuffer = _deviceContext.MapSubresource(_vertexConstantBuffer, 0, MapMode.WriteDiscard, MapFlags.None).DataPointer;
            unsafe
            {
                fixed (void* mvpPtr = mvp)
                {
                    System.Buffer.MemoryCopy(mvpPtr, constantBuffer.ToPointer(), 16 * sizeof(float), 16 * sizeof(float));
                }
            }
            _deviceContext.UnmapSubresource(_vertexConstantBuffer, 0);

            var oldState = BackupRenderState();

            // Setup desired DX state
            SetupRenderState(drawData);

            // Render command lists
            // (Because we merged all buffers into a single one, we maintain our own offset into them)
            vertexOffset = 0;
            indexOffset = 0;
            var clipOff = drawData.DisplayPos;
            for (int n = 0; n < drawData.CmdListsCount; n++)
            {
                var cmdList = drawData.CmdListsRange[n];
                for (int cmd = 0; cmd < cmdList.CmdBuffer.Size; cmd++)
                {
                    var pcmd = cmdList.CmdBuffer[cmd];
                    if (pcmd.UserCallback != IntPtr.Zero)
                    {
                        // TODO
                        throw new NotImplementedException();
                    }
                    else
                    {
                        // Apply scissor/clipping rectangle
                        _deviceContext.Rasterizer.SetScissorRectangle((int)(pcmd.ClipRect.X - clipOff.X), (int)(pcmd.ClipRect.Y - clipOff.Y), (int)(pcmd.ClipRect.Z - clipOff.X), (int)(pcmd.ClipRect.W - clipOff.Y));

                        // Bind texture, Draw
                        // TODO: might be nice to store samplers for loaded textures so that we can look them up and apply them here
                        // rather than just always using the font sampler
                        var textureSrv = ShaderResourceView.FromPointer<ShaderResourceView>(pcmd.TextureId);
                        _deviceContext.PixelShader.SetShaderResource(0, textureSrv);
                        _deviceContext.DrawIndexed((int)pcmd.ElemCount, (int)(pcmd.IdxOffset + indexOffset), (int)(pcmd.VtxOffset + vertexOffset));
                    }
                }

                indexOffset += cmdList.IdxBuffer.Size;
                vertexOffset += cmdList.VtxBuffer.Size;
            }

            RestoreRenderState(ref oldState);
        }

        public void CreateFontsTexture()
        {
            var io = ImGui.GetIO();

            // Build texture atlas
            io.Fonts.GetTexDataAsRGBA32(out IntPtr fontPixels, out int fontWidth, out int fontHeight, out int fontBytesPerPixel);

            // Upload texture to graphics system
            var texDesc = new Texture2DDescription
            {
                Width = fontWidth,
                Height = fontHeight,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.R8G8B8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Immutable,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            };

            using (var fontTexture = new Texture2D(_device, texDesc, new DataRectangle(fontPixels, fontWidth * fontBytesPerPixel)))
            {
                // Create texture view
                _fontResourceView = new ShaderResourceView(_device, fontTexture, new ShaderResourceViewDescription
                {
                    Format = texDesc.Format,
                    Dimension = ShaderResourceViewDimension.Texture2D,
                    Texture2D = { MipLevels = texDesc.MipLevels }
                });
            }

            // Store our identifier
            io.Fonts.SetTexID(_fontResourceView.NativePointer);
            io.Fonts.ClearTexData();

            // Create texture sampler
            _fontSampler = new SamplerState(_device, new SamplerStateDescription
            {
                Filter = Filter.MinMagMipLinear,
                AddressU = TextureAddressMode.Wrap,
                AddressV = TextureAddressMode.Wrap,
                AddressW = TextureAddressMode.Wrap,
                MipLodBias = 0,
                ComparisonFunction = Comparison.Always,
                MinimumLod = 0,
                MaximumLod = 0
            });
        }

        public bool CreateDeviceObjects()
        {
            if (_device == null)
            {
                return false;
            }

            if (_fontSampler != null)
            {
                InvalidateDeviceObjects();
            }

            // Create the vertex shader
            byte[] shaderData;

            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("imgui-vertex.hlsl.bytes"))
            {
                shaderData = new byte[stream.Length];
                stream.Read(shaderData, 0, shaderData.Length);
            }

            _vertexShader = new VertexShader(_device, shaderData);

            // Create the input layout
            _inputLayout = new InputLayout(_device, shaderData, new[]
            {
                new InputElement("POSITION", 0, Format.R32G32_Float, 0),
                new InputElement("TEXCOORD", 0, Format.R32G32_Float, 0),
                new InputElement("COLOR", 0, Format.R8G8B8A8_UNorm, 0)
            });

            // Create the constant buffer
            _vertexConstantBuffer = new Buffer(_device, new BufferDescription
            {
                Usage = ResourceUsage.Dynamic,
                BindFlags = BindFlags.ConstantBuffer,
                CpuAccessFlags = CpuAccessFlags.Write,
                OptionFlags = ResourceOptionFlags.None,
                SizeInBytes = 16 * sizeof(float)
            });

            // Create the pixel shader
            using (var stream = assembly.GetManifestResourceStream("imgui-frag.hlsl.bytes"))
            {
                shaderData = new byte[stream.Length];
                stream.Read(shaderData, 0, shaderData.Length);
            }

            _pixelShader = new PixelShader(_device, shaderData);

            // Create the blending setup
            // ...of course this was setup in a way that can't be done inline
            var blendStateDesc = new BlendStateDescription();
            blendStateDesc.AlphaToCoverageEnable = false;
            blendStateDesc.RenderTarget[0].IsBlendEnabled = true;
            blendStateDesc.RenderTarget[0].SourceBlend = BlendOption.SourceAlpha;
            blendStateDesc.RenderTarget[0].DestinationBlend = BlendOption.InverseSourceAlpha;
            blendStateDesc.RenderTarget[0].BlendOperation = BlendOperation.Add;
            blendStateDesc.RenderTarget[0].SourceAlphaBlend = BlendOption.InverseSourceAlpha;
            blendStateDesc.RenderTarget[0].DestinationAlphaBlend = BlendOption.Zero;
            blendStateDesc.RenderTarget[0].AlphaBlendOperation = BlendOperation.Add;
            blendStateDesc.RenderTarget[0].RenderTargetWriteMask = ColorWriteMaskFlags.All;
            _blendState = new BlendState(_device, blendStateDesc);

            // Create the rasterizer state
            _rasterizerState = new RasterizerState(_device, new RasterizerStateDescription
            {
                FillMode = FillMode.Solid,
                CullMode = CullMode.None,
                IsScissorEnabled = true,
                IsDepthClipEnabled = true
            });

            // Create the depth-stencil State
            _depthStencilState = new DepthStencilState(_device, new DepthStencilStateDescription
            {
                IsDepthEnabled = false,
                DepthWriteMask = DepthWriteMask.All,
                DepthComparison = Comparison.Always,
                IsStencilEnabled = false,
                FrontFace =
                {
                    FailOperation = StencilOperation.Keep,
                    DepthFailOperation = StencilOperation.Keep,
                    PassOperation = StencilOperation.Keep,
                    Comparison = Comparison.Always
                },
                BackFace =
                {
                    FailOperation = StencilOperation.Keep,
                    DepthFailOperation = StencilOperation.Keep,
                    PassOperation = StencilOperation.Keep,
                    Comparison = Comparison.Always
                }
            });

            CreateFontsTexture();

            return true;
        }

        public void InvalidateDeviceObjects()
        {
            if (_device == null)
            {
                return;
            }

            _fontSampler?.Dispose();
            _fontSampler = null;

            _fontResourceView?.Dispose();
            _fontResourceView = null;
            ImGui.GetIO().Fonts.SetTexID(IntPtr.Zero);

            _indexBuffer?.Dispose();
            _indexBuffer = null;

            _vertexBuffer?.Dispose();
            _vertexBuffer = null;

            _blendState?.Dispose();
            _blendState = null;

            _depthStencilState?.Dispose();
            _depthStencilState = null;

            _rasterizerState?.Dispose();
            _rasterizerState = null;

            _pixelShader?.Dispose();
            _pixelShader = null;

            _vertexConstantBuffer?.Dispose();
            _vertexConstantBuffer = null;

            _inputLayout?.Dispose();
            _inputLayout = null;

            _vertexShader?.Dispose();
            _vertexShader = null;
        }

        public void Init(params object[] initParams)
        {
            ImGui.GetIO().BackendFlags = ImGui.GetIO().BackendFlags | ImGuiBackendFlags.RendererHasVtxOffset;

            // BackendRendererName is readonly (and null) in ImGui.NET for some reason, but we can hack it via its internal pointer
            _renderNamePtr = Marshal.StringToHGlobalAnsi("imgui_impl_dx11_c#");
            unsafe
            {
                ImGui.GetIO().NativePtr->BackendRendererName = (byte*)_renderNamePtr.ToPointer();
            }

            _device = (Device)initParams[0];
            _deviceContext = (DeviceContext)initParams[1];

            // SharpDX also doesn't allow reference managment
        }

        public void Shutdown()
        {
            InvalidateDeviceObjects();

            // we don't own these, so no Dispose()
            _device = null;
            _deviceContext = null;

            if (_renderNamePtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_renderNamePtr);
                _renderNamePtr = IntPtr.Zero;
            }
        }

        public void NewFrame()
        {
            if (_fontSampler == null)
            {
                CreateDeviceObjects();
            }
        }
    }
}
