using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using UnityEngine;

namespace NumberManagerMod
{
    public class NumberConfig
    {
        private const string FONT_TEX_FILE = "num.png";

        [XmlIgnore]
        public SkinManagerMod.Skin Skin = null;

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

        private struct NumRange
        {
            public readonly int Min;
            public readonly int Max;

            public NumRange( int min, int max )
            {
                Min = min;
                Max = max;
            }
        }

        private static Dictionary<NumRange, int[]> SequenceCache = new Dictionary<NumRange, int[]>();

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
                int tmp = Sequence[i];
                Sequence[i] = Sequence[j];
                Sequence[j] = tmp;
            }

            SequenceCache[range] = Sequence;
        }

        public int GetRandomNum()
        {
            if( Sequence == null ) CreateShuffledOrder();

            int offset = NumberManager.Settings.AllowCarIdOffset ? Offset : 0;
            int result = Sequence[SequenceIdx] + offset;

            SequenceIdx += 1;
            if( SequenceIdx >= Sequence.Length ) SequenceIdx = 0;

            return result;
        }

        public NumAttachPoint[] AttachPoints;

        // Created on initialization
        [XmlIgnore]
        public Texture2D FontTexture = null;

        [XmlIgnore]
        public int TextureWidth = 0;

        [XmlIgnore]
        public int TextureHeight = 0;


        public void Initialize( string dirPath )
        {
            string fontPath = Path.Combine(dirPath, FONT_TEX_FILE);
            var imgData = File.ReadAllBytes(fontPath);

            var texture = new Texture2D(2, 2); // temporary smol image
            texture.LoadImage(imgData);

            FontTexture = texture;
            TextureWidth = texture.width;
            TextureHeight = texture.height;

            foreach( var f in Fonts )
            {
                f.Initialize();
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
        [XmlAttribute]
        public int Height;

        [XmlAttribute]
        public NumOrientation Orientation = NumOrientation.Horizontal;

        [XmlAttribute]
        public int Kerning;

        [XmlAttribute]
        public bool ReverseDigits = false;

        private int[] ParseIntArray( string s, string dbgName )
        {
            if( s == null ) throw new ArgumentException($"{dbgName} attribute cannot be null");

            try
            {
                int[] arr = s.Split(',')
                    .Select(n => int.Parse(n))
                    .ToArray();

                return arr;
            }
            catch( Exception ex ) when( ex is ArgumentException || ex is FormatException || ex is OverflowException )
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
        public int[] CharWidthArr { get; private set; }

        [XmlAttribute(AttributeName = "CharX")]
        public string CharXString = null;

        [XmlIgnore]
        public int[] CharXArr { get; private set; }

        [XmlAttribute(AttributeName = "CharY")]
        public string CharYString;

        [XmlIgnore]
        public int[] CharYArr { get; private set; }

        // Emission & Specular colors
        [XmlAttribute(AttributeName = "Emission")]
        public string EmissionString = null;
        public Color? EmissionColor = null;

        [XmlAttribute(AttributeName = "Specular")]
        public string SpecularString = null;
        public Color? SpecularColor = null;

        public void Initialize()
        {
            CharWidthArr = ParseIntArray(CharWidthString, "CharWidth");
            CharXArr = ParseIntArray(CharXString, "CharX");
            CharYArr = ParseIntArray(CharYString, "CharY");

            EmissionColor = ParseColor(EmissionString, "Emission");
            SpecularColor = ParseColor(SpecularString, "Specular");
        }
    }

    public enum NumOrientation { Horizontal, Vertical }
}
