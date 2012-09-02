using System;

namespace RayTracer
{
    public class Surface
    {
        public Func<Vector, Color> Diffuse;
        public Func<Vector, Color> Specular;
        public Func<Vector, float> Reflect;
        public float Roughness;
    }
}
