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
    public unsafe class RayTracer
    {
        private int screenWidth;
        private float screenWidth2;
        private float screenWidthHalf;

        private int screenHeight;
        private float screenHeight2;
        private float screenHeightHalf;

        private const int MaxDepth = 5;

        private byte* scanlines;

        public RayTracer(int screenWidth, int screenHeight, byte *scanlines)
        {
            this.screenWidth = screenWidth;
            this.screenWidth2 = screenWidth * 2.0f;
            this.screenWidthHalf = screenWidth / 2.0f;

            this.screenHeight = screenHeight;
            this.screenHeight2 = screenHeight * 2.0f;
            this.screenHeightHalf = screenHeight / 2.0f;

            this.scanlines = scanlines;
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
            Intersection closest = new Intersection() { Dist = float.MaxValue, Thing = null, Ray = null };

            for (int i = 0; i < scene.Things.Length; i++)
            {
                var thing = scene.Things[i];

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
            float d = float.MaxValue;

            for (int i = 0; i < scene.Things.Length; i++)
            {
                var thing = scene.Things[i];

                float intersectionDistance = thing.IntersectDistance(ray);

                if (intersectionDistance < d)
                {
                    d = intersectionDistance;
                }
            }

            return d == float.MaxValue ? 0 : d;
        }

        private Color TraceRay(Ray ray, Scene scene, int depth)
        {
            var i = ClosestIntersection(ray, scene);

            if (i == null)
            {
                return Color.Background;
            }

            return Shade(i, scene, depth);
        }

        private Color GetNaturalColor(SceneObject thing, Vector pos, Vector norm, Vector rd, Scene scene)
        {
            Color ret = new Color(Color.DefaultColor.R, Color.DefaultColor.G, Color.DefaultColor.B);
            Vector rdNormalized = Vector.Norm(rd);

            for (int i = 0; i < scene.Lights.Length; i++)
            {
                Light light = scene.Lights[i];

                float vx = light.Pos.X - pos.X;
                float vy = light.Pos.Y - pos.Y;
                float vz = light.Pos.Z - pos.Z;

                float sqrLength, invLength;

                sqrLength = vx * vx + vy * vy + vz * vz;
                invLength = SceneObject.InvSqrt(sqrLength);

                Vector livec = new Vector(vx * invLength, vy * invLength, vz * invLength);

                float neatIsect = TestRay(new Ray() { Start = pos, Dir = livec }, scene);

                bool isInShadow = !((neatIsect == 0) || (neatIsect > vx * vx + vy * vy + vz * vz));

                if (!isInShadow)
                {
                    float illum = livec.X * norm.X + livec.Y * norm.Y + livec.Z * norm.Z;
                    float specular = livec.X * rdNormalized.X + livec.Y * rdNormalized.Y + livec.Z * rdNormalized.Z;

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
            var pos = new Vector(
                                    isect.Dist * isect.Ray.Dir.X + isect.Ray.Start.X,
                                    isect.Dist * isect.Ray.Dir.Y + isect.Ray.Start.Y,
                                    isect.Dist * isect.Ray.Dir.Z + isect.Ray.Start.Z
                                );
            var normal = isect.Thing.Normal(pos);
            var reflectDir = Vector.Minus(d, Vector.Times(2 * Vector.Dot(normal, d), normal));
            // TODO:    whats wrong with this?
            /*var reflectDir = new Vector(
                                        d.X - (2.0f * normal.X * d.X * normal.X),
                                        d.Y - (2.0f * normal.Y * d.Y * normal.Y),
                                        d.Z - (2.0f * normal.Z * d.Z * normal.Z)
                                        );*/

            Color natColor = GetNaturalColor(isect.Thing, pos, normal, reflectDir, scene);
            Color ret = new Color(Color.DefaultColor.R + natColor.R,Color.DefaultColor.R + natColor.G, Color.DefaultColor.R + natColor.B);

            if (depth >= MaxDepth)
            {
                return new Color(ret.R + Color.Grey.R, ret.G + Color.Grey.G, ret.B + Color.Grey.B);
            }

            Color refColor = GetReflectionColor(isect.Thing, new Vector(pos.X + .001f * reflectDir.X, pos.Y + .001f * reflectDir.Y, pos.Z + .001f * reflectDir.Z), normal, reflectDir, scene, depth);

            return new Color(ret.R + refColor.R, ret.G + refColor.G, ret.B + refColor.B);
        }

        private float RecenterX(float x)
        {
            return (x - screenWidthHalf) / screenWidth2;
        }
        private float RecenterY(float y)
        {
            return -(y - screenHeightHalf) / screenHeight2;
        }

        private Vector GetPoint(float x, float y, Camera camera)
        {
            float rx = RecenterX(x);
            float ry = RecenterY(y);
            float vx = camera.Forward.X + (rx * camera.Right.X + ry * camera.Up.X);
            float vy = camera.Forward.Y + (rx * camera.Right.Y + ry * camera.Up.Y);
            float vz = camera.Forward.Z + (rx * camera.Right.Z + ry * camera.Up.Z);

            float sqrLength, invLength;

            sqrLength = vx * vx + vy * vy + vz * vz;
            invLength = SceneObject.InvSqrt(sqrLength);

            return new Vector(vx * invLength, vy * invLength, vz * invLength);
        }

        unsafe class ScanlineTask
        {
            private readonly int _width;
            private readonly byte* _scanlines;
            private readonly Func<int, int, Color> _trace;
            private readonly int _numRows;
            private readonly int _pitch;
            private readonly CountdownEvent _countEvent;
            private readonly Thread _thread;
            private int _y;
            private bool _skipThread;

            public ScanlineTask(bool skipThread, int width, int numRows, int y, byte* scanelines, CountdownEvent countEvent, Func<int, int, Color> trace)
            {
                _trace = trace;
                _width = width;
                _scanlines = scanelines;
                _numRows = numRows;
                _pitch = _width * 4;
                _countEvent = countEvent;
                _y = y;

                _skipThread = skipThread;

                if (!_skipThread)
                {
                    _thread = new Thread(Render);
                    _thread.IsBackground = false;
                    _thread.Priority = ThreadPriority.Highest;
                }
            }

            private void Render()
            {
                byte* ptr = _scanlines;

                for (int j = 0; j < _numRows; j++, _y++)
                {
                    for (int x = 0; x < _width; x++, ptr += 4)
                    {
                        Color color = _trace(x, _y);

                        ptr[0] = (byte)(color.B * 255);
                        ptr[1] = (byte)(color.G * 255);
                        ptr[2] = (byte)(color.R * 255);
                        ptr[3] = 255;
                    }
                }

                _countEvent.Signal();
            }

            public void Start()
            {
                if (!_skipThread)
                {
                    _thread.Start();
                }
                else
                {
                    Render();
                }
            }

        }

        internal unsafe void Render(Scene scene)
        {
            // TODO:    use more here? need to be height % processorCount == 0.
            //int processorCount = Environment.ProcessorCount;
            int processorCount = 12;
            ScanlineTask[] scanLineTasks = new ScanlineTask[processorCount];
            int numRowsPerTask = screenHeight / processorCount;
            CountdownEvent countEvent = new CountdownEvent(processorCount);
            int pitch = numRowsPerTask * screenWidth * 4;
            int currentY = 0;
            int offset = 0;

            for (int i = 0; i < processorCount; i++)
            {
                scanLineTasks[i] = new ScanlineTask(i == processorCount - 1, screenWidth, numRowsPerTask, currentY, scanlines + offset, countEvent, (x, y) => TraceRay(new Ray()
                {
                    Start = scene.Camera.Pos,
                    Dir = GetPoint(x, y, scene.Camera)
                }, scene, 0)
                );

                currentY += numRowsPerTask;
                offset += pitch;
            }

            for (int i = 0; i < processorCount; i++)
            {
                scanLineTasks[i].Start();
            }

            //Stopwatch t = Stopwatch.StartNew();
            countEvent.Wait();
            //t.Stop();
            //Console.WriteLine("wait " + t.ElapsedMilliseconds);
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
            var bits = bitmap.LockBits(new Rectangle(0, 0, width, height), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            RayTracer rayTracer = new RayTracer(width, height, (byte *)bits.Scan0);

            rayTracer.Render(rayTracer.DefaultScene);
            bitmap.UnlockBits(bits);

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
