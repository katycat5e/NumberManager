using System.Linq;
using UnityEngine;

namespace NumberManager.Shared
{
    public class NumShaderProps
    {
        public const string ID_MAIN_TEXTURE = "_MainTex";
        public const string ID_FONT_TEXTURE = "_FontTex";
        public const string ID_METAL_GLOSS_MAP = "_MetallicGlossMap";
        public const string ID_BUMP_MAP = "_BumpMap";
        public const string ID_EMISSION_MAP = "_EmissionMap";

        public const string ID_NUM_DIGITS = "_NDigits";
        public const string ID_DIGIT_BOUNDS = "_DigitBounds";
        public const string ID_DIGIT_UV = "_DigitUV";
        public const string ID_FONT_TRANSFORM = "_FontTransform";
        public const string ID_BLEND_MODE = "_BlendMode";
        public const string ID_FONT_EMISSION = "_FontEmission";
        public const string ID_USE_EMISSION = "_UseFEmit";
        public const string ID_FONT_SPECULAR = "_FontSpecular";
        public const string ID_USE_SPECULAR = "_UseFSpec";
        public const string ID_COLORIZE_WHITE_LEVEL = "_ColorizeWhiteLvl";

        public int NDigits;
        public Vector4[] DigitBounds;
        public Vector4[] DigitUV;
        public Vector2 FontTransform;
        public FontBlendMode BlendMode;
        public Vector4[] Emission;
        public bool[] UseEmission;
        public Vector4[] Specular;
        public bool[] UseSpecular;
        public float ColorizeWhiteLevel;

        public void ApplyTo( Material target )
        {
            target.SetInt(ID_NUM_DIGITS, NDigits);
            target.SetVectorArray(ID_DIGIT_BOUNDS, DigitBounds);
            target.SetVectorArray(ID_DIGIT_UV, DigitUV);
            target.SetVector(ID_FONT_TRANSFORM, FontTransform);
            target.SetInt(ID_BLEND_MODE, (int)BlendMode);
            target.SetVectorArray(ID_FONT_EMISSION, Emission);
            target.SetFloatArray(ID_USE_EMISSION, UseEmission.Select(b => b ? 1f : 0f).ToArray());
            target.SetVectorArray(ID_FONT_SPECULAR, Specular);
            target.SetFloatArray(ID_USE_SPECULAR, UseSpecular.Select(b => b ? 1f : 0f).ToArray());
            target.SetFloat(ID_COLORIZE_WHITE_LEVEL, ColorizeWhiteLevel);
        }
    }
}
