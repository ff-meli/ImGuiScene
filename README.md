# ImGuiScene
Currently a very rough library that wraps everything necessary to add ImGui support to a C# project.

ImGui integration is done with [ImGui.NET](https://github.com/mellinoe/ImGui.NET), while window creation and events use [SDL2-CS](https://github.com/flibitijibibo/SDL2-CS).  The semi-official ImGui backend implementations for [SDL](https://github.com/ocornut/imgui/blob/master/examples/imgui_impl_sdl.cpp) and [DX11](https://github.com/ocornut/imgui/blob/master/examples/imgui_impl_dx11.cpp) were ported as directly as possible to SDL2-CS and SharpDX.  The C++ sources for those implementations are extremely messy and poor as-is, and currently no effort has been made to clean them up for C#, or to impose reasonable code design on them.  They do work for now, and hopefully I can improve on them later.

A simple sample application will follow soon.


### Using in a project (THIS IS OUT OF DATE AS I CHANGE THINGS)
There are a couple ways to do this right now, but neither is great.  I may turn this into a NuGet package just to save some headache.  For now:
* Manual inclusion
  * Build this project
  * Add a reference in your project to ImGuiScene.dll in this project's output folder
  * Add a reference in your project to SDL2-CS.dll in this project's output folder (the NuGet package is out of date and not maintained, so do not use that)
  * Add a reference in your project to ImGui.NET.dll in this project's output folder (the NuGet version _might_ work for you, but there are dependency problems in some projects, which is why the local version exists)
  * Manually copy SDL2.dll from this project's output folder, ensure it is placed in your project's output folder(s) when built
* Project inclusion
  * Create your project
  * Add this project's _solution_ (not just the project!) to your project
  * Set the ImGuiScene project to have a build dependency on SDL2-CS project
  * Add a project reference in your project to the ImGuiScene project, the ImGui.NET project and the SDL2-CS project
  * You may need to symlink package directories to make NuGet behave properly, see [here](https://stackoverflow.com/a/43923071)

#### Note
You may need to ensure "Prefer 32-bit" is disabled for your project if you use the AnyCPU target.  This may be due to the version of the native SDL2.dll that is included; I will look into providing a 32-bit version as well in the future.


### Simple example application
This is a simple example that creates a transparent ('hidden') fullscreen window that just renders the default ImGui demo window
```csharp
static void Main(string[] args)
{
    using (var scene = new SimpleImGuiScene("ImGui Test", fullscreen: true))
    {
        scene.Window.MakeTransparent(SimpleSDLWindow.CreateColorKey(0, 0, 0));

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
```
