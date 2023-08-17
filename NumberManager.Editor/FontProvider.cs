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
        [Header("Font Layout")]
        [SourceFont]
        public Font SourceFont;

        [Min(1)]
        public int PointSize = 90;
        public FontRotation Rotation;
        public int Kerning;
        public string ExtraChararacters;

        [Header("Style")]
        public string Format = "{0:D1}";
        public Color Color = Color.white;

        [Header("Emission")]
        public bool UseEmission;
        [ColorUsage(false, true)]
        public Color EmissionColor;

        [Header("Metal/Rough")]
        public bool UseSpecular;
        [Range(0, 1)]
        public float Metallic;
        [Range(0, 1)]
        public float Smoothness;

        public bool IsRenderable => SourceFont && (PointSize != 0);

        public NumberFont NumberFont { get; private set; }

        private static Material _fontRenderMaterial;
        private static Material GetFontRenderMaterial(Material prototype)
        {
            if (!_fontRenderMaterial)
            {
                _fontRenderMaterial = new Material(prototype);
            }
            return _fontRenderMaterial;
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

        private const int ATLAS_SUPER_SCALE = 4;
        private const int FONT_CHAR_PADDING = 1;

        private Vector2Int GetScaledAtlasSizeInternal(TMP_FontAsset tmpFont)
        {
            //int scaledWidth = Mathf.CeilToInt(tmpFont.atlasWidth * Scale);
            //int scaledHeight = Mathf.CeilToInt(tmpFont.atlasHeight * Scale);
            return new Vector2Int(tmpFont.atlasWidth / ATLAS_SUPER_SCALE, tmpFont.atlasHeight / ATLAS_SUPER_SCALE);
        }

        public Vector2Int GetRotatedAtlasSize()
        {
            if (IsRenderable)
            {
                var tmpFont = GetTmpFont();
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

            var tmpFont = GetTmpFont();
            
            var scaledSize = GetScaledAtlasSizeInternal(tmpFont);
            var rotateProps = FontRotationProps.Get(Rotation);
            var rotatedSize = rotateProps.GetRotatedTexSize(scaledSize);

            RenderTexture previous = RenderTexture.active;

            // Apply font shader
            var superAtlas = RenderTexture.GetTemporary(tmpFont.atlasWidth, tmpFont.atlasHeight, 0, RenderTextureFormat.ARGB32);
            RenderTexture.active = superAtlas;
            GL.Clear(true, true, Color.clear);

            var fontMaterial = GetFontRenderMaterial(tmpFont.material);
            fontMaterial.SetColor(ShaderUtilities.ID_FaceColor, Color);

            Graphics.Blit(tmpFont.atlasTexture, superAtlas, fontMaterial);

            // Apply scale
            var scaledAtlas = RenderTexture.GetTemporary(scaledSize.x, scaledSize.y, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 8);
            RenderTexture.active = scaledAtlas;
            GL.Clear(true, true, Color.clear);

            Graphics.Blit(superAtlas, scaledAtlas);

            // Apply atlas transform
            var rotatedAtlas = RenderTexture.GetTemporary(rotatedSize.x, rotatedSize.y, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 8);
            RenderTexture.active = rotatedAtlas;
            GL.Clear(true, true, Color.clear);

            rotateProps.ApplyTo(RotateMaterial);
            Graphics.Blit(scaledAtlas, rotatedAtlas, RotateMaterial);

            var tex = new Texture2D(rotatedSize.x, rotatedSize.y, TextureFormat.ARGB32, 0, false);

            tex.ReadPixels(new Rect(0, 0, rotatedSize.x, rotatedSize.y), 0, 0);
            tex.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(superAtlas);
            RenderTexture.ReleaseTemporary(scaledAtlas);
            RenderTexture.ReleaseTemporary(rotatedAtlas);

            return tex;
        }

        public void CreateFontSettings(Vector2Int offset)
        {
            NumberFont = new NumberFont()
            {
                CharXArr = new int[10 + ExtraChararacters.Length],
                CharYArr = new int[10 + ExtraChararacters.Length],
                CharWidthArr = new int[10 + ExtraChararacters.Length],
            };

            if (!IsRenderable) return;

            var tmpFont = GetTmpFont();
            var rotateProps = FontRotationProps.Get(Rotation);
            var atlasSize = GetScaledAtlasSizeInternal(tmpFont);
            atlasSize = rotateProps.GetRotatedTexSize(atlasSize);

            char[] digits = "0123456789".ToCharArray();

            int maxHeight = 0;
            for (int i = 0; i < 10; i++)
            {
                var character = tmpFont.characterLookupTable[digits[i]];
                var glyph = tmpFont.glyphLookupTable[character.glyphIndex];

                if (glyph.glyphRect.height > maxHeight)
                {
                    maxHeight = glyph.glyphRect.height;
                }

                var charCoords = rotateProps.GetRotatedGlyphCoordinates(
                    Mathf.CeilToInt(glyph.glyphRect.x / (float)ATLAS_SUPER_SCALE),
                    Mathf.CeilToInt(glyph.glyphRect.y / (float)ATLAS_SUPER_SCALE),
                    Mathf.CeilToInt(glyph.glyphRect.width / (float)ATLAS_SUPER_SCALE),
                    Mathf.CeilToInt(glyph.glyphRect.height / (float)ATLAS_SUPER_SCALE),
                    atlasSize
                );

                NumberFont.CharXArr[i] = offset.x + charCoords.x - FONT_CHAR_PADDING;
                NumberFont.CharYArr[i] = charCoords.y + offset.y - atlasSize.y + FONT_CHAR_PADDING;
                NumberFont.CharWidthArr[i] = Mathf.CeilToInt(glyph.glyphRect.width / (float)ATLAS_SUPER_SCALE) + 2 * FONT_CHAR_PADDING;
            }

            NumberFont.ExtraChars = new ExtraChar[ExtraChararacters.Length];
            for (int i = 0; i < ExtraChararacters.Length; i++)
            {
                var character = tmpFont.characterLookupTable[ExtraChararacters[i]];
                var glyph = tmpFont.glyphLookupTable[character.glyphIndex];

                var charCoords = rotateProps.GetRotatedGlyphCoordinates(
                    Mathf.CeilToInt(glyph.glyphRect.x / (float)ATLAS_SUPER_SCALE),
                    Mathf.CeilToInt(glyph.glyphRect.y / (float)ATLAS_SUPER_SCALE),
                    Mathf.CeilToInt(glyph.glyphRect.width / (float)ATLAS_SUPER_SCALE),
                    Mathf.CeilToInt(glyph.glyphRect.height / (float)ATLAS_SUPER_SCALE),
                    atlasSize
                );

                NumberFont.ExtraChars[i] = new ExtraChar()
                {
                    Char = ExtraChararacters[i],
                    X = offset.x + charCoords.x - FONT_CHAR_PADDING,
                    Y = charCoords.y + offset.y - atlasSize.y + FONT_CHAR_PADDING,
                    Width = Mathf.CeilToInt(glyph.glyphRect.width / (float)ATLAS_SUPER_SCALE) + 2 * FONT_CHAR_PADDING,
                };

                NumberFont.CharXArr[i + 10] = NumberFont.ExtraChars[i].X;
                NumberFont.CharYArr[i + 10] = NumberFont.ExtraChars[i].Y;
                NumberFont.CharWidthArr[i + 10] = NumberFont.ExtraChars[i].Width;
            }

            NumberFont.Height = Mathf.CeilToInt(maxHeight / (float)ATLAS_SUPER_SCALE) + 2 * FONT_CHAR_PADDING;
            NumberFont.Orientation = rotateProps.FontOrientation;
            NumberFont.Kerning = Kerning;
            NumberFont.Format = Format;
            NumberFont.ReverseDigits = rotateProps.ReverseDigits;
            NumberFont.EmissionColor = UseEmission ? EmissionColor : (Color?)null;
            NumberFont.SpecularColor = UseSpecular ? new Color(Metallic, 0, 0, Smoothness) : (Color?)null;
        }

        #region Static Functions

        private class TmpFontWrapper
        {
            public string Key;
            public readonly TMP_FontAsset TmpFont;

            private LinkedList<FontProvider> _subscribers = new LinkedList<FontProvider>();

            public TmpFontWrapper(string key, TMP_FontAsset tmpFont)
            {
                Key = key;
                TmpFont = tmpFont;
            }

            public bool IsUnused => _subscribers.Count == 0;

            public void Subscribe(FontProvider subscriber)
            {
                if (!_subscribers.Contains(subscriber))
                {
                    _subscribers.AddLast(subscriber);
                }
            }

            public void Unsubscribe(FontProvider subscriber)
            {
                _subscribers.Remove(subscriber);
            }
        }

        private static readonly LinkedList<TmpFontWrapper> _fontCache = new LinkedList<TmpFontWrapper>();

        private TMP_FontAsset GetTmpFont()
        {
            string key = $"{SourceFont.name}_{PointSize}_{ExtraChararacters}";

            var node = _fontCache.First;
            while (node != null)
            {
                if (node.Value.Key == key)
                {
                    node.Value.Subscribe(this);
                    return node.Value.TmpFont;
                }

                var next = node.Next;
                if (node.Value.IsUnused)
                {
                    _fontCache.Remove(node);
                }
                node = next;
            }

            int padding = Math.Max(Mathf.CeilToInt(PointSize / 15f * ATLAS_SUPER_SCALE), FONT_CHAR_PADDING);
            int atlasDimension = (PointSize + padding) * 3 * ATLAS_SUPER_SCALE;

            var tmpFont = TMP_FontAsset.CreateFontAsset(SourceFont, PointSize * ATLAS_SUPER_SCALE, padding, 
                GlyphRenderMode.SDFAA_HINTED, atlasDimension, atlasDimension,
                AtlasPopulationMode.Dynamic, false);

            tmpFont.TryAddCharacters("0123456789");
            if (!string.IsNullOrEmpty(ExtraChararacters))
            {
                tmpFont.TryAddCharacters(ExtraChararacters);
            }
            tmpFont.atlasPopulationMode = AtlasPopulationMode.Static;

            _fontCache.AddLast(new TmpFontWrapper(key, tmpFont));
            return tmpFont;
        }

        #endregion
    }
}
