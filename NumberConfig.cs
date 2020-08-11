using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using UnityEngine;

namespace NumberManagerMod
{
    public class NumberConfig
    {
        private const string FONT_TEX_FILE = "num.png";
        private const string FONT_EMIT_FILE = "num_e.png";

        [XmlAttribute]
        public string TargetTexture;

        [XmlAttribute]
        public FontBlendMode BlendMode = FontBlendMode.Normal;

        public NumberFont[] Fonts;

        [XmlAttribute]
        public int MinNumber = 1;

        [XmlAttribute]
        public int MaxNumber = 9999;

        public int GetRandomNum()
        {
            return UnityEngine.Random.Range(MinNumber, MaxNumber);
        }

        public NumAttachPoint[] AttachPoints;

        // Created on initialization
        [XmlIgnore]
        public Texture2D FontTexture = null;

        [XmlIgnore]
        public Texture2D EmissionTexture = null;

        [XmlIgnore]
        public int TextureWidth = 0;

        [XmlIgnore]
        public int TextureHeight = 0;


        public bool Initialize( string dirPath )
        {
            bool allGood = true;

            string fontPath = Path.Combine(dirPath, FONT_TEX_FILE);
            var imgData = File.ReadAllBytes(fontPath);

            var texture = new Texture2D(2, 2); // temporary smol image
            texture.LoadImage(imgData);

            FontTexture = texture;
            TextureWidth = texture.width;
            TextureHeight = texture.height;

            // Attempt to load emission map
            string emitPath = Path.Combine(dirPath, FONT_EMIT_FILE);
            if( File.Exists(emitPath) )
            {
                try
                {
                    imgData = File.ReadAllBytes(emitPath);

                    texture = new Texture2D(2, 2);
                    texture.LoadImage(imgData);

                    if( (texture.width != TextureWidth) || (texture.height != TextureHeight) )
                    {
                        // emission map needs to be the same size as the diffuse
                        NumberManager.modEntry.Logger.Warning($"Font emission map size must match diffuse texture, emission will not be loaded");
                        allGood = false;
                    }
                    else
                    {
                        EmissionTexture = texture;
                    }
                }
                catch( Exception ex )
                {
                    NumberManager.modEntry.Logger.Warning($"Failed to load numbering emission map: {ex.Message}");
                    EmissionTexture = null;
                    allGood = false;
                }
            }

            return allGood;
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

        private int[] ParseIntArray( string s, string dbgName )
        {
            try
            {
                int[] arr = s.Split(',')
                    .Select(n => int.Parse(n))
                    .ToArray();

                return arr;
            }
            catch( Exception ex ) when( ex is ArgumentException || ex is FormatException || ex is OverflowException )
            {
                Debug.LogError($"Invalid {dbgName} integer list \"{s}\"");
            }
            return null;
        }

        // Comma-delineated list of int
        [XmlAttribute(AttributeName = "CharWidth")]
        public string CharWidthString;

        private int[] _cWidths = null;
        public int[] CharWidth
        {
            get
            {
                if( _cWidths == null ) _cWidths = ParseIntArray(CharWidthString, nameof(CharWidth));
                return _cWidths;
            }
        }

        [XmlAttribute(AttributeName = "CharX")]
        public string CharXString;

        private int[] _cX;
        public int[] CharX
        {
            get
            {
                if( _cX == null ) _cX = ParseIntArray(CharXString, nameof(CharX));
                return _cX;
            }
        }

        [XmlAttribute(AttributeName = "CharY")]
        public string CharYString;

        private int[] _cY;
        public int[] CharY
        {
            get
            {
                if( _cY == null ) _cY = ParseIntArray(CharYString, nameof(CharY));
                return _cY;
            }
        }
    }

    public enum NumOrientation { Horizontal, Vertical }
}
