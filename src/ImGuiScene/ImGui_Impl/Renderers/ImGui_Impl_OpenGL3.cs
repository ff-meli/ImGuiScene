using System;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using ImGuiNET;
using OpenGL;

namespace ImGuiScene
{
    /// <summary>
    /// Currently undocumented because it is a horrible mess.
    /// A near-direct port of https://github.com/ocornut/imgui/blob/master/examples/imgui_impl_opengl3.cpp
    /// State backup IS done for this renderer, because SDL does not play nicely when using OpenGL.
    /// </summary>
    public class ImGui_Impl_OpenGL3 : IImGuiRenderer
    {
        private IntPtr _renderNamePtr;
        private uint _vertHandle;
        private uint _fragHandle;
        private uint _shaderHandle;
        private int _attribLocationTex;
        private int _attribLocationProjMtx;
        private int _attribLocationVtxPos;
        private int _attribLocationVtxUV;
        private int _attribLocationVtxColor;
        private uint _vboHandle;
        private uint _elementsHandle;
        private uint _fontTexture;
        private uint _vertexArrayObject;

        public void RenderDrawData(ImDrawDataPtr drawData)
        {
            // Avoid rendering when minimized, scale coordinates for retina displays (screen coordinates != framebuffer coordinates)
            var fbWidth = drawData.DisplaySize.X * drawData.FramebufferScale.X;
            var fbHeight = drawData.DisplaySize.Y * drawData.FramebufferScale.Y;
            if (fbWidth < 0 || fbHeight < 0)
            {
                return;
            }

            if (!drawData.Valid || drawData.CmdListsCount == 0)
            {
                return;
            }

            // Backup GL state
            // The vast majority of this probably is not necessary, and ideally should be handled by the main render application
            // with a state cache if actually required.
            // At the very least, however, it appears that SDL does not maintain/reset its own state perfectly when using opengl
            // and the scissor rect will break things.  Since I'm not sure if anything else would also break, and since this backup
            // is not nearly as terrible as the DX11 version was, I am leaving this in place for now.
            Gl.GetInteger(GetPName.ActiveTexture, out int lastActiveTexture);
            Gl.GetInteger(GetPName.CurrentProgram, out int lastProgram);
            Gl.GetInteger(GetPName.TextureBinding2d, out int lastTexture);
            Gl.GetInteger(GetPName.ArrayBufferBinding, out int lastArrayBuffer);
            Gl.GetInteger(GetPName.VertexArrayBinding, out int lastVertexArrayObject);
            Gl.GetInteger(GetPName.PolygonMode, out int lastPolygonMode);
            var lastViewport = new int[4];
            Gl.Get(GetPName.Viewport, lastViewport);
            var lastScissorBox = new int[4];
            Gl.Get(GetPName.ScissorBox, lastScissorBox);
            Gl.GetInteger(GetPName.BlendSrcRgb, out int lastBlendSrcRgb);
            Gl.GetInteger(GetPName.BlendDstRgb, out int lastBlendDstRgb);
            Gl.GetInteger(GetPName.BlendSrcAlpha, out int lastBlendSrcAlpha);
            Gl.GetInteger(GetPName.BlendDstAlpha, out int lastBlendDstAlpha);
            Gl.GetInteger(GetPName.BlendEquationRgb, out int lastBlendEquationRgb);
            Gl.GetInteger(GetPName.BlendEquationAlpha, out int lastBlendEquationAlpha);
            var lastEnableBlend = Gl.IsEnabled(EnableCap.Blend);
            var lastEnableCullFace = Gl.IsEnabled(EnableCap.CullFace);
            var lastEnableDepthTest = Gl.IsEnabled(EnableCap.DepthTest);
            var lastEnableScissorTest = Gl.IsEnabled(EnableCap.ScissorTest);

            // Setup desired GL state
            Gl.ActiveTexture(TextureUnit.Texture0);
            // Recreate the VAO every time (this is to easily allow multiple GL contexts to be rendered to. VAO are not shared among GL contexts)
            // The renderer would actually work without any VAO bound, but then our VertexAttrib calls would overwrite the default one currently bound.
            _vertexArrayObject = Gl.GenVertexArray();
            SetupRenderState(drawData);

            // Will project scissor/clipping rectangles into framebuffer space
            var clipOff = drawData.DisplayPos;              // (0,0) unless using multi-viewports
            var clipScale = drawData.FramebufferScale;      // (1,1) unless using retina display which are often (2,2)

            // Render command lists
            for (var n = 0; n < drawData.CmdListsCount; n++)
            {
                var cmdList = drawData.CmdListsRange[n];

                // Upload vertex/index buffers
                // TODO: this is *awful*
                Gl.BufferData(BufferTarget.ArrayBuffer, (uint)(cmdList.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>()), cmdList.VtxBuffer.Data, BufferUsage.StreamDraw);
                Gl.BufferData(BufferTarget.ElementArrayBuffer, (uint)(cmdList.IdxBuffer.Size * sizeof(ushort)), cmdList.IdxBuffer.Data, BufferUsage.StreamDraw);

                for (var i = 0; i < cmdList.CmdBuffer.Size; i++)
                {
                    var pcmd = cmdList.CmdBuffer[i];
                    if (pcmd.UserCallback != IntPtr.Zero)
                    {
                        // TODO
                        throw new NotImplementedException();
                    }
                    else
                    {
                        // Project scissor/clipping rectangles into framebuffer space
                        var clipRect = new Vector4
                        {
                            X = (pcmd.ClipRect.X - clipOff.X) * clipScale.X,
                            Y = (pcmd.ClipRect.Y - clipOff.Y) * clipScale.Y,
                            Z = (pcmd.ClipRect.Z - clipOff.X) * clipScale.X,
                            W = (pcmd.ClipRect.W - clipOff.Y) * clipScale.Y,
                        };

                        if (clipRect.X < fbWidth && clipRect.Y < fbHeight && clipRect.Z >= 0 && clipRect.W >= 0)
                        {
                            // Apply scissor/clipping rectangle
                            Gl.Scissor((int)clipRect.X, (int)(fbHeight - clipRect.W), (int)(clipRect.Z - clipRect.X), (int)(clipRect.W - clipRect.Y));

                            // Bind texture, Draw
                            Gl.BindTexture(TextureTarget.Texture2d, (uint)pcmd.TextureId);
                            Gl.DrawElementsBaseVertex(PrimitiveType.Triangles, (int)pcmd.ElemCount, DrawElementsType.UnsignedShort, (IntPtr)(pcmd.IdxOffset * sizeof(short)), (int)pcmd.VtxOffset);
                        }
                    }
                }
            }

            // Destroy the temporary VAO
            Gl.DeleteVertexArrays(_vertexArrayObject);

            // Restore modified GL state
            Gl.UseProgram((uint)lastProgram);
            Gl.BindTexture(TextureTarget.Texture2d, (uint)lastTexture);
            Gl.ActiveTexture((TextureUnit)lastActiveTexture);
            Gl.BindVertexArray((uint)lastVertexArrayObject);
            Gl.BindBuffer(BufferTarget.ArrayBuffer, (uint)lastArrayBuffer);
            Gl.BlendEquationSeparate((BlendEquationMode)lastBlendEquationRgb, (BlendEquationMode)lastBlendEquationAlpha);
            Gl.BlendFuncSeparate((BlendingFactor)lastBlendSrcRgb, (BlendingFactor)lastBlendDstRgb, (BlendingFactor)lastBlendSrcAlpha, (BlendingFactor)lastBlendDstAlpha);
            if (lastEnableBlend)
            {
                Gl.Enable(EnableCap.Blend);
            } 
            else
            {
                Gl.Disable(EnableCap.Blend);
            }
            if (lastEnableCullFace)
            {
                Gl.Enable(EnableCap.CullFace);
            }
            else
            {
                Gl.Disable(EnableCap.CullFace);
            }
            if (lastEnableDepthTest)
            {
                Gl.Enable(EnableCap.DepthTest);
            }
            else
            {
                Gl.Disable(EnableCap.DepthTest);
            }
            if (lastEnableScissorTest)
            {
                Gl.Enable(EnableCap.ScissorTest);
            }
            else
            {
                Gl.Disable(EnableCap.ScissorTest);
            }
            Gl.PolygonMode(MaterialFace.FrontAndBack, (PolygonMode)lastPolygonMode);
            Gl.Viewport(lastViewport[0], lastViewport[1], lastViewport[2], lastViewport[3]);
            Gl.Scissor(lastScissorBox[0], lastScissorBox[1], lastScissorBox[2], lastScissorBox[3]);
        }

