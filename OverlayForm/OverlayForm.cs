using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OverlayForm
{
    public partial class OverlayForm : Form
    {
        SKControl canvas;
        System.Windows.Forms.Timer timer;
        Random rand = new Random();

        class Danmaku
        {
            public string Text;
            public SKColor Color;
            public float X;
            public float Y;
            public float Speed;
            public float Width;
        }

        List<Danmaku> danmakus = new List<Danmaku>();

        List<string> textList = new List<string>();
        string textFileName = "danmaku.txt";

        List<string> colorList = new List<string>();
        string colorFileName = "color.txt";

        int maxOnScreen = 20;
        int fontSize = 32;

        float[] trackLastX;

        // 全局熱鍵 ID
        const int HOTKEY_HIDE = 1;
        const int HOTKEY_CLOSE = 2;

        // WinAPI
        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        const uint MOD_ALT = 0x0001;
        const uint MOD_CONTROL = 0x0002;

        // 完整路徑
        string textFileFullPath;
        string colorFileFullPath;

        public OverlayForm()
        {
            // 設定完整路徑
            textFileFullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, textFileName);
            colorFileFullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, colorFileName);

            // Form 設定
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            ShowInTaskbar = false;
            Bounds = Screen.PrimaryScreen.Bounds;
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;

            // SKControl 設定
            canvas = new SKControl();
            canvas.Dock = DockStyle.Fill;
            canvas.PaintSurface += Canvas_PaintSurface;
            Controls.Add(canvas);

            // 分軌道
            int trackCount = this.Height / (fontSize + 10);
            trackLastX = new float[trackCount];

            // 讀文字檔 & 顏色檔
            LoadTextFile();
            LoadColorFile();

            // 設定 FileSystemWatcher
            SetupFileWatcher();

            // Timer
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 16;
            timer.Tick += Timer_Tick;
            timer.Start();

            // 註冊全局快捷鍵
            RegisterHotKey(this.Handle, HOTKEY_HIDE, MOD_CONTROL | MOD_ALT, (uint)Keys.H);
            RegisterHotKey(this.Handle, HOTKEY_CLOSE, MOD_CONTROL | MOD_ALT, (uint)Keys.Q);
        }

        private void SetupFileWatcher()
        {
            // 監控文字檔
            var textWatcher = new FileSystemWatcher(Path.GetDirectoryName(textFileFullPath))
            {
                Filter = Path.GetFileName(textFileFullPath),
                NotifyFilter = NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            textWatcher.Changed += (s, e) => LoadTextFile();

            // 監控顏色檔
            var colorWatcher = new FileSystemWatcher(Path.GetDirectoryName(colorFileFullPath))
            {
                Filter = Path.GetFileName(colorFileFullPath),
                NotifyFilter = NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            colorWatcher.Changed += (s, e) => LoadColorFile();
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_HOTKEY = 0x0312;
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == HOTKEY_HIDE)
                {
                    this.Visible = !this.Visible; // 隱藏/顯示
                }
                else if (id == HOTKEY_CLOSE)
                {
                    this.Close(); // 關閉程式
                }
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            UnregisterHotKey(this.Handle, HOTKEY_HIDE);
            UnregisterHotKey(this.Handle, HOTKEY_CLOSE);
            base.OnFormClosed(e);
        }

        private void LoadTextFile()
        {
            if (!File.Exists(textFileFullPath))
            {
                File.WriteAllLines(textFileFullPath, new string[] {
                    "ctrl + alt + h 隱藏",
                    "ctrl + alt + q 關閉",
                    "加入字幕 danmaku.txt",
                    "加入顏色 color.txt"
                });
            }
            try
            {
                textList = new List<string>(File.ReadAllLines(textFileFullPath));
            }
            catch { } // 避免檔案編輯中讀取錯誤
        }

        private void LoadColorFile()
        {
            if (!File.Exists(colorFileFullPath))
            {
                File.WriteAllLines(colorFileFullPath, new string[] {
                    "#FF4500", "#FF69B4", "#00CED1", "#1E90FF", "#FFD700"
                });
            }
            try
            {
                colorList = new List<string>(File.ReadAllLines(colorFileFullPath));
            }
            catch { }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdateDanmaku();
            canvas.Invalidate();
        }

        private void UpdateDanmaku()
        {
            int trackHeight = fontSize + 10;

            for (int i = danmakus.Count - 1; i >= 0; i--)
            {
                var d = danmakus[i];
                d.X -= d.Speed;

                if (d.X < -d.Width)
                {
                    int track = (int)(d.Y / trackHeight);
                    trackLastX[track] = 0;
                    danmakus.RemoveAt(i);
                }
            }

            int toAdd = maxOnScreen - danmakus.Count;
            for (int i = 0; i < toAdd; i++)
            {
                if (textList.Count == 0) break;

                string text = textList[rand.Next(textList.Count)];
                SKColor color = colorList.Count > 0 ? SKColor.Parse(colorList[rand.Next(colorList.Count)]) : SKColors.White;

                AddDanmaku(text, color);
            }
        }

        private void AddDanmaku(string text, SKColor color)
        {
            int trackCount = this.Height / (fontSize + 10);
            int track;
            int tryCount = 0;

            do
            {
                track = rand.Next(trackCount);
                tryCount++;
            } while (trackLastX[track] > this.Width - 100 && tryCount < 10);

            float y = track * (fontSize + 10);
            float startX = this.Width + rand.Next(0, 300);
            trackLastX[track] = startX;

            using (var paint = new SKPaint { TextSize = fontSize })
            {
                paint.Typeface = SKTypeface.FromFamilyName("Microsoft JhengHei", SKFontStyle.Bold);
                float w = paint.MeasureText(text);
                danmakus.Add(new Danmaku
                {
                    Text = text,
                    Color = color,
                    X = startX,
                    Y = y,
                    Speed = 3 + (float)rand.NextDouble() * 2,
                    Width = w
                });
            }
        }

        private void Canvas_PaintSurface(object sender, SkiaSharp.Views.Desktop.SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            using (var paintFill = new SkiaSharp.SKPaint())
            using (var paintStroke = new SkiaSharp.SKPaint())
            {
                paintFill.IsAntialias = true;
                paintFill.TextSize = fontSize;
                paintFill.IsStroke = false;
                paintFill.Typeface = SKTypeface.FromFamilyName("Microsoft JhengHei", SKFontStyle.Bold);

                paintStroke.IsAntialias = true;
                paintStroke.TextSize = fontSize;
                paintStroke.IsStroke = true;
                paintStroke.StrokeWidth = 3;
                paintStroke.Color = SkiaSharp.SKColors.Black;
                paintStroke.Typeface = SKTypeface.FromFamilyName("Microsoft JhengHei", SKFontStyle.Bold);

                foreach (var d in danmakus)
                {
                    // 黑邊
                    canvas.DrawText(d.Text, d.X, d.Y + fontSize, paintStroke);
                    // 彩色文字
                    paintFill.Color = d.Color;
                    canvas.DrawText(d.Text, d.X, d.Y + fontSize, paintFill);
                }
            }
        }
    }
}
