using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using EvilDICOM.Core.Helpers;
using EvilDICOM.Core;
using DeepBridgeWindowsApp.DICOM;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Policy;
using DeepBridgeWindowsApp.Dicom;

namespace DeepBridgeWindowsApp
{
    public partial class DicomViewerForm : Form
    {
        private readonly DicomDisplayManager displayManager;
        private PictureBox mainPictureBox;
        private TrackBar sliceTrackBar;
        private TrackBar windowWidthTrackBar;
        private TrackBar windowCenterTrackBar;
        private DoubleTrackBar doubleTrackBar;
        private Label sliceLabel;
        private Label windowCenterLabel;
        private Label windowWidthLabel;
        private Label minLabel;
        private Label maxLabel;
        private Point startPoint;
        private Point endPoint;
        private bool isDrawing = false;
        private Label startPointLabel;
        private Label endPointLabel;
        private Label areaLabel;
        private const int TARGET_SIZE = 512;

        public DicomViewerForm(DicomReader reader)
        {
            displayManager = new DicomDisplayManager(reader);
            InitializeComponents();
            mainPictureBox.MouseDown += MainPictureBox_MouseDown;
            mainPictureBox.MouseMove += MainPictureBox_MouseMove;
            mainPictureBox.MouseUp += MainPictureBox_MouseUp;
            mainPictureBox.Paint += MainPictureBox_Paint;
        }

        private void InitializeComponents()
        {
            this.Size = new Size(1424, 768); // Increased width to accommodate both panels
            this.Text = "DICOM Viewer";

            // Left info panel
            var infoPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 250,
                BackColor = SystemColors.Control,
                Padding = new Padding(5, 5, 5, 10),
            };

            var patientInfo = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
            };

            var currentSlice = displayManager.GetSlice(displayManager.GetCurrentSliceIndex() + 1);

            AddInfoRow(patientInfo, "Patient ID", currentSlice.PatientID);
            AddInfoRow(patientInfo, "Patient Name", currentSlice.PatientName);
            AddInfoRow(patientInfo, "Patient Sex", currentSlice.PatientSex);
            AddInfoRow(patientInfo, "Modality", currentSlice.Modality);
            AddInfoRow(patientInfo, "Resolution", currentSlice.Rows + " x " + currentSlice.Columns);
            infoPanel.Controls.Add(patientInfo);

            // Add labels for start point, end point, and area
            startPointLabel = new Label
            {
                Text = "Start Point: (0, 0)",
                AutoSize = true,
                Location = new Point(10, patientInfo.Bottom + 10)
            };
            endPointLabel = new Label
            {
                Text = "End Point: (0, 0)",
                AutoSize = true,
                Location = new Point(10, startPointLabel.Bottom + 10)
            };
            areaLabel = new Label
            {
                Text = "Area: 0",
                AutoSize = true,
                Location = new Point(10, endPointLabel.Bottom + 10)
            };