        public void Init(params object[] initParams)
        {
            var io = ImGui.GetIO();
            // We can honor the ImDrawCmd::VtxOffset field, allowing for large meshes.
            io.BackendFlags = io.BackendFlags | ImGuiBackendFlags.RendererHasVtxOffset;

            // BackendRendererName is readonly (and null) in ImGui.NET for some reason, but we can hack it via its internal pointer
            _renderNamePtr = Marshal.StringToHGlobalAnsi("imgui_impl_opengl3_c#");
            unsafe
            {
                io.NativePtr->BackendRendererName = (byte*)_renderNamePtr.ToPointer();
            }

            // literally nothing else in the source implementation of this function is useful
        }

        public void Shutdown()
        {
            DestroyDeviceObjects();

            if (_renderNamePtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_renderNamePtr);
                _renderNamePtr = IntPtr.Zero;
            }
        }

        public void NewFrame()
        {
            if (_shaderHandle == 0)
            {
                CreateDeviceObjects();
            }
        }

        private void SetupRenderState(ImDrawDataPtr drawData)
        {
            // Setup render state: alpha-blending enabled, no face culling, no depth testing, scissor enabled, polygon fill
            Gl.Enable(EnableCap.Blend);
            Gl.BlendEquation(BlendEquationMode.FuncAdd);
            Gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            Gl.Disable(EnableCap.CullFace);
            Gl.Disable(EnableCap.DepthTest);
            Gl.Enable(EnableCap.ScissorTest);
            Gl.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

            // Setup viewport, orthographic projection matrix
            // Our visible imgui space lies from draw_data->DisplayPos (top left) to draw_data->DisplayPos+data_data->DisplaySize (bottom right). DisplayPos is (0,0) for single viewport apps.
            Gl.Viewport(0, 0, (int)(drawData.DisplaySize.X * drawData.FramebufferScale.X), (int)(drawData.DisplaySize.Y * drawData.FramebufferScale.Y));
            var L = drawData.DisplayPos.X;
            var R = drawData.DisplayPos.X + drawData.DisplaySize.X;
            var T = drawData.DisplayPos.Y;
            var B = drawData.DisplayPos.Y + drawData.DisplaySize.Y;
            var ortho_projection = new float[]
            {
                2f/(R-L),     0,              0,      0,
                0,            2f/(T-B),       0,      0,
                0,            0,              -1f ,   0,
                (R+L)/(L-R),  (T+B)/(B-T),    0,      1f
            };
            Gl.UseProgram(_shaderHandle);
            Gl.Uniform1(_attribLocationTex, 0);
            Gl.UniformMatrix4(_attribLocationProjMtx, false, ortho_projection);

            Gl.BindVertexArray(_vertexArrayObject);

            // Bind vertex/index buffers and setup attributes for ImDrawVert
            Gl.BindBuffer(BufferTarget.ArrayBuffer, _vboHandle);
            Gl.BindBuffer(BufferTarget.ElementArrayBuffer, _elementsHandle);
            Gl.EnableVertexAttribArray((uint)_attribLocationVtxPos);
            Gl.EnableVertexAttribArray((uint)_attribLocationVtxUV);
            Gl.EnableVertexAttribArray((uint)_attribLocationVtxColor);
            Gl.VertexAttribPointer((uint)_attribLocationVtxPos, 2, VertexAttribType.Float, false, Unsafe.SizeOf<ImDrawVert>(), Marshal.OffsetOf(typeof(ImDrawVert), "pos"));
            Gl.VertexAttribPointer((uint)_attribLocationVtxUV, 2, VertexAttribType.Float, false, Unsafe.SizeOf<ImDrawVert>(), Marshal.OffsetOf(typeof(ImDrawVert), "uv"));
            Gl.VertexAttribPointer((uint)_attribLocationVtxColor, 4, VertexAttribType.UnsignedByte, true, Unsafe.SizeOf<ImDrawVert>(), Marshal.OffsetOf(typeof(ImDrawVert), "col"));
        }

