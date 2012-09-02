#define USE_CLOSEST

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Forms;
using System.Drawing;
using System.Threading;

// http://blogs.msdn.com/b/lukeh/archive/2007/04/03/a-ray-tracer-in-c-3-0.aspx
namespace RayTracer
{
    public class RayTracer
    {
        private int screenWidth;
        private int screenHeight;
        private const int MaxDepth = 5;

        public Action<int, int, byte[]> setScanlines;

        public RayTracer(int screenWidth, int screenHeight, Action<int, int, byte[]> setScanlines)
        {
            this.screenWidth = screenWidth;
            this.screenHeight = screenHeight;
            this.setScanlines = setScanlines;
        }

        private IEnumerable<Intersection> Intersections(Ray ray, Scene scene)
        {
            return scene.Things
                        .Select(obj => obj.Intersect(ray))
                        .Where(inter => inter != null)
                        .OrderBy(inter => inter.Dist);
        }

        private Intersection ClosestIntersection(Ray ray, Scene scene)
        {
            /*SortedList<float, Intersection> intersections = new SortedList<float, Intersection>(scene.Things.Length);

            foreach (var thing in scene.Things)
            {
                Intersection intersection = thing.Intersect(ray);

                if (intersection != null)
                {
                    if (!intersections.ContainsKey(intersection.Dist))
                    {
                        intersections.Add(intersection.Dist, intersection);
                    }
                    // if last equal distance hit is needed.
                    //else
                    //{
                    //    intersections[intersection.Dist] = intersection;
                    //}
                }
            }

            if (intersections.Count > 0)
            {
                return intersections.First().Value;
            }

            return null;*/
            Intersection closest = new Intersection() { Dist = float.MaxValue, Thing = null, Ray = null };

            foreach (var thing in scene.Things)
            {
                Intersection intersection = thing.Intersect(ray);

                if (intersection != null && intersection.Dist < closest.Dist)
                {
                    closest = intersection;
                }
            }

            return closest.Thing == null ? null : closest;
        }

        private float TestRay(Ray ray, Scene scene)
        {
#if !USE_CLOSEST
            var isects = Intersections(ray, scene);
            Intersection isect = isects.FirstOrDefault();
            if (isect == null)
                return 0;
            return isect.Dist;
#else
            var i = ClosestIntersection(ray, scene);

            return i != null ? i.Dist : 0;
#endif
        }

        private Color TraceRay(Ray ray, Scene scene, int depth)
        {
#if !USE_CLOSEST
            var isects = Intersections(ray, scene);
            Intersection isect = isects.FirstOrDefault();
            if (isect == null)
                return Color.Background;
            return Shade(isect, scene, depth);
#else
            var i = ClosestIntersection(ray, scene);

            if (i == null)
            {
                return Color.Background;
            }

            return Shade(i, scene, depth);
#endif
        }

        private Color GetNaturalColor(SceneObject thing, Vector pos, Vector norm, Vector rd, Scene scene)
        {
            Color ret = new Color(Color.DefaultColor.R, Color.DefaultColor.G, Color.DefaultColor.B);
            Vector rdNormalized = Vector.Norm(rd);

            foreach (Light light in scene.Lights)
            {
                Vector ldis = Vector.Minus(light.Pos, pos);
                Vector livec = Vector.Norm(ldis);
                float neatIsect = TestRay(new Ray() { Start = pos, Dir = livec }, scene);
                bool isInShadow = !((neatIsect == 0) || (neatIsect > Vector.Mag(ldis)));
                if (!isInShadow)
                {
                    float illum = Vector.Dot(livec, norm);
                    float specular = Vector.Dot(livec, rdNormalized);

                    Color lcolor = illum > 0 ? Color.Times(illum, light.Color) : Color.Background;

                    Color scolor = specular > 0 ? Color.Times((float)Math.Pow(specular, thing.Surface.Roughness), light.Color) : Color.Background;

                    var diffuseSurfaceColor = thing.Surface.Diffuse(pos);
                    var specularSurfaceColor = thing.Surface.Specular(pos);

                    ret.R += diffuseSurfaceColor.R * lcolor.R + specularSurfaceColor.R * scolor.R;
                    ret.G += diffuseSurfaceColor.G * lcolor.G + specularSurfaceColor.G * scolor.G;
                    ret.B += diffuseSurfaceColor.B * lcolor.B + specularSurfaceColor.B * scolor.B;
                    
                }
            }
            return ret;
        }

