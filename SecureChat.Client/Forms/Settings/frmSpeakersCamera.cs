using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Windows.Forms;
using NAudio.CoreAudioApi;
using SecureChat.Client.Services;

namespace SecureChat.Client.Forms.Settings
{
    public class frmSpeakersCamera : Form
    {
        // Colors read from TG at paint time

        private Panel _content = null!;

        private Label _lblOutputValue = null!;
        private Label _lblInputValue = null!;
        private Label _lblCallOutputValue = null!;
        private Label _lblCallInputValue = null!;
        private Label _lblCameraValue = null!;

        private CheckBox _chkUseSameDevices = null!;
        private CheckBox _chkAcceptCalls = null!;

        private Panel _callOutputRow = null!;
        private Panel _callInputRow = null!;

        private PictureBox _cameraPreview = null!;
        private OpenCvSharp.VideoCapture? _capture;
        private System.Windows.Forms.Timer _cameraTimer = null!;
        private readonly Dictionary<string, int> _cameraNameToIndex = new(StringComparer.OrdinalIgnoreCase);
        private bool _isRenderingFrame;

        private SpeakersCameraSettings _settings = null!;

        public frmSpeakersCamera()
        {
            NightModeService.ThemeChanged += OnThemeChanged;
            FormClosed += (_, __) => NightModeService.ThemeChanged -= OnThemeChanged;
            InitializeComponent();
            BuildUI();
            Load += (_, __) =>
            {
                _settings = SpeakersCameraSettings.Load();
                BindSettingsToUI();
            };
            Shown += (_, __) => BeginInvoke(new Action(StartCameraPreview));
            FormClosed += (_, __) => StopCameraPreview();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopCameraPreview();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent() { }

        private void BuildUI()
        {
            Text = "Speakers and Camera";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            HelpButton = false;
            ControlBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 840);
            BackColor = TG.WindowBg;
            SecureChat.Client.Services.ThemeRefreshHelper.Hook(this);
            Font = new Font("Segoe UI", 10f);
            DoubleBuffered = true;

            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = TG.WindowBg
            };
            Controls.Add(scroll);

            _content = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1200,
                BackColor = TG.WindowBg
            };
            scroll.Controls.Add(_content);

            int y = 0;
            y = AddHeader(y);

            y = AddSectionLabel("Speakers and headphones", y, 12);
            y = AddValueRow("Output device", y, out _lblOutputValue, OnChooseOutputDevice);

            y = AddSectionDivider(y);

            y = AddSectionLabel("Microphone", y);
            y = AddValueRow("Input device", y, out _lblInputValue, OnChooseInputDevice);
            y = AddMicMeter(y);

            y = AddSectionDivider(y);

            y = AddSectionLabel("Calls and video chats", y);
            y = AddToggleRow("Use the same devices for calls", y, out _chkUseSameDevices, true, OnUseSameChanged);
            _callOutputRow = CreateValueRow("Output device", out _lblCallOutputValue, OnChooseCallOutputDevice);
            _callInputRow = CreateValueRow("Microphone", out _lblCallInputValue, OnChooseCallInputDevice);
            _content.Controls.Add(_callOutputRow);
            _content.Controls.Add(_callInputRow);

            y = AddSectionDivider(y);

            y = AddSectionLabel("Camera", y);
            y = AddValueRow("Input device", y, out _lblCameraValue, OnChooseCameraInputDevice);
            y = AddCameraPreview(y);

            y = AddSectionDivider(y);

            y = AddSectionLabel("Other settings", y);
            y = AddToggleRow("Accept calls on this device", y, out _chkAcceptCalls, true, (_, __) => SaveSettingsFromUI());

            Resize += (_, __) => LayoutRows();
            _content.Resize += (_, __) => LayoutRows();
            LayoutRows();

