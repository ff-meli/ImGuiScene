# ImGuiScene
A simple ibrary that wraps everything necessary to add ImGui support to a C# project.  This is mainly useful inside a dll that is injected into a game or similar process, in order to provide a simple transparent overlay where ImGui controls can be trivially created.  It can also be used as a standalone application for prototyping or testing ImGui scenes etc.

ImGui integration is done with [ImGui.NET](https://github.com/mellinoe/ImGui.NET), while window creation and events use [SDL2-CS](https://github.com/flibitijibibo/SDL2-CS).  The semi-official ImGui backend implementations for [SDL](https://github.com/ocornut/imgui/blob/master/examples/imgui_impl_sdl.cpp), [DX11](https://github.com/ocornut/imgui/blob/master/examples/imgui_impl_dx11.cpp), and [OpenGL3](https://github.com/ocornut/imgui/blob/master/examples/imgui_impl_opengl3.cpp) were ported as directly as possible to SDL2-CS, SharpDX and OpenGL.NET.  The C++ sources for those implementations are extremely messy and poor as-is, and currently no effort has been made to clean them up for C#, or to impose reasonable code design on them.  They do work for now, and hopefully I can improve on them later.

### How to use
There are two main ways of using ImGuiScene in your project:
* As regular references (recommended):
  * Do a recursive clone of this project
  * Build it
  * Add the 3 output dlls (ImGuiScene.dll, SDL2-CS.dll and ImGui.NET.dll) as references in your project
* As a submodule:
  * Create your project and git repo
  * Add this project as a submodule
  * Do a recursive submodule init to pull in dependencies
  * Add the _solution_ from this project to your solution (Add existing project, change filter to sln)
    * This pulls in all the subprojects necessary to build
  * Add the ImGuiScene, ImGui.NET-472, and SDL2-CS projects as project references in your project
    * ImGuiSceneTest can be deleted from your project, or just ignored

#### Usage Note
You may need to ensure "Prefer 32-bit" is disabled for your project if you use the AnyCPU target.  This is due to the version of the native dlls that are included; I will look into providing a 32-bit version as well in the future.


### Simple example application
This is the simplest example, which just creates a transparent ('hidden') fullscreen window that renders the default ImGui demo window.  More control can be had by directly creating a SimpleImGuiScene from its constructor, and/or by modifying properties on the scene object.
```csharp
static void Main(string[] args)
{
    using (var scene = SimpleImGuiScene.CreateOverlay(RendererFactory.RendererBackend.DirectX11))
    {
        // Any ImGui code can be put into methods attached to OnBuildUI and will work as expected
        // Virtually all actual application logic will be inside these handlers
        scene.OnBuildUI += ImGui.ShowDemoWindow;

        // This blocks until a specified key is pressed.  Since we didn't override it in CreateOverlay(), it defaults to escape.
        // You can alternatively call scene.Update() in a loop, but you are then responsible for checking quit conditions.
        // Because this blocks, you will typically want to put your scene in a thread if you are using an injected application.
        scene.Run();
    }
}
```