        private Color GetReflectionColor(SceneObject thing, Vector pos, Vector norm, Vector rd, Scene scene, int depth)
        {
            return Color.Times(thing.Surface.Reflect(pos), TraceRay(new Ray() { Start = pos, Dir = rd }, scene, depth + 1));
        }

        private Color Shade(Intersection isect, Scene scene, int depth)
        {
            var d = isect.Ray.Dir;
            var pos = Vector.Plus(Vector.Times(isect.Dist, isect.Ray.Dir), isect.Ray.Start);
            var normal = isect.Thing.Normal(pos);
            var reflectDir = Vector.Minus(d, Vector.Times(2 * Vector.Dot(normal, d), normal));
            Color ret = Color.DefaultColor;
            ret = Color.Plus(ret, GetNaturalColor(isect.Thing, pos, normal, reflectDir, scene));
            if (depth >= MaxDepth)
            {
                return Color.Plus(ret, Color.Grey);
            }
            return Color.Plus(ret, GetReflectionColor(isect.Thing, Vector.Plus(pos, Vector.Times(.001f, reflectDir)), normal, reflectDir, scene, depth));
        }

        private float RecenterX(float x)
        {
            // TODO:    precalculate constant terms.
            return (x - (screenWidth / 2.0f)) / (2.0f * screenWidth);
        }
        private float RecenterY(float y)
        {
            // TODO:    precalculate constant terms.
            return -(y - (screenHeight / 2.0f)) / (2.0f * screenHeight);
        }

        private Vector GetPoint(float x, float y, Camera camera)
        {
            return Vector.Norm(Vector.Plus(camera.Forward, Vector.Plus(Vector.Times(RecenterX(x), camera.Right),
                                                                       Vector.Times(RecenterY(y), camera.Up))));
        }

        class ScanlineTask
        {
            private readonly int _width;
            private readonly byte[] _scanline;
            private readonly Func<int, int, Color> _trace;
            private readonly int _offset;

            public ScanlineTask(int width, int offset, byte[] scaneline, Func<int, int, Color> trace)
            {
                _trace = trace;
                _width = width;
                _scanline = scaneline;
                _offset = offset;
            }

            public Task Start(int y)
            {
                return Task.Factory.StartNew(() =>
                    {
                        for (int x = 0, i = _offset; x < _width; x++, i += 4)
                        {
                            Color color = _trace(x, y);

                            _scanline[i + 3] = 255;
                            _scanline[i + 0] = (byte)(color.B * 255);
                            _scanline[i + 1] = (byte)(color.G * 255);
                            _scanline[i + 2] = (byte)(color.R * 255);
                        }
                    });
            }

        }

        internal unsafe void Render(Scene scene)
        {
            /*for (int y = 0; y < screenHeight; y++)
            {
                byte[] scanline = new byte[screenWidth * 4];

                for (int x = 0; x < screenWidth; x++)
                {
                    Color color = TraceRay(new Ray() { Start = scene.Camera.Pos, Dir = GetPoint(x, y, scene.Camera) }, scene, 0);
                    int i = x * 4;
                    scanline[i + 3] = 255;
                    scanline[i + 0] = (byte)(color.B * 255);
                    scanline[i + 1] = (byte)(color.G * 255);
                    scanline[i + 2] = (byte)(color.R * 255);
                }

                setScanline(y, scanline);
            }*/

            // TODO:    use more here? need to be height % processorCount == 0.
            int processorCount = Environment.ProcessorCount;
            ScanlineTask[] scanLineTasks = new ScanlineTask[processorCount];
            Task[] tasks = new Task[processorCount];
            byte[] scanlines = new byte[processorCount * screenWidth * 4];
                
            for (int i = 0; i < processorCount; i++)
            {
                scanLineTasks[i] = new ScanlineTask(screenWidth, i * screenWidth * 4, scanlines, (x, y) => TraceRay(new Ray()
                {
                    Start = scene.Camera.Pos,
                    Dir = GetPoint(x, y, scene.Camera)
                }, scene, 0)
                );
            }

            for (int y = 0; y < screenHeight; y += processorCount)
            {
                for (int i = 0; i < processorCount; i++)
                {
                    tasks[i] = scanLineTasks[i].Start(y + i);
                }

                var doneTasks = Task.WhenAll(tasks);
                doneTasks.Wait();

                setScanlines(y, processorCount, scanlines);
            }
            
        }

