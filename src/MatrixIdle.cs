using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

internal static class Program
{
    internal static void Log(string message)
    {
        try
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MatrixIdle.log");
            File.AppendAllText(path, DateTime.Now.ToString("o") + " " + message + Environment.NewLine);
        }
        catch { }
    }

    [STAThread]
    private static int Main(string[] args)
    {
        int idleSeconds = ParseIdleSeconds(args);
        if (idleSeconds < 0) return 2;
        Log("Process started; idle threshold=" + idleSeconds + "s");
        Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
        {
            Log("Unhandled UI error: " + e.Exception);
            Application.Exit();
        };
        AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
        {
            Log("Unhandled error: " + e.ExceptionObject);
        };
        bool created;
        using (var mutex = new Mutex(true, @"Local\MatrixIdleSaver", out created))
        {
            if (!created) return 0;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { Application.Run(new SaverContext(idleSeconds)); }
            finally { Log("Process stopped"); }
        }
        return 0;
    }

    private static int ParseIdleSeconds(string[] args)
    {
        if (args.Length == 0) return 60;
        int seconds;
        if (args.Length == 2 && args[0] == "--idle-seconds" &&
            int.TryParse(args[1], out seconds) && seconds >= 5 && seconds <= 3600)
            return seconds;
        Log("Invalid arguments. Use --idle-seconds N, where N is 5 through 3600.");
        return -1;
    }
}

internal static class UserIdle
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Tick;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    public static double Seconds()
    {
        var info = new LastInputInfo();
        info.Size = (uint)Marshal.SizeOf(typeof(LastInputInfo));
        if (!GetLastInputInfo(ref info)) return 0;
        uint now = unchecked((uint)Environment.TickCount);
        return unchecked(now - info.Tick) / 1000.0;
    }
}

internal sealed class SaverContext : ApplicationContext
{
    private readonly System.Windows.Forms.Timer idleTimer;
    private readonly List<RainForm> forms = new List<RainForm>();
    private readonly int idleSeconds;
    private bool showing;
    private bool firstTick = true;

    public SaverContext(int idleSeconds)
    {
        this.idleSeconds = idleSeconds;
        Program.Log("Context created");
        idleTimer = new System.Windows.Forms.Timer();
        idleTimer.Interval = 250;
        idleTimer.Tick += CheckIdle;
        idleTimer.Start();
    }

    private void CheckIdle(object sender, EventArgs e)
    {
        double seconds = UserIdle.Seconds();
        if (firstTick)
        {
            Program.Log("First tick, idle " + seconds.ToString("F1"));
            firstTick = false;
        }
        if (!showing && seconds >= idleSeconds)
        {
            ShowRain();
        }
        else if (showing && seconds < 1)
        {
            HideRain();
        }
    }

    private void ShowRain()
    {
        Program.Log("Showing rain on " + Screen.AllScreens.Length + " screen(s)");
        showing = true;
        foreach (Screen screen in Screen.AllScreens)
        {
            var form = new RainForm(screen.Bounds);
            forms.Add(form);
            form.Show();
            form.BringToFront();
            Program.Log("Form visible=" + form.Visible + ", handle=" + form.Handle);
        }
        if (forms.Count > 0) forms[0].Activate();
    }

    private void HideRain()
    {
        foreach (RainForm form in forms) form.Close();
        forms.Clear();
        showing = false;
    }
}

internal sealed class RainForm : Form
{
    private sealed class Column
    {
        public float Head;
        public float Speed;
        public int Tail;
    }

    private const int CellWidth = 17;
    private const int CellHeight = 19;
    private const string Glyphs = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ@#$%&*<>+";
    private readonly Random random = new Random(Guid.NewGuid().GetHashCode());
    private readonly Font font = new Font("Consolas", 14, FontStyle.Regular, GraphicsUnit.Pixel);
    private readonly Brush[] colors = new Brush[]
    {
        new SolidBrush(Color.FromArgb(220, 255, 220)),
        new SolidBrush(Color.FromArgb(110, 255, 130)),
        new SolidBrush(Color.FromArgb(55, 200, 80)),
        new SolidBrush(Color.FromArgb(30, 145, 55)),
        new SolidBrush(Color.FromArgb(18, 100, 37)),
        new SolidBrush(Color.FromArgb(10, 65, 25))
    };
    private readonly Column[] columns;
    private readonly System.Windows.Forms.Timer frameTimer;
    private readonly int rows;

    public RainForm(Rectangle bounds)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        BackColor = Color.Black;
        TopMost = true;
        ShowInTaskbar = true;
        Text = "Matrix Idle";
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        rows = bounds.Height / CellHeight + 2;
        columns = new Column[bounds.Width / CellWidth + 2];
        for (int i = 0; i < columns.Length; i++) ResetColumn(i, true);
        frameTimer = new System.Windows.Forms.Timer();
        frameTimer.Interval = 70;
        frameTimer.Tick += Advance;
        Shown += delegate { frameTimer.Start(); };
        FormClosed += delegate { frameTimer.Stop(); frameTimer.Dispose(); font.Dispose(); foreach (Brush color in colors) color.Dispose(); };
    }

    private void ResetColumn(int index, bool initial)
    {
        columns[index] = new Column
        {
            Head = initial ? random.Next(-rows, rows) : -random.Next(2, rows / 2 + 2),
            Speed = 0.45f + (float)random.NextDouble() * 0.75f,
            Tail = random.Next(8, 26)
        };
    }

    private void Advance(object sender, EventArgs e)
    {
        for (int i = 0; i < columns.Length; i++)
        {
            columns[i].Head += columns[i].Speed;
            if (columns[i].Head - columns[i].Tail > rows) ResetColumn(i, false);
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Color.Black);
        e.Graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        for (int x = 0; x < columns.Length; x++)
        {
            Column column = columns[x];
            int head = (int)column.Head;
            for (int tail = 0; tail < column.Tail; tail++)
            {
                int row = head - tail;
                if (row < 0 || row > rows) continue;
                int level = tail == 0 ? 0 : Math.Min(5, 1 + tail * 5 / column.Tail);
                char glyph = Glyphs[random.Next(Glyphs.Length)];
                e.Graphics.DrawString(glyph.ToString(), font, colors[level], x * CellWidth, row * CellHeight);
            }
        }
    }
}
