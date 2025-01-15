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
        private int frontClip = 0;
        private int backClip = 0;
        private int totalSlices;
        private int currentVisibleIndices;

        // Slicing
        private readonly Dictionary<Vector3, Vector3> pointColors = new Dictionary<Vector3, Vector3>();
        private int sliceWidth;  // Original slice width
        private int sliceHeight; // Original slice height

        public Dicom3D(DicomDisplayManager ddm, int minSlice, int maxSlice, Action<ProcessingProgress> progressCallback = null)
        {
            this.progressCallback = progressCallback;
            this.totalSlices = ddm.GetTotalSlices();
            ProcessSlices(ddm, minSlice, maxSlice);
            currentVisibleIndices = indices.Count;
        }

        private void ProcessSlices(DicomDisplayManager ddm, int minSlice, int maxSlice)
        {
            // Get pixel spacing (in mm)
            var pixelSpacing = ddm.GetSlice(0).PixelSpacing;
            float pixelSpacingX = (float)pixelSpacing;
            float pixelSpacingY = (float)pixelSpacing;

            // Get physical dimensions of a slice in mm
            var firstSlice = ddm.GetCurrentSliceImage();
            float physicalWidth = firstSlice.Width * pixelSpacingX;
            float physicalHeight = firstSlice.Height * pixelSpacingY;

            // Get z-axis physical dimensions
            float firstSliceLocation = (float)ddm.GetSlice(0).SliceLocation;
            float lastSliceLocation = (float)ddm.GetSlice(ddm.GetTotalSlices() - 1).SliceLocation;
            float totalPhysicalDepth = Math.Abs(lastSliceLocation - firstSliceLocation);
            float sliceThickness = (float)ddm.GetSlice(0).SliceThickness;

            // Calculate scaling factors
            float maxDimension = Math.Max(Math.Max(physicalWidth, physicalHeight), totalPhysicalDepth);
            float scaleX = physicalWidth / maxDimension;
            float scaleY = physicalHeight / maxDimension;
            float scaleZ = totalPhysicalDepth / maxDimension;

            // Store Slicing dimensions
            sliceWidth = firstSlice.Width;
            sliceHeight = firstSlice.Height;
            totalSlices = maxSlice - minSlice;
            firstSlice.Dispose();

            var completedSlices = 0;
            var progress = new ProcessingProgress
            {
                TotalValue = maxSlice - minSlice,
                CurrentValue = 0,
                CurrentStep = "Processing DICOM slices"
            };

            Parallel.For(minSlice, maxSlice , new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, z =>
            {
                var localVertices = new List<Vector3>();
                var localColors = new List<Vector3>();
                var localIndices = new List<int>();

                ddm.SetSliceIndex(z);
                var slice = ddm.GetCurrentSliceImage();
                float currentSliceLocation = (float)ddm.GetSlice(z).SliceLocation;

                // Calculate z position with more explicit steps
                float normalizedZ = ((currentSliceLocation - firstSliceLocation) / totalPhysicalDepth) - 0.5f;
                float finalZ = normalizedZ * scaleZ;

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
                                float physicalX = (x * pixelSpacingX);
                                float physicalY = (y * pixelSpacingY);

                                Vector3 vertex = new Vector3(
                                    ((physicalX / physicalWidth) - 0.5f) * scaleX,
                                    ((physicalY / physicalHeight) - 0.5f) * scaleY,
                                    finalZ
                                );

                                Vector3 color = new Vector3(intensity, intensity, intensity);

                                lock (lockObject)
                                {
                                    pointColors[vertex] = color;
                                }

                                localVertices.Add(vertex);
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

        public Bitmap ExtractSlice(float xPosition)
        {
            // Convert normalized xPosition (-0.5 to 0.5) to slice width space (0 to sliceWidth)
            float pixelPos = (xPosition + 0.5f) * sliceWidth;
            int nearestPixelRow = (int)Math.Round(pixelPos);

            // Clamp to valid range
            nearestPixelRow = Math.Max(0, Math.Min(nearestPixelRow, sliceWidth - 1));

            Console.WriteLine($"Extracting slice at pixel row: {nearestPixelRow}");

            var bitmap = new Bitmap(totalSlices, sliceHeight);
            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.WriteOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            unsafe
            {
                byte* ptr = (byte*)bitmapData.Scan0;

                // Create a buffer to collect intensities for each position
                var intensityBuffer = new Dictionary<(int y, int z), List<float>>();

                // Collect all points that match our exact x-coordinate
                foreach (var point in pointColors)
                {
                    float pointPixelX = (point.Key.X + 0.5f) * sliceWidth;
                    int pointRow = (int)Math.Round(pointPixelX);

                    if (pointRow == nearestPixelRow)
                    {
                        // Convert normalized coordinates to image space
                        int y = (int)Math.Round((point.Key.Y + 0.5f) * (sliceHeight - 1));
                        int z = (int)Math.Round((point.Key.Z + 0.5f) * (totalSlices - 1));

                        if (y >= 0 && y < sliceHeight && z >= 0 && z < totalSlices)
                        {
                            var key = (y, z);
                            if (!intensityBuffer.ContainsKey(key))
                            {
                                intensityBuffer[key] = new List<float>();
                            }
                            intensityBuffer[key].Add(point.Value.X);
                        }
                    }
                }

                // Fill the bitmap
                for (int y = 0; y < sliceHeight; y++)
                {
                    for (int z = 0; z < totalSlices; z++)
                    {
                        int offset = y * bitmapData.Stride + z * 4;
                        byte value = 0;

                        if (intensityBuffer.TryGetValue((y, z), out var intensities))
                        {
                            // If we have multiple values for this position, take their average
                            float avgIntensity = intensities.Average();
                            value = (byte)(avgIntensity * 255);
                        }

                        ptr[offset] = value;     // B
                        ptr[offset + 1] = value; // G
                        ptr[offset + 2] = value; // R
                        ptr[offset + 3] = 255;   // A
                    }
                }
            }

            bitmap.UnlockBits(bitmapData);
            return bitmap;
        }

        private float FindNearestPoint((int y, int z) target, Dictionary<(int y, int z), float> points)
        {
            float nearestValue = 0;
            int nearestDistance = int.MaxValue;

            foreach (var point in points)
            {
                int dy = target.y - point.Key.y;
                int dz = target.z - point.Key.z;
                int distance = dy * dy + dz * dz;

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestValue = point.Value;
                }
            }

            return nearestValue;
        }

        public void SetClipPlanes(int front, int back)
        {
            frontClip = front;
            backClip = back;
            UpdateVisibleVertices();
        }

        private void UpdateVisibleVertices()
        {
            var visibleIndices = new List<int>();

            for (int i = 0; i < indices.Count; i++)
            {
                int vertexIndex = indices[i];
                float zPos = vertices[vertexIndex].Z + 0.5f; // Ajuster pour le décalage de -0.5f
                int slice = (int)(zPos * totalSlices);

                if (slice >= frontClip && slice <= (totalSlices - backClip - 1))
                {
                    visibleIndices.Add(indices[i]);
                }
            }

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, elementBufferObject[0]);
            GL.BufferData(BufferTarget.ElementArrayBuffer, visibleIndices.Count * sizeof(int),
                visibleIndices.ToArray(), BufferUsageHint.DynamicDraw);

            currentVisibleIndices = visibleIndices.Count;
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
