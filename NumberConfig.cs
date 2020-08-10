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

        [XmlAttribute]
        public string TargetTexture;

        public NumberFont[] Fonts;

        [XmlAttribute]
        public int MinNumber;

        [XmlAttribute]
        public int MaxNumber;

        public int GetRandomNum()
        {
            return UnityEngine.Random.Range(MinNumber, MaxNumber);
        }

        [XmlElement(ElementName = "Attachment")]
        public NumAttachPoint[] AttachPoints;

        // Created on initialization
        [XmlIgnore]
        public Texture2D FontTexture = null;

        [XmlIgnore]
        public int TextureWidth = 0;

        [XmlIgnore]
        public int TextureHeight = 0;


        public bool Initialize( string dirPath )
        {
            try
            {
                string fontPath = Path.Combine(dirPath, FONT_TEX_FILE);
                var imgData = File.ReadAllBytes(fontPath);

                var texture = new Texture2D(2, 2); // temporary smol image
                texture.LoadImage(imgData);

                FontTexture = texture;
                TextureWidth = texture.width;
                TextureHeight = texture.height;
            }
            catch( Exception ex )
            {
                Debug.LogError($"Couldn't load numbering font: {ex.Message}");
                return false;
            }

            return true;
        }

        public bool IsValidNumber( int carNum ) => (carNum >= MinNumber) && (carNum <= MaxNumber);
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
