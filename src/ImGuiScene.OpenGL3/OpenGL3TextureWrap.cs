using OpenGL;
using System;

namespace ImGuiScene.OpenGL3
{
    /// <summary>
    /// OpenGL 3 Implementation of <see cref="TextureWrap"/>.
    /// Provides a simple wrapped view of the disposeable resource as well as the handle for ImGui.
    /// </summary>
    public class GLTextureWrap : TextureWrap
    {
        public IntPtr ImGuiHandle { get; }
        public int Width { get; }
        public int Height { get; }

        public GLTextureWrap(uint texture, int width, int height)
        {
            ImGuiHandle = (IntPtr)texture;
            Width = width;
            Height = height;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                var textureId = (uint)ImGuiHandle;
                if (textureId != 0)
                {
                    Gl.DeleteTextures(textureId);
                }

                disposedValue = true;
            }
        }

        ~GLTextureWrap()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
