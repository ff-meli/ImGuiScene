using System;

namespace ImGuiScene
{
    public delegate void BuildUIDelegate();

    public interface IScene : IDisposable
    {
        event BuildUIDelegate OnBuildUI;

        string ImGuiIniPath { get; set; }

        void Frame();

        TextureWrap LoadImage(string path);
        TextureWrap LoadImage(byte[] imageBytes);
    }
}
