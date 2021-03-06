﻿using SharpGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Neuro.Tensors;

namespace DeepQL.Misc
{
    public static class Rendering
    {
        // Implementation based upon https://github.com/openai/gym/blob/master/gym/envs/classic_control/rendering.py
        public class Viewer : Form
        {
            public Viewer(int width, int height)
            {
                ClientSize = new Size(width, height);
                ((System.ComponentModel.ISupportInitialize)(OpenGLControl)).BeginInit();
                OpenGLControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                OpenGLControl.DrawFPS = false;
                OpenGLControl.FrameRate = 28;
                OpenGLControl.Location = new System.Drawing.Point(0, 0);
                OpenGLControl.Name = "OpenGL";
                OpenGLControl.RenderContextType = SharpGL.RenderContextType.FBO;
                OpenGLControl.RenderTrigger = RenderTrigger.Manual;
                OpenGLControl.Size = new System.Drawing.Size(width, height);
                OpenGLControl.TabIndex = 0;
                OpenGLControl.OpenGLDraw += new RenderEventHandler(OpenGLDrawFunc);
                OpenGLControl.OpenGL.Enable(OpenGL.GL_BLEND);
                OpenGLControl.OpenGL.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
                OpenGLControl.OpenGL.Viewport(0, 0, Width, Height);
                Controls.Add(OpenGLControl);
                ((System.ComponentModel.ISupportInitialize)(OpenGLControl)).EndInit();

                Name = Text = "Viewer";
                FormBorderStyle = FormBorderStyle.FixedSingle;


                Show();
            }

            public void SetBounds(float left, float right, float bottom, float top)
            {
                float scaleX = Width / (right - left);
                float scaleY = Height / (top - bottom);

                Trans.SetTranslation(-left * scaleX, -bottom * scaleY);
                Trans.SetScale(scaleX, scaleY);
            }

            public void AddGeom(Geom geom)
            {
                Geoms.Add(geom);
            }

            public void AddOneTime(Geom geom)
            {
                OneTimeGeoms.Add(geom);
            }

            public void Render(byte[] outRgbArray = null)
            {
                OutputRrbArray = outRgbArray;
                OpenGLControl.DoRender();
                Application.DoEvents();
            }

            private void OpenGLDrawFunc(object sender, RenderEventArgs e)
            {
                OpenGL gl = OpenGLControl.OpenGL;
                gl.ClearColor(1, 1, 1, 1);

                gl.MatrixMode(OpenGL.GL_PROJECTION);
                gl.LoadIdentity();
                gl.Ortho(0, Width, 0, Height, - 10, 10);
                gl.MatrixMode(OpenGL.GL_MODELVIEW);
                gl.LoadIdentity();

                gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);

                Trans.Enable(gl);
                foreach (var geom in Geoms)
                    geom.Render(gl);
                foreach (var geom in OneTimeGeoms)
                    geom.Render(gl);
                Trans.Disable(gl);

                OneTimeGeoms.Clear();

                if (OutputRrbArray != null)
                    gl.ReadPixels(0, 0, Width, Height, OpenGL.GL_RGB, OpenGL.GL_UNSIGNED_BYTE, OutputRrbArray);
            }

            public Geom DrawCircle(float radius = 10.0f, int res = 30, bool filled = true, List<Attr> attrs = null)
            {
                var geom = MakeCircle(radius, res, filled);
                AddAttrs(geom, attrs);
                AddOneTime(geom);
                return geom;
            }

            public Geom DrawPolygon(List<float[]> vertices, bool filled = true, List<Attr> attrs = null)
            {
                var geom = MakePolygon(vertices, filled);
                AddAttrs(geom, attrs);
                AddOneTime(geom);
                return geom;
            }

            public Geom DrawPolyline(List<float[]> vertices, List<Attr> attrs = null)
            {
                var geom = MakePolyLine(vertices);
                AddAttrs(geom, attrs);
                AddOneTime(geom);
                return geom;
            }


            public Geom DrawLine(float[] start, float[] end, List<Attr> attrs = null)
            {
                var geom = new Line(start, end);
                AddAttrs(geom, attrs);
                AddOneTime(geom);
                return geom;
            }

