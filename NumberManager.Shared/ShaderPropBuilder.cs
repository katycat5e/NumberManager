﻿using System;
using System.Linq;
using UnityEngine;

namespace NumberManager.Shared
{
    public static class ShaderPropBuilder
    {
        private const int MAX_DIGITS = 32;

        // Split the given integer into its decimal digits
        private static int[] GetDigits(NumberFont font, int number)
        {
            var formatted = string.Format(font.Format, number).ToCharArray();
            var digits = formatted.Select(c =>
            {
                // numeric digit
                if ((c >= '0') && (c <= '9'))
                {
                    return c - '0';
                }

                // special character
                for (int i = 0; i < font.ExtraChars.Length; i++)
                {
                    if (c == font.ExtraChars[i].Char)
                    {
                        return i + 10;
                    }
                }
                
                // fallback
                return 0;
            });

            return digits.ToArray();
        }

        // Helper methods for shader property initialization
        private static bool IsValidIdx<T>( int i, T[] arr ) => (i >= 0) && (i < arr.Length);
        private static Vector4 GetDigitUV( int x, int y, Vector2 texSize )
        {
            return new Vector4(x / texSize.x, (texSize.y - y) / texSize.y);
        }
        private static Vector4 GetUVBounds( float left, float bottom, float right, float top, Vector2 texSize )
        {
            return new Vector4(
                left / texSize.x,
                bottom / texSize.y,
                right / texSize.x,
                top / texSize.y
            );
        }

        public static NumShaderProps GetShaderProps(NumberConfig scheme, int number, int width, int height, Action<string> logWarning)
        {
            Vector2 fontTexSize = new Vector2(scheme.TextureWidth, scheme.TextureHeight);
            Vector2 mainSize = new Vector2(width, height);

            Vector4[] digitBounds = new Vector4[MAX_DIGITS];
            Vector4[] digitUV = new Vector4[MAX_DIGITS];

            Vector4[] emission = new Vector4[MAX_DIGITS];
            Vector4[] specular = new Vector4[MAX_DIGITS];
            bool[] useEmit = new bool[MAX_DIGITS];
            bool[] useSpec = new bool[MAX_DIGITS];

            int nTotalDigits = 0;
            int digitIdx = 0;

            foreach( var attachPoint in scheme.AttachPoints )
            {
                // get font associated with this point
                if( !IsValidIdx(attachPoint.FontIdx, scheme.Fonts) )
                {
                    logWarning($"Invalid numbering font index {attachPoint.FontIdx}");
                    continue;
                }
                var font = scheme.Fonts[attachPoint.FontIdx];

                int[] digits = GetDigits(font, number);

                if ( (nTotalDigits + digits.Length) > MAX_DIGITS )
                {
                    // too many digits! :(
                    logWarning("Maximum number of digits exceeded in numbering scheme");
                    break;
                }
                nTotalDigits += digits.Length;

                // get digit array for this attach point
                var adjustedDigits = font.ReverseDigits ? digits.Reverse().ToArray() : digits;

                // sum of char widths + (kerning * (nDigits - 1))
                int numberWidth = adjustedDigits.Select(d => font.CharWidthArr[d] + font.Kerning).Sum() - font.Kerning; // in pixels

                int mainStart, mainEnd, transStart, transEnd;

                if( font.Orientation == NumOrientation.Horizontal )
                {
                    mainStart = attachPoint.X - (numberWidth / 2);
                    transStart = (height - attachPoint.Y) - (font.Height / 2);
                    transEnd = transStart + font.Height;
                }
                else
                {
                    mainStart = (height - attachPoint.Y) - (numberWidth / 2);
                    transStart = attachPoint.X - (font.Height / 2);
                    transEnd = transStart + font.Height;
                }

                // digit widths as MainTex uv distance
                foreach( int d in adjustedDigits )
                {
                    mainEnd = mainStart + font.CharWidthArr[d];
                    int nextMain = mainEnd + font.Kerning;

                    if( font.Orientation == NumOrientation.Horizontal )
                    {
                        // main axis = X, transverse axis = Y
                        digitBounds[digitIdx] = GetUVBounds(mainStart, transStart, mainEnd, transEnd, mainSize);
                    }
                    else
                    {
                        // main axis = Y, transverse axis = X
                        digitBounds[digitIdx] = GetUVBounds(transStart, mainStart, transEnd, mainEnd, mainSize);
                    }

                    digitUV[digitIdx] = GetDigitUV(font.CharXArr[d], font.CharYArr[d], fontTexSize);

                    // Emission setup per font
                    if( font.EmissionColor.HasValue )
                    {
                        emission[digitIdx] = font.EmissionColor.Value;
                        useEmit[digitIdx] = true;
                    }
                    else useEmit[digitIdx] = false;

                    // Specular setup per font
                    if( font.SpecularColor.HasValue )
                    {
                        specular[digitIdx] = font.SpecularColor.Value;
                        useSpec[digitIdx] = true;
                    }
                    else useSpec[digitIdx] = false;


                    mainStart = nextMain;
                    digitIdx += 1;
                }
            }

            // transform from MainTex uv to FontTex uv
            Vector2 transform = mainSize / new Vector2(scheme.TextureWidth, scheme.TextureHeight);

            return new NumShaderProps()
            {
                NDigits = nTotalDigits,
                DigitBounds = digitBounds,
                DigitUV = digitUV,
                FontTransform = transform,
                BlendMode = scheme.BlendMode,
                Emission = emission,
                UseEmission = useEmit,
                Specular = specular,
                UseSpecular = useSpec
            };
        }
    }
}
