using ImGuiNET;
using System;
using System.Numerics;


namespace ImGuiScene
{
    /// <summary>
    /// Abstraction for a simple renderer that can be used with ImGui
    /// </summary>
    public interface IRenderer : IDisposable
    {
        /// <summary>
        /// The type (API/version) of this renderer.
        /// </summary>
        RendererFactory.RendererBackend Type { get; }

        /// <summary>
        /// The color to use when clearing the window.
        /// </summary>
        Vector4 ClearColor { get; set; }

        /// <summary>
        /// Whether or not the renderer should sync presentation to the monitor's refresh rate.
        /// </summary>
        bool Vsync { get; set; }

        /// <summary>
        /// Whether this renderer was created with debuggable state.
        /// </summary>
        bool Debuggable { get; }

        // TODO: explicit coupling to SDL windows for now
        // I don't want to add another layer of abstraction that isn't used
        // It would be nice to support arbitrary HWNDs though - DX can fine, but GL is a bit of a mess

        /// <summary>
        /// Attach this renderer to the specified window to begin rendering into it.
        /// </summary>
        /// <param name="sdlWindow">The <see cref="SimpleSDLWindow"/> in which to render.</param>
        /// <remarks>It is necessary for the renderer to call sdlWindow.Show() at some point during this method.</remarks>
        void AttachToWindow(SimpleSDLWindow sdlWindow);

        /// <summary>
        /// Clear the window.
        /// </summary>
        void Clear();

        /// <summary>
        /// Finalize any rendering and swap it to the screen.
        /// </summary>
        void Present();

        /// <summary>
        /// Helper method to create and upload a gpu texture from raw image data.
        /// </summary>
        /// <param name="pixelData">A pointer to the raw pixel data</param>
        /// <param name="width">The width of the image</param>
        /// <param name="height">The height of the image</param>
        /// <param name="bytesPerPixel">The bytes per pixel of the image, used for stride calculations</param>
        /// <returns>The created texture resource for the image, null on failure.</returns>
        /// <remarks>The resource created by this method is not managed, and it is up to calling code to invoke Dispose() when done</remarks>
        unsafe TextureWrap CreateTexture(void* pixelData, int width, int height, int bytesPerPixel);

        // ImGui handlers
        // Eventually it'd be nice to clean this up and have it less explicit
        void ImGui_Init();
        void ImGui_Shutdown();
        void ImGui_NewFrame();
        void ImGui_RenderDrawData(ImDrawDataPtr drawData);
    }

    /// <summary>
    /// Simple wrapper to handle texture resources from different APIs, while accounting for resource freeing and ImGui interaction.
    /// </summary>
    public interface TextureWrap : IDisposable
    {
        /// <summary>
        /// A texture handle suitable for direct use with ImGui::Image() etc.
        /// </summary>
        IntPtr ImGuiHandle { get; }
        int Width { get; }
        int Height { get; }
    }
}
