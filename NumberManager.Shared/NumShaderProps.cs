using System.Linq;
using UnityEngine;

namespace NumberManager.Shared
{
    public class NumShaderProps
    {
        public int NDigits;
        public Vector4[] DigitBounds;
        public Vector4[] DigitUV;
        public Vector2 FontTransform;
        public FontBlendMode BlendMode;
        public Vector4[] Emission;
        public bool[] UseEmission;
        public Vector4[] Specular;
        public bool[] UseSpecular;

        public void ApplyTo( Material target )
        {
            target.SetInt("_NDigits", NDigits);
            target.SetVectorArray("_DigitBounds", DigitBounds);
            target.SetVectorArray("_DigitUV", DigitUV);
            target.SetVector("_FontTransform", FontTransform);
            target.SetInt("_BlendMode", (int)BlendMode);
            target.SetVectorArray("_FontEmission", Emission);
            target.SetFloatArray("_UseFEmit", UseEmission.Select(b => b ? 1f : 0f).ToArray());
            target.SetVectorArray("_FontSpecular", Specular);
            target.SetFloatArray("_UseFSpec", UseSpecular.Select(b => b ? 1f : 0f).ToArray());
        }
    }
}
