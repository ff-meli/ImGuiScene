
using System;

namespace ImGuiScene
{
    public interface IImGuiInputHandler : IDisposable
    {
        void NewFrame(int width, int height);
    }
}