            infoPanel.Controls.Add(startPointLabel);
            infoPanel.Controls.Add(endPointLabel);
            infoPanel.Controls.Add(areaLabel);

            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                BackColor = SystemColors.Control
            };

            var renderButton = new Button
            {
                Dock = DockStyle.Bottom,
                Text = "3D Render",
                AutoSize = true,
            };
            renderButton.Click += Button_Click;

            buttonPanel.Controls.Add(renderButton);
            infoPanel.Controls.Add(buttonPanel);

            // Right top view panel
            var globalViewPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 300,
                BackColor = SystemColors.Control
            };

            var globalTopViewPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 768 / 2
            };

            var globalViewPictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            globalViewPictureBox.Image = displayManager.GetGlobalViewImage();
            globalTopViewPanel.Controls.Add(globalViewPictureBox);

            // Right control view panel
            var globalBottomViewPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 768 / 2,
            };

            var controlPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 768 / 4
            };

            windowWidthTrackBar = new TrackBar
            {
                Dock = DockStyle.Bottom,
                Minimum = 0,
                Maximum = 4000,
                Value = displayManager.windowWidth,
                TickStyle = TickStyle.TopLeft
            };
            windowWidthTrackBar.ValueChanged += TrackBar_ValueChanged;

            windowCenterTrackBar = new TrackBar
            {
                Dock = DockStyle.Bottom,
                Minimum = 0,
                Maximum = 800,
                Value = displayManager.windowCenter,
                TickStyle = TickStyle.TopLeft
            };
            windowCenterTrackBar.ValueChanged += TrackBar_ValueChanged;

            windowCenterLabel = new Label
            {
                Dock = DockStyle.Bottom,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 20,
                Text = "Window Center: " + displayManager.windowCenter
            };

            windowWidthLabel = new Label
            {
                Dock = DockStyle.Bottom,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 20,
                Text = "Window Width: " + displayManager.windowWidth
            };

            controlPanel.Controls.AddRange(new Control[] { windowCenterLabel, windowCenterTrackBar,
                windowWidthLabel, windowWidthTrackBar });

            globalBottomViewPanel.Controls.Add(controlPanel);

            globalViewPanel.Controls.AddRange(new Control[] { globalTopViewPanel, globalBottomViewPanel });

            // Main content
            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill
            };

            mainPictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom
            };

            sliceTrackBar = new TrackBar
            {
                Dock = DockStyle.Bottom,
                Minimum = 0,
                Maximum = displayManager.GetTotalSlices() - 1,
                TickStyle = TickStyle.TopLeft
            };
            sliceTrackBar.ValueChanged += TrackBar_ValueChanged;

            sliceLabel = new Label
            {
                Dock = DockStyle.Bottom,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 20
            };

            // Double slider min max
            doubleTrackBar = new DoubleTrackBar
            {
                Dock = DockStyle.Bottom,
                Minimum = 0,
                Maximum = displayManager.GetTotalSlices() - 1,
                MinValue = 0,
                MaxValue = displayManager.GetTotalSlices() - 1,
                TickStyle = TickStyle.TopLeft
            };
            doubleTrackBar.ValueChanged += DoubleTrackBar_ValueChanged;
            doubleTrackBar.MouseMove += DoubleTrackBar_MouseMove;
            doubleTrackBar.MouseUp += DoubleTrackBar_MouseUp;

            minLabel = new Label
            {
                Dock = DockStyle.Bottom,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 20,
                Text = "Min: 0"
            };

            maxLabel = new Label
            {
                Dock = DockStyle.Bottom,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 20,
                Text = $"Max: {displayManager.GetTotalSlices() - 1}"
            };

            contentPanel.Controls.AddRange(new Control[] { mainPictureBox, sliceLabel, sliceTrackBar, doubleTrackBar, minLabel, maxLabel });

            this.Controls.AddRange(new Control[] { contentPanel, infoPanel, globalViewPanel });
            UpdateDisplay();
        }

        private void Button_Click(object sender, EventArgs e)
        {
            var resizedStartPoint = ConvertToResizedCoordinates(startPoint);
            var resizedEndPoint = ConvertToResizedCoordinates(endPoint);
            var resizedRect = GetResizedRectangle(GetRectangle(startPoint, endPoint));

            var renderForm = new RenderDicomForm(
                displayManager,
                doubleTrackBar.MinValue,
                doubleTrackBar.MaxValue,
                resizedStartPoint.X,
                resizedStartPoint.Y,
                resizedEndPoint.X,
                resizedEndPoint.Y,
                resizedRect.Width,
                resizedRect.Height
            );
            renderForm.Show();
        }

        private void TrackBar_ValueChanged(object sender, EventArgs e)
        {
            displayManager.SetSliceIndex(sliceTrackBar.Value);
            UpdateDisplay();
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

        private void UpdateDisplay()
        {
            mainPictureBox.Image?.Dispose();
            var originalImage = displayManager.GetCurrentSliceImage(windowWidthTrackBar.Value, windowCenterTrackBar.Value);

            var resizedImage = new Bitmap(TARGET_SIZE, TARGET_SIZE);
            using (var g = Graphics.FromImage(resizedImage))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(originalImage, 0, 0, TARGET_SIZE, TARGET_SIZE);
            }

            mainPictureBox.Image = resizedImage;
            sliceLabel.Text = $"Slice {displayManager.GetCurrentSliceIndex() + 1} of {displayManager.GetTotalSlices()}";
            windowCenterLabel.Text = "Window Center: " + windowCenterTrackBar.Value;
            windowWidthLabel.Text = "Window Width: " + windowWidthTrackBar.Value;
        }

        private Point ConvertToResizedCoordinates(Point clickPoint)
        {
            var displayedSize = GetDisplayedImageSize();
            var picBox = mainPictureBox;

            int offsetX = (picBox.ClientSize.Width - displayedSize.Width) / 2;
            int offsetY = (picBox.ClientSize.Height - displayedSize.Height) / 2;

            clickPoint.X -= offsetX;
            clickPoint.Y -= offsetY;

            if (clickPoint.X < 0 || clickPoint.Y < 0 ||
                clickPoint.X > displayedSize.Width || clickPoint.Y > displayedSize.Height)
                return Point.Empty;

            float scaleX = (float)TARGET_SIZE / displayedSize.Width;
            float scaleY = (float)TARGET_SIZE / displayedSize.Height;

            return new Point(
                (int)(clickPoint.X * scaleX),
                (int)(clickPoint.Y * scaleY)
            );
        }

        private Rectangle GetResizedRectangle(Rectangle originalRect)
        {
            var p1 = ConvertToResizedCoordinates(new Point(originalRect.X, originalRect.Y));
            var p2 = ConvertToResizedCoordinates(new Point(originalRect.Right, originalRect.Bottom));

            if (p1 == Point.Empty || p2 == Point.Empty)
                return Rectangle.Empty;

            return new Rectangle(
                Math.Min(p1.X, p2.X),
                Math.Min(p1.Y, p2.Y),
                Math.Abs(p2.X - p1.X),
                Math.Abs(p2.Y - p1.Y)
            );
        }

        private void DoubleTrackBar_MouseUp(object sender, MouseEventArgs e)
        {
            minLabel.Text = "Min: " + doubleTrackBar.MinValue;
            maxLabel.Text = "Max: " + doubleTrackBar.MaxValue;
        }

        private void DoubleTrackBar_MouseMove(object sender, MouseEventArgs e)
        {
            minLabel.Text = "Min: " + doubleTrackBar.MinValue;
            maxLabel.Text = "Max: " + doubleTrackBar.MaxValue;
        }

        private void DoubleTrackBar_ValueChanged(object sender, EventArgs e)
        {
            minLabel.Text = "Min: " + doubleTrackBar.MinValue;
            maxLabel.Text = "Max: " + doubleTrackBar.MaxValue;
        }

        private void MainPictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDrawing = true;
                startPoint = e.Location;
                var resizedPoint = ConvertToResizedCoordinates(startPoint);
                if (resizedPoint != Point.Empty)
                {
                    startPointLabel.Text = $"Start Point: ({resizedPoint.X}, {resizedPoint.Y})";
                }
            }
        }

        private void MainPictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDrawing)
            {
                endPoint = e.Location;
                var resizedPoint = ConvertToResizedCoordinates(endPoint);
                if (resizedPoint != Point.Empty)
                {
                    endPointLabel.Text = $"End Point: ({resizedPoint.X}, {resizedPoint.Y})";
                    var resizedRect = GetResizedRectangle(GetRectangle(startPoint, endPoint));
                    if (resizedRect != Rectangle.Empty)
                    {
                        areaLabel.Text = $"Area: {resizedRect.Width * resizedRect.Height}";
                    }
                }
                mainPictureBox.Invalidate();
            }
        }

        private void MainPictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDrawing = false;
                endPoint = e.Location;
                var resizedPoint = ConvertToResizedCoordinates(endPoint);
                if (resizedPoint != Point.Empty)
                {
                    endPointLabel.Text = $"End Point: ({resizedPoint.X}, {resizedPoint.Y})";
                    var resizedRect = GetResizedRectangle(GetRectangle(startPoint, endPoint));
                    if (resizedRect != Rectangle.Empty)
                    {
                        areaLabel.Text = $"Area: {resizedRect.Width * resizedRect.Height}";
                    }
                }
                mainPictureBox.Invalidate();
            }
        }

        private void MainPictureBox_Paint(object sender, PaintEventArgs e)
        {
            if (isDrawing || startPoint != endPoint)
            {
                var rect = GetRectangle(startPoint, endPoint);
                e.Graphics.DrawRectangle(Pens.Red, rect);
            }
        }

        private Rectangle GetRectangle(Point p1, Point p2)
        {
            return new Rectangle(
                Math.Min(p1.X, p2.X),
                Math.Min(p1.Y, p2.Y),
                Math.Abs(p1.X - p2.X),
                Math.Abs(p1.Y - p2.Y));
        }

        private Size GetDisplayedImageSize()
        {
            if (mainPictureBox.Image == null) return Size.Empty;

            var image = mainPictureBox.Image;
            var picBox = mainPictureBox;

            float imageRatio = (float)image.Width / image.Height;
            float containerRatio = (float)picBox.ClientSize.Width / picBox.ClientSize.Height;

            if (imageRatio > containerRatio)
            {
                return new Size(
                    picBox.ClientSize.Width,
                    (int)(picBox.ClientSize.Width / imageRatio)
                );
            }
            else
            {
                return new Size(
                    (int)(picBox.ClientSize.Height * imageRatio),
                    picBox.ClientSize.Height
                );
            }
        }
    }
}