            UiLocalization.ApplyToForm(this);
        }

        private void LayoutRows()
        {
            int width = _content.ClientSize.Width;
            int y = 0;

            // Header
            var header = _content.Controls[0];
            Place(header, 0, y, width, 74); y += 74;
            y += 12;

            for (int i = 1; i < _content.Controls.Count; i++)
            {
                var c = _content.Controls[i];
                int h = c.Height;

                if (c == _callOutputRow || c == _callInputRow)
                {
                    if (!_callOutputRow.Visible && !_callInputRow.Visible)
                    {
                        c.Location = new Point(0, y);
                        continue;
                    }
                }

                if (!c.Visible)
                {
                    c.Location = new Point(0, y);
                    continue;
                }

                Place(c, 0, y, width, h);
                y += h;
            }

            _content.Height = y + 16;
        }

        private static void Place(Control c, int x, int y, int w, int h)
        {
            c.Location = new Point(x, y);
            c.Size = new Size(w, h);
        }

        private int AddHeader(int y)
        {
            var header = new Panel { Height = 74, BackColor = TG.WindowBg };

            var back = new PictureBox
            {
                Size = new Size(24, 24),
                Location = new Point(10, 22),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = SettingsGlyphIcons.Create("title_back.png", 24),
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent
            };
            back.Click += (_, __) => Close();

            var title = new Label
            {
                Text = "Speakers and Camera",
                Font = new Font("Segoe UI Semibold", 13f),
                ForeColor = TG.TextPrimary,
                AutoSize = true,
                Location = new Point(46, 20),
                BackColor = Color.Transparent
            };

            var close = new Button
            {
                Text = "X",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = TG.TextSecondary,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                TabStop = false,
                Padding = new Padding(6, 2, 6, 2)
            };
            close.FlatAppearance.BorderSize = 0;
            close.Click += (_, __) => Close();
            header.Resize += (_, __) => close.Location = new Point(header.Width - close.Width - 14, 16);

            var sep = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = TG.Divider };

            header.Controls.Add(back);
            header.Controls.Add(title);
            header.Controls.Add(close);
            header.Controls.Add(sep);
            _content.Controls.Add(header);
            return y + 74;
        }

        private int AddSectionLabel(string text, int y, int marginTop = 0)
        {
            y += marginTop;
            var lbl = new Label
            {
                Text = text,
                ForeColor = TG.CAccent,
                Font = new Font("Segoe UI Semibold", 11f),
                AutoSize = false,
                Height = 42,
                BackColor = TG.WindowBg,
                Padding = new Padding(28, 10, 0, 0)
            };
            _content.Controls.Add(lbl);
            return y + lbl.Height;
        }

        private int AddSectionDivider(int y)
        {
            var div = new Panel
            {
                Height = 10,
                BackColor = TG.Divider
            };
            _content.Controls.Add(div);
            return y + div.Height;
        }

        private int AddValueRow(string text, int y, out Label valueLabel, Action onClick)
        {
            var row = CreateValueRow(text, out valueLabel, onClick);
            _content.Controls.Add(row);
            return y + row.Height;
        }

        private Panel CreateValueRow(string text, out Label valueLabel, Action onClick)
        {
            var row = new Panel
            {
                Height = 50,
                BackColor = TG.WindowBg,
                Cursor = Cursors.Hand
            };

            var lbl = new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = TG.TextPrimary,
                Location = new Point(28, 14),
                BackColor = Color.Transparent
            };

            valueLabel = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = TG.CAccent,
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true,
                BackColor = Color.Transparent
            };
            var value = valueLabel;

            row.Resize += (_, __) => value.SetBounds(row.Width - 210, 12, 180, 24);

            var sep = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = TG.Divider };

            foreach (Control c in new Control[] { row, lbl, value })
            {
                c.Click += (_, __) => onClick();
                c.MouseEnter += (_, __) => row.BackColor = TG.SidebarHover;
                c.MouseLeave += (_, __) => row.BackColor = TG.WindowBg;
            }

            row.Controls.Add(lbl);
            row.Controls.Add(value);
            row.Controls.Add(sep);
            return row;
        }

        private int AddToggleRow(string text, int y, out CheckBox toggle, bool initial, EventHandler onChanged)
        {
            var row = new Panel { Height = 54, BackColor = TG.WindowBg };

            var lbl = new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = TG.TextPrimary,
                Location = new Point(28, 16),
                BackColor = Color.Transparent
            };

            toggle = new CheckBox
            {
                Appearance = Appearance.Button,
                AutoSize = false,
                Size = new Size(44, 24),
                FlatStyle = FlatStyle.Flat,
                Checked = initial,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            var t = toggle;
            toggle.FlatAppearance.BorderSize = 0;
            toggle.Paint += (_, e) => DrawToggle(t, e.Graphics);
            toggle.CheckedChanged += onChanged;

            row.Resize += (_, __) => t.Location = new Point(row.Width - t.Width - 24, (row.Height - t.Height) / 2);

            var sep = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = TG.Divider };

            row.Controls.Add(lbl);
            row.Controls.Add(toggle);
            row.Controls.Add(sep);
            _content.Controls.Add(row);
            return y + row.Height;
        }

        private int AddMicMeter(int y)
        {
            var host = new Panel { Height = 44, BackColor = TG.WindowBg };
            host.Paint += (_, e) =>
            {
                int startX = 28;
                int top = 16;
                int barW = 4;
                int gap = 6;
                int count = Math.Max(20, (host.Width - 56) / (barW + gap));
                using var brush = new SolidBrush(Color.FromArgb(0xDB, 0xE6, 0xEF));
                for (int i = 0; i < count; i++)
                {
                    e.Graphics.FillRectangle(brush, startX + i * (barW + gap), top, barW, 22);
                }
            };

            var sep = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = TG.Divider };
            host.Controls.Add(sep);
            _content.Controls.Add(host);
            return y + host.Height;
        }

        private int AddCameraPreview(int y)
        {
            var host = new Panel { Height = 250, BackColor = TG.WindowBg };
            _cameraPreview = new PictureBox
            {
                Location = new Point(28, 14),
                Size = new Size(464, 210),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = TG.SidebarHover
            };
            ShowCameraFallback();
            host.Resize += (_, __) => _cameraPreview.Size = new Size(Math.Max(120, host.Width - 56), 210);
            host.Resize += (_, __) => ApplyRoundedRegion(_cameraPreview, 10);

            var sep = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = TG.Divider };
            host.Controls.Add(_cameraPreview);
            host.Controls.Add(sep);
            _content.Controls.Add(host);
            ApplyRoundedRegion(_cameraPreview, 10);
            return y + host.Height;
        }

        private static void ApplyRoundedRegion(Control control, int radius)
        {
            if (control.Width <= 0 || control.Height <= 0) return;

            using var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(0, 0, d, d, 180, 90);
            path.AddArc(control.Width - d, 0, d, d, 270, 90);
            path.AddArc(control.Width - d, control.Height - d, d, d, 0, 90);
            path.AddArc(0, control.Height - d, d, d, 90, 90);
            path.CloseFigure();
            control.Region = new Region(path);
        }

        private static void DrawToggle(CheckBox chk, Graphics g)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, chk.Width - 1, chk.Height - 1);
            int r = rect.Height / 2;
            var track = chk.Checked ? TG.CAccent : TG.TextSecondary;

            using var trackBrush = new SolidBrush(track);
            using var thumbBrush = new SolidBrush(TG.WindowBg);

            g.FillEllipse(trackBrush, rect.Left, rect.Top, rect.Height, rect.Height);
            g.FillEllipse(trackBrush, rect.Right - rect.Height, rect.Top, rect.Height, rect.Height);
            g.FillRectangle(trackBrush, rect.Left + r, rect.Top, rect.Width - rect.Height, rect.Height);

            int thumbX = chk.Checked ? rect.Right - rect.Height + 2 : rect.Left + 2;
            g.FillEllipse(thumbBrush, thumbX, rect.Top + 2, rect.Height - 4, rect.Height - 4);
        }

        private void BindSettingsToUI()
        {
            _lblOutputValue.Text = string.IsNullOrWhiteSpace(_settings.OutputDevice) ? "Default" : _settings.OutputDevice;
            _lblInputValue.Text = string.IsNullOrWhiteSpace(_settings.InputDevice) ? "Default" : _settings.InputDevice;
            _lblCallOutputValue.Text = string.IsNullOrWhiteSpace(_settings.CallOutputDevice) ? "Default" : _settings.CallOutputDevice;
            _lblCallInputValue.Text = string.IsNullOrWhiteSpace(_settings.CallInputDevice) ? "Default" : _settings.CallInputDevice;
            _lblCameraValue.Text = string.IsNullOrWhiteSpace(_settings.CameraInputDevice) ? "HD Webcam" : _settings.CameraInputDevice;

            _chkUseSameDevices.Checked = _settings.UseSameDevicesForCalls;
            _chkAcceptCalls.Checked = _settings.AcceptCallsOnThisDevice;

            UpdateCallRowsVisibility();
        }

        private void SaveSettingsFromUI()
        {
            _settings.OutputDevice = _lblOutputValue.Text;
            _settings.InputDevice = _lblInputValue.Text;
            _settings.CallOutputDevice = _lblCallOutputValue.Text;
            _settings.CallInputDevice = _lblCallInputValue.Text;
            _settings.CameraInputDevice = _lblCameraValue.Text;
            _settings.UseSameDevicesForCalls = _chkUseSameDevices.Checked;
            _settings.AcceptCallsOnThisDevice = _chkAcceptCalls.Checked;
            _settings.Save();
        }

        private void OnUseSameChanged(object? sender, EventArgs e)
        {
            UpdateCallRowsVisibility();
            SaveSettingsFromUI();
        }

        private void UpdateCallRowsVisibility()
        {
            bool show = !_chkUseSameDevices.Checked;
            _callOutputRow.Visible = show;
            _callInputRow.Visible = show;
            LayoutRows();
        }

        private void OnChooseOutputDevice()
        {
            var options = GetOutputDevices();
            var picked = ShowDeviceOptionDialog("Output device", options, _lblOutputValue.Text);
            if (picked == null) return;
            _lblOutputValue.Text = picked;
            if (_chkUseSameDevices.Checked) _lblCallOutputValue.Text = picked;
            SaveSettingsFromUI();
        }

        private void OnChooseInputDevice()
        {
            var options = GetInputDevices();
            var picked = ShowDeviceOptionDialog("Input device", options, _lblInputValue.Text);
            if (picked == null) return;
            _lblInputValue.Text = picked;
            if (_chkUseSameDevices.Checked) _lblCallInputValue.Text = picked;
            SaveSettingsFromUI();
        }

        private void OnChooseCallOutputDevice()
        {
            var options = GetOutputDevices();
            var picked = ShowDeviceOptionDialog("Output device", options, _lblCallOutputValue.Text);
            if (picked == null) return;
            _lblCallOutputValue.Text = picked;
            SaveSettingsFromUI();
        }

        private void OnChooseCallInputDevice()
        {
            var options = GetInputDevices();
            var picked = ShowDeviceOptionDialog("Input device", options, _lblCallInputValue.Text);
            if (picked == null) return;
            _lblCallInputValue.Text = picked;
            SaveSettingsFromUI();
        }

        private void OnChooseCameraInputDevice()
        {
            var options = GetCameraDevices(_lblCameraValue.Text);
            var picked = ShowDeviceOptionDialog("Input device", options, _lblCameraValue.Text);
            if (picked == null) return;
            _lblCameraValue.Text = picked;
            SaveSettingsFromUI();
            RestartCameraPreview();
        }

        private static List<string> GetOutputDevices()
        {
            var list = new List<string> { "Default" };
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                foreach (var d in devices)
                {
                    if (!list.Contains(d.FriendlyName)) list.Add(d.FriendlyName);
                }
            }
            catch
            {
                if (!list.Contains("Speakers (Realtek(R) Audio)")) list.Add("Speakers (Realtek(R) Audio)");
                if (!list.Contains("Headphones (AP Pro 2)")) list.Add("Headphones (AP Pro 2)");
            }

            return list;
        }

        private static List<string> GetInputDevices()
        {
            var list = new List<string> { "Default" };
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                foreach (var d in devices)
                {
                    if (!list.Contains(d.FriendlyName)) list.Add(d.FriendlyName);
                }
            }
            catch
            {
                if (!list.Contains("Microphone Array (Intel(R) Smart Sound)")) list.Add("Microphone Array (Intel(R) Smart Sound)");
                if (!list.Contains("Headset (AP Pro 2)")) list.Add("Headset (AP Pro 2)");
            }

            return list;
        }

        private List<string> GetCameraDevices(string currentValue)
        {
            var list = new List<string> { "Default" };

            RefreshCameraDeviceMap();
            list.AddRange(_cameraNameToIndex.Keys);

            if (list.Count == 1) list.Add("Camera 1");

            if (!string.IsNullOrWhiteSpace(currentValue) && !list.Contains(currentValue))
            {
                list.Add(currentValue);
            }

            return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private string? ShowDeviceOptionDialog(string title, List<string> options, string current)
        {
            int rowHeight = 40;
            int visibleRows = Math.Min(6, Math.Max(3, options.Count));
            int listHeight = visibleRows * rowHeight;

            using var dlg = new Form
            {
                Text = title,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                HelpButton = false,
                ShowIcon = false,
                ShowInTaskbar = false,
                ClientSize = new Size(520, 124 + listHeight),
                BackColor = TG.WindowBg,
                Font = new Font("Segoe UI", 10.5f)
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI Semibold", 17f),
                ForeColor = TG.TextPrimary,
                AutoSize = true,
                Location = new Point(28, 22),
                BackColor = Color.Transparent
            };

            var panel = new FlowLayoutPanel
            {
                Location = new Point(28, 68),
                Size = new Size(464, listHeight),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = TG.WindowBg
            };

            var radios = new List<RadioButton>();
            foreach (var opt in options)
            {
                string display = TruncateToFit(opt, new Font("Segoe UI", 11f), 420);
                var rb = new RadioButton
                {
                    Text = display,
                    AutoSize = false,
                    Width = 438,
                    Height = 30,
                    ForeColor = TG.TextPrimary,
                    BackColor = TG.WindowBg,
                    Font = new Font("Segoe UI", 11f),
                    Margin = new Padding(0, 0, 0, 8),
                    Checked = string.Equals(opt, current, StringComparison.OrdinalIgnoreCase),
                    Tag = opt
                };
                radios.Add(rb);
                panel.Controls.Add(rb);
            }

            if (!radios.Any(r => r.Checked) && radios.Count > 0)
                radios[0].Checked = true;

            var btnOk = new LinkLabel
            {
                Text = "OK",
                AutoSize = true,
                LinkBehavior = LinkBehavior.NeverUnderline,
                LinkColor = TG.CAccent,
                ActiveLinkColor = TG.CAccent,
                VisitedLinkColor = TG.CAccent,
                Font = new Font("Segoe UI Semibold", 10.8f),
                Location = new Point(456, 82 + listHeight),
                BackColor = Color.Transparent
            };
            btnOk.Click += (_, __) => dlg.DialogResult = DialogResult.OK;

            dlg.Controls.Add(lblTitle);
            dlg.Controls.Add(panel);
            dlg.Controls.Add(btnOk);

            if (dlg.ShowDialog(this) != DialogResult.OK) return null;
            return radios.FirstOrDefault(r => r.Checked)?.Tag?.ToString();
        }

        private static string TruncateToFit(string text, Font font, int maxWidth)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            if (TextRenderer.MeasureText(text, font).Width <= maxWidth) return text;

            const string ellipsis = "...";
            var value = text;
            while (value.Length > 1)
            {
                value = value[..^1];
                if (TextRenderer.MeasureText(value + ellipsis, font).Width <= maxWidth)
                    return value + ellipsis;
            }

            return ellipsis;
        }

        private void StartCameraPreview()
        {
            StopCameraPreview();

            try
            {
                int cameraIndex = ParseCameraIndexFromMap(_lblCameraValue.Text);
                _capture = new OpenCvSharp.VideoCapture(cameraIndex);

                if (!_capture.IsOpened() && cameraIndex != 0)
                {
                    _capture.Release();
                    _capture.Dispose();
                    _capture = new OpenCvSharp.VideoCapture(0);
                }

                if (_capture.IsOpened())
                {
                    // Lower capture resolution to improve startup/render responsiveness.
                    _capture.Set(OpenCvSharp.VideoCaptureProperties.FrameWidth, 640);
                    _capture.Set(OpenCvSharp.VideoCaptureProperties.FrameHeight, 360);

                    _cameraTimer = new System.Windows.Forms.Timer { Interval = 180 };
                    _cameraTimer.Tick += CameraTimer_Tick;
                    _cameraTimer.Start();
                    return;
                }
            }
            catch
            {
                // ignore fallback below
            }

            ShowCameraFallback();
        }

        private void RestartCameraPreview()
        {
            StartCameraPreview();
        }

        private int ParseCameraIndexFromMap(string cameraValue)
        {
            if (string.IsNullOrWhiteSpace(cameraValue) ||
                string.Equals(cameraValue, "Default", StringComparison.OrdinalIgnoreCase))
                return 0;

            if (_cameraNameToIndex.TryGetValue(cameraValue, out var mapped))
                return mapped;

            var digits = new string(cameraValue.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var oneBased) && oneBased > 0)
                return Math.Max(0, oneBased - 1);

            return 0;
        }

        private void RefreshCameraDeviceMap()
        {
            _cameraNameToIndex.Clear();

            var friendlyNames = QueryFriendlyCameraNames();
            int friendlyCursor = 0;

            for (int i = 0; i < 8; i++)
            {
                try
                {
                    using var probe = new OpenCvSharp.VideoCapture(i);
                    if (!probe.IsOpened()) continue;

                    string label;
                    if (friendlyCursor < friendlyNames.Count)
                    {
                        label = friendlyNames[friendlyCursor++];
                    }
                    else
                    {
                        label = $"Camera {i + 1}";
                    }

                    if (_cameraNameToIndex.ContainsKey(label))
                        label = $"{label} ({i + 1})";

                    _cameraNameToIndex[label] = i;
                    probe.Release();
                }
                catch
                {
                    // ignore probe failures
                }
            }
        }

        private static List<string> QueryFriendlyCameraNames()
        {
            var names = new List<string>();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name FROM Win32_PnPEntity WHERE PNPClass = 'Image'");

                foreach (ManagementObject obj in searcher.Get())
                {
                    var name = obj["Name"]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name))
                        names.Add(name);
                }
            }
            catch
            {
                // fallback handled by probe labels
            }

            return names;
        }

        private void CameraTimer_Tick(object? sender, EventArgs e)
        {
            if (_isRenderingFrame) return;
            if (_capture == null || !_capture.IsOpened())
            {
                ShowCameraFallback();
                return;
            }

            try
            {
                _isRenderingFrame = true;
                using var frame = new OpenCvSharp.Mat();
                if (!_capture.Read(frame) || frame.Empty()) return;

                byte[] bytes = frame.ToBytes(".bmp");
                using var ms = new MemoryStream(bytes);
                using var bmp = new Bitmap(ms);

                var old = _cameraPreview.Image;
                _cameraPreview.Image = new Bitmap(bmp);
                old?.Dispose();
            }
            catch
            {
                ShowCameraFallback();
            }
            finally
            {
                _isRenderingFrame = false;
            }
        }

        private void ShowCameraFallback()
        {
            var bmp = new Bitmap(Math.Max(320, _cameraPreview.Width), Math.Max(180, _cameraPreview.Height));
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(0xE9, 0xEF, 0xF5));
                using var brush = new SolidBrush(Color.FromArgb(0x96, 0xA2, 0xAF));
                using var f = new Font("Segoe UI", 11f);
                var text = "Camera preview unavailable";
                var sz = g.MeasureString(text, f);
                g.DrawString(text, f, brush, (bmp.Width - sz.Width) / 2f, (bmp.Height - sz.Height) / 2f);
            }

            var old = _cameraPreview.Image;
            _cameraPreview.Image = bmp;
            old?.Dispose();
        }

        private void StopCameraPreview()
        {
            try
            {
                if (_cameraTimer != null)
                {
                    _cameraTimer.Stop();
                    _cameraTimer.Tick -= CameraTimer_Tick;
                    _cameraTimer.Dispose();
                }

                _capture?.Release();
                _capture?.Dispose();
                _capture = null;
                _isRenderingFrame = false;

                _cameraPreview?.Image?.Dispose();
                _cameraPreview.Image = null;
            }
            catch
            {
                // ignore
            }
        }

        private sealed class SpeakersCameraSettings
        {
            private const string FileName = "speakerscamera.config";

            public string OutputDevice { get; set; } = "Default";
            public string InputDevice { get; set; } = "Default";
            public bool UseSameDevicesForCalls { get; set; } = true;
            public string CallOutputDevice { get; set; } = "Default";
            public string CallInputDevice { get; set; } = "Default";
            public string CameraInputDevice { get; set; } = "HD Webcam";
            public bool AcceptCallsOnThisDevice { get; set; } = true;

            public static SpeakersCameraSettings Load()
            {
                var s = new SpeakersCameraSettings();
                try
                {
                    var path = Path.Combine(AppContext.BaseDirectory, FileName);
                    if (!File.Exists(path)) return s;

                    var text = File.ReadAllText(path, Encoding.UTF8);
                    var parts = text.Contains('\u001F') ? text.Split('\u001F') : text.Split('|');
                    if (parts.Length >= 7)
                    {
                        s.OutputDevice = parts[0];
                        s.InputDevice = parts[1];
                        if (bool.TryParse(parts[2], out var b1)) s.UseSameDevicesForCalls = b1;
                        s.CallOutputDevice = parts[3];
                        s.CallInputDevice = parts[4];
                        s.CameraInputDevice = parts[5];
                        if (bool.TryParse(parts[6], out var b2)) s.AcceptCallsOnThisDevice = b2;
                    }
                }
                catch
                {
                    // ignore and use defaults
                }

                return s;
            }

            public void Save()
            {
                try
                {
                    var path = Path.Combine(AppContext.BaseDirectory, FileName);
                    var data = string.Join("\u001F",
                        OutputDevice,
                        InputDevice,
                        UseSameDevicesForCalls,
                        CallOutputDevice,
                        CallInputDevice,
                        CameraInputDevice,
                        AcceptCallsOnThisDevice);
                    File.WriteAllText(path, data, Encoding.UTF8);
                }
                catch
                {
                    // ignore
                }
            }
        }
        private void OnThemeChanged()
        {
            if (InvokeRequired) { Invoke(new Action(OnThemeChanged)); return; }
            BackColor = TG.WindowBg;
            Invalidate(true);
            ApplyThemeToControls(Controls);
        }

        private static void ApplyThemeToControls(System.Windows.Forms.Control.ControlCollection controls)
        {
            foreach (Control c in controls)
            {
                if (c.BackColor != Color.Transparent &&
                    c.BackColor != TG.Blue &&
                    c.BackColor != TG.SidebarActive &&
                    c.BackColor != TG.TitleBarBg &&
                    c.Tag as string != "accent")
                    c.BackColor = TG.WindowBg;
                if (c.ForeColor != Color.White && c.Tag as string != "white-fg")
                    c.ForeColor = TG.TextPrimary;
                c.Invalidate();
                ApplyThemeToControls(c.Controls);
            }
        }

    }
}
