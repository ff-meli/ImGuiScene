using ImGuiNET;
using ImGuiScene;
using static SDL2.SDL;

namespace ImGuiSceneTest
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var scene = SimpleImGuiScene.CreateOverlay(RendererFactory.RendererBackend.DirectX11))
            {
                scene.OnBuildUI += ImGui.ShowDemoWindow;

                scene.Run();
            }
        }
    }
}
