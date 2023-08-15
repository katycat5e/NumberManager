//using DV.ThingTypes;
//using SMShared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using UnityEngine;

namespace NumberManager.Shared
{
    public class NumberConfig
    {
        private const string FONT_TEX_FILE = "num.png";

        [XmlIgnore]
        public string LiveryId = null;
        [XmlIgnore]
        public string SkinName = null;
        [XmlIgnore]
        public bool IsDefault = false;

        [XmlAttribute]
        public string TargetTexture;

        [XmlAttribute]
        public FontBlendMode BlendMode = FontBlendMode.Normal;

        public NumberFont[] Fonts;

        [XmlAttribute]
        public int MinNumber = 1;

        [XmlAttribute]
        public int MaxNumber = 9999;

        [XmlAttribute]
        public int Offset = 0;

        [XmlAttribute]
        public bool ForceRandom = false;

        [XmlIgnore]
        private int[] Sequence = null;
        [XmlIgnore]
        private int SequenceIdx = 0;

        private readonly struct NumRange
        {
            public readonly int Min;
            public readonly int Max;

            public NumRange( int min, int max )
            {
                Min = min;
                Max = max;
            }
        }

        private static readonly Dictionary<NumRange, int[]> SequenceCache = new Dictionary<NumRange, int[]>();

        private void CreateShuffledOrder()
        {
            var range = new NumRange(MinNumber, MaxNumber);
            if( SequenceCache.TryGetValue(range, out Sequence) )
            {
                return;
            }

            Sequence = Enumerable.Range(MinNumber, MaxNumber - MinNumber + 1).ToArray();

            // Fisher-Yates shuffle
            var rand = new System.Random();

            // for i from 0 to n-2
            for( int i = 0; i < Sequence.Length - 1; i++ )
            {
                // arr[i] <-> arr[j]
                int j = rand.Next(0, i);
                (Sequence[j], Sequence[i]) = (Sequence[i], Sequence[j]);
            }

            SequenceCache[range] = Sequence;
        }

        public int GetRandomNum(bool allowOffset)
        {
            if( Sequence == null ) CreateShuffledOrder();

            int offset = allowOffset ? Offset : 0;
            int result = Sequence[SequenceIdx] + offset;

            SequenceIdx += 1;
            if( SequenceIdx >= Sequence.Length ) SequenceIdx = 0;

            return result;
        }

        public NumAttachPoint[] AttachPoints;

        // Created on initialization
        [XmlIgnore]
        public Texture FontTexture = null;

        [XmlIgnore]
        public int TextureWidth = 0;

        [XmlIgnore]
        public int TextureHeight = 0;


        public void Initialize(string carId, string dirPath, IRemapProvider remapProvider)
        {
            if (remapProvider.TryGetUpdatedTextureName(carId, TargetTexture, out string newTexName))
            {
                TargetTexture = newTexName;
            }
            LiveryId = carId;

            string fontPath = Path.Combine(dirPath, FONT_TEX_FILE);
            if (File.Exists(fontPath))
            {
                var imgData = File.ReadAllBytes(fontPath);

                var texture = new Texture2D(2, 2); // temporary smol image
                texture.LoadImage(imgData);

                FontTexture = texture;
                TextureWidth = texture.width;
                TextureHeight = texture.height;
            }

            foreach( var f in Fonts )
            {
                f.Initialize();
            }
        }

        public void StringPack()
        {
            foreach (var f in Fonts)
            {
                f.StringPack();
            }
        }
    }

    public enum FontBlendMode
    {
        Normal = 0,
        Add = 1,
        Subtract = 2,
        Multipy = 3,
        Divide = 4,
        Colorize = 5
    }

    [Serializable]
    public class NumAttachPoint
    {
        [XmlAttribute]
        public int FontIdx;

        [XmlAttribute]
        public int X;

        [XmlAttribute]
        public int Y;
    }

    public class NumberFont
    {
        // Character Properties
        [XmlAttribute]
        public int Height;

