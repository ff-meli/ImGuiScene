using SharpDX.DXGI;

namespace ImGuiScene
{
    // ported from https://github.com/crosire/reshade/blob/master/source/dxgi/format_utils.hpp
    // which was also probably copied from the DirecTex stuff
    public static class FormatUtils
    {
        public static Format MakeSrgb(Format format)
        {
            switch (format)
            {
                case Format.R8G8B8A8_Typeless:
                case Format.R8G8B8A8_UNorm:
                    return Format.R8G8B8A8_UNorm_SRgb;

                case Format.BC1_Typeless:
                case Format.BC1_UNorm:
                    return Format.BC1_UNorm_SRgb;

                case Format.BC2_Typeless:
                case Format.BC2_UNorm:
                    return Format.BC2_UNorm_SRgb;

                case Format.BC3_Typeless:
                case Format.BC3_UNorm:
                    return Format.BC3_UNorm_SRgb;

                default:
                    return format;
            }
        }

        public static Format MakeNormal(Format format)
        {
            switch (format)
            {
                case Format.R32G8X24_Typeless:
                    return Format.R32_Float_X8X24_Typeless;

                case Format.R8G8B8A8_Typeless:
                case Format.R8G8B8A8_UNorm_SRgb:
                    return Format.R8G8B8A8_UNorm;

                case Format.R32_Typeless:
                    return Format.R32_Float;

                case Format.R24G8_Typeless:
                    return Format.R24_UNorm_X8_Typeless;

                case Format.R16_Typeless:
                    return Format.R16_Float;

                case Format.BC1_Typeless:
                case Format.BC1_UNorm_SRgb:
                    return Format.BC1_UNorm;

                case Format.BC2_Typeless:
                case Format.BC2_UNorm_SRgb:
                    return Format.BC2_UNorm;

                case Format.BC3_Typeless:
                case Format.BC3_UNorm_SRgb:
                    return Format.BC3_UNorm;

                default:
                    return format;
            }
        }

        public static Format MakeTypeless(Format format)
        {
            switch (format)
            {
                case Format.D32_Float_S8X24_UInt:
                case Format.R32_Float_X8X24_Typeless:
                    return Format.R32G8X24_Typeless;

                case Format.R8G8B8A8_UNorm:
                case Format.R8G8B8A8_UNorm_SRgb:
                    return Format.R8G8B8A8_Typeless;

                case Format.D32_Float:
                case Format.R32_Float:
                    return Format.R32_Typeless;

                case Format.D24_UNorm_S8_UInt:
                case Format.R24_UNorm_X8_Typeless:
                    return Format.R24G8_Typeless;

                case Format.D16_UNorm:
                case Format.R16_Float:
                    return Format.R16_Typeless;

                case Format.BC1_UNorm:
                case Format.BC1_UNorm_SRgb:
                    return Format.BC1_Typeless;

                case Format.BC2_UNorm:
                case Format.BC2_UNorm_SRgb:
                    return Format.BC2_Typeless;

                case Format.BC3_UNorm:
                case Format.BC3_UNorm_SRgb:
                    return Format.BC3_Typeless;

                default:
                    return format;
            }
        }
    }
}
