using System;
using System.Runtime.CompilerServices;

namespace RayTracer
{
    public class Vector
    {
        public float X;
        public float Y;
        public float Z;

        public Vector(float x, float y, float z) { X = x; Y = y; Z = z; }
        
        /*public Vector(string str)
        {
            string[] nums = str.Split(',');
            if (nums.Length != 3) throw new ArgumentException();
            X = double.Parse(nums[0]);
            Y = double.Parse(nums[1]);
            Z = double.Parse(nums[2]);
        }*/

        public static Vector Make(float x, float y, float z) 
        { 
            return new Vector(x, y, z); 
        }

        public static Vector Make(double x, double y, double z)
        {
            return new Vector((float)x, (float)y, (float)z);
        }

        public static Vector Times(float n, Vector v)
        {
            return new Vector(v.X * n, v.Y * n, v.Z * n);
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static Vector Minus(Vector v1, Vector v2)
        {
            return new Vector(v1.X - v2.X, v1.Y - v2.Y, v1.Z - v2.Z);
        }
        public static Vector Plus(Vector v1, Vector v2)
        {
            return new Vector(v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z);
        }
        public static float Dot(Vector v1, Vector v2)
        {
            return (v1.X * v2.X) + (v1.Y * v2.Y) + (v1.Z * v2.Z);
        }
        public static float Mag(Vector v) 
        { 
            return SceneObject.Sqrt(Dot(v, v)); 
        }

        public static Vector Norm(Vector v)
        {
            /*float mag = Mag(v);
            float div = mag == 0 ? float.PositiveInfinity : 1 / mag;
            return Times(div, v);*/
            float sqrLength, invLength;

	        sqrLength = v.X * v.X + v.Y * v.Y + v.Z * v.Z;
	        invLength = SceneObject.InvSqrt(sqrLength);
	        
            return new Vector(v.X * invLength, v.Y * invLength, v.Z * invLength);
        }

        public static Vector Cross(Vector v1, Vector v2)
        {
            return new Vector(((v1.Y * v2.Z) - (v1.Z * v2.Y)),
                              ((v1.Z * v2.X) - (v1.X * v2.Z)),
                              ((v1.X * v2.Y) - (v1.Y * v2.X)));
        }

        public static bool Equals(Vector v1, Vector v2)
        {
            return (v1.X == v2.X) && (v1.Y == v2.Y) && (v1.Z == v2.Z);
        }
    }
}