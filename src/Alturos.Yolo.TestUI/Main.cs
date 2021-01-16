using Alturos.Yolo.Model;
using Alturos.Yolo.TestUI.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Alturos.Yolo.TestUI
{
    public partial class Main : Form
    {
        private YoloWrapper _yoloWrapper;

        public Main()
        {
            InitializeComponent();

            buttonProcessImage.Enabled = false;
            buttonStartTracking.Enabled = false;

            menuStrip1.Visible = false;

            toolStripStatusLabelYoloInfo.Text = string.Empty;

            Text = $"Alturos Yolo TestUI {Application.ProductVersion}";
            dataGridViewFiles.AutoGenerateColumns = false;
            dataGridViewResult.AutoGenerateColumns = false;

            var imageInfos = new DirectoryImageReader().Analyze(@".\Images");
            dataGridViewFiles.DataSource = imageInfos.ToList();

            Task.Run(() => Initialize("."));
            LoadAvailableConfigurations();
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            _yoloWrapper?.Dispose();
        }

        private void LoadAvailableConfigurations()
        {
            var configPath = "config";

            if (!Directory.Exists(configPath))
            {
                return;
            }

            var configs = Directory.GetDirectories(configPath);
            if (configs.Length == 0)
            {
                return;
            }

            menuStrip1.Visible = true;

            foreach (var config in configs)
            {
                var menuItem = new ToolStripMenuItem();
                menuItem.Text = config;
                menuItem.Click += (sender, e) => { Initialize(config); };
                configurationToolStripMenuItem.DropDownItems.Add(menuItem);
            }
        }

        private ImageInfo GetCurrentImage()
        {
            var item = dataGridViewFiles.CurrentRow?.DataBoundItem as ImageInfo;
            return item;
        }

        private void dataGridViewFiles_SelectionChanged(object sender, EventArgs e)
        {
            var oldImage = pictureBox1.Image;
            var imageInfo = GetCurrentImage();           
            pictureBox1.Image = Image.FromFile(imageInfo.Path);            
            oldImage?.Dispose();

            dataGridViewResult.DataSource = null;
            groupBoxResult.Text = $"Result";
        }

        private void dataGridViewFiles_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                DetectSelectedImage();
            }
        }

        private void dataGridViewResult_SelectionChanged(object sender, EventArgs e)
        {
            if (!dataGridViewResult.Focused)
            {
                return;
            }

            var items = dataGridViewResult.DataSource as List<YoloItem>;
            var selectedItem = dataGridViewResult.CurrentRow?.DataBoundItem as YoloItem;
            DrawBoundingBoxes(items, selectedItem);
        }

        private void openFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dialogResult = folderBrowserDialog1.ShowDialog();
            if (dialogResult != DialogResult.OK)
            {
                return;
            }

            var imageInfos = new DirectoryImageReader().Analyze(folderBrowserDialog1.SelectedPath);
            dataGridViewFiles.DataSource = imageInfos.ToList();
        }

        private void buttonProcessImage_Click(object sender, EventArgs e)
        {
            DetectSelectedImage();
        }

        private async void buttonStartTracking_Click(object sender, EventArgs e)
        {
            await StartTrackingAsync();
        }

        private async Task StartTrackingAsync()
        {
            buttonStartTracking.Enabled = false;

            var imageInfo = GetCurrentImage();

            var yoloTracking = new YoloTracking(imageInfo.Width, imageInfo.Height);
            var count = dataGridViewFiles.RowCount;
            for (var i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    dataGridViewFiles.Rows[i - 1].Selected = false;
                }

                dataGridViewFiles.Rows[i].Selected = true;
                dataGridViewFiles.CurrentCell = dataGridViewFiles.Rows[i].Cells[0];

                var items = Detect();

                var trackingItems = yoloTracking.Analyse(items);
                DrawBoundingBoxes(trackingItems);

                await Task.Delay(100);
            }

            buttonStartTracking.Enabled = true;
        }

        private void DrawBoundingBoxes(IEnumerable<YoloTrackingItem> items)
        {
            var imageInfo = GetCurrentImage();
            //Load the image(probably from your stream)
            var image = Image.FromFile(imageInfo.Path);

            using (var font = new Font(FontFamily.GenericSansSerif, 16))
            using (var canvas = Graphics.FromImage(image))
            {
                foreach (var item in items)
                {
                    var x = item.X;
                    var y = item.Y;
                    var width = item.Width;
                    var height = item.Height;

                    var brush = GetBrush(item.Confidence);
                    var penSize = image.Width / 100.0f;

                    using (var pen = new Pen(brush, penSize))
                    {
                        canvas.DrawRectangle(pen, x, y, width, height);
                        canvas.FillRectangle(brush, x - (penSize / 2), y - 15, width + penSize, 25);
                    }
                }

                foreach (var item in items)
                {
                    canvas.DrawString(item.ObjectId, font, Brushes.White, item.X, item.Y - 12);
                }

                canvas.Flush();
            }

            var oldImage = pictureBox1.Image;
            pictureBox1.Image = image;
            oldImage?.Dispose();
        }

        private void DrawBoundingBoxes(List<YoloItem> items, YoloItem selectedItem = null)
        {
            var imageInfo = GetCurrentImage();
            //Load the image(probably from your stream)
            var image = Image.FromFile(imageInfo.Path);

            using (var canvas = Graphics.FromImage(image))
            {
                foreach (var item in items)
                {
                    var x = item.X;
                    var y = item.Y;
                    var width = item.Width;
                    var height = item.Height;
                    
                    var brush = GetBrush(item.Confidence);
                    var penSize = image.Width / 100.0f;

                    using (var pen = new Pen(brush, penSize))
                    using (var overlayBrush = new SolidBrush(Color.FromArgb(150, 255, 255, 102)))
                    {
                        if (item.Equals(selectedItem))
                        {
                            canvas.FillRectangle(overlayBrush, x, y, width, height);
                        }

                        canvas.DrawRectangle(pen, x, y, width, height);
                    }
                }

                canvas.Flush();
            }

            var oldImage = pictureBox1.Image;
            pictureBox1.Image = image;
            oldImage?.Dispose();
        }

        private Brush GetBrush(double confidence)
        {
            if (confidence > 0.5)
            {
                return Brushes.GreenYellow;
            }
            else if (confidence > 0.2 && confidence <= 0.5)
            {
                return Brushes.Orange;
            }

            return Brushes.DarkRed;
        }

        private void Initialize(string path)
        {
            var configurationDetector = new YoloConfigurationDetector();
            try
            {
                var config = configurationDetector.Detect(path);
                if (config == null)
                {
                    return;
                }

                Initialize(config);
            }
            catch (Exception exception)
            {
                MessageBox.Show($"Cannot found a valid dataset {exception}", "No Dataset available", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Initialize(YoloConfiguration config)
        {
            try
            {
                if (_yoloWrapper != null)
                {
                    _yoloWrapper.Dispose();
                }

                var gpuConfig = new GpuConfig();
                var useOnlyCpu = cpuToolStripMenuItem.Checked;
                if (useOnlyCpu)
                {
                    gpuConfig = null;
                }

                toolStripStatusLabelYoloInfo.Text = $"Initialize...";

                var sw = new Stopwatch();
                sw.Start();
                _yoloWrapper = new YoloWrapper(config.ConfigFile, config.WeightsFile, config.NamesFile, gpuConfig);
                sw.Stop();

                var action = new MethodInvoker(delegate
                {
                    var deviceName = _yoloWrapper.GetGraphicDeviceName(gpuConfig);
                    toolStripStatusLabelYoloInfo.Text = $"Initialize Yolo in {sw.Elapsed.TotalMilliseconds:0} ms - Detection System:{_yoloWrapper.DetectionSystem} {deviceName} Weights:{config.WeightsFile}";
                });

                statusStrip1.Invoke(action);
                buttonProcessImage.Invoke(new MethodInvoker(delegate { buttonProcessImage.Enabled = true; }));
                buttonStartTracking.Invoke(new MethodInvoker(delegate { buttonStartTracking.Enabled = true; }));
            }
            catch (Exception exception)
            {
                MessageBox.Show($"{nameof(Initialize)} - {exception}", "Error Initialize", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }        

        private void DetectSelectedImage()
        {
            var items = Detect();
            dataGridViewResult.DataSource = items;
            DrawBoundingBoxes(items);
        }

        private List<YoloItem> Detect(bool memoryTransfer = true)
        {
            if (_yoloWrapper == null)
            {
                return null;
            }

            var imageInfo = GetCurrentImage();
            var imageData = File.ReadAllBytes(imageInfo.Path);

            var sw = new Stopwatch();
            sw.Start();
            List<YoloItem> items;
            if (memoryTransfer)
            {
                items = _yoloWrapper.Detect(imageData).ToList();
            }
            else
            {
                items = _yoloWrapper.Detect(imageInfo.Path).ToList();
            }

            sw.Stop();
            groupBoxResult.Text = $"Result [ processed in {sw.Elapsed.TotalMilliseconds:0} ms ]";

            return items;
        }

        private void gpuToolStripMenuItem_Click(object sender, EventArgs e)
        {
            cpuToolStripMenuItem.Checked = !cpuToolStripMenuItem.Checked;
        }

        private async void downloadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var repository = new YoloPreTrainedDataSetRepository();
            var dataSets = await repository.GetDataSetsAsync();
            foreach (var dataSet in dataSets)
            {
                statusStrip1.Invoke(new MethodInvoker(delegate { toolStripStatusLabelYoloInfo.Text = $"Start download for {dataSet} dataset..."; }));
                await repository.DownloadDataSetAsync(dataSet, $@"config\{dataSet}");
            }

            LoadAvailableConfigurations();
            statusStrip1.Invoke(new MethodInvoker(delegate { toolStripStatusLabelYoloInfo.Text = $"Download done"; }));
        }
    }
}