        private void CreateFontsTexture()
        {
            var io = ImGui.GetIO();

            io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int fontWidth, out int fontHeight, out int fontBytesPerPixel);

            Gl.GetInteger(GetPName.TextureBinding2d, out int lastTexture);

            _fontTexture = Gl.GenTexture();
            Gl.BindTexture(TextureTarget.Texture2d, _fontTexture);
            Gl.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMinFilter, TextureMinFilter.Linear);
            Gl.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, TextureMagFilter.Linear);
            Gl.TexImage2D(TextureTarget.Texture2d, 0, InternalFormat.Rgba, fontWidth, fontHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

            io.Fonts.SetTexID((IntPtr)_fontTexture);
            io.Fonts.ClearTexData();

            Gl.BindTexture(TextureTarget.Texture2d, (uint)lastTexture);
        }

        private void DestroyFontsTexture()
        {
            if (_fontTexture != 0)
            {
                Gl.DeleteTextures(_fontTexture);
                _fontTexture = 0;

                ImGui.GetIO().Fonts.SetTexID(IntPtr.Zero);
            }
        }

        private bool CreateDeviceObjects()
        {
            // Backup GL state
            // but why??
            Gl.GetInteger(GetPName.TextureBinding2d, out int lastTexture);
            Gl.GetInteger(GetPName.ArrayBufferBinding, out int lastArrayBuffer);
            Gl.GetInteger(GetPName.VertexArrayBinding, out int lastVertexArray);

            // Create shaders
            string shaderSource;

            var assembly = Assembly.GetExecutingAssembly();
            using (var data = assembly.GetManifestResourceStream("imgui-vertex.glsl"))
            using (var stream = new StreamReader(data))
            {
                shaderSource = stream.ReadToEnd();
            }

            _vertHandle = Gl.CreateShader(ShaderType.VertexShader);
            Gl.ShaderSource(_vertHandle, new string[] { shaderSource });
            Gl.CompileShader(_vertHandle);
            CheckShader(_vertHandle, "vertex shader");

            using (var data = assembly.GetManifestResourceStream("imgui-frag.glsl"))
            using (var stream = new StreamReader(data))
            {
                shaderSource = stream.ReadToEnd();
            }

            _fragHandle = Gl.CreateShader(ShaderType.FragmentShader);
            Gl.ShaderSource(_fragHandle, new string[] { shaderSource });
            Gl.CompileShader(_fragHandle);
            CheckShader(_fragHandle, "fragment shader");

            _shaderHandle = Gl.CreateProgram();
            Gl.AttachShader(_shaderHandle, _vertHandle);
            Gl.AttachShader(_shaderHandle, _fragHandle);
            Gl.LinkProgram(_shaderHandle);
            CheckProgram(_shaderHandle, "shader program");

            _attribLocationTex = Gl.GetUniformLocation(_shaderHandle, "Texture");
            _attribLocationProjMtx = Gl.GetUniformLocation(_shaderHandle, "ProjMtx");
            _attribLocationVtxPos = Gl.GetAttribLocation(_shaderHandle, "Position");
            _attribLocationVtxUV = Gl.GetAttribLocation(_shaderHandle, "UV");
            _attribLocationVtxColor = Gl.GetAttribLocation(_shaderHandle, "Color");

            _vboHandle = Gl.GenBuffer();
            _elementsHandle = Gl.GenBuffer();

            CreateFontsTexture();

            // Restore modified GL state
            Gl.BindTexture(TextureTarget.Texture2d, (uint)lastTexture);
            Gl.BindBuffer(BufferTarget.ArrayBuffer, (uint)lastArrayBuffer);
            Gl.BindVertexArray((uint)lastVertexArray);

            return true;
        }

        private void DestroyDeviceObjects()
        {
            if (_vboHandle != 0)
            {
                Gl.DeleteBuffers(_vboHandle);
                _vboHandle = 0;
            }

            if (_elementsHandle != 0)
            {
                Gl.DeleteBuffers(_elementsHandle);
                _elementsHandle = 0;
            }

            // why do they do this, it's entirely pointless...
            if (_shaderHandle != 0 && _vertHandle != 0)
            {
                Gl.DetachShader(_shaderHandle, _vertHandle);
            }
            if (_shaderHandle != 0 && _fragHandle != 0)
            {
                Gl.DetachShader(_shaderHandle, _fragHandle);
            }

            if (_vertHandle != 0)
            {
                Gl.DeleteShader(_vertHandle);
            }

            if (_fragHandle != 0)
            {
                Gl.DeleteShader(_fragHandle);
            }

            if (_shaderHandle != 0)
            {
                Gl.DeleteProgram(_shaderHandle);
            }

            DestroyFontsTexture();
        }

        private void CheckShader(uint shader, string desc)
        {
            Gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
            if (status == Gl.FALSE)
            {
                string message = string.Empty;

                Gl.GetShader(shader, ShaderParameterName.InfoLogLength, out int logLength);
                if (logLength > 1)
                {
                    var logBuilder = new StringBuilder();
                    logBuilder.Capacity = logLength;
                    Gl.GetShaderInfoLog(shader, logLength, out int length, logBuilder);
                    message = logBuilder.ToString();
                }

                throw new Exception($"{desc} failed to compile: {message}");
            }
        }

        private void CheckProgram(uint program, string desc)
        {
            Gl.GetProgram(program, ProgramProperty.LinkStatus, out int status);
            if (status == Gl.FALSE)
            {
                string message = string.Empty;

                Gl.GetProgram(program, ProgramProperty.InfoLogLength, out int logLength);
                if (logLength > 1)
                {
                    var logBuilder = new StringBuilder();
                    logBuilder.Capacity = logLength;
                    Gl.GetProgramInfoLog(program, logLength, out int length, logBuilder);
                    message = logBuilder.ToString();
                }

                throw new Exception($"failed to link {desc}: {message}");
            }
        }
    }
}
