using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace NaiwaDesktopPet
{
    internal static class Program
    {
        internal static bool UITest;
        internal static readonly string DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NaiwaDesktopPet");
        private const string InstanceName = "Local\\NaiwaDesktopPet.Instance.v1";
        private const string ExitName = "Local\\NaiwaDesktopPet.Exit.v1";

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                UITest = Array.IndexOf(args, "--ui-test") >= 0;
                Native.SetProcessDPIAware();
                if (Array.IndexOf(args, "--exit") >= 0)
                {
                    try { using (EventWaitHandle existing = EventWaitHandle.OpenExisting(ExitName)) { existing.Set(); } }
                    catch (WaitHandleCannotBeOpenedException) { }
                    return 0;
                }
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { Log(e.Exception); };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e) { Log(e.ExceptionObject as Exception); };
                string root = AppDomain.CurrentDomain.BaseDirectory;
                string diagnosticPath = ArgumentValue(args, "--diagnostics");
                bool selfTest = Array.IndexOf(args, "--self-test") >= 0;
                if (diagnosticPath != null || selfTest)
                {
                    if (diagnosticPath == null) diagnosticPath = ArgumentValue(args, "--self-test");
                    if (String.IsNullOrEmpty(diagnosticPath)) diagnosticPath = Path.Combine(root, "diagnostics.json");
                    using (PetAssets assets = new PetAssets(root)) { assets.WriteDiagnostics(diagnosticPath); }
                    return 0;
                }
                bool created;
                using (Mutex mutex = new Mutex(true, InstanceName, out created))
                {
                    if (!created) return 0;
                    try
                    {
                        using (EventWaitHandle exit = new EventWaitHandle(false, EventResetMode.AutoReset, ExitName))
                        using (PetAssets assets = new PetAssets(root))
                        {
                            Application.EnableVisualStyles();
                            Application.SetCompatibleTextRenderingDefault(false);
                            string timingPath = ArgumentValue(args, "--timing-test");
                            if (timingPath == "") timingPath = Path.Combine(root, "timing-test.json");
                            string burstPath = ArgumentValue(args, "--burst-test");
                            if (burstPath == "") burstPath = Path.Combine(root, "burst-test.json");
                            using (PetContext context = new PetContext(assets, exit, ArgumentValue(args, "--smoke-test"), timingPath, burstPath, Array.IndexOf(args, "--burst-demo") >= 0))
                            { Application.Run(context); }
                        }
                    }
                    finally { mutex.ReleaseMutex(); }
                }
                return 0;
            }
            catch (Exception ex)
            {
                Log(ex);
                string output = ArgumentValue(args, "--burst-test") ?? ArgumentValue(args, "--timing-test") ?? ArgumentValue(args, "--smoke-test") ?? ArgumentValue(args, "--diagnostics") ?? ArgumentValue(args, "--self-test");
                if (!String.IsNullOrEmpty(output)) WriteText(output, "{\"ok\":false,\"error\":" + Json(ex.ToString()) + "}");
                else MessageBox.Show("桌宠启动失败：\n" + ex.Message + "\n\n日志：" + Path.Combine(DataDirectory, "error.log"), "奶龙桌宠", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        internal static string ArgumentValue(string[] args, string key)
        {
            int i = Array.IndexOf(args, key);
            if (i < 0) return null;
            return i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[i + 1] : "";
        }
        internal static void Log(Exception ex)
        {
            if (ex == null) return;
            try { Directory.CreateDirectory(DataDirectory); File.AppendAllText(Path.Combine(DataDirectory, "error.log"), DateTime.Now.ToString("s") + " " + ex + Environment.NewLine, Encoding.UTF8); }
            catch { }
        }
        internal static string Json(string s)
        {
            if (s == null) return "null";
            StringBuilder b = new StringBuilder("\"");
            foreach (char c in s)
            {
                if (c == '"') b.Append("\\\"");
                else if (c == '\\') b.Append("\\\\");
                else if (c == '\n') b.Append("\\n");
                else if (c == '\r') b.Append("\\r");
                else if (c == '\t') b.Append("\\t");
                else if (c < 32) b.Append("\\u" + ((int)c).ToString("x4"));
                else b.Append(c);
            }
            return b.Append('"').ToString();
        }
        internal static void WriteText(string path, string value)
        {
            path = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(path);
            if (!String.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, value, new UTF8Encoding(false));
        }
    }

    internal sealed class Settings
    {
        internal int Height = 170;
        internal bool TopMost = true;
        internal bool Wander = true;
        internal bool Muted = true;
        internal int X = Int32.MinValue;
        internal int Y = Int32.MinValue;
        internal static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                string path = Path.Combine(Program.DataDirectory, "settings.ini");
                if (!File.Exists(path)) return s;
                foreach (string line in File.ReadAllLines(path))
                {
                    string[] pair = line.Split(new char[] { '=' }, 2);
                    if (pair.Length != 2) continue;
                    int value;
                    if (!Int32.TryParse(pair[1], out value)) continue;
                    if (pair[0] == "height" && value >= 120 && value <= 230) s.Height = value;
                    if (pair[0] == "topmost") s.TopMost = value != 0;
                    if (pair[0] == "wander") s.Wander = value != 0;
                    if (pair[0] == "muted") s.Muted = value != 0;
                    if (pair[0] == "x") s.X = value;
                    if (pair[0] == "y") s.Y = value;
                }
            }
            catch (Exception ex) { Program.Log(ex); }
            return s;
        }
        internal void Save()
        {
            try
            {
                Directory.CreateDirectory(Program.DataDirectory);
                string content = "height=" + Height + "\ntopmost=" + (TopMost ? 1 : 0) + "\nwander=" + (Wander ? 1 : 0) + "\nmuted=" + (Muted ? 1 : 0) + "\nx=" + X + "\ny=" + Y + "\n";
                Program.WriteText(Path.Combine(Program.DataDirectory, "settings.ini"), content);
            }
            catch (Exception ex) { Program.Log(ex); }
        }
    }

    internal sealed class PetAssets : IDisposable
    {
        internal static readonly string[] Actions = { "idle", "walk", "laugh", "wave", "sleep", "dragged", "run", "near" };
        private readonly Dictionary<string, List<Bitmap>> originals = new Dictionary<string, List<Bitmap>>();
        private readonly Dictionary<string, Bitmap> renderCache = new Dictionary<string, Bitmap>();
        private readonly Dictionary<string, PointerSilhouette> pointerShapes = new Dictionary<string, PointerSilhouette>();
        private readonly Dictionary<string, double> frameSeconds = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private readonly string animationDirectory;
        internal readonly string LaughSoundPath;
        internal readonly double LaughSoundSeconds;
        internal readonly string TimingConfiguration;
        internal PetAssets(string root)
        {
            animationDirectory = Path.Combine(root, "assets", "animations");
            TimingConfiguration = Path.Combine(root, "assets", "animation.ini");
            try
            {
                foreach (string action in Actions) frameSeconds[action] = action == "run" ? 1.0 / 30 : (action == "sleep" ? 0.24 : (action == "walk" || action == "laugh" ? 0.105 : 0.14));
                LoadTimings(TimingConfiguration);
                foreach (string action in Actions)
                {
                    string directory = Path.Combine(animationDirectory, action);
                    if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("缺少动作资源：" + directory);
                    string[] paths = Directory.GetFiles(directory, "*.png");
                    Array.Sort(paths, delegate(string a, string b)
                    {
                        int ai, bi;
                        if (Int32.TryParse(Path.GetFileNameWithoutExtension(a), out ai) && Int32.TryParse(Path.GetFileNameWithoutExtension(b), out bi)) return ai.CompareTo(bi);
                        return StringComparer.OrdinalIgnoreCase.Compare(a, b);
                    });
                    if (paths.Length == 0) throw new InvalidDataException("动作文件夹中没有 PNG：" + directory);
                    List<Bitmap> frames = new List<Bitmap>();
                    originals.Add(action, frames);
                    foreach (string path in paths)
                    {
                        using (Image image = Image.FromFile(path))
                        {
                            if (image.Width < 1 || image.Height < 1) throw new InvalidDataException("无效图片：" + path);
                            Bitmap frame = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppPArgb);
                            using (Graphics graphics = Graphics.FromImage(frame))
                            { graphics.CompositingMode = CompositingMode.SourceCopy; graphics.DrawImage(image, 0, 0, image.Width, image.Height); }
                            frames.Add(frame);
                        }
                    }
                }
                string soundPath = Path.Combine(root, "assets", "sounds", "laugh.wav");
                if (File.Exists(soundPath)) { LaughSoundPath = soundPath; LaughSoundSeconds = ReadWaveSeconds(soundPath); }
            }
            catch { Dispose(); throw; }
        }
        internal int Count(string action) { return originals[action].Count; }
        private void LoadTimings(string path)
        {
            if (!File.Exists(path)) return;
            string section = "";
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim().TrimStart('\ufeff');
                if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;
                if (line[0] == '[' && line.EndsWith("]", StringComparison.Ordinal)) { section = line.Substring(1, line.Length - 2).Trim(); continue; }
                string[] pair = line.Split(new char[] { '=' }, 2);
                if (pair.Length != 2) continue;
                string key = pair[0].Trim();
                string action = key.Equals("fps", StringComparison.OrdinalIgnoreCase) ? section : key;
                if (action.EndsWith(".fps", StringComparison.OrdinalIgnoreCase)) action = action.Substring(0, action.Length - 4);
                string value = pair[1].Split(new char[] { ';', '#' }, 2)[0].Trim();
                double fps;
                if (frameSeconds.ContainsKey(action) && Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fps) && fps >= 0.1 && fps <= 240 && !Double.IsNaN(fps) && !Double.IsInfinity(fps)) frameSeconds[action] = 1.0 / fps;
            }
        }
        internal double FrameSeconds(string action) { return frameSeconds[action]; }
        internal double Duration(string action) { return Count(action) * FrameSeconds(action); }
        internal int FrameIndex(string action, double seconds, bool loop)
        {
            int count = Count(action);
            long frame = (long)Math.Floor(Math.Max(0, seconds) / FrameSeconds(action) + 1e-9);
            return loop ? (int)(frame % count) : (int)Math.Min(count - 1, frame);
        }
        private static double ReadWaveSeconds(string path)
        {
            try
            {
                using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
                {
                    if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF") return 0;
                    reader.ReadUInt32();
                    if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE") return 0;
                    uint byteRate = 0; long dataBytes = 0;
                    while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
                    {
                        string chunk = Encoding.ASCII.GetString(reader.ReadBytes(4));
                        uint size = reader.ReadUInt32(); long start = reader.BaseStream.Position;
                        if (chunk == "fmt " && size >= 16)
                        { reader.ReadUInt16(); reader.ReadUInt16(); reader.ReadUInt32(); byteRate = reader.ReadUInt32(); }
                        if (chunk == "data") dataBytes += Math.Min((long)size, reader.BaseStream.Length - start);
                        reader.BaseStream.Position = Math.Min(reader.BaseStream.Length, start + size + (size & 1));
                    }
                    return byteRate == 0 ? 0 : (double)dataBytes / byteRate;
                }
            }
            catch (Exception ex) { Program.Log(ex); return 0; }
        }
        internal static bool HasVisiblePixels(Bitmap frame)
        {
            BitmapData data = frame.LockBits(new Rectangle(0, 0, frame.Width, frame.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                byte[] row = new byte[frame.Width * 4];
                for (int y = 0; y < frame.Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                    for (int x = 3; x < row.Length; x += 4) if (row[x] > 0) return true;
                }
                return false;
            }
            finally { frame.UnlockBits(data); }
        }
        internal Bitmap Frame(string action, int frame, int height, bool mirror)
        {
            frame = Math.Max(0, Math.Min(Count(action) - 1, frame));
            string key = action + ":" + frame + ":" + height + ":" + mirror;
            Bitmap output;
            if (renderCache.TryGetValue(key, out output)) return output;
            Bitmap source = originals[action][frame];
            int width = (int)Math.Round(height * 192.0 / 208.0);
            output = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            using (Graphics graphics = Graphics.FromImage(output))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float scale = Math.Min((float)width / source.Width, (float)height / source.Height);
                float w = source.Width * scale;
                float h = source.Height * scale;
                graphics.DrawImage(source, new RectangleF((width - w) / 2, height - h, w, h), new RectangleF(0, 0, source.Width, source.Height), GraphicsUnit.Pixel);
            }
            if (mirror) output.RotateFlip(RotateFlipType.RotateNoneFlipX);
            renderCache.Add(key, output);
            return output;
        }
        internal PointerSilhouette PointerShape(int height, bool mirror)
        {
            string key = height + ":" + mirror;
            PointerSilhouette shape;
            if (!pointerShapes.TryGetValue(key, out shape))
            {
                // Use both silhouettes so entering the raised hand cancels the pose
                // without oscillating when that hand disappears on returning to idle.
                shape = new PointerSilhouette(Frame("idle", 0, height, mirror), Frame("near", 0, height, mirror));
                pointerShapes.Add(key, shape);
            }
            return shape;
        }
        internal void WriteDiagnostics(string output)
        {
            bool ok = true;
            StringBuilder b = new StringBuilder("{\"ok\":");
            StringBuilder actions = new StringBuilder();
            bool first = true;
            foreach (string action in Actions)
            {
                long transparent = 0, visible = 0, partial = 0;
                int minWidth = Int32.MaxValue, maxWidth = 0, minHeight = Int32.MaxValue, maxHeight = 0;
                foreach (Bitmap frame in originals[action])
                {
                    minWidth = Math.Min(minWidth, frame.Width); maxWidth = Math.Max(maxWidth, frame.Width);
                    minHeight = Math.Min(minHeight, frame.Height); maxHeight = Math.Max(maxHeight, frame.Height);
                    BitmapData data = frame.LockBits(new Rectangle(0, 0, frame.Width, frame.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                    try
                    {
                        byte[] row = new byte[frame.Width * 4];
                        for (int y = 0; y < frame.Height; y++)
                        {
                            Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                            for (int x = 3; x < row.Length; x += 4)
                            { if (row[x] == 0) transparent++; else { visible++; if (row[x] < 255) partial++; } }
                        }
                    }
                    finally { frame.UnlockBits(data); }
                }
                bool valid = transparent > 0 && visible > 0;
                ok = ok && valid;
                if (!first) actions.Append(','); first = false;
                actions.Append("{\"action\":").Append(Program.Json(action)).Append(",\"frames\":").Append(Count(action))
                    .Append(",\"fps\":").Append((1.0 / FrameSeconds(action)).ToString("R", CultureInfo.InvariantCulture))
                    .Append(",\"durationSeconds\":").Append(Duration(action).ToString("R", CultureInfo.InvariantCulture))
                    .Append(",\"minWidth\":").Append(minWidth).Append(",\"maxWidth\":").Append(maxWidth)
                    .Append(",\"minHeight\":").Append(minHeight).Append(",\"maxHeight\":").Append(maxHeight)
                    .Append(",\"transparentPixels\":").Append(transparent).Append(",\"visiblePixels\":").Append(visible)
                    .Append(",\"partialAlphaPixels\":").Append(partial).Append(",\"alphaValid\":").Append(valid ? "true" : "false").Append('}');
                Frame(action, 0, 120, false); Frame(action, 0, 170, false); Frame(action, 0, 230, true);
            }
            b.Append(ok ? "true" : "false").Append(",\"resourceRoot\":").Append(Program.Json(animationDirectory))
                .Append(",\"canvas\":{\"width\":192,\"height\":208},\"defaultHeight\":170,\"maxPets\":null,\"petLimitEnabled\":false,\"actions\":[")
                .Append(actions).Append("],\"timingConfiguration\":").Append(Program.Json(TimingConfiguration))
                .Append(",\"laughSoundSeconds\":").Append(LaughSoundSeconds.ToString("R", CultureInfo.InvariantCulture)).Append('}');
            Program.WriteText(output, b.ToString());
        }
        public void Dispose()
        {
            foreach (Bitmap frame in renderCache.Values) frame.Dispose();
            renderCache.Clear();
            pointerShapes.Clear();
            foreach (List<Bitmap> frames in originals.Values) foreach (Bitmap frame in frames) frame.Dispose();
            originals.Clear();
        }
    }

    internal sealed class PointerSilhouette
    {
        private readonly bool[,] opaque;
        private readonly List<Point> outline = new List<Point>();
        internal PointerSilhouette(Bitmap idle, Bitmap near)
        {
            opaque = new bool[idle.Width, idle.Height];
            for (int y = 0; y < idle.Height; y++)
                for (int x = 0; x < idle.Width; x++)
                    opaque[x, y] = idle.GetPixel(x, y).A >= 12 || near.GetPixel(x, y).A >= 12;
            for (int y = 0; y < idle.Height; y++)
                for (int x = 0; x < idle.Width; x++)
                    if (opaque[x, y] && (!Contains(new Point(x - 1, y)) || !Contains(new Point(x + 1, y)) ||
                        !Contains(new Point(x, y - 1)) || !Contains(new Point(x, y + 1)))) outline.Add(new Point(x, y));
        }
        internal bool Contains(Point p)
        {
            return p.X >= 0 && p.Y >= 0 && p.X < opaque.GetLength(0) && p.Y < opaque.GetLength(1) && opaque[p.X, p.Y];
        }
        internal double Distance(Point p)
        {
            if (Contains(p)) return 0;
            double closest = Double.MaxValue;
            foreach (Point edge in outline)
            {
                double dx = (double)p.X - edge.X, dy = (double)p.Y - edge.Y;
                closest = Math.Min(closest, dx * dx + dy * dy);
            }
            return Math.Sqrt(closest);
        }
    }

    // Each pet owns a separate MCI waveaudio device. SoundPlayer/PlaySound is process-wide
    // and would truncate another pet's laugh when starting or stopping a new one.
    internal sealed class PetAudio : IDisposable
    {
        private string alias;
        internal bool Started;
        internal string LastError;
        internal bool IsOpen { get { return alias != null; } }
        internal bool IsPlaying
        {
            get
            {
                if (alias == null) return false;
                StringBuilder result = new StringBuilder(64);
                return Native.MciSendString("status " + alias + " mode", result, result.Capacity, IntPtr.Zero) == 0 && result.ToString().Trim().Equals("playing", StringComparison.OrdinalIgnoreCase);
            }
        }
        internal void Play(string path)
        {
            Stop(); Started = false; LastError = null;
            if (String.IsNullOrEmpty(path)) return;
            alias = "naiwa_" + Guid.NewGuid().ToString("N");
            try
            {
                Command("open \"" + path + "\" type waveaudio alias " + alias);
                Command("set " + alias + " time format milliseconds");
                Command("play " + alias + " from 0");
                Started = true;
            }
            catch (Exception ex) { LastError = ex.Message; Program.Log(ex); Stop(); }
        }
        private static void Command(string command)
        {
            uint error = Native.MciSendString(command, null, 0, IntPtr.Zero);
            if (error == 0) return;
            StringBuilder message = new StringBuilder(256); Native.MciGetErrorString(error, message, message.Capacity);
            throw new InvalidOperationException("音频播放失败 (" + error + ")：" + message);
        }
        internal void Stop()
        {
            if (alias == null) return;
            Native.MciSendString("stop " + alias, null, 0, IntPtr.Zero);
            Native.MciSendString("close " + alias, null, 0, IntPtr.Zero);
            alias = null;
        }
        public void Dispose() { Stop(); }
    }

    internal sealed class MenuToggleControl : CheckBox
    {
        private readonly string onState;
        private readonly string offState;
        private int menuTextLeft = -1;
        private TextFormatFlags labelTextFormat = TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
        internal MenuToggleControl(string label, string enabledState, string disabledState)
        {
            AutoSize = false;
            Margin = Padding.Empty;
            Text = label;
            onState = enabledState; offState = disabledState;
            Size = GetPreferredSize(Size.Empty);
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.CheckButton;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        internal void ShowCurrentState(bool enabled)
        {
            Checked = enabled;
            AccessibleName = Text + "开关，" + (Checked ? onState : offState);
            AccessibleDescription = "绿色表示开启，灰色表示关闭。点击或按空格切换。";
            Invalidate();
        }
        internal void AlignLabelToMenuText(int textLeft, TextFormatFlags format)
        {
            if (menuTextLeft == textLeft && labelTextFormat == format) return;
            menuTextLeft = textLeft; labelTextFormat = format;
            Invalidate();
        }
        private int LabelWidth
        {
            get { return TextRenderer.MeasureText(Text ?? String.Empty, Font, Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width; }
        }
        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            Size = GetPreferredSize(Size.Empty);
            Invalidate();
        }
        public override Size GetPreferredSize(Size proposedSize)
        {
            return new Size(10 + LabelWidth + 8 + 50 + 6, 28);
        }
        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);
            AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
            Invalidate();
        }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                OnClick(EventArgs.Empty); e.Handled = true; e.SuppressKeyPress = true;
                return;
            }
            base.OnKeyDown(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            bool highContrast = SystemInformation.HighContrast;
            ToolStrip strip = Parent as ToolStrip;
            ToolStripProfessionalRenderer renderer = strip == null ? null : strip.Renderer as ToolStripProfessionalRenderer;
            Color background = highContrast ? SystemColors.Menu :
                (renderer != null ? renderer.ColorTable.ToolStripDropDownBackground :
                (Parent != null ? Parent.BackColor : SystemColors.Menu));
            g.Clear(background);
            Rectangle track = new Rectangle(Width - 50 - 6, (Height - 22) / 2, 50, 22);
            int labelLeft = menuTextLeft < 0 ? 10 : Math.Max(0, menuTextLeft - Left);
            TextRenderer.DrawText(g, Text, Font,
                new Rectangle(labelLeft, 0, Math.Max(0, track.Left - 6 - labelLeft), Height),
                SystemColors.MenuText, labelTextFormat);
            Color trackColor = Checked ? (highContrast ? SystemColors.Highlight : Color.FromArgb(37, 156, 109)) :
                (highContrast ? SystemColors.GrayText : Color.FromArgb(161, 170, 183));
            using (GraphicsPath path = new GraphicsPath())
            using (Brush brush = new SolidBrush(trackColor))
            {
                path.AddArc(track.Left, track.Top, track.Height, track.Height, 90, 180);
                path.AddArc(track.Right - track.Height, track.Top, track.Height, track.Height, 270, 180);
                path.CloseFigure(); g.FillPath(brush, path);
            }
            int knobSize = track.Height - 6;
            int knobX = Checked ? track.Right - 3 - knobSize : track.Left + 3;
            using (Brush brush = new SolidBrush(Color.White)) g.FillEllipse(brush, knobX, track.Top + 3, knobSize, knobSize);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, new Rectangle(3, 3, Width - 7, Height - 7));
        }
    }

    internal sealed class MenuToggleHost : ToolStripControlHost
    {
        private ToolStrip observedOwner;
        private bool arranging;
        internal MenuToggleHost(MenuToggleControl control) : base(control)
        {
            AutoSize = false;
            Size = control.GetPreferredSize(Size.Empty);
            Margin = new Padding(0, 1, 0, 1); Padding = Padding.Empty;
            control.FontChanged += delegate { Size = control.GetPreferredSize(Size.Empty); FitControlToRow(); };
        }
        protected override void OnHostedControlResize(EventArgs e) { FitControlToRow(); }
        protected override void OnBoundsChanged() { base.OnBoundsChanged(); FitControlToRow(); }
        protected override void OnOwnerChanged(EventArgs e)
        {
            if (observedOwner != null) observedOwner.SizeChanged -= OwnerSizeChanged;
            base.OnOwnerChanged(e);
            observedOwner = Owner;
            if (observedOwner != null) observedOwner.SizeChanged += OwnerSizeChanged;
            FitControlToRow();
        }
        private void OwnerSizeChanged(object sender, EventArgs e) { FitControlToRow(); }
        private void FitControlToRow()
        {
            if (arranging || Owner == null || Control == null || Control.IsDisposed) return;
            Rectangle row = new Rectangle(Owner.DisplayRectangle.Left, Bounds.Top,
                Owner.DisplayRectangle.Width, Bounds.Height);
            if (row.Width <= 0 || Control.Bounds == row) return;
            arranging = true;
            try { Control.Bounds = row; }
            finally { arranging = false; }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && observedOwner != null) observedOwner.SizeChanged -= OwnerSizeChanged;
            base.Dispose(disposing);
        }
    }

    internal sealed class PetContext : ApplicationContext
    {
        internal readonly PetAssets Assets;
        internal readonly Settings Options;
        internal readonly Random Random = new Random();
        internal readonly List<PetForm> Pets = new List<PetForm>();
        private readonly List<MenuToggleControl> soundSwitches = new List<MenuToggleControl>();
        private readonly List<MenuToggleControl> topmostSwitches = new List<MenuToggleControl>();
        private readonly EventWaitHandle exitEvent;
        private readonly System.Windows.Forms.Timer timer;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly NotifyIcon tray;
        private readonly Icon trayIcon;
        private readonly ContextMenuStrip trayMenu;
        private double lastTime;
        private bool shuttingDown;
        private readonly string smokeOutput;
        private readonly List<string> smokeResults = new List<string>();
        private int smokeStep;
        private double nextSmoke;
        private PetForm smokePet;
        private Point smokeStart;
        private readonly string timingOutput;
        private PetForm timingPet;
        private PetForm timingOtherPet;
        private readonly List<string> timingChecks = new List<string>();
        private bool timingStarted;
        private bool timingSecondaryStarted;
        private bool timingSecondaryStopped;
        private bool timingFrameValid = true;
        private bool timingStayedLaughing = true;
        private int timingSamples;
        private int timingLastFrame = -1;
        private bool timingMonotonic = true;
        private double timingStart;
        private double timingLastTick;
        private double timingMaxGap;
        private readonly string burstOutput;
        private readonly bool burstDemo;
        private bool burstDemoStarted;
        private bool burstDemoFired;
        private double burstDemoAt;
        private int nextPetId;
        private int burstTestStage;
        private double burstTestAt = 0.25;
        private double burstTestStartedAt;
        private PetForm burstTestParent;
        private PetForm burstDragPet;
        private PetForm burstResizePet;
        private Point burstDragStopped;
        private Point burstResizeStopped;
        private Point burstParentPosition;
        private readonly List<PetForm> burstTestChildren = new List<PetForm>();
        private readonly List<string> burstChecks = new List<string>();
        private readonly List<string> burstSamples = new List<string>();
        private readonly Dictionary<int, Point> burstStoppedPositions = new Dictionary<int, Point>();
        private bool burstAllWithinBounds = true;
        private bool burstObservedMovement;
        private readonly bool proximityTest;
        internal PetForm LastPet;
        internal PetContext(PetAssets assets, EventWaitHandle signal, string smokePath, string timingPath, string burstPath, bool showBurstDemo, bool testProximity = false)
        {
            Assets = assets;
            proximityTest = testProximity;
            Options = smokePath == null && timingPath == null && burstPath == null && !proximityTest ? Settings.Load() : new Settings();
            if (proximityTest) { Options.Wander = false; Options.Muted = true; }
            exitEvent = signal;
            smokeOutput = smokePath;
            timingOutput = timingPath;
            burstOutput = burstPath;
            burstDemo = showBurstDemo;
            if (timingOutput != null) { Options.Wander = false; Options.Muted = false; }
            if (burstOutput != null) { Options.Wander = false; Options.Muted = true; Options.Height = 230; Random = new Random(20261024); }
            if (burstDemo) Options.Wander = false;
            trayIcon = CreateTrayIcon(assets.Frame("idle", 0, 120, false));
            trayMenu = BuildMenu(null);
            tray = new NotifyIcon();
            tray.Icon = trayIcon;
            tray.Text = String.Empty;
            tray.ContextMenuStrip = trayMenu;
            tray.DoubleClick += delegate { Spawn(LastPet, true); };
            tray.Visible = true;
            PetForm pet = Spawn(null, false);
            if (smokeOutput != null) { smokePet = pet; nextSmoke = 0.8; }
            if (timingOutput != null) timingPet = pet;
            if (burstOutput != null || burstDemo) burstTestParent = pet;
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 25;
            timer.Tick += Tick;
            timer.Start();
        }
        private static Icon CreateTrayIcon(Bitmap image)
        {
            using (Bitmap small = new Bitmap(32, 32, PixelFormat.Format32bppPArgb))
            {
                using (Graphics graphics = Graphics.FromImage(small)) { graphics.DrawImage(image, new Rectangle(1, 0, 30, 32)); }
                IntPtr hicon = small.GetHicon();
                try { using (Icon temp = Icon.FromHandle(hicon)) { return (Icon)temp.Clone(); } }
                finally { Native.DestroyIcon(hicon); }
            }
        }
        internal PetForm Spawn(PetForm parent, bool animate)
        {
            return SpawnBurst(parent, 0, Now);
        }
        private PetForm SpawnBurst(PetForm parent, double delay, double start, int targetRegion = -1)
        {
            if (shuttingDown) return null;
            int height = parent == null ? Options.Height : 230;
            Point point;
            if (parent != null)
            {
                int width = (int)Math.Round(height * 192.0 / 208.0);
                point = new Point(parent.Left + (parent.Width - width) / 2, parent.Top + (parent.Height - height) / 2);
            }
            else
            {
                Rectangle work = Screen.PrimaryScreen.WorkingArea;
                point = Options.X == Int32.MinValue ? new Point(work.Right - 245, work.Bottom - height - 14) : new Point(Options.X, Options.Y);
            }
            PetForm pet = new PetForm(this, height, point, ++nextPetId);
            if (parent != null)
            {
                pet.InheritPose(parent);
                Rectangle work = Screen.FromRectangle(parent.Bounds).WorkingArea;
                point = new Point(Math.Max(work.Left, Math.Min(work.Right - pet.Width, point.X)),
                    Math.Max(work.Top, Math.Min(work.Bottom - pet.Height, point.Y)));
                Point target = ChooseBurstTarget(pet, work, targetRegion < 0 ? Random.Next(4) : targetRegion);
                double distance = Math.Sqrt((target.X - point.X) * (double)(target.X - point.X) +
                    (target.Y - point.Y) * (double)(target.Y - point.Y));
                double duration = 1.25 + Random.NextDouble() * 0.45 + Math.Min(0.65, distance / 1500.0);
                pet.BeginBurst(point, new Point(parent.Left + parent.Width / 2, parent.Top + parent.Height / 2),
                    work, target, duration, start + delay);
                parent.HoldWanderUntil(start + delay + 3.5);
            }
            Pets.Add(pet); LastPet = parent ?? pet;
            if (parent == null || delay == 0) pet.Show();
            else { IntPtr handle = pet.Handle; }
            pet.ClampToWorkArea(); pet.Render();
            return pet;
        }
        private Point ChooseBurstTarget(PetForm child, Rectangle work, int region)
        {
            int availableWidth = Math.Max(0, work.Width - child.Width);
            int availableHeight = Math.Max(0, work.Height - child.Height);
            int insetX = Math.Min(12, availableWidth / 8), insetY = Math.Min(12, availableHeight / 8);
            double left = work.Left + insetX, right = work.Left + availableWidth - insetX;
            double top = work.Top + insetY, bottom = work.Top + availableHeight - insetY;
            double middleX = (left + right) / 2, middleY = (top + bottom) / 2;
            if ((region & 1) == 0) right = middleX; else left = middleX;
            if ((region & 2) == 0) bottom = middleY; else top = middleY;
            Point best = new Point((int)Math.Round(left), (int)Math.Round(top));
            double bestDistance = -1;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                Point candidate = new Point((int)Math.Round(left + Random.NextDouble() * (right - left)),
                    (int)Math.Round(top + Random.NextDouble() * (bottom - top)));
                double nearest = Double.MaxValue;
                foreach (PetForm existing in Pets)
                {
                    Point occupied = existing.BurstActive ? existing.BurstTarget : existing.Location;
                    double dx = (candidate.X + child.Width / 2.0 - occupied.X - existing.Width / 2.0) /
                        ((child.Width + existing.Width) / 2.0 + 12);
                    double dy = (candidate.Y + child.Height / 2.0 - occupied.Y - existing.Height / 2.0) /
                        ((child.Height + existing.Height) / 2.0 + 12);
                    nearest = Math.Min(nearest, dx * dx + dy * dy);
                }
                if (nearest > bestDistance) { best = candidate; bestDistance = nearest; }
            }
            return best;
        }
        internal void SplitFour(PetForm source)
        {
            source = Resolve(source);
            if (source == null) return;
            const int count = 4;
            int[] regions = { 0, 1, 2, 3 };
            for (int i = regions.Length - 1; i > 0; i--)
            {
                int swap = Random.Next(i + 1), value = regions[i];
                regions[i] = regions[swap]; regions[swap] = value;
            }
            double start = Now;
            for (int i = 0; i < count; i++) SpawnBurst(source, i * 0.10, start, regions[i]);
        }
        internal void KeepOnly(PetForm keep)
        {
            if (keep == null) return;
            foreach (PetForm pet in Pets.ToArray()) if (pet != keep) Remove(pet);
            LastPet = keep;
        }
        internal void Remove(PetForm pet)
        {
            if (Pets.Count <= 1 || pet == null || !Pets.Remove(pet)) return;
            pet.DisposePet();
            if (Pets.Count == 0 && !shuttingDown) Shutdown();
            else if (LastPet == pet) LastPet = Pets.Count > 0 ? Pets[0] : null;
        }
        private PetForm Resolve(PetForm pet)
        {
            if (pet != null && Pets.Contains(pet)) return pet;
            if (LastPet != null && Pets.Contains(LastPet)) return LastPet;
            return Pets.Count > 0 ? Pets[0] : null;
        }
        internal void LaughAll()
        {
            double start = Now;
            foreach (PetForm pet in Pets.ToArray()) pet.Play("laugh", false, start);
        }
        internal ContextMenuStrip BuildMenu(PetForm target)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.AutoClose = true;
            menu.ShowImageMargin = false;
            menu.Padding = new Padding(1, 2, 1, 2);
            ToolStripMenuItem title = new ToolStripMenuItem("奶龙桌宠"); title.Enabled = false; menu.Items.Add(title);
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem spawn = new ToolStripMenuItem("再生一只（双击分身）", null, delegate { Spawn(Resolve(target), true); }); menu.Items.Add(spawn);
            menu.Items.Add("分裂4只", null, delegate { SplitFour(Resolve(target)); });
            menu.Items.Add("移除本只", null, delegate { Remove(Resolve(target)); });
            menu.Items.Add("收回，只留本只", null, delegate { KeepOnly(Resolve(target)); });
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem actions = new ToolStripMenuItem("播放动作");
            string[] names = { "待机", "跑步", "大笑", "挥手", "闭眼休息" };
            string[] actionKeys = { "idle", "run", "laugh", "wave", "sleep" };
            for (int i = 0; i < names.Length; i++)
            {
                string action = actionKeys[i];
                ToolStripItem actionItem = actions.DropDownItems.Add(names[i], null, delegate { PetForm pet = Resolve(target); if (pet != null) pet.Play(action, action == "idle" || action == "run"); });
                actionItem.Tag = action;
                if (action == "laugh")
                {
                    ToolStripMenuItem laughAll = new ToolStripMenuItem("齐齐哈尔", null, delegate { LaughAll(); });
                    actions.DropDownItems.Add(laughAll);
                }
            }
            actions.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem wander = new ToolStripMenuItem("自由散步（附近走动）");
            wander.Click += delegate { Options.Wander = !Options.Wander; foreach (PetForm pet in Pets) pet.ResetWander(); SaveOptions(); };
            actions.DropDownItems.Add(wander);
            actions.DropDownOpening += delegate { wander.Checked = Options.Wander; };
            menu.Items.Add(actions);
            ToolStripMenuItem sizes = new ToolStripMenuItem("角色大小");
            int[] values = { 120, 170, 230 }; string[] labels = { "小 · 120 像素", "中 · 170 像素", "大 · 230 像素" };
            for (int i = 0; i < values.Length; i++)
            {
                int height = values[i];
                ToolStripMenuItem item = new ToolStripMenuItem(labels[i]); item.Tag = height;
                item.Click += delegate { PetForm pet = Resolve(target); if (pet != null) { pet.ResizePet(height); Options.Height = height; SaveOptions(); } };
                sizes.DropDownItems.Add(item);
            }
            menu.Items.Add(sizes);
            MenuToggleControl topmostSwitch = new MenuToggleControl("始终置顶", "当前：已开启", "当前：已关闭");
            topmostSwitch.Font = menu.Font;
            topmostSwitch.ShowCurrentState(Options.TopMost);
            topmostSwitches.Add(topmostSwitch);
            topmostSwitch.Disposed += delegate { topmostSwitches.Remove(topmostSwitch); };
            topmostSwitch.Click += delegate { SetTopMostEnabled(topmostSwitch.Checked); };
            menu.Items.Add(new MenuToggleHost(topmostSwitch));
            MenuToggleControl soundSwitch = new MenuToggleControl("声音", "当前：不静音", "当前：静音");
            soundSwitch.Font = menu.Font;
            soundSwitch.ShowCurrentState(!Options.Muted);
            soundSwitches.Add(soundSwitch);
            soundSwitch.Disposed += delegate { soundSwitches.Remove(soundSwitch); };
            soundSwitch.Click += delegate { SetSoundEnabled(soundSwitch.Checked); };
            MenuToggleHost soundHost = new MenuToggleHost(soundSwitch);
            menu.Items.Add(soundHost);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("全部退出", null, delegate { Shutdown(); });
            foreach (ToolStripItem item in menu.Items)
            {
                ToolStripMenuItem menuItem = item as ToolStripMenuItem;
                if (menuItem == null) continue;
                menuItem.Padding = new Padding(1, 0, 1, 0);
                if (menuItem.ShortcutKeys == Keys.None && String.IsNullOrEmpty(menuItem.ShortcutKeyDisplayString))
                    menuItem.ShowShortcutKeys = false;
            }
            ToolStripRenderer menuRenderer = menu.Renderer;
            ToolStripItemTextRenderEventHandler alignLabels = delegate(object sender, ToolStripItemTextRenderEventArgs e)
            {
                if (e.Item.Owner != menu || !(e.Item is ToolStripMenuItem)) return;
                int textLeft = e.Item.Bounds.Left + e.TextRectangle.Left;
                topmostSwitch.AlignLabelToMenuText(textLeft, e.TextFormat);
                soundSwitch.AlignLabelToMenuText(textLeft, e.TextFormat);
            };
            menuRenderer.RenderItemText += alignLabels;
            menu.Disposed += delegate { menuRenderer.RenderItemText -= alignLabels; };
            menu.Opening += delegate
            {
                PetForm pet = Resolve(target);
                title.Text = "奶龙桌宠 · " + Pets.Count + " 只";
                topmostSwitch.ShowCurrentState(Options.TopMost); wander.Checked = Options.Wander;
                soundSwitch.ShowCurrentState(!Options.Muted);
                foreach (ToolStripMenuItem item in sizes.DropDownItems) item.Checked = pet != null && pet.PetHeight == (int)item.Tag;
                if (pet != null) { LastPet = pet; pet.PauseForMenu(); }
            };
            return menu;
        }
        private void SetSoundEnabled(bool enabled)
        {
            Options.Muted = !enabled;
            if (Options.Muted) foreach (PetForm pet in Pets) pet.StopSound();
            foreach (MenuToggleControl soundSwitch in soundSwitches.ToArray())
                if (!soundSwitch.IsDisposed) soundSwitch.ShowCurrentState(enabled);
            SaveOptions();
        }
        private void SetTopMostEnabled(bool enabled)
        {
            Options.TopMost = enabled;
            foreach (PetForm pet in Pets) pet.ApplyTopMost();
            foreach (MenuToggleControl topmostSwitch in topmostSwitches.ToArray())
                if (!topmostSwitch.IsDisposed) topmostSwitch.ShowCurrentState(enabled);
            SaveOptions();
        }
        private void Tick(object sender, EventArgs e)
        {
            if (shuttingDown) return;
            if (exitEvent.WaitOne(0)) { Shutdown(); return; }
            double now = clock.Elapsed.TotalSeconds;
            double dt = Math.Max(0, Math.Min(0.12, now - lastTime)); lastTime = now;
            foreach (PetForm pet in Pets.ToArray()) pet.Tick(dt, now);
            if (smokeOutput != null && now >= nextSmoke) RunSmoke(now);
            if (timingOutput != null) RunTiming(now);
            if (burstOutput != null) RunBurstTest(now);
            else if (burstDemo) RunBurstDemo(now);
        }
        internal double Now { get { return clock.Elapsed.TotalSeconds; } }
        internal bool AutomaticProximityEnabled { get { return smokeOutput == null && timingOutput == null && burstOutput == null && !burstDemo; } }
        internal bool AnyMenuVisible
        {
            get
            {
                if (trayMenu.Visible) return true;
                foreach (PetForm pet in Pets) if (pet.Menu.Visible) return true;
                return false;
            }
        }
        internal bool IsTimingTest { get { return timingOutput != null; } }
        private bool IsTestSession { get { return smokeOutput != null || timingOutput != null || burstOutput != null || burstDemo || proximityTest; } }
        private void SaveOptions() { if (!IsTestSession) Options.Save(); }
        internal void SavePosition(PetForm pet)
        {
            if (pet == null || IsTestSession) return;
            Options.X = pet.Left; Options.Y = pet.Top; Options.Height = pet.PetHeight; Options.Save();
        }
        private void SmokeCheck(string name, bool passed)
        {
            smokeResults.Add("{\"name\":" + Program.Json(name) + ",\"passed\":" + (passed ? "true" : "false") + "}");
        }
        private void RunSmoke(double now)
        {
            try
            {
                nextSmoke = now + 0.45;
                if (smokeStep == 0)
                {
                    SmokeCheck("real_layered_window_visible", smokePet.Visible && smokePet.IsHandleCreated && Native.IsWindowVisible(smokePet.Handle));
                    SmokeCheck("noactivate_layered_toolwindow", (Native.GetWindowLong(smokePet.Handle, -20) & (0x80000 | 0x8000000 | 0x80)) == (0x80000 | 0x8000000 | 0x80));
                    smokeStart = smokePet.Location;
                    Spawn(smokePet, true);
                }
                else if (smokeStep == 1)
                {
                    SmokeCheck("clone", Pets.Count == 2);
                    smokePet.TestDrag(new Point(30, -18));
                    SmokeCheck("drag_position", smokePet.Location != smokeStart);
                    smokePet.ResizePet(230);
                }
                else if (smokeStep == 2)
                {
                    SmokeCheck("resize", smokePet.PetHeight == 230 && smokePet.Height == 230);
                    smokePet.Play("laugh", false);
                    SmokeCheck("laugh_action", smokePet.CurrentAction == "laugh");
                    smokePet.Play("sleep", true);
                    SmokeCheck("sleep_action", smokePet.CurrentAction == "sleep");
                    SplitFour(smokePet);
                }
                else if (smokeStep == 3)
                {
                    SmokeCheck("split_four_adds_four", Pets.Count == 6);
                    KeepOnly(smokePet);
                    SmokeCheck("keep_one", Pets.Count == 1 && Pets[0] == smokePet);
                    for (int i = 0; i < 15; i++) Spawn(smokePet, false);
                    SmokeCheck("clones_can_exceed_twelve", Pets.Count == 16);
                }
                else if (smokeStep == 4)
                {
                    KeepOnly(smokePet);
                    smokePet.Menu.Show(smokePet, new Point(smokePet.Width / 2, smokePet.Height / 2));
                    SmokeCheck("native_chinese_context_menu", smokePet.Menu.Visible && smokePet.Menu.Items.Count > 10);
                }
                else
                {
                    smokePet.Menu.Close();
                    smokePet.ResizePet(170);
                    SmokeCheck("context_menu_closed", !smokePet.Menu.Visible);
                    Shutdown();
                    SmokeCheck("clean_shutdown", Pets.Count == 0 && !tray.Visible);
                    bool passed = true;
                    foreach (string result in smokeResults) if (result.IndexOf("false", StringComparison.Ordinal) >= 0) passed = false;
                    Program.WriteText(smokeOutput, "{\"ok\":" + (passed ? "true" : "false") + ",\"checks\":[" + String.Join(",", smokeResults.ToArray()) + "]}");
                }
                smokeStep++;
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                Program.WriteText(smokeOutput, "{\"ok\":false,\"error\":" + Program.Json(ex.ToString()) + ",\"checks\":[" + String.Join(",", smokeResults.ToArray()) + "]}");
                Shutdown();
            }
        }
        private void TimingCheck(string name, bool passed)
        {
            timingChecks.Add("{\"name\":" + Program.Json(name) + ",\"passed\":" + (passed ? "true" : "false") + "}");
        }
        private void RunTiming(double now)
        {
            try
            {
                double expected = Assets.Duration("laugh");
                double frameSeconds = Assets.FrameSeconds("laugh");
                if (!timingStarted)
                {
                    timingPet.Play("laugh", false);
                    timingStart = timingPet.ActionStartedAt;
                    timingLastTick = timingStart;
                    timingStarted = true;
                    TimingCheck("nonloop_final_frame_clamps", Assets.FrameIndex("laugh", expected + frameSeconds * 3, false) == Assets.Count("laugh") - 1);
                    TimingCheck("first_frame_at_zero", Assets.FrameIndex("laugh", 0, false) == 0);
                    TimingCheck("frame_boundary_before", Assets.FrameIndex("laugh", frameSeconds * 0.999, false) == 0);
                    TimingCheck("frame_boundary_after", Assets.Count("laugh") < 2 || Assets.FrameIndex("laugh", frameSeconds * 1.001, false) == 1);
                    if (Assets.LaughSoundPath != null)
                    {
                        TimingCheck("pcm_audio_duration_matches_animation", Math.Abs(Assets.LaughSoundSeconds - expected) <= Math.Max(0.06, frameSeconds * 2));
                        TimingCheck("primary_audio_started", timingPet.Audio.Started && timingPet.Audio.IsPlaying);
                    }
                    return;
                }
                double elapsed = now - timingStart;
                timingMaxGap = Math.Max(timingMaxGap, now - timingLastTick); timingLastTick = now;
                timingSamples++;
                if (elapsed < expected)
                {
                    if (timingPet.CurrentAction != "laugh") timingStayedLaughing = false;
                    if (timingPet.CurrentAction == "laugh")
                    {
                        int wanted = Assets.FrameIndex("laugh", elapsed, false);
                        if (Math.Abs(wanted - timingPet.CurrentFrameIndex) > 1 || timingPet.CurrentFrameIndex < 0 || timingPet.CurrentFrameIndex >= Assets.Count("laugh")) timingFrameValid = false;
                        if (timingPet.CurrentFrameIndex < timingLastFrame) timingMonotonic = false;
                        timingLastFrame = timingPet.CurrentFrameIndex;
                    }
                    if (!timingSecondaryStarted && elapsed >= expected * 0.25)
                    {
                        timingOtherPet = Spawn(timingPet, false);
                        timingOtherPet.Play("laugh", false);
                        timingSecondaryStarted = true;
                        if (Assets.LaughSoundPath != null) TimingCheck("two_independent_audio_devices_play", timingPet.Audio.IsPlaying && timingOtherPet.Audio.IsPlaying);
                    }
                    if (timingSecondaryStarted && !timingSecondaryStopped && elapsed >= expected * 0.40)
                    {
                        timingOtherPet.Play("wave", false);
                        timingSecondaryStopped = true;
                        if (Assets.LaughSoundPath != null)
                        {
                            TimingCheck("switching_one_pet_stops_its_audio", !timingOtherPet.Audio.IsOpen);
                            TimingCheck("other_pet_audio_not_interrupted", timingPet.Audio.IsPlaying);
                        }
                    }
                    return;
                }
                if (timingPet.CurrentAction == "laugh" && elapsed < expected + Math.Max(0.5, timingMaxGap * 2)) return;
                TimingCheck("laugh_stays_until_full_duration", timingStayedLaughing);
                TimingCheck("laugh_returns_to_idle", timingPet.CurrentAction == "idle");
                TimingCheck("frame_indices_follow_real_time", timingFrameValid);
                TimingCheck("frames_do_not_wrap", timingMonotonic);
                TimingCheck("no_empty_rendered_frames", timingPet.RenderedEmptyFrames == 0);
                double actual = timingPet.LastLaughCompletedAt > 0 ? timingPet.LastLaughCompletedAt - timingStart : elapsed;
                TimingCheck("completion_within_timer_tolerance", actual >= expected - 0.002 && actual - expected <= Math.Max(0.15, timingMaxGap + frameSeconds));
                if (Assets.LaughSoundPath != null) TimingCheck("audio_device_closed_after_animation", !timingPet.Audio.IsOpen);
                bool passed = true;
                foreach (string result in timingChecks) if (result.IndexOf("false", StringComparison.Ordinal) >= 0) passed = false;
                string output = "{\"ok\":" + (passed ? "true" : "false") + ",\"frames\":" + Assets.Count("laugh")
                    + ",\"fps\":" + (1.0 / frameSeconds).ToString("R", CultureInfo.InvariantCulture)
                    + ",\"expectedDurationSeconds\":" + expected.ToString("R", CultureInfo.InvariantCulture)
                    + ",\"actualDurationSeconds\":" + actual.ToString("R", CultureInfo.InvariantCulture)
                    + ",\"audioDurationSeconds\":" + Assets.LaughSoundSeconds.ToString("R", CultureInfo.InvariantCulture)
                    + ",\"completionErrorSeconds\":" + (actual - expected).ToString("R", CultureInfo.InvariantCulture)
                    + ",\"timerSamples\":" + timingSamples
                    + ",\"maximumTimerGapSeconds\":" + timingMaxGap.ToString("R", CultureInfo.InvariantCulture)
                    + ",\"lastObservedLaughFrame\":" + timingLastFrame
                    + ",\"emptyRenderedFrames\":" + timingPet.RenderedEmptyFrames
                    + ",\"checks\":[" + String.Join(",", timingChecks.ToArray()) + "]}";
                Program.WriteText(timingOutput, output);
                Shutdown();
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                Program.WriteText(timingOutput, "{\"ok\":false,\"error\":" + Program.Json(ex.ToString()) + ",\"checks\":[" + String.Join(",", timingChecks.ToArray()) + "]}");
                Shutdown();
            }
        }
        private static string Number(double value) { return value.ToString("0.######", CultureInfo.InvariantCulture); }
        private void BurstCheck(string name, bool passed)
        {
            burstChecks.Add("{\"name\":" + Program.Json(name) + ",\"passed\":" + (passed ? "true" : "false") + "}");
        }
        private void PlaceBurstSource(PetForm pet, bool corner)
        {
            Rectangle work = Screen.FromRectangle(pet.Bounds).WorkingArea;
            pet.CancelBurst("test-placement");
            pet.Location = corner ? new Point(work.Right - pet.Width - 2, work.Top + 2)
                : new Point(work.Left + (work.Width - pet.Width) / 2, work.Top + (int)((work.Height - pet.Height) * 0.62));
            pet.ClampToWorkArea(); pet.HoldWanderUntil(Now + 30); pet.Render();
        }
        private void RunBurstDemo(double now)
        {
            if (!burstDemoStarted)
            {
                burstDemoStarted = true;
                PlaceBurstSource(burstTestParent, false);
                burstTestParent.Play("laugh", false);
                burstDemoAt = now + 3;
            }
            else if (!burstDemoFired && now >= burstDemoAt)
            {
                burstDemoFired = true;
                SplitFour(burstTestParent);
            }
        }
        private void RecordBurstSample(double now, string phase)
        {
            StringBuilder sample = new StringBuilder("{\"elapsed\":").Append(Number(Math.Max(0, now - burstTestStartedAt)))
                .Append(",\"phase\":").Append(Program.Json(phase)).Append(",\"petCount\":").Append(Pets.Count).Append(",\"pets\":[");
            bool first = true;
            foreach (PetForm pet in Pets)
            {
                if (!first) sample.Append(','); first = false;
                sample.Append("{\"id\":").Append(pet.PetId).Append(",\"x\":").Append(pet.Left).Append(",\"y\":").Append(pet.Top)
                    .Append(",\"width\":").Append(pet.Width).Append(",\"height\":").Append(pet.Height)
                    .Append(",\"visible\":").Append(pet.Visible ? "true" : "false")
                    .Append(",\"burstActive\":").Append(pet.BurstActive ? "true" : "false")
                    .Append(",\"pending\":").Append(pet.BurstPending ? "true" : "false")
                    .Append(",\"burstStart\":").Append(Number(pet.BurstStartedAt - burstTestStartedAt))
                    .Append(",\"originX\":").Append(pet.BurstOrigin.X).Append(",\"originY\":").Append(pet.BurstOrigin.Y)
                    .Append(",\"sourceCenterX\":").Append(pet.BurstSourceCenter.X).Append(",\"sourceCenterY\":").Append(pet.BurstSourceCenter.Y)
                    .Append(",\"targetX\":").Append(pet.BurstTarget.X).Append(",\"targetY\":").Append(pet.BurstTarget.Y)
                    .Append(",\"duration\":").Append(Number(pet.BurstDuration)).Append(",\"progress\":").Append(Number(pet.BurstProgress))
                    .Append(",\"action\":").Append(Program.Json(pet.CurrentAction)).Append(",\"frameIndex\":").Append(pet.CurrentFrameIndex).Append('}');
                if (pet.BurstWorkArea.Width > 0 && !pet.BurstWorkArea.Contains(pet.Bounds)) burstAllWithinBounds = false;
                if (pet != burstTestParent && pet.BurstActive)
                {
                    if (Math.Abs(pet.Left - pet.BurstOrigin.X) + Math.Abs(pet.Top - pet.BurstOrigin.Y) > 15) burstObservedMovement = true;
                }
            }
            sample.Append("]}"); burstSamples.Add(sample.ToString());
        }
        private bool AllBurstChildrenStopped()
        {
            foreach (PetForm pet in burstTestChildren) if (pet.BurstActive || !pet.Visible) return false;
            return true;
        }
        private void RunBurstTest(double now)
        {
            try
            {
                if (burstTestStage >= 2) RecordBurstSample(now, burstTestStage < 4 ? "fan" : "edge");
                if (now < burstTestAt) return;
                if (burstTestStage == 0)
                {
                    PlaceBurstSource(burstTestParent, false);
                    burstTestParent.Play("laugh", false);
                    burstTestStage = 1; burstTestAt = now + 0.65;
                }
                else if (burstTestStage == 1)
                {
                    burstParentPosition = burstTestParent.Location;
                    burstTestStartedAt = Now;
                    SplitFour(burstTestParent);
                    foreach (PetForm pet in Pets) if (pet != burstTestParent) burstTestChildren.Add(pet);
                    BurstCheck("five_pets_including_original", Pets.Count == 5 && burstTestChildren.Count == 4);
                    bool sameOrigin = true, sameClock = true, largeSize = true, stagger = true;
                    Point center = new Point(burstTestParent.Left + burstTestParent.Width / 2, burstTestParent.Top + burstTestParent.Height / 2);
                    for (int i = 0; i < burstTestChildren.Count; i++)
                    {
                        PetForm pet = burstTestChildren[i];
                        sameOrigin &= pet.BurstSourceCenter == center && pet.Location == pet.BurstOrigin
                            && Math.Abs(pet.Left + pet.Width / 2 - center.X) <= 1 && Math.Abs(pet.Top + pet.Height / 2 - center.Y) <= 1;
                        sameClock &= pet.CurrentAction == burstTestParent.CurrentAction && Math.Abs(pet.ActionStartedAt - burstTestParent.ActionStartedAt) < 0.000001 && pet.Mirrored == burstTestParent.Mirrored;
                        largeSize &= pet.PetHeight == 230 && pet.Height == 230;
                        if (i > 0) stagger &= Math.Abs(pet.BurstStartedAt - burstTestChildren[i - 1].BurstStartedAt - 0.10) < 0.00001;
                    }
                    BurstCheck("all_children_start_at_same_source_center", sameOrigin);
                    BurstCheck("pose_clock_and_mirror_inherited", sameClock);
                    BurstCheck("children_use_large_size_230px", largeSize);
                    BurstCheck("births_staggered_by_100ms", stagger);
                    RecordBurstSample(burstTestStartedAt, "fan");
                    burstTestStage = 2; burstTestAt = now + 0.9;
                }
                else if (burstTestStage == 2)
                {
                    if (!AllBurstChildrenStopped() && now - burstTestStartedAt < 5) { burstTestAt = now + 0.025; return; }
                    BurstCheck("real_timer_observed_outward_movement", burstObservedMovement);
                    BurstCheck("all_four_children_settled", AllBurstChildrenStopped());
                    BurstCheck("source_remains_in_place", burstTestParent.Location == burstParentPosition);
                    bool separated = true, arrived = true, aboveBottom = false;
                    int minimumTop = Int32.MaxValue, maximumTop = Int32.MinValue;
                    for (int i = 0; i < burstTestChildren.Count; i++)
                    {
                        PetForm pet = burstTestChildren[i];
                        burstStoppedPositions[pet.PetId] = pet.Location;
                        arrived &= pet.Location == pet.BurstTarget && pet.BurstProgress == 1 && pet.BurstWorkArea.Contains(pet.Bounds);
                        aboveBottom |= pet.Bottom < pet.BurstWorkArea.Bottom - 60;
                        minimumTop = Math.Min(minimumTop, pet.Top); maximumTop = Math.Max(maximumTop, pet.Top);
                        for (int j = i + 1; j < burstTestChildren.Count; j++)
                        {
                            double dx = pet.Left - burstTestChildren[j].Left, dy = pet.Top - burstTestChildren[j].Top;
                            separated &= dx * dx + dy * dy >= 25 * 25;
                        }
                    }
                    BurstCheck("settled_at_random_targets_inside_workarea", arrived);
                    BurstCheck("random_positions_span_multiple_heights", aboveBottom && maximumTop - minimumTop >= 60);
                    BurstCheck("children_end_at_distinct_random_positions", separated);
                    burstTestStage = 3; burstTestAt = now + 0.35;
                }
                else if (burstTestStage == 3)
                {
                    bool stable = true;
                    foreach (PetForm pet in burstTestChildren) stable &= pet.Location == burstStoppedPositions[pet.PetId];
                    BurstCheck("settled_windows_stay_still", stable);
                    KeepOnly(burstTestParent);
                    burstTestParent.ResizePet(120);
                    PlaceBurstSource(burstTestParent, true);
                    SplitFour(burstTestParent);
                    burstTestChildren.Clear();
                    foreach (PetForm pet in Pets) if (pet != burstTestParent) burstTestChildren.Add(pet);
                    burstTestStage = 4; burstTestAt = now + 0.18;
                }
                else if (burstTestStage == 4)
                {
                    burstDragPet = burstTestChildren[0]; burstResizePet = burstTestChildren[1];
                    bool wasMoving = burstDragPet.BurstActive;
                    burstDragPet.TestDrag(new Point(-35, 28));
                    burstDragStopped = burstDragPet.Location;
                    BurstCheck("native_drag_cancels_live_flight", wasMoving && !burstDragPet.BurstActive);
                    wasMoving = burstResizePet.BurstActive;
                    burstResizePet.ResizePet(170); burstResizeStopped = burstResizePet.Location;
                    BurstCheck("resize_cancels_live_flight", wasMoving && !burstResizePet.BurstActive);
                    burstTestStage = 5; burstTestAt = now + 0.7;
                }
                else if (burstTestStage == 5)
                {
                    if (!AllBurstChildrenStopped() && now - burstTestStartedAt < 9) { burstTestAt = now + 0.025; return; }
                    BurstCheck("all_motion_remained_inside_workarea", burstAllWithinBounds);
                    BurstCheck("dragged_pet_does_not_resume_flight", burstDragPet.Location == burstDragStopped && !burstDragPet.BurstActive);
                    BurstCheck("resized_pet_does_not_resume_flight", burstResizePet.Location == burstResizeStopped && !burstResizePet.BurstActive);
                    for (int i = 0; i < 16; i++) Spawn(burstTestParent, true);
                    BurstCheck("clones_exceed_twelve_including_pending_births", Pets.Count == 21);
                    burstTestStage = 6; burstTestAt = now + 0.2;
                }
                else
                {
                    BurstCheck("uncapped_count_stable_across_timer_ticks", Pets.Count == 21);
                    KeepOnly(burstTestParent);
                    SplitFour(burstTestParent);
                    List<PetForm> pendingRecall = new List<PetForm>();
                    bool hadPendingRecall = false;
                    foreach (PetForm pet in Pets) if (pet != burstTestParent)
                    { pendingRecall.Add(pet); hadPendingRecall |= pet.BurstPending; }
                    KeepOnly(burstTestParent);
                    bool pendingRecallDisposed = hadPendingRecall && pendingRecall.Count == 4;
                    foreach (PetForm pet in pendingRecall) pendingRecallDisposed &= pet.IsDisposed;
                    BurstCheck("recall_removes_active_and_pending_clones", pendingRecallDisposed && Pets.Count == 1 && Pets[0] == burstTestParent);
                    SplitFour(burstTestParent);
                    List<PetForm> pendingExit = new List<PetForm>(Pets);
                    bool hadPendingExit = false;
                    foreach (PetForm pet in pendingExit) hadPendingExit |= pet.BurstPending;
                    Shutdown();
                    bool allExitDisposed = hadPendingExit;
                    foreach (PetForm pet in pendingExit) allExitDisposed &= pet.IsDisposed;
                    BurstCheck("clean_exit_disposes_all_windows_and_tray", allExitDisposed && Pets.Count == 0 && burstTestParent.IsDisposed && !tray.Visible);
                    bool passed = true;
                    foreach (string check in burstChecks) if (check.IndexOf("false", StringComparison.Ordinal) >= 0) passed = false;
                    Program.WriteText(burstOutput, "{\"ok\":" + (passed ? "true" : "false") + ",\"motion\":\"inertial_burst_with_smooth_deceleration_to_random_targets\",\"birthIntervalSeconds\":0.1,\"sampleIntervalTargetSeconds\":0.025,\"checks\":[" + String.Join(",", burstChecks.ToArray()) + "],\"samples\":[" + String.Join(",", burstSamples.ToArray()) + "]}");
                }
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                Program.WriteText(burstOutput, "{\"ok\":false,\"error\":" + Program.Json(ex.ToString()) + ",\"checks\":[" + String.Join(",", burstChecks.ToArray()) + "],\"samples\":[" + String.Join(",", burstSamples.ToArray()) + "]}");
                Shutdown();
            }
        }
        internal void Shutdown()
        {
            if (shuttingDown) return;
            shuttingDown = true;
            if (!IsTestSession && Pets.Count > 0) SavePosition(Resolve(null));
            if (timer != null) timer.Stop();
            tray.Visible = false;
            foreach (PetForm pet in Pets.ToArray()) pet.DisposePet();
            Pets.Clear(); LastPet = null;
            ExitThread();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Shutdown();
                if (timer != null) timer.Dispose();
                tray.Dispose(); trayMenu.Dispose(); trayIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class PetForm : Form
    {
        private readonly PetContext context;
        internal new readonly ContextMenuStrip Menu;
        internal readonly int PetId;
        internal int PetHeight;
        internal string CurrentAction = "idle";
        private bool loop = true;
        private bool pinnedAction;
        private double elapsed;
        private double actionDeadline;
        internal double ActionStartedAt;
        internal double LastLaughCompletedAt;
        internal readonly PetAudio Audio = new PetAudio();
        internal int RenderedEmptyFrames;
        private int displayedFrame = -1;
        internal int CurrentFrameIndex { get { return displayedFrame; } }
        private Bitmap displayedBitmap;
        private bool mirror;
        private bool lastMirror;
        private Point anchor;
        private double positionX;
        private double wanderAt;
        private double wanderUntil;
        private double moveDirection = 1;
        private bool pointerDown;
        private bool dragging;
        private bool doubleClick;
        private double proximityCandidateAt = -1;
        private double proximityNotBefore;
        private bool proximityBlockedUntilExit;
        private Point dragMouse;
        private Point dragWindow;
        private readonly System.Windows.Forms.Timer clickTimer;
        internal bool BurstActive;
        internal Point BurstOrigin { get; private set; }
        internal Point BurstSourceCenter { get; private set; }
        internal Point BurstTarget { get; private set; }
        internal Rectangle BurstWorkArea { get; private set; }
        internal double BurstDuration { get; private set; }
        internal double BurstProgress { get; private set; }
        internal double BurstStartedAt;
        internal double BurstEndedAt;
        internal string BurstCancelReason;
        private double wanderHoldUntil;
        internal bool BurstPending { get { return BurstActive && context.Now < BurstStartedAt; } }
        internal bool Mirrored { get { return mirror; } }
        internal PetForm(PetContext owner, int height, Point location, int id)
        {
            context = owner; PetHeight = height; PetId = id;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = Program.UITest;
            StartPosition = FormStartPosition.Manual; AutoScaleMode = AutoScaleMode.None;
            Size = new Size((int)Math.Round(height * 192.0 / 208.0), height);
            Location = location; anchor = location; positionX = location.X;
            Text = "奶龙桌宠"; AccessibleName = "奶龙桌宠";
            Menu = context.BuildMenu(this);
            clickTimer = new System.Windows.Forms.Timer();
            clickTimer.Interval = SystemInformation.DoubleClickTime + 35;
            clickTimer.Tick += delegate
            {
                clickTimer.Stop();
                if (IsDisposed || pointerDown || Menu.Visible) return;
                Play("laugh", false);
            };
            ResetWander();
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80000 | 0x8000000;
                if (!Program.UITest) cp.ExStyle |= 0x80;
                return cp;
            }
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e); ApplyTopMost();
        }
        internal void ApplyTopMost()
        {
            if (IsHandleCreated) Native.SetWindowPos(Handle, context.Options.TopMost ? new IntPtr(-1) : new IntPtr(-2), 0, 0, 0, 0, 0x1 | 0x2 | 0x10);
        }
        internal void PauseForMenu() { SuppressProximity(); wanderUntil = 0; if (CurrentAction == "walk") Play("idle", true); wanderAt = context.Now + 5; }
        internal void ResetWander()
        {
            anchor = Location; positionX = Left; wanderUntil = 0;
            wanderAt = context.Now + 5 + context.Random.NextDouble() * 7;
            if (CurrentAction == "walk") Play("idle", true);
        }
        internal void HoldWanderUntil(double until)
        {
            wanderHoldUntil = Math.Max(wanderHoldUntil, until);
            wanderUntil = 0; wanderAt = Math.Max(wanderAt, until + 2);
            anchor = Location; positionX = Left;
        }
        internal void InheritPose(PetForm source)
        {
            Audio.Stop();
            if (source.CurrentAction == "near") { mirror = source.mirror; Play("idle", true); return; }
            CurrentAction = source.CurrentAction;
            loop = source.loop; pinnedAction = source.pinnedAction;
            actionDeadline = source.actionDeadline;
            ActionStartedAt = source.ActionStartedAt;
            elapsed = Math.Max(0, context.Now - ActionStartedAt);
            mirror = source.mirror; displayedFrame = -1;
            // The original audio continues once; copied poses do not restart the laugh.
        }
        internal void BeginBurst(Point origin, Point sourceCenter, Rectangle work, Point target, double duration, double start)
        {
            BurstActive = true; BurstCancelReason = null; BurstOrigin = origin; BurstSourceCenter = sourceCenter;
            BurstWorkArea = work; BurstStartedAt = start; BurstEndedAt = 0;
            BurstTarget = target; BurstDuration = Math.Max(0.001, duration); BurstProgress = 0;
            Location = origin;
            wanderUntil = 0; wanderAt = start + 5;
        }
        internal void CancelBurst(string reason)
        {
            if (!BurstActive) return;
            BurstActive = false; BurstEndedAt = context.Now; BurstCancelReason = reason;
            if (!Visible && !IsDisposed) Show();
            anchor = Location; positionX = Left; wanderUntil = 0; wanderAt = context.Now + 5;
        }
        private void UpdateBurst(double now)
        {
            if (!BurstActive || now < BurstStartedAt) return;
            if (!Visible) Show();
            double left = BurstWorkArea.Left, right = Math.Max(left, BurstWorkArea.Right - Width);
            double top = BurstWorkArea.Top, bottom = Math.Max(top, BurstWorkArea.Bottom - Height);
            BurstProgress = Math.Max(0, Math.Min(1, (now - BurstStartedAt) / BurstDuration));
            double remaining = 1 - BurstProgress;
            // Launch along the displacement to the chosen resting point. Resistance
            // reduces speed by remaining^2; velocity and acceleration both reach
            // zero at the end, without a steering force or a last-frame snap.
            double distanceFraction = 1 - remaining * remaining * remaining;
            double x = BurstOrigin.X + (BurstTarget.X - BurstOrigin.X) * distanceFraction;
            double y = BurstOrigin.Y + (BurstTarget.Y - BurstOrigin.Y) * distanceFraction;
            Location = new Point((int)Math.Round(Math.Max(left, Math.Min(right, x))), (int)Math.Round(Math.Max(top, Math.Min(bottom, y))));
            if (BurstProgress >= 1)
            { BurstActive = false; BurstEndedAt = now; BurstCancelReason = "settled"; }
            positionX = Left;
            if (!BurstActive)
            {
                anchor = Location; wanderUntil = 0; wanderAt = now + 3.5 + context.Random.NextDouble() * 3;
            }
        }
        internal void Play(string action, bool repeat, double? synchronizedStart = null)
        {
            if (IsDisposed) return;
            if (CurrentAction == "near" && action != "near") proximityBlockedUntilExit = true;
            if (synchronizedStart.HasValue) clickTimer.Stop();
            Audio.Stop();
            if (action == "laugh" || action == "sleep") repeat = false;
            if (action == "run" || action == "wave") repeat = true;
            CurrentAction = action; loop = repeat; pinnedAction = repeat && (action == "run" || action == "wave" || action == "near");
            elapsed = 0; displayedFrame = -1;
            actionDeadline = context.Assets.Duration(action) * (action == "run" || action == "wave" ? 2 : 1);
            if (action != "walk") wanderUntil = 0;
            if (action == "laugh" && !context.Options.Muted) Audio.Play(context.Assets.LaughSoundPath);
            ActionStartedAt = synchronizedStart ?? context.Now;
            elapsed = Math.Max(0, context.Now - ActionStartedAt);
            Render();
        }
        internal void StopSound() { Audio.Stop(); }
        internal double ProximityRadius { get { return Math.Round(90.0 * PetHeight / 170.0); } }
        private void EndProximity(double now)
        {
            if (CurrentAction != "near") return;
            Play("idle", true);
            wanderAt = now + 5;
            proximityNotBefore = now + 0.3;
        }
        private void SuppressProximity()
        {
            EndProximity(context.Now);
            proximityBlockedUntilExit = true;
            proximityCandidateAt = -1;
        }
        internal void UpdateProximity(Point pointer, double now)
        {
            if (IsDisposed || !Visible) return;
            Point local = new Point(pointer.X - Left, pointer.Y - Top);
            double radius = ProximityRadius;
            // Fast reject before consulting the cached pixel outline.
            Rectangle nearby = Bounds; nearby.Inflate((int)radius + 16, (int)radius + 16);
            double distance = nearby.Contains(pointer) ? context.Assets.PointerShape(PetHeight, mirror).Distance(local) : radius + 17;
            if (distance > radius + 12)
            {
                EndProximity(now);
                proximityBlockedUntilExit = false; proximityCandidateAt = -1;
                return;
            }
            bool hovered = distance == 0 || (displayedBitmap != null && local.X >= 0 && local.Y >= 0 &&
                local.X < displayedBitmap.Width && local.Y < displayedBitmap.Height && displayedBitmap.GetPixel(local.X, local.Y).A >= 12);
            if (hovered || pointerDown || dragging || Capture || clickTimer.Enabled || context.AnyMenuVisible || BurstActive || Control.MouseButtons != MouseButtons.None)
            {
                EndProximity(now);
                // Hover alone permits a fresh reaction when the pointer moves off the body.
                // Explicit interactions must leave the nearby area before rearming.
                if (!hovered || pointerDown || clickTimer.Enabled || context.AnyMenuVisible || BurstActive)
                    proximityBlockedUntilExit = true;
                else proximityBlockedUntilExit = false;
                proximityCandidateAt = -1;
                return;
            }
            if (CurrentAction == "near") return;
            if (proximityBlockedUntilExit || now < proximityNotBefore || distance > radius || (CurrentAction != "idle" && CurrentAction != "walk"))
            { proximityCandidateAt = -1; return; }
            if (proximityCandidateAt < 0) proximityCandidateAt = now;
            if (now - proximityCandidateAt < 0.12) return;
            Play("near", true);
            proximityCandidateAt = -1;
        }
        internal void Tick(double dt, double now)
        {
            if (IsDisposed) return;
            UpdateBurst(now);
            if (context.AutomaticProximityEnabled) UpdateProximity(Cursor.Position, now);
            elapsed = Math.Max(0, now - ActionStartedAt);
            if ((!loop || CurrentAction == "run" || CurrentAction == "wave") && !dragging && elapsed >= actionDeadline)
            {
                if (CurrentAction == "laugh") LastLaughCompletedAt = now;
                Play("idle", true); wanderAt = now + 4 + context.Random.NextDouble() * 6;
            }
            if (context.Options.Wander && !BurstActive && now >= wanderHoldUntil && !pointerDown && !Menu.Visible && !pinnedAction)
            {
                if (now >= wanderAt && CurrentAction == "idle")
                {
                    moveDirection = context.Random.Next(2) == 0 ? -1 : 1;
                    mirror = moveDirection < 0;
                    wanderUntil = now + 1.3 + context.Random.NextDouble() * 1.7;
                    wanderAt = now + 9 + context.Random.NextDouble() * 12;
                    Play("walk", true);
                }
                if (CurrentAction == "walk" && wanderUntil > now)
                {
                    positionX += moveDirection * 19.0 * dt * PetHeight / 170.0;
                    int limit = Math.Max(45, PetHeight * 65 / 170);
                    if (positionX < anchor.X - limit || positionX > anchor.X + limit)
                    { positionX = Math.Max(anchor.X - limit, Math.Min(anchor.X + limit, positionX)); moveDirection = -moveDirection; mirror = moveDirection < 0; }
                    Location = new Point((int)Math.Round(positionX), Top);
                    ClampToWorkArea(false);
                }
                else if (CurrentAction == "walk" && loop) Play("idle", true);
            }
            Render();
        }
        internal void Render()
        {
            if (!IsHandleCreated || IsDisposed) return;
            int frame = context.Assets.FrameIndex(CurrentAction, elapsed, loop);
            if (frame == displayedFrame && lastMirror == mirror && displayedBitmap != null && displayedBitmap.Height == PetHeight) return;
            displayedBitmap = context.Assets.Frame(CurrentAction, frame, PetHeight, mirror);
            if (context.IsTimingTest && !PetAssets.HasVisiblePixels(displayedBitmap)) RenderedEmptyFrames++;
            displayedFrame = frame; lastMirror = mirror;
            Native.RenderLayered(Handle, displayedBitmap, Left, Top);
        }
        internal void ResizePet(int height)
        {
            SuppressProximity();
            CancelBurst("resize");
            int feet = Bottom;
            int center = Left + Width / 2;
            PetHeight = height;
            Size = new Size((int)Math.Round(height * 192.0 / 208.0), height);
            Location = new Point(center - Width / 2, feet - height);
            displayedFrame = -1; ClampToWorkArea(); ResetWander(); Render();
        }
        internal void ClampToWorkArea() { ClampToWorkArea(true); }
        private void ClampToWorkArea(bool reset)
        {
            Rectangle work = Screen.FromRectangle(Bounds).WorkingArea;
            int x = Math.Max(work.Left, Math.Min(work.Right - Width, Left));
            int y = Math.Max(work.Top, Math.Min(work.Bottom - Height, Top));
            bool changed = x != Left || y != Top;
            if (changed) Location = new Point(x, y);
            if (reset || changed) positionX = Left;
            if (reset) anchor = Location;
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e); context.LastPet = this;
            SuppressProximity();
            if (e.Button == MouseButtons.Right)
            {
                clickTimer.Stop(); PauseForMenu();
                // A foreground owner lets the native menu loop dismiss on clicks outside the app.
                Native.SetForegroundWindow(Handle);
                Menu.Show(this, e.Location);
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            if (Menu.Visible) Menu.Close(ToolStripDropDownCloseReason.AppClicked);
            CancelBurst("pointer");
            clickTimer.Stop(); pointerDown = true; dragging = false; doubleClick = e.Clicks >= 2;
            dragMouse = PointToScreen(e.Location); dragWindow = Location;
            Capture = true;
            if (doubleClick) context.Spawn(this, true);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!pointerDown) return;
            Point current = PointToScreen(e.Location);
            int dx = current.X - dragMouse.X, dy = current.Y - dragMouse.Y;
            if (!dragging && Math.Abs(dx) + Math.Abs(dy) >= 5)
            { dragging = true; clickTimer.Stop(); Play("dragged", true); }
            if (dragging)
            {
                Location = new Point(dragWindow.X + dx, dragWindow.Y + dy);
                positionX = Left;
            }
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !pointerDown) return;
            pointerDown = false; Capture = false;
            if (dragging)
            {
                dragging = false; ClampToWorkArea(); Play("idle", true); ResetWander(); context.SavePosition(this);
            }
            else if (!doubleClick) clickTimer.Start();
            doubleClick = false;
        }
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture && pointerDown)
            { pointerDown = false; dragging = false; ClampToWorkArea(); Play("idle", true); ResetWander(); }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; }
            if (m.Msg == 0x84 && displayedBitmap != null && !pointerDown)
            {
                long value = m.LParam.ToInt64();
                int x = (short)(value & 0xffff), y = (short)((value >> 16) & 0xffff);
                Point local = PointToClient(new Point(x, y));
                if (local.X < 0 || local.Y < 0 || local.X >= displayedBitmap.Width || local.Y >= displayedBitmap.Height || displayedBitmap.GetPixel(local.X, local.Y).A < 12)
                { m.Result = new IntPtr(-1); return; }
                m.Result = new IntPtr(1); return;
            }
            if (m.Msg == 0x7e || m.Msg == 0x2e0) { base.WndProc(ref m); CancelBurst("display-change"); ClampToWorkArea(); displayedFrame = -1; Render(); return; }
            base.WndProc(ref m);
        }
        internal void TestDrag(Point offset)
        {
            int x = Width / 2, y = Height / 2;
            Native.SendMessage(Handle, 0x201, new IntPtr(1), Native.MousePoint(x, y));
            Native.SendMessage(Handle, 0x200, new IntPtr(1), Native.MousePoint(x + offset.X, y + offset.Y));
            Native.SendMessage(Handle, 0x202, IntPtr.Zero, Native.MousePoint(x, y));
        }
        internal void DisposePet()
        {
            BurstActive = false;
            Audio.Dispose(); clickTimer.Stop(); clickTimer.Dispose(); Menu.Close(); Menu.Dispose(); Close(); Dispose();
        }
    }

    internal static class Native
    {
        [DllImport("winmm.dll", EntryPoint = "mciSendStringW", CharSet = CharSet.Unicode)] internal static extern uint MciSendString(string command, StringBuilder result, int resultLength, IntPtr callback);
        [DllImport("winmm.dll", EntryPoint = "mciGetErrorStringW", CharSet = CharSet.Unicode)] internal static extern bool MciGetErrorString(uint error, StringBuilder message, int messageLength);
        [StructLayout(LayoutKind.Sequential)] internal struct POINT { internal int x; internal int y; internal POINT(int px, int py) { x = px; y = py; } }
        [StructLayout(LayoutKind.Sequential)] internal struct SIZE { internal int cx; internal int cy; internal SIZE(int x, int y) { cx = x; cy = y; } }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] internal struct BLENDFUNCTION { internal byte BlendOp; internal byte BlendFlags; internal byte SourceConstantAlpha; internal byte AlphaFormat; }
        [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
        [DllImport("user32.dll", SetLastError = true)] private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr destinationDC, ref POINT destination, ref SIZE size, IntPtr sourceDC, ref POINT source, int colorKey, ref BLENDFUNCTION blend, int flags);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
        [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] internal static extern int GetWindowLong(IntPtr hwnd, int index);
        internal static IntPtr MousePoint(int x, int y) { return new IntPtr((y << 16) | (x & 0xffff)); }
        internal static void RenderLayered(IntPtr hwnd, Bitmap image, int x, int y)
        {
            IntPtr screen = GetDC(IntPtr.Zero);
            IntPtr memory = IntPtr.Zero, bitmap = IntPtr.Zero, old = IntPtr.Zero;
            try
            {
                memory = CreateCompatibleDC(screen);
                if (memory == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "无法创建透明窗口绘图设备。");
                bitmap = image.GetHbitmap(Color.FromArgb(0));
                old = SelectObject(memory, bitmap);
                POINT destination = new POINT(x, y), source = new POINT(0, 0);
                SIZE size = new SIZE(image.Width, image.Height);
                BLENDFUNCTION blend = new BLENDFUNCTION(); blend.SourceConstantAlpha = 255; blend.AlphaFormat = 1;
                if (!UpdateLayeredWindow(hwnd, screen, ref destination, ref size, memory, ref source, 0, ref blend, 2))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "透明窗口绘制失败。");
            }
            finally
            {
                if (old != IntPtr.Zero && memory != IntPtr.Zero) SelectObject(memory, old);
                if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
                if (memory != IntPtr.Zero) DeleteDC(memory);
                if (screen != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screen);
            }
        }
    }
}
