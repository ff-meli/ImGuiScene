using System;
using static SDL2.SDL;

namespace ImGuiScene
{
    /// <summary>
    /// OpenGL specialization of SimpleSDLWindow, for setting up additional necessary states during window creation.
    /// </summary>
    public class SDLWindowGL : SimpleSDLWindow
    {
        internal SDLWindowGL(IRenderer renderer, WindowCreateInfo createInfo) : base(renderer, createInfo)
        {
        }

        /// <summary>
        /// Initialize this window for use with an OpenGL renderer.  Sets necessary flags and attributes.
        /// </summary>
        /// <param name="renderer">The OpenGL renderer.</param>
        protected override void InitForRenderer(IRenderer renderer)
        {
            var ogl = (SimpleOGL3)renderer;

            // normally you don't use forward compat, but ImGui does in their sample so...
            var contextFlags = (int)SDL_GLcontext.SDL_GL_CONTEXT_FORWARD_COMPATIBLE_FLAG;
            if (renderer.Debuggable)
            {
                contextFlags |= (int)SDL_GLcontext.SDL_GL_CONTEXT_DEBUG_FLAG;
            }

            var bufferParams = ogl.WindowBufferParams;

            SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_FLAGS, contextFlags);
            SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_PROFILE_MASK, (int)SDL_GLprofile.SDL_GL_CONTEXT_PROFILE_CORE);
            SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_MAJOR_VERSION, ogl.ContextMajorVersion);
            SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_MINOR_VERSION, ogl.ContextMinorVersion);

            SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_DOUBLEBUFFER, 1);
            SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_DEPTH_SIZE, bufferParams.DepthBits);
            SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_STENCIL_SIZE, bufferParams.StencilBits);

            SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_RED_SIZE, bufferParams.RedBits);
            SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_GREEN_SIZE, bufferParams.GreenBits);
            SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_BLUE_SIZE, bufferParams.BlueBits);
            SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_ALPHA_SIZE, bufferParams.AlphaBits);
        }

        /// <summary>
        /// Return the set of window flags necessary to create a window matching what is requested in <paramref name="createInfo"/>
        /// </summary>
        /// <param name="createInfo">The requested creation parameters for the window.</param>
        /// <returns>The full set of SDL_WindowFlags to use when creating this window.</returns>
        protected override SDL_WindowFlags WindowCreationFlags(WindowCreateInfo createInfo)
        {
            var flags = base.WindowCreationFlags(createInfo) | SDL_WindowFlags.SDL_WINDOW_OPENGL;
            if (createInfo.Fullscreen && createInfo.TransparentColor != null)
            {
                // OpenGL seemingly can't render transparent fullscreen windows, regardless of window styles
                // However, borderless 'regular' windows that just happen to be the same size as the screen will work
                // ...except in SDL, where it appears to detect that and make it 'real' fullscreen, so instead we have
                // to reduce the size by 1 pixel

                flags &= ~SDL_WindowFlags.SDL_WINDOW_FULLSCREEN_DESKTOP;

                // TODO: proper monitor detection
                SDL_GetCurrentDisplayMode(0, out SDL_DisplayMode mode);

                createInfo.XPos = 0;
                createInfo.YPos = 0;
                createInfo.Width = mode.w - 1;
                createInfo.Height = mode.h - 1;
            }

            return flags;
        }
    }
}
