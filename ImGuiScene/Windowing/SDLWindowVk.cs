using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SDL2.SDL;

namespace ImGuiScene
{
    /// <summary>
    /// Vulkan specialization of SimpleSDLWindow, for setting up additional necessary states during window creation.
    /// </summary>
    public class SDLWindowVk : SimpleSDLWindow
    {
        internal SDLWindowVk(IRenderer renderer, WindowCreateInfo createInfo) : base(renderer, createInfo)
        {
        }

        /// <summary>
        /// Initialize this window for use with a Vulkan renderer.  Sets necessary flags and attributes.
        /// </summary>
        /// <param name="renderer">The Vulkan renderer.</param>
        protected override void InitForRenderer(IRenderer renderer)
        {
            //SDL_Vulkan_LoadLibrary(null);
        }

        /// <summary>
        /// Return the set of window flags necessary to create a window matching what is requested in <paramref name="createInfo"/>
        /// </summary>
        /// <param name="createInfo">The requested creation parameters for the window.</param>
        /// <returns>The full set of SDL_WindowFlags to use when creating this window.</returns>
        protected override SDL_WindowFlags WindowCreationFlags(WindowCreateInfo createInfo)
        {
            return base.WindowCreationFlags(createInfo) | SDL_WindowFlags.SDL_WINDOW_VULKAN;
        }
    }
}
