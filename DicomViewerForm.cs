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

        public DicomViewerForm(DicomReader reader)
        {
            displayManager = new DicomDisplayManager(reader);
            InitializeComponents();
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
                Maximum = displayManager.GetTotalSlices(),
                MinValue = 0,
                MaxValue = displayManager.GetTotalSlices(),
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
                Text = $"Max: {displayManager.GetTotalSlices()}"
            };

            contentPanel.Controls.AddRange(new Control[] { mainPictureBox, sliceLabel, sliceTrackBar, doubleTrackBar, minLabel, maxLabel });

            this.Controls.AddRange(new Control[] { contentPanel, infoPanel, globalViewPanel });
            UpdateDisplay();
        }

        private void Button_Click(object sender, EventArgs e)
        {
            var renderForm = new RenderDicomForm(displayManager, doubleTrackBar.MinValue, doubleTrackBar.MaxValue);
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
            mainPictureBox.Image = displayManager.GetCurrentSliceImage(windowWidthTrackBar.Value, windowCenterTrackBar.Value);
            sliceLabel.Text = $"Slice {displayManager.GetCurrentSliceIndex() + 1} of {displayManager.GetTotalSlices()}";
            windowCenterLabel.Text = "Window Center: " + windowCenterTrackBar.Value;
            windowWidthLabel.Text = "Window Width: " + windowWidthTrackBar.Value;
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

    }
}