using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RayTracer
{
    [StructLayout(LayoutKind.Explicit)]
    struct _flint
    {
        [FieldOffset(0)]
        public int i;

        [FieldOffset(0)]
        public float f;
    }

    public abstract class SceneObject
    {
        public Surface Surface;
        public abstract Intersection Intersect(Ray ray);
        public abstract Vector Normal(Vector pos);

        /*enum {
		LOOKUP_BITS				= 8,							
		EXP_POS					= 23,							
		EXP_BIAS				= 127,							
		LOOKUP_POS				= (EXP_POS-LOOKUP_BITS),
		SEED_POS				= (EXP_POS-8),
		SQRT_TABLE_SIZE			= (2<<LOOKUP_BITS),
		LOOKUP_MASK				= (SQRT_TABLE_SIZE-1)
	};

	union _flint {
		dword					i;
		float					f;
	};

	static dword				iSqrt[SQRT_TABLE_SIZE];*/
        private const int LOOKUP_BITS = 8;
        private const int EXP_POS = 23;
        private const int EXP_BIAS = 127;
        private const int LOOKUP_POS = (EXP_POS - LOOKUP_BITS);
        private const int SEED_POS = (EXP_POS - 8);
        private const int SQRT_TABLE_SIZE = (2 << LOOKUP_BITS);
        private const int LOOKUP_MASK = (SQRT_TABLE_SIZE - 1);

        private static int[] iSqrt = new int[SQRT_TABLE_SIZE];

        public static unsafe float InvSqrt(float x)
        {
            int a = ((_flint*)(&x))->i;
	        _flint seed;

            // TODO:    can "not initilized" error for f be avoided?
            //          can get away with assigment if so.
            seed.f = 0;

	        double y = x * 0.5f;
	        seed.i = (( ( (3*EXP_BIAS-1) - ( (a >> EXP_POS) & 0xFF) ) >> 1)<<EXP_POS) | iSqrt[(a >> (EXP_POS-LOOKUP_BITS)) & LOOKUP_MASK];
	        double r = seed.f;
	        r = r * ( 1.5f - r * r * y );
	        r = r * ( 1.5f - r * r * y );
	        return (float) r;
        }

        public static float Sqrt(float x)
        {
            return x * InvSqrt(x);
        }

        static SceneObject()
        {
            _flint fi, fo;

            // TODO:    can "not initilized" error for f/i be avoided?
            //          can get away with assigment if so.
            fi.f = 0;
            fo.i = 0;

            for (int i = 0; i < SQRT_TABLE_SIZE; i++) 
            {
                fi.i	 = ((EXP_BIAS-1) << EXP_POS) | (i << LOOKUP_POS);
                fo.f	 = (float)( 1.0 / Math.Sqrt( fi.f ) );
                iSqrt[i] = ((int)(((fo.i + (1<<(SEED_POS-2))) >> SEED_POS) & 0xFF))<<SEED_POS;
            }
    
	        iSqrt[SQRT_TABLE_SIZE / 2] = ((int)(0xFF))<<(SEED_POS); 
        }
    }

    public class Sphere : SceneObject
    {
        public Vector Center;
        public float Radius;
        public float Radius2;

        public override Intersection Intersect(Ray ray)
        {
            float eox = Center.X - ray.Start.X;
            float eoy = Center.Y - ray.Start.Y;
            float eoz = Center.Z - ray.Start.Z;
            float v = eox * ray.Dir.X + eoy * ray.Dir.Y + eoz * ray.Dir.Z;
            
            if (v < 0)
            {
                return null;
            }

            float disc = Radius2 - ((eox * eox + eoy * eoy + eoz * eoz) - v * v);

            if (disc < 0)
            {
                return null;
            }

            float dist = (v - Sqrt(disc));

            return new Intersection()
            {
                Thing = this,
                Ray = ray,
                Dist = dist
            };
        }

        public override Vector Normal(Vector pos)
        {
            float vx = pos.X - Center.X;
            float vy = pos.Y - Center.Y;
            float vz = pos.Z - Center.Z;
            float sqrLength, invLength;

            sqrLength = vx * vx + vy * vy + vz * vz;
            invLength = SceneObject.InvSqrt(sqrLength);

            return new Vector(vx * invLength, vy * invLength, vz * invLength);
        }
    }

    public class Plane : SceneObject
    {
        public Vector Norm;
        public float Offset;

        public override Intersection Intersect(Ray ray)
        {
            float denom = Norm.X * ray.Dir.X + Norm.Y * ray.Dir.Y + Norm.Z * ray.Dir.Z;
            if (denom > 0) return null;

            return new Intersection()
            {
                Thing = this,
                Ray = ray,
                Dist = (Norm.X * ray.Start.X + Norm.Y * ray.Start.Y + Norm.Z * ray.Start.Z + Offset) / (-denom)
            };
        }

        public override Vector Normal(Vector pos)
        {
            return Norm;
        }
    }
}
