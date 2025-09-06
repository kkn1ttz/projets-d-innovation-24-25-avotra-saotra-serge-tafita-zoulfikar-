using System;
using System.Drawing;
using System.Windows.Forms;

public class MyNewPage : Form
{
    public MyNewPage()
    {
        InitializeComponents();
    }
    private TextBox filePathTextBox;
    private Button browseButton;
    private ComboBox modelComboBox;
    private Button runButton;
    private RichTextBox resultsBox;

    private void InitializeComponents()
    {
        // === Form settings ===
        this.Size = new Size(1000, 600);
        this.MinimumSize = new Size(800, 400);
        this.Text = "Model Runner";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.White;

        // === Main layout ===
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60)); // file selector
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60)); // model + run
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // results
        this.Controls.Add(layout);

        // === Row 0: File selector ===
        var filePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80));
        filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

        filePathTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            PlaceholderText = "No file selected..."
        };

        browseButton = new Button
        {
            Text = "Browse",
            Dock = DockStyle.Fill
        };
        browseButton.Click += BrowseButton_Click;

        filePanel.Controls.Add(filePathTextBox, 0, 0);
        filePanel.Controls.Add(browseButton, 1, 0);
        layout.Controls.Add(filePanel, 0, 0);

        // === Row 1: Model + Run button ===
        var modelPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 10, 0, 10)
        };
        modelPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        modelPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        modelComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        // Example models
        modelComboBox.Items.AddRange(new object[]
        {
        "Logistic Regression",
        "LightGBM",
        "Random Forest"
        });
        modelComboBox.SelectedIndex = 0;

        runButton = new Button
        {
            Text = "Run Analysis",
            Dock = DockStyle.Fill
        };
        runButton.Click += RunButton_Click;

        modelPanel.Controls.Add(modelComboBox, 0, 0);
        modelPanel.Controls.Add(runButton, 1, 0);
        layout.Controls.Add(modelPanel, 0, 1);

        // === Row 2: Results ===
        resultsBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Font = new Font("Consolas", 11, FontStyle.Regular),
            BorderStyle = BorderStyle.FixedSingle
        };
        layout.Controls.Add(resultsBox, 0, 2);
    }

    // === Handlers ===
    private void BrowseButton_Click(object sender, EventArgs e)
    {
        using (var ofd = new OpenFileDialog())
        {
            ofd.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                filePathTextBox.Text = ofd.FileName;
            }
        }
    }

    private void RunButton_Click(object sender, EventArgs e)
    {
        // Example: replace with actual analysis
        if (string.IsNullOrWhiteSpace(filePathTextBox.Text))
        {
            MessageBox.Show("Please select a CSV file first.");
            return;
        }

        var model = modelComboBox.SelectedItem?.ToString() ?? "None";
        resultsBox.Text = $"Running {model} on file:\n{filePathTextBox.Text}\n\nResult: SUCCESS (example)";
    }

}
