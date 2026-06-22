using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using SecureChat.Client.Services;

namespace SecureChat.Client.Components.Chat
{
    public partial class ucFileBubble : UserControl
    {
        // Public properties
        [Category("Data")] public string FileName { get => lblFileName.Text; set => lblFileName.Text = value; }
        [Category("Data")] public string FileSize { get => lblFileSize.Text; set => lblFileSize.Text = value; }
        [Category("Data")] public string TimeStamp { get => lblTime.Text; set => lblTime.Text = value; }
        [Category("Appearance")] public bool IsOutgoing { get; set; } = false;

        // Events
        public event EventHandler<FileClickEventArgs>? FileClicked;
        public event EventHandler? DownloadCanceled;

        public void OnNightModeChanged()
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    lblFileName.ForeColor = TG.TextPrimary;
                    lblFileSize.ForeColor = TG.FileSizeColor;
                    lblTime.ForeColor = TG.TextSecondary;
                    pnlBubble.Invalidate();
                }));
            }
            else
            {
                lblFileName.ForeColor = TG.TextPrimary;
                lblFileSize.ForeColor = TG.FileSizeColor;
                lblTime.ForeColor = TG.TextSecondary;
                pnlBubble.Invalidate();
            }
        }

        // Internal controls
        private Panel pnlBubble;
        private PictureBox picIcon;
        private Label lblFileName;
        private Label lblFileSize;
        private ProgressBar prgDownload;
        private Label lblTime;
        private PictureBox picStatus;
        private Button btnCancel;

        public ucFileBubble()
        {
            InitializeComponent();
            DoubleBuffered = true;
            prgDownload.Visible = false;
            btnCancel.Visible = false;
        }

        private void InitializeComponent()
        {
            pnlBubble = new Panel();
            picIcon = new PictureBox();
            lblFileName = new Label();
            lblFileSize = new Label();
            prgDownload = new ProgressBar();
            lblTime = new Label();
            picStatus = new PictureBox();
            btnCancel = new Button();

            SuspendLayout();

            // pnlBubble
            pnlBubble.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            pnlBubble.Location = new Point(4, 4);
            pnlBubble.Padding = new Padding(10);
            pnlBubble.Size = new Size(360, 64);
            pnlBubble.Paint += PnlBubble_Paint;
            pnlBubble.Click += Bubble_Click;

            // picIcon
            picIcon.Size = new Size(40, 40);
            picIcon.Location = new Point(10, 12);
            picIcon.SizeMode = PictureBoxSizeMode.CenterImage;
            picIcon.BackColor = Color.Transparent;
            picIcon.Cursor = Cursors.Hand;
            picIcon.Click += Bubble_Click;
            picIcon.Paint += PicIcon_Paint;

            // lblFileName
            lblFileName.AutoSize = false;
            lblFileName.Location = new Point(60, 8);
            lblFileName.Size = new Size(220, 20);
            lblFileName.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblFileName.ForeColor = TG.TextPrimary;
            lblFileName.Cursor = Cursors.Hand;
            lblFileName.Click += Bubble_Click;

            // lblFileSize
            lblFileSize.AutoSize = false;
            lblFileSize.Location = new Point(60, 30);
            lblFileSize.Size = new Size(220, 16);
            lblFileSize.Font = new Font("Segoe UI", 8F, FontStyle.Regular);
            lblFileSize.ForeColor = TG.FileSizeColor;
            lblFileSize.Cursor = Cursors.Hand;
            lblFileSize.Click += Bubble_Click;

            // prgDownload
            prgDownload.Location = new Point(60, 46);
            prgDownload.Size = new Size(200, 8);
            prgDownload.Style = ProgressBarStyle.Continuous;
            prgDownload.Maximum = 100;
            prgDownload.Value = 0;

            // lblTime
            lblTime.AutoSize = true;
            lblTime.Location = new Point(275, 34);
            lblTime.Font = new Font("Segoe UI", 8F);
            lblTime.ForeColor = TG.TextSecondary;

            // picStatus
            picStatus.Size = new Size(16, 16);
            picStatus.Location = new Point(300, 32);
            picStatus.SizeMode = PictureBoxSizeMode.CenterImage;
            picStatus.Image = null; // caller can set delivery/read icon
            picStatus.Anchor = AnchorStyles.Right;

            // btnCancel
            btnCancel.Size = new Size(20, 20);
            btnCancel.Location = new Point(330, 32);
            btnCancel.Text = "✕";
            btnCancel.Font = new Font("Segoe UI", 7F);
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.BackColor = Color.Transparent;
            btnCancel.Visible = false;
            btnCancel.Click += BtnCancel_Click;

            // Add controls to pnlBubble
            pnlBubble.Controls.Add(picIcon);
            pnlBubble.Controls.Add(lblFileName);
            pnlBubble.Controls.Add(lblFileSize);
            pnlBubble.Controls.Add(prgDownload);
            pnlBubble.Controls.Add(lblTime);
            pnlBubble.Controls.Add(picStatus);
            pnlBubble.Controls.Add(btnCancel);

            // Add to UserControl
            Controls.Add(pnlBubble);
            Size = new Size(380, 72);
            ResumeLayout(false);
        }

        private void PnlBubble_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, pnlBubble.Width - 1, pnlBubble.Height - 1);
            using var path = RoundedRect(rect, 14);
            Color back = IsOutgoing ? TG.MsgOutBg : TG.MsgInBg;
            using var brush = new SolidBrush(back);
            g.FillPath(brush, path);
        }

        // Rounded rectangle helper
        private GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // Click on bubble -> raise file clicked
        private void Bubble_Click(object? sender, EventArgs e)
        {
            FileClicked?.Invoke(this, new FileClickEventArgs(FileName));
        }

        private void BtnCancel_Click(object? sender, EventArgs e)
        {
            CancelDownloadInternal();
            DownloadCanceled?.Invoke(this, EventArgs.Empty);
        }

        private volatile bool _isDownloading;
        private Action? _cancelCallback;

        // Start a download and display progress. Caller provides a function that accepts IProgress<int> and returns Task.
        // Example usage from form:
        // await bubble.StartDownloadAsync(async progress =>
        // {
        //     // download and call progress.Report(percent) periodically
        //     await DownloadFileAsync(..., new Progress<int>(p => progress.Report(p)));
        // });
        public async Task StartDownloadAsync(Func<IProgress<int>, Task> downloadAction)
        {
            if (_isDownloading) return;
            _isDownloading = true;

            ShowProgressInternal(true);

            var progress = new Progress<int>(p => SetDownloadProgress(p));

            var ctsCalled = false;
            var tcs = new TaskCompletionSource<bool>();
            _cancelCallback = () =>
            {
                ctsCalled = true;
                tcs.TrySetResult(true);
            };

            try
            {
                var downloadTask = downloadAction(progress);
                var completedTask = await Task.WhenAny(downloadTask, tcs.Task).ConfigureAwait(false);

                if (completedTask == tcs.Task || ctsCalled)
                {
                    // cancelled
                    await RunOnUiThreadAsync(() =>
                    {
                        ShowProgressInternal(false);
                        prgDownload.Value = 0;
                    });
                }
                else
                {
                    await downloadTask.ConfigureAwait(false);
                    await RunOnUiThreadAsync(() =>
                    {
                        SetDownloadProgress(100);
                        ShowProgressInternal(false);
                    });
                }
            }
            catch
            {
                // hide progress on error - caller should show message
                await RunOnUiThreadAsync(() => ShowProgressInternal(false));
                throw;
            }
            finally
            {
                _isDownloading = false;
                _cancelCallback = null;
            }
        }

        private void CancelDownloadInternal()
        {
            _cancelCallback?.Invoke();
        }


        private void PicIcon_Paint(object? sender, PaintEventArgs e)
        {
            string ext = System.IO.Path.GetExtension(FileName ?? "").ToLowerInvariant();

            Color iconColor = ext switch
            {
                ".pdf" => Color.FromArgb(0xE5, 0x45, 0x45),
                ".doc" or ".docx" => Color.FromArgb(0x29, 0x5F, 0xB5),
                ".xls" or ".xlsx" => Color.FromArgb(0x1E, 0x7E, 0x45),
                ".zip" or ".rar" => Color.FromArgb(0xF4, 0x8C, 0x4A),
                ".mp3" or ".wav" => Color.FromArgb(0x9B, 0x59, 0xB6),
                ".mp4" or ".mov" => Color.FromArgb(0x20, 0x9E, 0xD9),
                _ => Color.FromArgb(0x24, 0xAA, 0x6B),
            };

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw rounded square
            var rect = new Rectangle(0, 0, picIcon.Width - 1, picIcon.Height - 1);
            using var path = RoundedRect(rect, 8);
            using var brush = new SolidBrush(iconColor);
            g.FillPath(brush, path);

            // Draw extension label
            string label = string.IsNullOrEmpty(ext) ? "FILE" : ext.TrimStart('.').ToUpper();
            if (label.Length > 4) label = label[..4];
            using var font = new Font("Segoe UI", 7f, FontStyle.Bold);
            using var textBrush = new SolidBrush(TG.TitleBarFg);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(label, font, textBrush, rect, sf);
        }

        // Set progress (0-100). Safe to call from any thread.
        public void SetDownloadProgress(int percent)
        {
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            if (prgDownload.InvokeRequired)
            {
                prgDownload.BeginInvoke(new Action(() => prgDownload.Value = percent));
            }
            else
            {
                prgDownload.Value = percent;
            }
        }

        private void ShowProgressInternal(bool visible)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    prgDownload.Visible = visible;
                    btnCancel.Visible = visible;
                    picStatus.Visible = !visible;
                }));
            }
            else
            {
                prgDownload.Visible = visible;
                btnCancel.Visible = visible;
                picStatus.Visible = !visible;
            }
        }

        private Task RunOnUiThreadAsync(Action action)
        {
            var tcs = new TaskCompletionSource<bool>();
            if (IsDisposed) { tcs.SetResult(true); return tcs.Task; }

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    try { action(); tcs.SetResult(true); }
                    catch (Exception ex) { tcs.SetException(ex); }
                }));
            }
            else
            {
                try { action(); tcs.SetResult(true); } catch (Exception ex) { tcs.SetException(ex); }
            }

            return tcs.Task;
        }
    }

    public class FileClickEventArgs : EventArgs
    {
        public string FileName { get; }
        public FileClickEventArgs(string fileName) => FileName = fileName;
    }
}