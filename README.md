# ImGuiScene
Currently a fairly rough library that wraps everything necessary to add ImGui support to a C# project.

ImGui integration is done with [ImGui.NET](https://github.com/mellinoe/ImGui.NET), while window creation and events use [SDL2-CS](https://github.com/flibitijibibo/SDL2-CS).  The semi-official ImGui backend implementations for [SDL](https://github.com/ocornut/imgui/blob/master/examples/imgui_impl_sdl.cpp), [DX11](https://github.com/ocornut/imgui/blob/master/examples/imgui_impl_dx11.cpp), and [OpenGL3](https://github.com/ocornut/imgui/blob/master/examples/imgui_impl_opengl3.cpp) were ported as directly as possible to SDL2-CS, SharpDX and OpenGL.NET.  The C++ sources for those implementations are extremely messy and poor as-is, and currently no effort has been made to clean them up for C#, or to impose reasonable code design on them.  They do work for now, and hopefully I can improve on them later.

A simple sample application will follow soon.

#### Usage Note
You may need to ensure "Prefer 32-bit" is disabled for your project if you use the AnyCPU target.  This may be due to the version of the native dlls that are included; I will look into providing a 32-bit version as well in the future.


### Simple example application
This is a simple example that creates a transparent ('hidden') fullscreen window that just renders the default ImGui demo window
```csharp
static void Main(string[] args)
{
    using (var scene = new SimpleImGuiScene(RendererFactory.RendererBackend.DirectX11, new WindowCreateInfo
    {
        Title = "Test Window",
        Fullscreen = true,
        TransparentColor = new float[] { 0, 0, 0 }
    }))
    {
        scene.Window.OnSDLEvent += (ref SDL.SDL_Event sdlEvent) =>
        {
            if (sdlEvent.type == SDL.SDL_EventType.SDL_KEYDOWN && sdlEvent.key.keysym.scancode == SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE)
            {
                scene.ShouldQuit = true;
            }
            return true;
        };

        scene.OnBuildUI += () =>
        {
            // Any ImGui code can be put here (or on other methods attached to OnBuildUI) and will work as expected
            // Virtually all actual application logic will be inside these handlers
            ImGui.ShowDemoWindow();
        };

        scene.Run();
    }
}
```
