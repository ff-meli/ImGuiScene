using ImGuiNET;
using System;

namespace ImGuiScene
{
    public class SimpleImGuiScene : IDisposable
    {
        public SimpleSDLWindow Window { get; private set; }
        public SimpleD3D D3D { get; private set; }

        public delegate void BuildUIDelegate();

        public BuildUIDelegate OnBuildUI;

        public SimpleImGuiScene(string title, int xPos = SDL2.SDL.SDL_WINDOWPOS_UNDEFINED, int yPos = SDL2.SDL.SDL_WINDOWPOS_UNDEFINED, int width = 0, int height = 0, bool fullscreen = false)
        {
            Window = new SimpleSDLWindow(title, xPos, yPos, width, height, fullscreen);
            D3D = new SimpleD3D(Window.GetHWnd());

            ImGui.CreateContext();

            ImGui_Impl_SDL.Init(Window.Window);
            ImGui_Impl_DX11.Init(D3D.Device, D3D.Context, false);

            Window.OnSDLEvent += ImGui_Impl_SDL.ProcessEvent;
        }

        public void Update(out bool quit)
        {
            quit = false;
            Window.ProcessEvents(out quit);

            ImGui_Impl_DX11.NewFrame();
            ImGui_Impl_SDL.NewFrame();

            ImGui.NewFrame();
                OnBuildUI?.Invoke();
            ImGui.Render();

            D3D.Clear();

            ImGui_Impl_DX11.RenderDrawData(ImGui.GetDrawData());

            D3D.Present();
        }

        #region IDisposable Support
        private bool disposedValue = false;

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
                ImGui_Impl_DX11.Shutdown();
                ImGui_Impl_SDL.Shutdown();

                ImGui.DestroyContext();

                D3D?.Dispose();
                D3D = null;

                Window?.Dispose();
                Window = null;

                disposedValue = true;
            }
        }

        ~SimpleImGuiScene()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
