using System;

namespace ImGuiScene
{
    /// <summary>
    /// Simple factory to create a renderer for a given backend API/version/etc.
    /// </summary>
    public class RendererFactory
    {
        public enum RendererBackend
        {
            DirectX11,
            OpenGL3
        }

        /// <summary>
        /// Creates a renderer of the specified backend type.
        /// </summary>
        /// <param name="backend">Which renderer type to create</param>
        /// <param name="enableDebugging">Whether to enable debugging in the internal render state.  This is likely to greatly affect performance and should generally be avoided.</param>
        /// <returns></returns>
        public static IRenderer CreateRenderer(RendererBackend backend, bool enableDebugging)
        {
            switch (backend)
            {
                case RendererBackend.DirectX11:
                    return new SimpleD3D(enableDebugging);

                case RendererBackend.OpenGL3:
                    return new SimpleOGL3(enableDebugging);

                default:
                    throw new ArgumentException();
            }
        }
    }
}
