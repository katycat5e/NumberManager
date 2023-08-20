using NumberManager.Shared;
using UnityEngine;

namespace NumberManager.Editor
{
    public readonly struct FontRotationProps
    {
        private static readonly FontRotationProps[] _transforms =
        {
            new FontRotationProps(false, false, false),
            new FontRotationProps(true, false, false),
            new FontRotationProps(true, true, true),
            new FontRotationProps(false, true, true),
        };

        public static FontRotationProps Get(FontRotation rotation)
        {
            return _transforms[(int)rotation];
        }

        public readonly bool Rotate;
        public readonly bool InvertX;
        public readonly bool InvertY;

        public FontRotationProps(bool rotate, bool invertX, bool invertY)
        {
            Rotate = rotate;
            InvertX = invertX;
            InvertY = invertY;
        }

        public NumOrientation FontOrientation => Rotate ? NumOrientation.Vertical : NumOrientation.Horizontal;
        public bool ReverseDigits => 
            (Rotate && !InvertX && !InvertY) ||
            (!Rotate && InvertX && InvertY);

        public Vector2Int GetRotatedTexSize(Vector2Int size)
        {
            if (Rotate)
            {
                return new Vector2Int(size.y, size.x);
            }
            return size;
        }

        public void ApplyTo(Material mat)
        {
            mat.SetFloat("_Rotate", Rotate ? 1 : 0);
            mat.SetFloat("_InvertX", InvertX ? 1 : 0);
            mat.SetFloat("_InvertY", InvertY ? 1 : 0);
        }

        public Vector2Int GetRotatedGlyphCoordinates(int inputX, int inputY, int width, int height, Vector2Int atlasSize)
        {
            // rotate first, then invert
            int x = inputX;
            int y = atlasSize.y - inputY;
            int xDim = width;
            int yDim = height;

            if (Rotate)
            {
                x = inputY;
                y = inputX + width;
                xDim = height;
                yDim = width;
            }

            if (InvertX)
            {
                x = atlasSize.x - x - xDim;
            }

            if (InvertY)
            {
                y = atlasSize.x - y + yDim;
            }

            return new Vector2Int(x, y);
        }
    }
}