            //public Tensor GetArray()
            //{
            //    float[] r = new Tensor(new Shape(Width, Height));
            //    OpenGLControl.OpenGL.ReadPixels(0, 0, Width, Height, OpenGL.GL_R, OpenGL.GL_UNSIGNED_BYTE, r.get);
            //    self.window.flip()
            //    image_data = pyglet.image.get_buffer_manager().get_color_buffer().get_image_data()
            //    self.window.flip()
            //    arr = np.fromstring(image_data.data, dtype = np.uint8, sep = '')
            //    arr = arr.reshape(self.height, self.width, 4)
            //    return arr[::-1, :, 0:3]
            //}

            protected override void Dispose(bool disposing)
            {
                OpenGLControl.Dispose();
                base.Dispose(disposing);
            }

            private byte[] OutputRrbArray;
            private readonly OpenGLControl OpenGLControl = new OpenGLControl();
            private readonly List<Geom> Geoms = new List<Geom>();
            private readonly List<Geom> OneTimeGeoms = new List<Geom>();
            private readonly Transform Trans = new Transform();
        }

        public static void AddAttrs(Geom geom, List<Attr> attrs)
        {
            if (attrs == null)
                return;

            foreach (var attr in attrs)
            {
                if (attr is Color)
                {
                    Color color = attr as Color;
                    geom.SetColor(color.Vec4[0], color.Vec4[1], color.Vec4[2]);
                }
                else if (attr is LineWidth)
                {
                    LineWidth lineWidth = attr as LineWidth;
                    geom.SetLineWidth(lineWidth.Stroke);
                }
            }
        }

        public abstract class Geom
        {
            protected Geom()
            {
                _Color = new Color(new float[] {0, 0, 0, 1});
                AddAttr(_Color);
                _LineWidth = new LineWidth(1);
                AddAttr(_LineWidth);
            }

            public void Render(OpenGL gl)
            {
                for (int i = Attrs.Count - 1; i >= 0; --i)
                    Attrs[i].Enable(gl);
                OnRender(gl);
                for (int i = Attrs.Count - 1; i >= 0; --i)
                    Attrs[i].Disable(gl);
            }

            protected abstract void OnRender(OpenGL gl);

            public Geom AddAttr(Attr attr)
            {
                Attrs.Add(attr);
                return this;
            }

            public Geom SetColor(float r, float g, float b)
            {
                _Color.Vec4 = new [] {r, g, b, 1};
                return this;
            }

            public Geom SetLineWidth(int stroke)
            {
                _LineWidth.Stroke = stroke;
                return this;
            }

            public readonly List<Attr> Attrs = new List<Attr>();
            protected readonly Color _Color;
            protected readonly LineWidth _LineWidth;
        }

        public abstract class Attr
        {
            public abstract void Enable(OpenGL gl);
            public virtual void Disable(OpenGL gl) { }
        }

        public class Transform : Attr
        {
            public Transform(float[] translation = null, float rotation = 0, float[] scale = null)
            {
                if (translation != null) SetTranslation(translation[0], translation[1]);
                SetRotation(rotation);
                if (scale != null) SetScale(scale[0], scale[1]);
            }

            public override void Enable(OpenGL gl)
            {
                gl.PushMatrix();
                gl.Translate(Translation[0], Translation[1], 0);
                gl.Rotate(RAD2DEG * Rotation, 0, 0, 1.0);
                gl.Scale(Scale[0], Scale[1], 1);
            }

            public override void Disable(OpenGL gl)
            {
                gl.PopMatrix();
            }

            public void SetTranslation(float newX, float newY)
            {
                Translation[0] = newX;
                Translation[1] = newY;
            }

            public void SetRotation(float rot)
            {
                Rotation = rot;
            }

            public void SetScale(float newX, float newY)
            {
                Scale[0] = newX;
                Scale[1] = newY;
            }

            private float[] Translation = new float[2];
            private float Rotation;
            private float[] Scale = new float[2] {1, 1};

            private const float RAD2DEG = 57.29577951308232f;
        }

        public class Color : Attr
        {
            public Color(float[] vec4)
            {
                Vec4 = (float[]) vec4.Clone();
            }

            public override void Enable(OpenGL gl)
            {
                gl.Color(Vec4);
            }

            public float[] Vec4;
        }

        public class LineStyle : Attr
        {
            public LineStyle(ushort style)
            {
                Style = style;
            }

            public override void Enable(OpenGL gl)
            {
                gl.Enable(OpenGL.GL_LINE_STIPPLE);
                gl.LineStipple(1, Style);
            }

            public override void Disable(OpenGL gl)
            {
                gl.Disable(OpenGL.GL_LINE_STIPPLE);
            }

