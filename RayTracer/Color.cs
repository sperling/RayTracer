using System;

namespace RayTracer
{
    public class Color
    {
        public float R;
        public float G;
        public float B;

        public Color(float r, float g, float b) { R = r; G = g; B = b; }
        
        /*public Color(string str)
        {
            string[] nums = str.Split(',');
            if (nums.Length != 3) throw new ArgumentException();
            R = double.Parse(nums[0]);
            G = double.Parse(nums[1]);
            B = double.Parse(nums[2]);
        }*/

        public static Color Make(float r, float g, float b) 
        { 
            return new Color(r, g, b); 
        }

        public static Color Make(double r, double g, double b)
        {
            return new Color((float)r, (float)g, (float)b);
        }

        public static Color Times(float n, Color v)
        {
            return new Color(n * v.R, n * v.G, n * v.B);
        }
        public static Color Times(Color v1, Color v2)
        {
            return new Color(v1.R * v2.R, v1.G * v2.G, v1.B * v2.B);
        }

        public static Color Plus(Color v1, Color v2)
        {
            return new Color(v1.R + v2.R, v1.G + v2.G, v1.B + v2.B);
        }
        public static Color Minus(Color v1, Color v2)
        {
            return new Color(v1.R - v2.R, v1.G - v2.G, v1.B - v2.B);
        }

        public static readonly Color Background = Make(0, 0, 0);
        public static readonly Color DefaultColor = Make(0, 0, 0);
        public static readonly Color Black = Make(0, 0, 0);
        public static readonly Color White = Make(1, 1, 1);
        public static readonly Color Grey = Make(0.5, 0.5, 0.5);

        public float Legalize(float d)
        {
            return d > 1 ? 1 : d;
        }

        internal System.Drawing.Color ToDrawingColor()
        {
            return System.Drawing.Color.FromArgb((int)(Legalize(R) * 255), (int)(Legalize(G) * 255), (int)(Legalize(B) * 255));
        }

    }
}
