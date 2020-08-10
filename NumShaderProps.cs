using UnityEngine;

namespace NumberManagerMod
{
    public class NumShaderProps
    {
        public readonly int NDigits;
        public readonly Vector4[] DigitBounds;
        public readonly Vector4[] DigitUV;
        public readonly Vector2 FontTransform;

        public NumShaderProps( int nDigits, Vector4[] bounds, Vector4[] uvs, Vector2 transform )
        {
            NDigits = nDigits;
            DigitBounds = bounds;
            DigitUV = uvs;
            FontTransform = transform;
        }

        public void ApplyTo( Material target )
        {
            target.SetInt("_NDigits", NDigits);
            target.SetVectorArray("_DigitBounds", DigitBounds);
            target.SetVectorArray("_DigitUV", DigitUV);
            target.SetVector("_FontTransform", FontTransform);
        }
    }
}