            public ushort Style;
        }

        public class LineWidth : Attr
        {
            public LineWidth(int stroke)
            {
                Stroke = stroke;
            }

            public override void Enable(OpenGL gl)
            {
                gl.LineWidth(Stroke);
            }

            public int Stroke;
        }
    
        public class Point : Geom
        {
            protected override void OnRender(OpenGL gl)
            {
                gl.Begin(OpenGL.GL_POINTS);
                gl.Vertex(0.0, 0.0, 0.0);
                gl.End();
            }
        }

        public class FilledPolygon : Geom
        {
            public FilledPolygon(List<float[]> vertices)
            {
                Vertices = new List<float[]>(vertices);
            }

            protected override void OnRender(OpenGL gl)
            {
                if (Vertices.Count == 4)
                    gl.Begin(OpenGL.GL_QUADS);
                else if (Vertices.Count > 4)
                    gl.Begin(OpenGL.GL_POLYGON);
                else
                    gl.Begin(OpenGL.GL_TRIANGLES);

                foreach (var p in Vertices)
                    gl.Vertex(p[0], p[1], 0); // draw each vertex

                gl.End();
            }

            public List<float[]> Vertices;
        }

        public class Image : Geom
        {
            public Image(string fname, int width, int height)
            {
                Width = width;
                Height = height;
            }

            public Image(byte[] pixels, int width, int height)
            {
                Width = width;
                Height = height;
                Pixels = (byte[])pixels.Clone();
            }

            protected override void OnRender(OpenGL gl)
            {
                // add support foir grayscale Pixels.Length == Width * Height ? OpenGL.GL_gr
                gl.DrawPixels(Width, Height, OpenGL.GL_RGB, Pixels);
            }

            private int Width;
            private int Height;
            public byte[] Pixels;
        }

        public class Line : Geom
        {
            public Line(float[] start, float[] end)
            {
                Start = (float[]) start.Clone();
                End = (float[]) end.Clone();
            }

            protected override void OnRender(OpenGL gl)
            {
                gl.Begin(OpenGL.GL_LINES);
                gl.Vertex(Start[0], Start[1], 0);
                gl.Vertex(End[0], End[1], 0);
                gl.End();
            }

            public float[] Start;
            public float[] End;
        }

        public class Polyline : Geom
        {
            public Polyline(List<float[]> vertices, bool close)
            {
                Vertices = new List<float[]>(vertices);
                Close = close;
            }

            protected override void OnRender(OpenGL gl)
            {
                gl.Begin(Close ? OpenGL.GL_LINE_LOOP : OpenGL.GL_LINE_STRIP);
                foreach (var p in Vertices)
                    gl.Vertex(p[0], p[1], 0); // draw each vertex
                gl.End();
            }

            public List<float[]> Vertices;
            public bool Close;
        }

        public class Compound : Geom
        {
            public Compound(Geom[] geoms)
            {
                Geoms = geoms;
                foreach (var geom in geoms)
                    geom.Attrs.RemoveAll(attr => attr is Color);
            }

            protected override void OnRender(OpenGL gl)
            {
                foreach (var geom in Geoms)
                    geom.Render(gl);
            }

            private readonly Geom[] Geoms;
        }

        public static Geom MakeCircle(float radius = 10, int res = 30, bool filled = true)
        {
            List<float[]> points = new List<float[]>();
            for (int i = 0; i < res; ++i)
            {
                float ang = 2 * (float)Math.PI * i / res;
                points.Add(new[] { (float)Math.Cos(ang) * radius, (float)Math.Sin(ang) * radius});
            }

            if (filled)
                return new FilledPolygon(points);
            return new Polyline(points, true);
        }

        public static Geom MakePolygon(List<float[]> v, bool filled = true)
        {
            if (filled)
                return new FilledPolygon(v);
            return new Polyline(v, true);
        }

        public static Geom MakePolyLine(List<float[]> v)
        {
            return new Polyline(v, false);
        }

        public static Geom MakeCapsule(float length, float width)
        {
            float l = 0, r = length, t = width / 2, b = -width / 2;
            var box = MakePolygon(new List<float[]> {new[] {l, b}, new[] {l, t}, new[] {r, t}, new[] {r, b}});
            var circ0 = MakeCircle(width / 2);
            var circ1 = MakeCircle(width / 2);
            circ1.AddAttr(new Transform(new[] {length, 0}));
            return new Compound(new[] {box, circ0, circ1});
        }
    }
}
