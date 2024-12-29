using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace DeepBridgeWindowsApp.Dicom
{
    public class ProcessingProgress
    {
        public string CurrentStep { get; set; }
        public int CurrentValue { get; set; }
        public int TotalValue { get; set; }
        public float Percentage => (float)CurrentValue / TotalValue * 100;
    }

    public class Dicom3D
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> colors = new List<Vector3>();
        private readonly List<int> indices = new List<int>();
        private int[] vertexBufferObject;
        private int[] colorBufferObject;
        private int[] elementBufferObject;
        private int vertexArrayObject;
        private readonly Action<ProcessingProgress> progressCallback;
        private readonly object lockObject = new object();

        public Dicom3D(DicomDisplayManager ddm, Action<ProcessingProgress> progressCallback = null)
        {
            this.progressCallback = progressCallback;
            ProcessSlices(ddm);
        }

        private void ProcessSlices(DicomDisplayManager ddm)
        {
            float sliceThickness = (float)ddm.GetCurrentSlice(0).SliceThickness;
            float zScale = sliceThickness * 0.25f;

            // Calculate exact capacity needed
            var firstSlice = ddm.GetCurrentSliceImage();
            long totalPixels = (long)firstSlice.Width * firstSlice.Height * ddm.GetTotalSlices();
            firstSlice.Dispose();

            // Create lists with chunks instead of one giant list
            var vertexChunks = new List<List<Vector3>>();
            var colorChunks = new List<List<Vector3>>();
            var indexChunks = new List<List<int>>();

            var completedSlices = 0;
            var progress = new ProcessingProgress
            {
                TotalValue = ddm.GetTotalSlices(),
                CurrentValue = 0,
                CurrentStep = "Processing DICOM slices"
            };

            Parallel.For(0, ddm.GetTotalSlices(), new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, z =>
            {
                var localVertices = new List<Vector3>();
                var localColors = new List<Vector3>();
                var localIndices = new List<int>();

                ddm.SetSliceIndex(z);
                var slice = ddm.GetCurrentSliceImage();

                BitmapData bitmapData = slice.LockBits(
                    new Rectangle(0, 0, slice.Width, slice.Height),
                    ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                unsafe
                {
                    byte* ptr = (byte*)bitmapData.Scan0;

                    for (int y = 0; y < slice.Height; y++)
                    {
                        for (int x = 0; x < slice.Width; x++)
                        {
                            int offset = y * bitmapData.Stride + x * 4;
                            byte b = ptr[offset];
                            byte g = ptr[offset + 1];
                            byte r = ptr[offset + 2];

                            float intensity = (r * 0.299f + g * 0.587f + b * 0.114f) / 255f;

                            if (intensity > 0.15f)
                            {
                                localVertices.Add(new Vector3(
                                    (x / (float)slice.Width) - 0.5f,
                                    (y / (float)slice.Height) - 0.5f,
                                    ((z / ((float)ddm.GetTotalSlices()) / zScale)) - 0.5f
                                ));

                                localColors.Add(new Vector3(intensity, intensity, intensity));
                                localIndices.Add(localVertices.Count - 1);
                            }
                        }
                    }
                }

                slice.UnlockBits(bitmapData);

                lock (lockObject)
                {
                    int baseIndex = vertices.Count;
                    vertices.AddRange(localVertices);
                    colors.AddRange(localColors);
                    indices.AddRange(localIndices.Select(i => i + baseIndex));
                }

                slice.Dispose();
                Interlocked.Increment(ref completedSlices);
                progress.CurrentValue = completedSlices;
                progressCallback?.Invoke(progress);
            });
        }

        public void InitializeGL()
        {
            vertexBufferObject = new int[1];
            colorBufferObject = new int[1];
            elementBufferObject = new int[1];
            vertexArrayObject = GL.GenVertexArray();
            GL.GenBuffers(1, vertexBufferObject);
            GL.GenBuffers(1, colorBufferObject);
            GL.GenBuffers(1, elementBufferObject);

            GL.BindVertexArray(vertexArrayObject);

            GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferObject[0]);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Count * Vector3.SizeInBytes, vertices.ToArray(), BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, Vector3.SizeInBytes, 0);
            GL.EnableVertexAttribArray(0);

            GL.BindBuffer(BufferTarget.ArrayBuffer, colorBufferObject[0]);
            GL.BufferData(BufferTarget.ArrayBuffer, colors.Count * Vector3.SizeInBytes, colors.ToArray(), BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, Vector3.SizeInBytes, 0);
            GL.EnableVertexAttribArray(1);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, elementBufferObject[0]);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Count * sizeof(int), indices.ToArray(), BufferUsageHint.StaticDraw);
        }

        public void Render(int shader, Matrix4 model, Matrix4 view, Matrix4 projection)
        {
            GL.UseProgram(shader);

            GL.UniformMatrix4(GL.GetUniformLocation(shader, "model"), false, ref model);
            GL.UniformMatrix4(GL.GetUniformLocation(shader, "view"), false, ref view);
            GL.UniformMatrix4(GL.GetUniformLocation(shader, "projection"), false, ref projection);

            GL.PointSize(2.0f);
            GL.BindVertexArray(vertexArrayObject);
            GL.DrawElements(PrimitiveType.Points, indices.Count, DrawElementsType.UnsignedInt, 0);
        }

        public void Dispose()
        {
            if (vertexBufferObject != null)
                GL.DeleteBuffer(vertexBufferObject[0]);
            if (colorBufferObject != null)
                GL.DeleteBuffer(colorBufferObject[0]);
            if (elementBufferObject != null)
                GL.DeleteBuffer(elementBufferObject[0]);
            GL.DeleteVertexArray(vertexArrayObject);
        }
    }
}
