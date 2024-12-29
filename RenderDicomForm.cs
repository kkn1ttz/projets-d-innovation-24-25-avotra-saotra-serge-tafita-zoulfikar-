using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DeepBridgeWindowsApp.Dicom;
using DeepBridgeWindowsApp.Utils;
using OpenTK;
using OpenTK.GLControl;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using MethodInvoker = System.Windows.Forms.MethodInvoker;
using ProgressBar = System.Windows.Forms.ProgressBar;

namespace DeepBridgeWindowsApp
{
    public partial class RenderDicomForm : Form
    {
        private Dicom3D render;
        private DicomMetadata dicom;
        private DicomDisplayManager ddm;
        private GLControl gl;

        private ProgressBar progressBar;
        private Label progressLabel;

        // Camera properties
        private Vector3 cameraPosition = new Vector3(0, 0, 3f);
        private Vector3 cameraTarget = Vector3.Zero;
        private Vector3 cameraUp = Vector3.UnitY;

        private Matrix4 model;
        private Matrix4 projection;
        private Matrix4 view;

        private float rotationX = 0;
        private float rotationY = 0;
        
        private Point lastMousePos;
        private bool isMouseDown = false;
        private float zoom = 3.0f;

        private int shaderProgram;
        private const string vertexShaderSource = @"
        #version 330 core
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec3 aColor;
        out vec3 vertexColor;
        uniform mat4 model;
        uniform mat4 view;
        uniform mat4 projection;
        void main()
        {
            gl_Position = projection * view * model * vec4(aPosition, 1.0);
            vertexColor = aColor;
        }";

        private const string fragmentShaderSource = @"
        #version 330 core
        in vec3 vertexColor;
        out vec4 FragColor;
        void main()
        {
            FragColor = vec4(vertexColor, 1.0);
        }";

        private int ColorShaderProgram;
        private const string ColorVertexShader = @"#version 330 core
        layout(location = 0) in vec3 aPosition;
        uniform mat4 model;
        uniform mat4 view;
        uniform mat4 projection;
        void main() {
            gl_Position = projection * view * model * vec4(aPosition, 1.0);
        }";

        private const string ColorFragmentShader = @"#version 330 core
        uniform vec3 color;
        out vec4 FragColor;
        void main() {
            FragColor = vec4(color, 1.0);
        }";

        public RenderDicomForm(DicomDisplayManager ddm)
        {
            this.ddm = ddm;
            this.dicom = this.ddm.globalView;
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            this.Size = new Size(1424, 768);
            this.Text = "DICOM Render";

            // Left Panel
            var leftPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 250,
                BackColor = SystemColors.Control
            };

            var patientInfo = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Padding = new Padding(5),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
            };

            AddInfoRow(patientInfo, "Patient ID", dicom.PatientID);
            AddInfoRow(patientInfo, "Patient Name", dicom.PatientName);
            AddInfoRow(patientInfo, "Patient Sex", dicom.PatientSex);
            AddInfoRow(patientInfo, "Modality", dicom.Modality);
            AddInfoRow(patientInfo, "Resolution", dicom.Rows + " x " + dicom.Columns);
            AddInfoRow(patientInfo, "Content Time", dicom.ContentTime);
            leftPanel.Controls.Add(patientInfo);

            // OpenGL content
            gl = new GLControl
            {
                Dock = DockStyle.Fill,
            };
            gl.Resize += GLControl_Resize;
            //gl.Paint += GLControl_Paint;
            gl.MouseDown += GL_MouseDown;
            gl.MouseUp += GL_MouseUp;
            gl.MouseMove += GL_MouseMove;
            gl.MouseWheel += GL_MouseWheel;
            
