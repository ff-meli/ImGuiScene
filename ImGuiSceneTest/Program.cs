using ImGuiNET;
using ImGuiScene;
using static SDL2.SDL;

namespace ImGuiSceneTest
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var scene = new SimpleImGuiScene(RendererFactory.RendererBackend.OpenGL3, new WindowCreateInfo
            {
                Title = "ImGui Test",
                Fullscreen = true,
                TransparentColor = new float[] { 0, 0, 0 }
            }))
            {
                scene.Renderer.ClearColor = new System.Numerics.Vector4(0, 0, 0, 0);

                scene.Window.OnSDLEvent += (ref SDL_Event sdlEvent) =>
                {
                    if (sdlEvent.type == SDL_EventType.SDL_KEYDOWN && sdlEvent.key.keysym.scancode == SDL_Scancode.SDL_SCANCODE_ESCAPE)
                    {
                        scene.ShouldQuit = true;
                    }
                    return true;
                };

                scene.OnBuildUI += () =>
                {
                    ImGui.ShowDemoWindow();
                };

                scene.Run();
            }
        }
    }
}
