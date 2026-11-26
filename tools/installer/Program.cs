using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace HnsInstaller
{
    // ---------------------------------------------------------------------------------------------
    // Colours: the same dark red look as the in-game killer menu.
    // ---------------------------------------------------------------------------------------------
    static class Theme
    {
        public static readonly Color Bg      = Color.FromArgb(18, 15, 15);
        public static readonly Color Card    = Color.FromArgb(34, 28, 28);
        public static readonly Color CardHot = Color.FromArgb(50, 41, 41);
        public static readonly Color Line    = Color.FromArgb(66, 54, 54);
        public static readonly Color Red     = Color.FromArgb(140, 20, 20);
        public static readonly Color RedHot  = Color.FromArgb(180, 30, 30);
        public static readonly Color Text    = Color.FromArgb(235, 235, 235);
        public static readonly Color Muted   = Color.FromArgb(150, 150, 150);
        public static readonly Color Accent  = Color.FromArgb(240, 90, 90);
        public static readonly Color Good    = Color.FromArgb(110, 200, 120);

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            int d = Math.Max(2, radius * 2);
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // A dark rounded panel with a thin border.
    class CardPanel : Panel
    {
        public int Radius = 8;
        public CardPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Bg);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = Theme.Round(r, Radius);
            using var fill = new SolidBrush(Theme.Card);
            using var pen = new Pen(Theme.Line);
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(pen, path);
        }
    }

    // The big red button.
    class BigButton : Control
    {
        bool hover, down;
        public BigButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            BackColor = Theme.Bg;
            Font = new Font("Segoe UI Semibold", 11f);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Focus(); Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override bool IsInputKey(Keys keyData) { return keyData == Keys.Enter || keyData == Keys.Space || base.IsInputKey(keyData); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (Enabled && (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)) { OnClick(EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = !Enabled ? Theme.Card : down ? Theme.Red : hover ? Theme.RedHot : Color.FromArgb(160, 24, 24);
            using (var path = Theme.Round(r, 8))
            using (var b = new SolidBrush(fill))
            {
                g.FillPath(b, path);
                if (Focused && Enabled)
                    using (var pen = new Pen(Theme.Accent)) g.DrawPath(pen, path);
            }
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var tb = new SolidBrush(Enabled ? Color.White : Theme.Muted);
            g.DrawString(Text, Font, tb, new RectangleF(0, 0, Width, Height), sf);
        }
    }

    // The thin progress line.
    class SlimBar : Control
    {
        double value;
        public double Value { get { return value; } set { this.value = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public SlimBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var track = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.Round(track, Height / 2))
            using (var b = new SolidBrush(Theme.Card))
                g.FillPath(b, path);
            int w = (int)Math.Round((Width - 1) * value / 100.0);
            if (w > Height)
            {
                var fillRect = new Rectangle(0, 0, w, Height - 1);
                using var path = Theme.Round(fillRect, Height / 2);
                using var br = new LinearGradientBrush(fillRect, Theme.Red, Theme.Accent, LinearGradientMode.Horizontal);
                g.FillPath(br, path);
            }
        }
    }

    public class MainForm : Form
    {
        // Every addon is its own zip on the GitHub release "mods", plus manifest.txt listing them.
        private const string BaseUrl =
            "https://github.com/Chupaflaba/Fight-or-Flight-Hide-n-Seek/releases/download/mods/";
        private static string Base { get { return Environment.GetEnvironmentVariable("FOF_BASE_URL") ?? BaseUrl; } }

        private sealed class Entry { public string Name, Hash; public long Size; }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        private readonly float scale;
        private int S(int v) { return (int)Math.Round(v * scale); }

        private Label pathLabel, changeLink, statusLabel, detailsLink;
        private readonly ToolTip tip = new ToolTip();
        private CardPanel logCard;
        private TextBox logBox;
        private BigButton mainButton;
        private SlimBar bar;
        private string gmodAddonsPath;
        private bool busy, installedOk, detailsOpen;

        private int collapsedHeight, expandedHeight;

        public MainForm()
        {
#if NETFW
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f;
#else
            scale = DeviceDpi / 96f;
#endif
            InitializeUI();
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            DetectGmodPath();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int on = 1; // dark title bar (Windows 10 2004+ and 11); harmless if it is not supported
                if (DwmSetWindowAttribute(Handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(Handle, 19, ref on, 4);
            }
            catch { }
        }

        private Label MakeLabel(string text, int x, int y, int w, int h, Font font, Color color)
        {
            var l = new Label
            {
                Text = text, Location = new Point(S(x), S(y)), Size = new Size(S(w), S(h)),
                Font = font, ForeColor = color, BackColor = Theme.Bg, AutoEllipsis = true, UseMnemonic = false
            };
            return l;
        }

        private void InitializeUI()
        {
            Text = "Fight or Flight : Hide N Seek";
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            BackColor = Theme.Bg;
            DoubleBuffered = true;

            const int W = 460, M = 24, IW = W - M * 2;
            collapsedHeight = 262;
            expandedHeight = collapsedHeight + 168;
            ClientSize = new Size(S(W), S(collapsedHeight));

            // header: red tick, title, subtitle
            var tick = new Panel { Location = new Point(S(M), S(24)), Size = new Size(S(4), S(38)), BackColor = Theme.RedHot };
            var title = MakeLabel("Fight or Flight : Hide N Seek", M + 14, 20, IW - 14, 28, new Font("Segoe UI Semibold", 16f), Theme.Text);
            var sub = MakeLabel("Installs the gamemode addons straight into Garry's Mod", M + 15, 47, IW - 15, 18, new Font("Segoe UI", 9f), Theme.Muted);

            // folder card
            var folderCard = new CardPanel { Location = new Point(S(M), S(84)), Size = new Size(S(IW), S(60)) };
            var folderCaption = new Label
            {
                Text = "GARRY'S MOD ADDONS FOLDER", Location = new Point(S(14), S(10)), Size = new Size(S(240), S(14)),
                Font = new Font("Segoe UI Semibold", 7.5f), ForeColor = Theme.Muted, BackColor = Theme.Card
            };
            pathLabel = new Label
            {
                Text = "Looking for Garry's Mod...", Location = new Point(S(14), S(28)), Size = new Size(S(IW - 28 - 70), S(22)),
                Font = new Font("Segoe UI", 9.5f), ForeColor = Theme.Text, BackColor = Theme.Card, AutoEllipsis = true, UseMnemonic = false
            };
            changeLink = new Label
            {
                Text = "Change", Location = new Point(S(IW - 14 - 62), S(28)), Size = new Size(S(62), S(22)),
                Font = new Font("Segoe UI Semibold", 9.5f), ForeColor = Theme.Accent, BackColor = Theme.Card,
                TextAlign = ContentAlignment.MiddleRight, Cursor = Cursors.Hand
            };
            changeLink.Click += (s, e) => { if (!busy) BrowseForFolder(); };
            changeLink.MouseEnter += (s, e) => changeLink.ForeColor = Color.White;
            changeLink.MouseLeave += (s, e) => changeLink.ForeColor = Theme.Accent;
            folderCard.Controls.Add(folderCaption);
            folderCard.Controls.Add(pathLabel);
            folderCard.Controls.Add(changeLink);

            // main button
            mainButton = new BigButton
            {
                Text = "INSTALL / UPDATE", Location = new Point(S(M), S(162)), Size = new Size(S(IW), S(46))
            };
            mainButton.Click += MainButton_Click;

            // progress + status line + details toggle
            bar = new SlimBar { Location = new Point(S(M), S(222)), Size = new Size(S(IW), S(6)) };
            statusLabel = MakeLabel("Ready.", M, 234, IW - 70, 18, new Font("Segoe UI", 9f), Theme.Muted);
            detailsLink = new Label
            {
                Text = "Details \u25BE", Location = new Point(S(M + IW - 66), S(234)), Size = new Size(S(66), S(18)),
                Font = new Font("Segoe UI", 9f), ForeColor = Theme.Muted, BackColor = Theme.Bg,
                TextAlign = ContentAlignment.MiddleRight, Cursor = Cursors.Hand
            };
            detailsLink.Click += (s, e) => SetDetails(!detailsOpen);
            detailsLink.MouseEnter += (s, e) => detailsLink.ForeColor = Theme.Text;
            detailsLink.MouseLeave += (s, e) => detailsLink.ForeColor = Theme.Muted;

            // log (hidden until Details is opened)
            logCard = new CardPanel { Location = new Point(S(M), S(collapsedHeight - 2)), Size = new Size(S(IW), S(150)), Visible = false };
            logBox = new TextBox
            {
                Location = new Point(S(10), S(8)), Size = new Size(S(IW - 20), S(134)),
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 8.5f), BackColor = Theme.Card, ForeColor = Color.FromArgb(205, 200, 200)
            };
            logCard.Controls.Add(logBox);

            Controls.Add(tick);
            Controls.Add(title);
            Controls.Add(sub);
            Controls.Add(folderCard);
            Controls.Add(mainButton);
            Controls.Add(bar);
            Controls.Add(statusLabel);
            Controls.Add(detailsLink);
            Controls.Add(logCard);
        }

        private void SetDetails(bool open)
        {
            detailsOpen = open;
            detailsLink.Text = open ? "Details \u25B4" : "Details \u25BE";
            logCard.Visible = open;
            ClientSize = new Size(ClientSize.Width, S(open ? expandedHeight : collapsedHeight));
        }

        private void Ui(Action a)
        {
            if (IsDisposed) return;
            if (InvokeRequired) BeginInvoke(a); else a();
        }

        private void Log(string msg)
        {
            Ui(() => logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\r\n"));
        }

        private void SetStatus(string text, Color color)
        {
            Ui(() => { statusLabel.Text = text; statusLabel.ForeColor = color; });
        }

        private void SetProgress(double v) { Ui(() => bar.Value = v); }

        private static string Mb(long bytes) { return (bytes / 1048576.0).ToString("0.0") + " MB"; }

        // ------------------------------------------------------------------ finding Garry's Mod
        private void DetectGmodPath()
        {
            string steamPath = null;

            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (key != null)
                        steamPath = key.GetValue("SteamPath") as string;
                }
            }
            catch { }

            if (string.IsNullOrEmpty(steamPath))
            {
                var candidates = new[] {
                    @"C:\Program Files (x86)\Steam",
                    @"C:\Program Files\Steam",
                    @"D:\Steam",
                    @"D:\Program Files (x86)\Steam",
                    @"E:\Steam"
                };
                foreach (var candidate in candidates)
                {
                    if (Directory.Exists(candidate)) { steamPath = candidate; break; }
                }
            }

            // Check default library first
            if (!string.IsNullOrEmpty(steamPath))
            {
                var defaultAddons = Path.Combine(steamPath, "steamapps", "common", "GarrysMod", "garrysmod", "addons");
                if (Directory.Exists(defaultAddons))
                {
                    SetPath(defaultAddons, "Detected");
                    return;
                }

                // Check additional Steam library folders (for non-C: drive installs)
                var libraryVdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(libraryVdf))
                {
                    try
                    {
                        var content = File.ReadAllText(libraryVdf);
                        var matches = Regex.Matches(content, "\"path\"\\s+\"([^\"]+)\"");
                        foreach (Match m in matches)
                        {
                            var libPath = m.Groups[1].Value.Replace(@"\\", @"\");
                            var addons = Path.Combine(libPath, "steamapps", "common", "GarrysMod", "garrysmod", "addons");
                            if (Directory.Exists(addons))
                            {
                                SetPath(addons, "Detected");
                                return;
                            }
                        }
                    }
                    catch (Exception ex) { Log("Warning: could not parse Steam library folders: " + ex.Message); }
                }
            }

            pathLabel.Text = "Not found - click Change and pick garrysmod\\addons";
            pathLabel.ForeColor = Theme.Accent;
            SetStatus("Garry's Mod was not found automatically.", Theme.Accent);
            Log("Could not auto-detect Garry's Mod.");
            Log("It's typically at: <SteamLibrary>\\steamapps\\common\\GarrysMod\\garrysmod\\addons");
        }

        private void SetPath(string path, string how)
        {
            gmodAddonsPath = path;
            pathLabel.Text = ShortPath(path);
            tip.SetToolTip(pathLabel, path); // the full path on hover
            pathLabel.ForeColor = Theme.Text;
            Log(how + " GMod addons folder: " + path);
        }

        // C:\Program Files (x86)\Steam\steamapps\common\GarrysMod\garrysmod\addons  ->  C:\...\GarrysMod\garrysmod\addons
        private static string ShortPath(string path)
        {
            var parts = path.Split('\\');
            if (path.Length <= 46 || parts.Length <= 4) return path;
            return parts[0] + "\\...\\" + string.Join("\\", parts, parts.Length - 3, 3);
        }

        private void BrowseForFolder()
        {
            using var fbd = new FolderBrowserDialog
            {
#if !NETFW
                UseDescriptionForTitle = true,
#endif
                Description = "Select your GMod addons folder (usually 'garrysmod\\addons')"
            };
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                SetPath(fbd.SelectedPath, "Selected");
                installedOk = false;
                mainButton.Text = "INSTALL / UPDATE";
                SetStatus("Ready.", Theme.Muted);
            }
        }

        // ------------------------------------------------------------------ the button
        private void LaunchGame()
        {
            try { Process.Start(new ProcessStartInfo("steam://rungameid/4000") { UseShellExecute = true }); }
            catch (Exception ex) { Log("Could not start Garry's Mod: " + ex.Message); }
        }

        private async void MainButton_Click(object sender, EventArgs _e)
        {
            if (busy) return;
            if (installedOk) { LaunchGame(); return; }

            if (string.IsNullOrEmpty(gmodAddonsPath) || !Directory.Exists(gmodAddonsPath))
            {
                SetStatus("Pick your Garry's Mod addons folder first.", Theme.Accent);
                BrowseForFolder();
                return;
            }

            busy = true;
            mainButton.Enabled = false;
            mainButton.Text = "INSTALLING...";
            bar.Value = 0;

            var tempDir = Path.Combine(Path.GetTempPath(), "fof_" + Guid.NewGuid());

            try
            {
                Directory.CreateDirectory(tempDir);
                SetStatus("Checking for updates...", Theme.Muted);
                Log("Manifest: " + Base + "manifest.txt");
                var manifest = await FetchManifest();
                var state = LoadState();

                var todo = manifest.Where(e =>
                {
                    string h;
                    return !state.TryGetValue(e.Name, out h) || h != e.Hash
                           || !Directory.Exists(Path.Combine(gmodAddonsPath, e.Name));
                }).ToList();
                var gone = state.Keys.Where(k => !manifest.Any(m => m.Name == k)).ToList();
                Log(manifest.Count + " addons listed, " + todo.Count + " to download, " + gone.Count + " to remove.");

                var newState = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var e in manifest)
                {
                    string h;
                    if (state.TryGetValue(e.Name, out h) && h == e.Hash && Directory.Exists(Path.Combine(gmodAddonsPath, e.Name)))
                        newState[e.Name] = h;
                }

                int inUse = 0;
                long total = todo.Sum(e => e.Size), done = 0;
                using (var handler = new HttpClientHandler { AllowAutoRedirect = true })
                using (var client = new HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromMinutes(15);
                    client.DefaultRequestHeaders.Add("User-Agent", "HnsInstaller/3.0");
                    for (int i = 0; i < todo.Count; i++)
                    {
                        var e = todo[i];
                        var zip = Path.Combine(tempDir, e.Name + ".zip");
                        Log("Downloading " + e.Name + " (" + Mb(e.Size) + ")");
                        await DownloadFileAsync(client, Base + Uri.EscapeDataString(e.Name) + ".zip", zip, done, total, e.Name, i + 1, todo.Count);
                        done += e.Size;
                        SetStatus("Installing " + e.Name + "...", Theme.Muted);
                        int bad = await Task.Run(() => ExtractAddon(zip, Path.Combine(gmodAddonsPath, e.Name)));
                        try { File.Delete(zip); } catch { }
                        if (bad == 0) newState[e.Name] = e.Hash; else inUse += bad;
                    }
                }

                foreach (var g in gone)
                {
                    var dir = Path.Combine(gmodAddonsPath, g);
                    try { if (Directory.Exists(dir)) { Directory.Delete(dir, true); Log("Removed " + g); } }
                    catch (Exception ex) { Log("Could not remove " + g + ": " + ex.Message); newState[g] = state[g]; }
                }
                SaveState(newState);

                SetProgress(100);
                if (inUse > 0)
                {
                    SetStatus($"Installed, but {inUse} files were in use. Close Garry's Mod and run it again.", Theme.Accent);
                    mainButton.Text = "TRY AGAIN";
                    Log($"{inUse} files were in use and were skipped.");
                }
                else
                {
                    SetStatus(todo.Count == 0 ? "Already up to date." : "All set. Restart Garry's Mod to load the addons.", Theme.Good);
                    mainButton.Text = "LAUNCH GARRY'S MOD";
                    installedOk = true;
                    Log(todo.Count == 0 ? "Nothing to update." : "INSTALLATION SUCCESSFUL. Restart Garry's Mod to see the addons active.");
                }
            }
            catch (Exception ex)
            {
                Log("ERROR: " + ex.Message);
                if (ex.InnerException != null)
                    Log("Details: " + ex.InnerException.Message);
                SetStatus("Failed: " + ex.Message, Theme.Accent);
                mainButton.Text = "TRY AGAIN";
                if (!detailsOpen) SetDetails(true);
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
                busy = false;
                mainButton.Enabled = true;
            }
        }

        // ------------------------------------------------------------------ manifest + state
        private async Task<List<Entry>> FetchManifest()
        {
            using (var handler = new HttpClientHandler { AllowAutoRedirect = true })
            using (var client = new HttpClient(handler))
            {
                client.Timeout = TimeSpan.FromMinutes(2);
                client.DefaultRequestHeaders.Add("User-Agent", "HnsInstaller/3.0");
                var text = await client.GetStringAsync(Base + "manifest.txt");
                var lines = text.Replace("\r", "").Split('\n');
                if (lines.Length == 0 || lines[0].Trim() != "FOF1") throw new Exception("The addon list on the server is not in the expected format.");
                var list = new List<Entry>();
                foreach (var l in lines.Skip(1))
                {
                    var p = l.Split('|');
                    long size;
                    if (p.Length == 3 && long.TryParse(p[2], out size) && p[0].Length > 0 && p[0].IndexOfAny(new[] { '/', '\\', ':' }) < 0 && p[0] != "..")
                        list.Add(new Entry { Name = p[0], Hash = p[1], Size = size });
                }
                if (list.Count == 0) throw new Exception("The addon list on the server is empty.");
                return list;
            }
        }

        private string StatePath
        {
            get
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FightOrFlightInstaller");
                return Path.Combine(dir, "installed.txt");
            }
        }

        // Which addon versions this installer put into the chosen addons folder last time.
        private Dictionary<string, string> LoadState()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(StatePath)) return d;
                var lines = File.ReadAllLines(StatePath);
                if (lines.Length == 0 || !string.Equals(lines[0], gmodAddonsPath, StringComparison.OrdinalIgnoreCase)) return d;
                foreach (var l in lines.Skip(1))
                {
                    var p = l.Split('|');
                    if (p.Length == 2) d[p[0]] = p[1];
                }
            }
            catch { }
            return d;
        }

        private void SaveState(Dictionary<string, string> state)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StatePath));
                var lines = new List<string> { gmodAddonsPath };
                lines.AddRange(state.Select(kv => kv.Key + "|" + kv.Value));
                File.WriteAllLines(StatePath, lines.ToArray());
            }
            catch (Exception ex) { Log("Could not save install state: " + ex.Message); }
        }

        // ------------------------------------------------------------------ download
        private async Task DownloadFileAsync(HttpClient client, string url, string destPath, long doneBefore, long totalAll, string name, int index, int count)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var buffer = new byte[81920];
            long fileRead = 0;
            var lastStatusTime = DateTime.MinValue;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);

            int read;
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read);
                fileRead += read;

                var now = DateTime.UtcNow;
                if ((now - lastStatusTime).TotalMilliseconds >= 100)
                {
                    lastStatusTime = now;
                    long all = doneBefore + fileRead;
                    if (totalAll > 0) SetProgress(Math.Min(85.0, all * 85.0 / totalAll));
                    SetStatus($"Downloading {name}  ({index}/{count})  {Mb(all)} / {Mb(totalAll)}", Theme.Muted);
                }
            }
        }

        // ------------------------------------------------------------------ extract one addon (returns how many files were in use and skipped)
        private int ExtractAddon(string zipPath, string addonDir)
        {
            // Start clean so files you removed from an addon disappear too. If something is locked, overwrite in place instead.
            try { if (Directory.Exists(addonDir)) Directory.Delete(addonDir, true); } catch { }
            Directory.CreateDirectory(addonDir);

            string destFull = Path.GetFullPath(addonDir).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            int inUse = 0;
            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue; // folder entry
                var extractPath = Path.GetFullPath(Path.Combine(addonDir, entry.FullName));
                if (!extractPath.StartsWith(destFull, StringComparison.OrdinalIgnoreCase))
                {
                    Log("Skipped (outside the addon folder): " + entry.FullName);
                    continue;
                }
                var parentDir = Path.GetDirectoryName(extractPath);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir)) Directory.CreateDirectory(parentDir);
                try { entry.ExtractToFile(extractPath, true); }
                catch (IOException ioEx)
                {
                    inUse++;
                    Log("Skipped (in use): " + entry.FullName + " (" + ioEx.Message + ")");
                }
            }
            return inUse;
        }

        [STAThread]
        public static void Main()
        {
#if NETFW
            // Stock .NET Framework build: older TLS defaults would break the GitHub download.
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
#else
            ApplicationConfiguration.Initialize();
#endif
            Application.Run(new MainForm());
        }
    }
}