        internal readonly Scene DefaultScene =
            new Scene()
            {
                Things = new SceneObject[] { 
                                new Plane() {
                                    Norm = Vector.Make(0,1,0),
                                    Offset = 0,
                                    Surface = Surfaces.CheckerBoard
                                },
                                new Sphere() {
                                    Center = Vector.Make(0,1,0),
                                    Radius = 1,
                                    Radius2 = 1,
                                    Surface = Surfaces.Shiny
                                },
                                new Sphere() {
                                    Center = Vector.Make(-1,.5,1.5),
                                    Radius = .5f,
                                    Radius2 = .5f * .5f,
                                    Surface = Surfaces.Shiny
                                }},
                Lights = new Light[] { 
                                new Light() {
                                    Pos = Vector.Make(-2,2.5,0),
                                    Color = Color.Make(.49,.07,.07)
                                },
                                new Light() {
                                    Pos = Vector.Make(1.5,2.5,1.5),
                                    Color = Color.Make(.07,.07,.49)
                                },
                                new Light() {
                                    Pos = Vector.Make(1.5,2.5,-1.5),
                                    Color = Color.Make(.07,.49,.071)
                                },
                                new Light() {
                                    Pos = Vector.Make(0,3.5,0),
                                    Color = Color.Make(.21,.21,.35)
                                }},
                Camera = Camera.Create(Vector.Make(3, 2, 4), Vector.Make(-1, .5, 0))
            };
    }

    public static class Surfaces
    {
        // Only works with X-Z plane.
        public static readonly Surface CheckerBoard =
            new Surface()
            {
                Diffuse = pos => ((Math.Floor(pos.Z) + Math.Floor(pos.X)) % 2 != 0)
                                    ? Color.White
                                    : Color.Black,
                Specular = pos => Color.White,
                Reflect = pos => ((Math.Floor(pos.Z) + Math.Floor(pos.X)) % 2 != 0)
                                    ? .1f
                                    : .7f,
                Roughness = 150
            };


        public static readonly Surface Shiny =
            new Surface()
            {
                Diffuse = pos => Color.White,
                Specular = pos => Color.Grey,
                Reflect = pos => .6f,
                Roughness = 50
            };
    }

    public partial class RayTracerForm : Form
    {
        Bitmap bitmap;
        PictureBox pictureBox;
        const int width = 600;
        const int height = 600;

        public RayTracerForm()
        {
            bitmap = new Bitmap(width, height);

            pictureBox = new PictureBox();
            pictureBox.Dock = DockStyle.Fill;
            pictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox.Image = bitmap;

            ClientSize = new System.Drawing.Size(width, height + 24);
            Controls.Add(pictureBox);
            Text = "Ray Tracer";
            Load += RayTracerForm_Load;

            Show();
        }

        private unsafe void RayTracerForm_Load(object sender, EventArgs e)
        {
            Stopwatch timer = Stopwatch.StartNew();

            this.Show();
            int updateFreq = (height / Environment.ProcessorCount) / 4;
            int updateCount = 0;
            
            RayTracer rayTracer = new RayTracer(width, height, (int y, int rows, byte[] scanlines) =>
            {
                var bits = bitmap.LockBits(new Rectangle(0, y, width, rows), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                
                System.Runtime.InteropServices.Marshal.Copy(scanlines, 0, bits.Scan0, rows * bits.Stride);

                bitmap.UnlockBits(bits);

                if (updateCount % updateFreq == 0)
                {
                    // TODO:    need to die! this is slow.
                    pictureBox.Refresh();
                }
                updateCount++;
            });
            rayTracer.Render(rayTracer.DefaultScene);
            pictureBox.Invalidate();

            timer.Stop();
            Console.WriteLine(timer.ElapsedMilliseconds + " ms");
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();

            Application.Run(new RayTracerForm());
        }
    }
}
