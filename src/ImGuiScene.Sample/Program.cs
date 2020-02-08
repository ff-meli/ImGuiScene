using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
                scene.OnBuildUI += ImGui.ShowDemoWindow;
                scene.Run();
            }
        }
    }
}
