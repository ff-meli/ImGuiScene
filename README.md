# ImGuiScene
Currently a very rough library that wraps everything necessary to add ImGui support to a C# project.

ImGui integration is done with [ImGui.NET](https://github.com/mellinoe/ImGui.NET), while window creation and events use [SDL2-CS](https://github.com/flibitijibibo/SDL2-CS).  The semi-official ImGui backend implementations for [SDL](https://github.com/ocornut/imgui/blob/master/examples/imgui_impl_sdl.cpp) and [DX11](https://github.com/ocornut/imgui/blob/master/examples/imgui_impl_dx11.cpp) were ported as directly as possible to SDL2-CS and SharpDX.  The C++ sources for those implementations are extremely messy and poor as-is, and currently no effort has been made to clean them up for C#, or to impose reasonable code design on them.  They do work for now, and hopefully I can improve on them later.

A simple sample usage will follow soon.

### Note
You may need to ensure "Prefer 32-bit" is disabled for your project if you use the AnyCPU target.  This may be due to the version of the native SDL2.dll that is included; I will look into providing a 32-bit version as well in the future.
