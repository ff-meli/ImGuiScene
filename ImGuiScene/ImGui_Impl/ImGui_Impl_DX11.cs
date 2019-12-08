using ImGuiNET;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using DeviceChild = SharpDX.Direct3D11.DeviceChild;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace ImGuiScene
{
    public class ImGui_Impl_DX11
    {
        private static Device _device = null;
        private static DeviceContext _deviceContext = null;
        private static ShaderResourceView _fontResourceView = null;
        private static SamplerState _fontSampler = null;
        private static VertexShader _vertexShader = null;
        private static PixelShader _pixelShader = null;
        private static InputLayout _inputLayout = null;
        private static Buffer _vertexConstantBuffer = null;
        private static BlendState _blendState = null;
        private static RasterizerState _rasterizerState = null;
        private static DepthStencilState _depthStencilState = null;
        private static Buffer _vertexBuffer = null;
        private static Buffer _indexBuffer = null;
        private static int _vertexBufferSize = 0;
        private static int _indexBufferSize = 0;

        private static bool _backupState = true;

        private struct BackupDx11State
        {
            public Rectangle[] ScissorRects; //16
            public RawViewportF[] Viewports;// 16
            public IntPtr RasterizerState;
            public IntPtr BlendState;
            public RawColor4 BlendFactor;
            public int SampleMask;
            public int StencilRef;
            public IntPtr DepthStencilState;
            public IntPtr PSShaderResource;
            public IntPtr PSSampler;
            public IntPtr PS;
            public IntPtr VS;
            public IntPtr GS;
            public ClassInstance[] PSInstances;
            public ClassInstance[] VSInstances;
            public ClassInstance[] GSInstances;
            public PrimitiveTopology PrimitiveTopology;
            public IntPtr IndexBuffer;
            public IntPtr VertexBuffer;
            public IntPtr VSConstantBuffer;
            public int IndexBufferOffset;
            public int VertexBufferStride;
            public int VertexBufferOffset;
            public Format IndexBufferFormat;
            public IntPtr InputLayout;
        }

        private static T GetShaderInstances<T>(CommonShaderStage<T> shader, out ClassInstance[] instances) where T : DeviceChild
        {
            var tempInstances = new ClassInstance[256];
            var ret = shader.Get(tempInstances);

            int count;
            for (count = 0; count < 256; count++)
            {
                if (tempInstances[count] == null)
                {
                    break;
                }
            }

            if (count == 0)
            {
                instances = null;
            }
            else
            {
                instances = new ClassInstance[count];
                Array.Copy(tempInstances, 0, instances, 0, count);
            }

            return ret;
        }

        // This is pretty awful in the main code, and SharpDX makes it worse
        // Best to only use this when actually necessary
        private static BackupDx11State? BackupState()
        {
            if (_backupState)
            {
                // (unfortunately this is very ugly looking and verbose. Close your eyes!)
                BackupDx11State old = new BackupDx11State();
                old.ScissorRects = new Rectangle[16];
                old.Viewports = new RawViewportF[16];
                _deviceContext.Rasterizer.GetScissorRectangles(old.ScissorRects);
                _deviceContext.Rasterizer.GetViewports(old.Viewports);
                old.RasterizerState = _deviceContext.Rasterizer.State?.NativePointer ?? IntPtr.Zero;
                old.BlendState = _deviceContext.OutputMerger.GetBlendState(out old.BlendFactor, out old.SampleMask)?.NativePointer ?? IntPtr.Zero;
                old.DepthStencilState = _deviceContext.OutputMerger.GetDepthStencilState(out old.StencilRef)?.NativePointer ?? IntPtr.Zero;
                old.PSShaderResource = _deviceContext.PixelShader.GetShaderResources(0, 1)[0]?.NativePointer ?? IntPtr.Zero;
                old.PSSampler = _deviceContext.PixelShader.GetSamplers(0, 1)[0]?.NativePointer ?? IntPtr.Zero;
                // this is really poorly done in SharpDX... they hide the methods that would return the count
                old.PS = GetShaderInstances(_deviceContext.PixelShader, out old.PSInstances)?.NativePointer ?? IntPtr.Zero;
                old.VS = GetShaderInstances(_deviceContext.VertexShader, out old.VSInstances)?.NativePointer ?? IntPtr.Zero;
                old.VSConstantBuffer = _deviceContext.VertexShader.GetConstantBuffers(0, 1)[0]?.NativePointer ?? IntPtr.Zero;
                old.GS = GetShaderInstances(_deviceContext.GeometryShader, out old.GSInstances)?.NativePointer ?? IntPtr.Zero;

                old.PrimitiveTopology = _deviceContext.InputAssembler.PrimitiveTopology;
                _deviceContext.InputAssembler.GetIndexBuffer(out Buffer indexBufferRef, out old.IndexBufferFormat, out old.IndexBufferOffset);
                old.IndexBuffer = indexBufferRef?.NativePointer ?? IntPtr.Zero;
                var vertexBuffersOut = new Buffer[1];
                var stridesRef = new int[1];
                var offsetsRef = new int[1];
                _deviceContext.InputAssembler.GetVertexBuffers(0, 1, vertexBuffersOut, stridesRef, offsetsRef);
                old.VertexBuffer = vertexBuffersOut[0]?.NativePointer ?? IntPtr.Zero;
                old.VertexBufferStride = stridesRef[0];
                old.VertexBufferOffset = offsetsRef[0];
                old.InputLayout = _deviceContext.InputAssembler.InputLayout?.NativePointer ?? IntPtr.Zero;

                return old;
            }

            return null;
        }

        private static void RestoreState(BackupDx11State? oldState)
        {
            if (!_backupState || !oldState.HasValue)
            {
                return;
            }

            BackupDx11State old = oldState.Value;

            _deviceContext.Rasterizer.SetScissorRectangles(old.ScissorRects);
            _deviceContext.Rasterizer.SetViewports(old.Viewports);
            _deviceContext.Rasterizer.State = new RasterizerState(old.RasterizerState);
            _deviceContext.OutputMerger.SetBlendState(new BlendState(old.BlendState), old.BlendFactor, old.SampleMask);
            _deviceContext.OutputMerger.SetDepthStencilState(new DepthStencilState(old.DepthStencilState));
            _deviceContext.PixelShader.SetShaderResource(0, new ShaderResourceView(old.PSShaderResource));
            _deviceContext.PixelShader.SetSampler(0, new SamplerState(old.PSSampler));
            _deviceContext.PixelShader.Set(new PixelShader(old.PS), old.PSInstances);
            _deviceContext.VertexShader.Set(new VertexShader(old.VS), old.VSInstances);
            _deviceContext.VertexShader.SetConstantBuffer(0, new Buffer(old.VSConstantBuffer));
            _deviceContext.GeometryShader.Set(new GeometryShader(old.GS), old.GSInstances);
            _deviceContext.InputAssembler.PrimitiveTopology = old.PrimitiveTopology;
            _deviceContext.InputAssembler.SetIndexBuffer(new Buffer(old.IndexBuffer), old.IndexBufferFormat, old.IndexBufferOffset);
            _deviceContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding
            {
                Buffer = new Buffer(old.VertexBuffer),
                Stride = old.VertexBufferStride,
                Offset = old.VertexBufferOffset
            });
            _deviceContext.InputAssembler.InputLayout = new InputLayout(old.InputLayout);
        }

        public static void SetupRenderState(ImDrawDataPtr drawData)
        {
            // Setup viewport
            _deviceContext.Rasterizer.SetViewport(0, 0, drawData.DisplaySize.X, drawData.DisplaySize.Y);

            // Setup shader and vertex buffers
            _deviceContext.InputAssembler.InputLayout = _inputLayout;
            _deviceContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding
            {
                Buffer = _vertexBuffer,
                Offset = 0,
                Stride = Unsafe.SizeOf<ImDrawVert>()
            });
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
            _deviceContext.OutputMerger.BlendFactor = new RawColor4(0, 0, 0, 0);
            _deviceContext.OutputMerger.DepthStencilState = _depthStencilState;
            _deviceContext.Rasterizer.State = _rasterizerState;
        }

        public static void RenderDrawData(ImDrawDataPtr drawData)
        {
            // Avoid rendering when minimized
            if (drawData.DisplaySize.X <= 0 || drawData.DisplaySize.Y <= 0)
            {
                return;
            }

            if (!drawData.Valid || drawData.CmdListsCount == 0)
                return;

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

            // Backup DX state that will be modified to restore it afterwards
            // note that this does nothing if _backupState is false
            var oldState = BackupState();

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
                        // Not sure why this bind is done in the loops instead of once per frame, but leaving to match the source for now
                        var textureSrv = ShaderResourceView.FromPointer<ShaderResourceView>(pcmd.TextureId);
                        _deviceContext.PixelShader.SetShaderResource(0, textureSrv);
                        _deviceContext.DrawIndexed((int)pcmd.ElemCount, (int)(pcmd.IdxOffset + indexOffset), (int)(pcmd.VtxOffset + vertexOffset)); 
                    }
                }

                indexOffset += cmdList.IdxBuffer.Size;
                vertexOffset += cmdList.VtxBuffer.Size;
            }

            // Restore modified DX state
            RestoreState(oldState);
        }

        public static void CreateFontsTexture()
        {
            var io = ImGui.GetIO();

            unsafe
            {
                // Build texture atlas
                io.Fonts.GetTexDataAsRGBA32(out byte* fontPixels, out int fontWidth, out int fontHeight, out int fontBytesPerPixel);

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
                
                using (var fontTexture = new Texture2D(_device, texDesc, new DataRectangle(new IntPtr(fontPixels), fontWidth * fontBytesPerPixel)))
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
        }

        public static bool CreateDeviceObjects()
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

        public static void InvalidateDeviceObjects()
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

        public static bool Init(Device device, DeviceContext context, bool backupState = true)
        {
            // ImGui.GetIO() backend properties are read-only for some reason, so we can't set the name etc
            ImGui.GetIO().BackendFlags = ImGui.GetIO().BackendFlags | ImGuiBackendFlags.RendererHasVtxOffset;

            _device = device;
            _deviceContext = context;
            _backupState = backupState;

            // SharpDX also doesn't allow reference managment

            return true;
        }

        public static void Shutdown()
        {
            InvalidateDeviceObjects();

            // we don't own these, so no Dispose()
            _device = null;
            _deviceContext = null;
        }

        public static void NewFrame()
        {
            if (_fontSampler == null)
            {
                CreateDeviceObjects();
            }
        }
    }
}
