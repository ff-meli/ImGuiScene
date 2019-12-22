using ImGuiNET;
using ImGuiScene;

namespace ImGuiSceneTest
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var scene = SimpleImGuiScene.CreateOverlay(RendererFactory.RendererBackend.DirectX11))
            {
                scene.PauseWhenUnfocused = true;

                scene.OnBuildUI += ImGui.ShowDemoWindow;

                scene.Run();
            }
        }
    }
}
