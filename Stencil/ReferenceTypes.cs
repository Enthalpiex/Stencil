using System;

namespace Stencil
{
    public enum ReferenceModelFormat
    {
        Obj,
        Fbx,
        Dae,
        Stl,
        Unknown
    }

    public enum ReferenceDisplayMode
    {
        OuterShell,
        Mixed
    }

    public enum ReferenceSliceMode
    {
        All,
        OneSide,
        Between
    }

    public enum ReferenceSliceSide
    {
        Positive,
        Negative
    }

    public struct Float4
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        public readonly float W;

        public Float4(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public static Float4 White
        {
            get { return new Float4(1f, 1f, 1f, 1f); }
        }
    }

    public struct Float3
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public Float3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Float3 One
        {
            get { return new Float3(1f, 1f, 1f); }
        }

        public static Float3 Zero
        {
            get { return new Float3(0f, 0f, 0f); }
        }
    }

    public struct FloatRect
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Width;
        public readonly float Height;

        public FloatRect(float x, float y, float width, float height)
        {
            if (width <= 0f)
            {
                throw new ArgumentOutOfRangeException("width", "Width must be > 0.");
            }

            if (height <= 0f)
            {
                throw new ArgumentOutOfRangeException("height", "Height must be > 0.");
            }

            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }
}