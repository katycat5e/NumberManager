using NumberManager.Shared;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace NumberManager.Editor
{
    public enum FontRotation
    {
        LeftToRight = 0,
        TopDown = 1,
        BottomUp = 2,
        InvertedRightToLeft = 3,
    }

    [Serializable]
    public class FontProvider
    {
        public Font SourceFont;
        [Min(0)]
        public float Scale;
        public FontRotation Rotation;
        public int Kerning;
        public string Format = "{0:D1}";
        public Color Color;
        
        public bool UseEmission;
        [ColorUsage(false, true)]
        public Color EmissionColor = Color.white;

        public bool UseSpecular;
        [Range(0, 1)]
        public float Metallic;
        [Range(0, 1)]
        public float Smoothness;

        public bool IsRenderable => SourceFont && (Scale != 0);

        public NumberFont NumberFont { get; private set; }

        private static Material _fontRenderMaterial;
        private static Material FontRenderMaterial
        {
            get
            {
                if (!_fontRenderMaterial)
                {
                    _fontRenderMaterial = new Material(Shader.Find("TextMeshPro/Bitmap"));
                }
                return _fontRenderMaterial;
            }
        }

        private static Material _fontRotateMaterial;
        private static Material RotateMaterial
        {
            get
            {
                if (!_fontRotateMaterial)
                {
                    _fontRotateMaterial = new Material(Shader.Find("Custom/RotateFragment"));
                }
                return _fontRotateMaterial;
            }
        }

        private Vector2Int GetScaledAtlasSizeInternal(TMP_FontAsset tmpFont)
        {
            int scaledWidth = Mathf.CeilToInt(tmpFont.atlasWidth * Scale);
            int scaledHeight = Mathf.CeilToInt(tmpFont.atlasHeight * Scale);
            return new Vector2Int(scaledWidth, scaledHeight);
        }

        public Vector2Int GetRotatedAtlasSize()
        {
            if (IsRenderable)
            {
                var tmpFont = GetTmpFont(SourceFont);
                var scaled = GetScaledAtlasSizeInternal(tmpFont);
                var rotateProps = FontRotationProps.Get(Rotation);
                return rotateProps.GetRotatedTexSize(scaled);
            }
            else
            {
                return new Vector2Int(Texture2D.blackTexture.width, Texture2D.blackTexture.height);
            }
        }

        public Texture2D RenderFontToAtlas()
        {
            if (!IsRenderable) return Texture2D.blackTexture;

            var tmpFont = GetTmpFont(SourceFont);
            
            var scaledSize = GetScaledAtlasSizeInternal(tmpFont);
            var rotateProps = FontRotationProps.Get(Rotation);
            var rotatedSize = rotateProps.GetRotatedTexSize(scaledSize);

            RenderTexture previous = RenderTexture.active;

            // grab temporary & clear
            var scaledAtlas = RenderTexture.GetTemporary(scaledSize.x, scaledSize.y, 16, RenderTextureFormat.ARGB32);

            // Apply font shader & scale
            RenderTexture.active = scaledAtlas;
            GL.Clear(true, true, Color.clear);

            //FontRenderMaterial.SetTexture(ShaderUtilities.ID_MainTex, tmpFont.atlasTexture);
            //FontRenderMaterial.SetColor(ShaderUtilities.ID_FaceColor, Color);
            
            Graphics.Blit(tmpFont.atlasTexture, scaledAtlas, tmpFont.material);

            // Apply atlas transform
            var rotatedAtlas = RenderTexture.GetTemporary(rotatedSize.x, rotatedSize.y, 16, RenderTextureFormat.ARGB32);
            RenderTexture.active = rotatedAtlas;
            GL.Clear(true, true, Color.clear);

            rotateProps.ApplyTo(RotateMaterial);
            Graphics.Blit(scaledAtlas, rotatedAtlas, RotateMaterial);

            // Save atlas to temporary texture
            var tex = new Texture2D(rotatedSize.x, rotatedSize.y, TextureFormat.ARGB32, true);

            tex.ReadPixels(new Rect(0, 0, rotatedSize.x, rotatedSize.y), 0, 0);
            tex.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(scaledAtlas);
            RenderTexture.ReleaseTemporary(rotatedAtlas);

            return tex;
        }

        public void CreateFontSettings(Vector2Int offset)
        {
            NumberFont = new NumberFont()
            {
                CharXArr = new int[10],
                CharYArr = new int[10],
                CharWidthArr = new int[10],
            };

            if (!IsRenderable) return;

            var tmpFont = GetTmpFont(SourceFont);
            var rotateProps = FontRotationProps.Get(Rotation);
            var atlasSize = GetScaledAtlasSizeInternal(tmpFont);
            atlasSize = rotateProps.GetRotatedTexSize(atlasSize);

            char[] digits = "0123456789".ToCharArray();

            float maxHeight = 0;
            for (int i = 0; i < 10; i++)
            {
                var character = tmpFont.characterLookupTable[digits[i]];
                var glyph = tmpFont.glyphLookupTable[character.glyphIndex];

                if (glyph.glyphRect.height > maxHeight)
                {
                    maxHeight = glyph.glyphRect.height;
                }

                var charCoords = rotateProps.GetRotatedGlyphCoordinates(
                    Mathf.CeilToInt(glyph.glyphRect.x * Scale),
                    Mathf.CeilToInt(glyph.glyphRect.y * Scale),
                    Mathf.CeilToInt(glyph.glyphRect.width * Scale),
                    Mathf.CeilToInt(glyph.glyphRect.height * Scale),
                    atlasSize
                );

                NumberFont.CharXArr[i] = offset.x + charCoords.x;
                NumberFont.CharYArr[i] = charCoords.y + offset.y - atlasSize.y;
                NumberFont.CharWidthArr[i] = Mathf.CeilToInt(glyph.glyphRect.width * Scale);
            }

            NumberFont.Height = Mathf.CeilToInt(maxHeight * Scale);
            NumberFont.Orientation = rotateProps.FontOrientation;
            NumberFont.Kerning = Kerning;
            NumberFont.Format = Format;
            NumberFont.ReverseDigits = rotateProps.ReverseDigits;
            NumberFont.EmissionColor = UseEmission ? EmissionColor : (Color?)null;
            NumberFont.SpecularColor = UseSpecular ? new Color(Metallic, 0, 0, Smoothness) : (Color?)null;
        }

        #region Static Functions

        private static readonly Dictionary<Font, TMP_FontAsset> _fontCache = new Dictionary<Font, TMP_FontAsset>();

        private static TMP_FontAsset GetTmpFont(Font unityFont)
        {
            if (_fontCache.TryGetValue(unityFont, out var tmpFont))
            {
                return tmpFont;
            }

            tmpFont = TMP_FontAsset.CreateFontAsset(unityFont, 128, 4, 
                GlyphRenderMode.SDFAA_HINTED, 512, 512, 
                AtlasPopulationMode.Dynamic, false);

            tmpFont.TryAddCharacters("0123456789");
            tmpFont.atlasPopulationMode = AtlasPopulationMode.Static;

            _fontCache.Add(unityFont, tmpFont);
            return tmpFont;
        }

        #endregion
    }
}
