using ImGuiNET;
using ImGuiScene;
using ImGuiScene.DX11;

namespace ImGuiSceneTest
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var scene = SimpleImGuiScene.CreateOverlay(new DX11Renderer()))
            {
                scene.OnBuildUI += ImGui.ShowDemoWindow;
                scene.Run();
            }
        }
    }
}