            // Right Panel
            var rightPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 250,
                BackColor = SystemColors.Control
            };

            // Add progress bar and label at the center of the form
            progressBar = new ProgressBar
            {
                Width = 300,
                Height = 23,
                Style = ProgressBarStyle.Continuous,
                Visible = false // Hidden by default
            };

            progressLabel = new Label
            {
                AutoSize = true,
                Width = 300,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false // Hidden by default
            };

            // Center the controls
            progressBar.Location = new Point(
            (this.ClientSize.Width - progressBar.Width) / 2,
                (this.ClientSize.Height - progressBar.Height) / 2
            );
            progressLabel.Location = new Point(
                (this.ClientSize.Width - progressLabel.Width) / 2,
                progressBar.Location.Y - 25
            );

            this.Controls.Add(progressBar);
            this.Controls.Add(progressLabel);

            //this.Controls.AddRange(new Control[] { leftPanel, gl, rightPanel });
            this.Controls.Add(gl);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            //gl.MakeCurrent();
            //GL.ClearColor(Color.Black);
            //GL.Enable(EnableCap.DepthTest);
            //GL.Enable(EnableCap.Texture2D);
            //this.render = new Dicom3D(this.ddm);
            //InitializeShaders();
            //render.InitializeGL();
            this.Shown += RenderDicomForm_Load;
        }

        private async void RenderDicomForm_Load(object sender, EventArgs e)
        {
            Console.WriteLine("Loading 3D render...");
            progressBar.Visible = true;
            progressLabel.Visible = true;
            progressBar.Maximum = 100;
            progressBar.Value = 0;

            try
            {
                await Task.Run(() =>
                {
                    this.Invoke((MethodInvoker)delegate {
                        gl.MakeCurrent();
                        GL.ClearColor(Color.Black);
                        GL.Enable(EnableCap.DepthTest);
                        GL.Enable(EnableCap.Texture2D);
                    });

                    this.render = new Dicom3D(this.ddm, (progress) =>
                    {
                        this.Invoke((MethodInvoker)delegate {
                            progressBar.Value = (int)progress.Percentage;
                            progressLabel.Text = $"{progress.CurrentStep} - {progress.CurrentValue} of {progress.TotalValue} slices ({progress.Percentage:F1}%)";
                        });
                    });

                    this.Invoke((MethodInvoker)delegate {
                        InitializeShaders();
                        render.InitializeGL();
                        progressBar.Visible = false;
                        progressLabel.Visible = false;
                        gl.Invalidate();
                    });

                    gl.Paint += GLControl_Paint;
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing 3D render: {ex.Message}");
            }
        }

        private void InitializeShaders()
        {
            // Monochrome
            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, vertexShaderSource);
            GL.CompileShader(vertexShader);

            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, fragmentShaderSource);
            GL.CompileShader(fragmentShader);

            shaderProgram = GL.CreateProgram();
            GL.AttachShader(shaderProgram, vertexShader);
            GL.AttachShader(shaderProgram, fragmentShader);
            GL.LinkProgram(shaderProgram);

            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);

            string infoLog = GL.GetShaderInfoLog(vertexShader);
            Console.WriteLine($"Vertex Shader Log: {infoLog}");
            infoLog = GL.GetShaderInfoLog(fragmentShader);
            Console.WriteLine($"Fragment Shader Log: {infoLog}");
            infoLog = GL.GetProgramInfoLog(shaderProgram);
            Console.WriteLine($"Program Log: {infoLog}");

            // Color
            int CVertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(CVertexShader, ColorVertexShader);
            GL.CompileShader(CVertexShader);

            int CFragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(CFragmentShader, ColorFragmentShader);
            GL.CompileShader(CFragmentShader);

            ColorShaderProgram = GL.CreateProgram();
            GL.AttachShader(ColorShaderProgram, CVertexShader);
            GL.AttachShader(ColorShaderProgram, CFragmentShader);
            GL.LinkProgram(ColorShaderProgram);

            infoLog = GL.GetShaderInfoLog(CVertexShader);
            Console.WriteLine($"Vertex Shader Log: {infoLog}");
            infoLog = GL.GetShaderInfoLog(CFragmentShader);
            Console.WriteLine($"Fragment Shader Log: {infoLog}");
            infoLog = GL.GetProgramInfoLog(ColorShaderProgram);
            Console.WriteLine($"Program Log: {infoLog}");
        }

        private void GLControl_Paint(object sender, PaintEventArgs e)
        {
            gl.MakeCurrent();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.UseProgram(shaderProgram);

            float aspect = (float)gl.ClientSize.Width / gl.ClientSize.Height;
            Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, aspect, 0.1f, 100f);
            Matrix4 view = Matrix4.LookAt(cameraPosition, cameraTarget, cameraUp);

            // Create model matrix with proper transformations
            Matrix4 model = Matrix4.Identity;

            // Add rotation based on mouse movement (you already have this via camera)
            model *= Matrix4.CreateRotationX(MathHelper.DegreesToRadians(rotationX));
            model *= Matrix4.CreateRotationY(MathHelper.DegreesToRadians(rotationY));

            // Scale to fit the bounding box (-0.5 to 0.5 in all dimensions)
            float scaleFactor = 1.0f;  // Start with 1.0 since your box is already -0.5 to 0.5
            model *= Matrix4.CreateScale(scaleFactor);

            // Center the model (your bounding box is already centered at 0,0,0)
            // If you need to adjust position:
            Vector3 centerOffset = new Vector3(0f, 0f, 0f);
            model *= Matrix4.CreateTranslation(centerOffset);

            int modelLoc = GL.GetUniformLocation(shaderProgram, "model");
            int viewLoc = GL.GetUniformLocation(shaderProgram, "view");
            int projLoc = GL.GetUniformLocation(shaderProgram, "projection");

            GL.UniformMatrix4(modelLoc, false, ref model);
            GL.UniformMatrix4(viewLoc, false, ref view);
            GL.UniformMatrix4(projLoc, false, ref projection);

            render.Render(shaderProgram, model, view, projection);
            DrawBoundingBox(model, view, projection);
            gl.SwapBuffers();
        }

        private void DrawBoundingBox(Matrix4 model, Matrix4 view, Matrix4 projection)
        {
            GL.UseProgram(ColorShaderProgram);

            GL.UniformMatrix4(GL.GetUniformLocation(ColorShaderProgram, "model"), false, ref model);
            GL.UniformMatrix4(GL.GetUniformLocation(ColorShaderProgram, "view"), false, ref view);
            GL.UniformMatrix4(GL.GetUniformLocation(ColorShaderProgram, "projection"), false, ref projection);
            GL.Uniform3(GL.GetUniformLocation(ColorShaderProgram, "color"), 1.0f, 0.0f, 0.0f); // Red color

            GL.Begin(PrimitiveType.Lines);

            // Bottom face
            GL.Vertex3(-0.5f, -0.5f, -0.5f); GL.Vertex3(0.5f, -0.5f, -0.5f);
            GL.Vertex3(0.5f, -0.5f, -0.5f); GL.Vertex3(0.5f, -0.5f, 0.5f);
            GL.Vertex3(0.5f, -0.5f, 0.5f); GL.Vertex3(-0.5f, -0.5f, 0.5f);
            GL.Vertex3(-0.5f, -0.5f, 0.5f); GL.Vertex3(-0.5f, -0.5f, -0.5f);

            // Top face
            GL.Vertex3(-0.5f, 0.5f, -0.5f); GL.Vertex3(0.5f, 0.5f, -0.5f);
            GL.Vertex3(0.5f, 0.5f, -0.5f); GL.Vertex3(0.5f, 0.5f, 0.5f);
            GL.Vertex3(0.5f, 0.5f, 0.5f); GL.Vertex3(-0.5f, 0.5f, 0.5f);
            GL.Vertex3(-0.5f, 0.5f, 0.5f); GL.Vertex3(-0.5f, 0.5f, -0.5f);

            // Vertical edges
            GL.Vertex3(-0.5f, -0.5f, -0.5f); GL.Vertex3(-0.5f, 0.5f, -0.5f);
            GL.Vertex3(0.5f, -0.5f, -0.5f); GL.Vertex3(0.5f, 0.5f, -0.5f);
            GL.Vertex3(0.5f, -0.5f, 0.5f); GL.Vertex3(0.5f, 0.5f, 0.5f);
            GL.Vertex3(-0.5f, -0.5f, 0.5f); GL.Vertex3(-0.5f, 0.5f, 0.5f);

            GL.End();
            GL.UseProgram(shaderProgram); // Switch back to main shader
        }

        private void DrawColorBox()
        {
            GL.UseProgram(ColorShaderProgram);

            float aspect = (float)gl.ClientSize.Width / gl.ClientSize.Height;
            projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, aspect, 0.1f, 100f);
            view = Matrix4.LookAt(cameraPosition, cameraTarget, cameraUp);
            model = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(rotationX))
                   * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(rotationY));

            int modelLoc = GL.GetUniformLocation(ColorShaderProgram, "model");
            int viewLoc = GL.GetUniformLocation(ColorShaderProgram, "view");
            int projLoc = GL.GetUniformLocation(ColorShaderProgram, "projection");
            int colorLoc = GL.GetUniformLocation(ColorShaderProgram, "color");

            GL.UniformMatrix4(modelLoc, false, ref model);
            GL.UniformMatrix4(viewLoc, false, ref view);
            GL.UniformMatrix4(projLoc, false, ref projection);
            GL.Uniform3(colorLoc, 1.0f, 0.0f, 0.0f); // Red color

            GL.Begin(PrimitiveType.LineLoop);
            GL.Vertex3(-0.5f, -0.5f, 0.0f);
            GL.Vertex3(0.5f, -0.5f, 0.0f);
            GL.Vertex3(0.5f, 0.5f, 0.0f);
            GL.Vertex3(-0.5f, 0.5f, 0.0f);
            GL.End();

            GL.UseProgram(shaderProgram);
        }

        private void GLControl_Resize(object sender, EventArgs e)
        {
            if (gl == null) return;

            gl.MakeCurrent();
            GL.Viewport(0, 0, gl.ClientSize.Width, gl.ClientSize.Height);
        }

        private void GL_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isMouseDown = true;
                lastMousePos = e.Location;
            }
        }

        private void GL_MouseUp(object sender, MouseEventArgs e)
        {
            isMouseDown = false;
        }

        private void GL_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isMouseDown) return;

            float deltaX = -(e.X - lastMousePos.X) * 0.5f;  // Negative deltaX to reverse direction
            float deltaY = (e.Y - lastMousePos.Y) * 0.5f;

            Vector3 viewDir = (cameraTarget - cameraPosition).Normalized();
            Vector3 right = Vector3.Cross(cameraUp, viewDir).Normalized();
            Vector3 up = Vector3.Cross(viewDir, right);

            // Convertir les Matrix3 en Quaternion
            Quaternion rotX = Quaternion.FromAxisAngle(up, MathHelper.DegreesToRadians(deltaX));
            Quaternion rotY = Quaternion.FromAxisAngle(right, MathHelper.DegreesToRadians(deltaY));

            // Multiplier les quaternions
            Quaternion finalRotation = rotX * rotY;

            cameraPosition = Vector3.Transform(cameraPosition - cameraTarget, finalRotation) + cameraTarget;
            cameraUp = Vector3.Transform(cameraUp, finalRotation);

            lastMousePos = e.Location;
            gl.Invalidate();
        }

        private void GL_MouseWheel(object sender, MouseEventArgs e)
        {
            float zoomFactor = 1.0f - (e.Delta * 0.001f);
            Vector3 zoomDir = cameraPosition - cameraTarget;
            cameraPosition = cameraTarget + zoomDir * zoomFactor;
            gl.Invalidate();
        }
        
        private void AddInfoRow(TableLayoutPanel table, string label, string value)
        {
            var labelControl = new Label
            {
                Text = label,
                AutoSize = true,
                Margin = new Padding(2),
                Font = new Font(Font.FontFamily, 9, FontStyle.Bold)
            };

            var valueControl = new Label
            {
                Text = value,
                AutoSize = true,
                Margin = new Padding(2)
            };

            table.Controls.Add(labelControl);
            table.Controls.Add(valueControl);
        }
    }
}