        [XmlAttribute]
        public NumOrientation Orientation = NumOrientation.Horizontal;

        [XmlAttribute]
        public int Kerning;

        [XmlAttribute]
        public bool ReverseDigits = false;

        [XmlAttribute]
        public string Format = "{0:D1}";

        private int[] ParseIntArray(string s, string dbgName, IEnumerable<int> extraValues = null)
        {
            if (s == null) throw new ArgumentException($"{dbgName} attribute cannot be null");

            try
            {
                var arr = s.Split(',')
                    .Select(n => int.Parse(n));

                if (extraValues != null)
                {
                    arr = arr.Concat(extraValues);
                }

                return arr.ToArray();
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is OverflowException)
            {
                throw new ArgumentException($"{dbgName} attribute is invalid integer list", ex);
            }
        }

        private Color? ParseColor( string s, string dbgName )
        {
            if( s == null ) return null;
            float[] parts;

            try
            {
                parts = s.Split(',').Select(f => float.Parse(f)).ToArray();
            }
            catch( Exception ex )
            {
                throw new ArgumentException($"{dbgName} attribute is invalid color", ex);
            }

            if( parts.Length == 4 )
            {
                return new Color(parts[0], parts[1], parts[2], parts[3]);
            }
            else if( parts.Length == 3 )
            {
                return new Color(parts[0], parts[1], parts[2]);
            }
            else throw new ArgumentException($"{dbgName} attribute is invalid color, must have 3 or 4 components");
        }

        // Comma-delineated list of int
        [XmlAttribute(AttributeName = "CharWidth")]
        public string CharWidthString;

        [XmlIgnore]
        public int[] CharWidthArr { get; set; }

        [XmlAttribute(AttributeName = "CharX")]
        public string CharXString = null;

        [XmlIgnore]
        public int[] CharXArr { get; set; }

        [XmlAttribute(AttributeName = "CharY")]
        public string CharYString;

        [XmlIgnore]
        public int[] CharYArr { get; set; }

        // Emission & Specular colors
        [XmlAttribute(AttributeName = "Emission")]
        public string EmissionString = null;
        [XmlIgnore]
        public Color? EmissionColor = null;

        [XmlAttribute(AttributeName = "Specular")]
        public string SpecularString = null;
        [XmlIgnore]
        public Color? SpecularColor = null;

        public ExtraChar[] ExtraChars;

        public void Initialize()
        {
            CharWidthArr = ParseIntArray(CharWidthString, "CharWidth", ExtraChars.Select(e => e.Width));
            CharXArr = ParseIntArray(CharXString, "CharX", ExtraChars.Select(e => e.X));
            CharYArr = ParseIntArray(CharYString, "CharY", ExtraChars.Select(e => e.Y));

            EmissionColor = ParseColor(EmissionString, "Emission");
            SpecularColor = ParseColor(SpecularString, "Specular");
        }

        public void StringPack()
        {
            CharXString = string.Join(",", CharXArr);
            CharYString = string.Join(",", CharYArr);
            CharWidthString = string.Join(",", CharWidthArr);

            if (EmissionColor.HasValue)
            {
                var c = EmissionColor.Value;
                EmissionString = $"{c.r:F3}, {c.g:F3}, {c.b:F3}, {c.a:F3}";
            }
            else
            {
                EmissionString = null;
            }

            if (SpecularColor.HasValue)
            {
                var c = SpecularColor.Value;
                SpecularString = $"{c.r:F3}, {c.g:F3}, {c.b:F3}, {c.a:F3}";
            }
            else
            {
                SpecularString = null;
            }
        }
    }

    [Serializable]
    public class ExtraChar
    {
        [XmlAttribute]
        public char Char;

        [XmlAttribute]
        public int X;

        [XmlAttribute]
        public int Y;

        [XmlAttribute]
        public int Width;
    }

    public enum NumOrientation { Horizontal, Vertical }
}
