using System.Globalization;
using AptStudio.Core.Audio;

namespace AptStudio.Desktop;

internal sealed class AudioView : Panel
{
    private readonly Label _title = new();
    private readonly Label _subtitle = new();
    private readonly Label _details = new();
    private readonly Label _source = new();
    private readonly Label _status = new();
    private readonly Label _note = new();
    private readonly Button _play = new();
    private readonly Button _export = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 200 };
    private readonly AudioPlayer _player = new();
    private AudioEntry? _entry;

    public AudioView()
    {
        BackColor = Theme.Background;
        DoubleBuffered = true;

        _title.AutoEllipsis = true;
        _title.Font = new Font("Segoe UI Semibold", 9.5f);
        _title.ForeColor = Theme.Text;
        _title.TextAlign = ContentAlignment.MiddleLeft;
        _title.Padding = new Padding(8, 0, 8, 0);
        _title.Dock = DockStyle.Top;
        _title.Height = 26;

        _subtitle.AutoEllipsis = true;
        _subtitle.ForeColor = Theme.Muted;
        _subtitle.TextAlign = ContentAlignment.MiddleLeft;
        _subtitle.Padding = new Padding(8, 0, 8, 0);
        _subtitle.Dock = DockStyle.Top;
        _subtitle.Height = 20;

        _details.ForeColor = Theme.Text;
        _details.TextAlign = ContentAlignment.TopLeft;
        _details.Padding = new Padding(10, 10, 10, 0);
        _details.Dock = DockStyle.Top;
        _details.Height = 42;

        _source.ForeColor = Theme.Muted;
        _source.TextAlign = ContentAlignment.TopLeft;
        _source.Padding = new Padding(10, 0, 10, 0);
        _source.Dock = DockStyle.Top;
        _source.Height = 44;

        _play.Text = "Play";
        _play.AutoSize = true;
        _play.FlatStyle = FlatStyle.Flat;
        _play.BackColor = Theme.Accent;
        _play.ForeColor = Color.White;
        _play.Padding = new Padding(14, 0, 14, 0);
        _play.MinimumSize = new Size(80, 28);
        _play.UseVisualStyleBackColor = false;
        _play.FlatAppearance.BorderSize = 0;
        _play.FlatAppearance.MouseOverBackColor = Theme.Selection;
        _play.FlatAppearance.MouseDownBackColor = Theme.SelectionInactive;
        _play.Enabled = false;
        _play.Click += (_, _) =>
        {
            if (_player.IsPlaying)
            {
                StopPlayback();
            }
            else
            {
                PlayRequested?.Invoke();
            }
        };

        _export.Text = "Export Ogg";
        _export.AutoSize = true;
        _export.FlatStyle = FlatStyle.Flat;
        _export.BackColor = Theme.Surface;
        _export.ForeColor = Theme.Text;
        _export.Padding = new Padding(10, 0, 10, 0);
        _export.MinimumSize = new Size(90, 28);
        _export.UseVisualStyleBackColor = false;
        _export.FlatAppearance.BorderSize = 0;
        _export.FlatAppearance.MouseOverBackColor = Theme.SelectionInactive;
        _export.FlatAppearance.MouseDownBackColor = Theme.Selection;
        _export.Enabled = false;
        _export.Click += (_, _) => ExportRequested?.Invoke();

        _status.AutoEllipsis = true;
        _status.ForeColor = Theme.Muted;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.Dock = DockStyle.Fill;

        TableLayoutPanel bar = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Theme.Background,
            Margin = Padding.Empty,
            Padding = new Padding(8, 4, 8, 4),
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.Controls.Add(_play, 0, 0);
        bar.Controls.Add(_export, 1, 0);
        bar.Controls.Add(_status, 2, 0);

        _note.AutoEllipsis = true;
        _note.ForeColor = Theme.Muted;
        _note.TextAlign = ContentAlignment.TopLeft;
        _note.Padding = new Padding(10, 18, 10, 0);
        _note.Dock = DockStyle.Fill;
        _note.Text = "Wwise Vorbis is converted to Ogg on demand. Playback goes through the system audio device; export writes a standard Ogg file.";

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            BackColor = Theme.Background,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(_title, 0, 0);
        layout.Controls.Add(_subtitle, 0, 1);
        layout.Controls.Add(_details, 0, 2);
        layout.Controls.Add(_source, 0, 3);
        layout.Controls.Add(bar, 0, 4);
        layout.Controls.Add(_note, 0, 5);
        Controls.Add(layout);

        _timer.Tick += (_, _) =>
        {
            if (_player.IsPlaying)
            {
                _status.Text = $"{Time(_player.Position.TotalSeconds)} / {Time(Duration)}";
                return;
            }

            _timer.Stop();
            _play.Text = "Play";
            _status.Text = "Finished";
        };
    }

    public event Action? PlayRequested;

    public event Action? ExportRequested;

    public AudioEntry? Entry => _entry;

    private double Duration => _entry?.DurationSeconds ?? 0;

    public void SetAudio(AudioEntry entry)
    {
        StopPlayback();
        _entry = entry;
        WemInfo info = entry.Info;
        _title.Text = entry.Name ?? $"Audio {entry.IdText}";
        _subtitle.Text =
            $"{entry.PackName} | bank 0x{entry.BankId:X8} | {entry.Language} | {entry.IdText}";
        _details.Text =
            $"{info.Codec} | {info.Channels} ch | {info.SampleRate:N0} Hz | {Time(entry.DurationSeconds)}\n"
            + $"{info.SampleCount:N0} samples | {entry.Size:N0} bytes";
        _source.Text =
            $"offset {entry.Offset:N0} | {Path.GetFileName(entry.PackPath)}"
            + (entry.BankId == 0 ? " | header sound table" : " | bank DIDX");
        _play.Enabled = true;
        _play.Text = "Play";
        _export.Enabled = true;
        _status.ForeColor = Theme.Muted;
        _status.Text = "Ready";
    }

    public void SetBusy(string text)
    {
        _play.Enabled = false;
        _export.Enabled = false;
        _status.Text = text;
    }

    public void StartPlayback(byte[] ogg)
    {
        try
        {
            _player.Play(ogg);
            _play.Text = "Stop";
            _play.Enabled = true;
            _export.Enabled = _entry is not null;
            _status.ForeColor = Theme.Muted;
            _status.Text = $"0:00 / {Time(Duration)}";
            _timer.Start();
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException)
        {
            ShowError(ex.Message);
        }
    }

    public void StopPlayback()
    {
        _timer.Stop();
        _player.Stop();
        _play.Text = "Play";
        if (_entry is not null)
        {
            _play.Enabled = true;
            _export.Enabled = true;
            _status.ForeColor = Theme.Muted;
            _status.Text = "Ready";
        }
    }

    public void SetStatus(string text)
    {
        _status.ForeColor = Theme.Muted;
        _status.Text = text;
    }

    public void ShowError(string message)
    {
        _timer.Stop();
        _player.Stop();
        _play.Text = "Play";
        _play.Enabled = _entry is not null;
        _export.Enabled = _entry is not null;
        _status.Text = message;
        _status.ForeColor = Theme.Error;
    }

    public void Reset()
    {
        StopPlayback();
        _entry = null;
        _title.Text = "";
        _subtitle.Text = "";
        _details.Text = "";
        _source.Text = "";
        _status.Text = "";
        _status.ForeColor = Theme.Muted;
        _play.Enabled = false;
        _export.Enabled = false;
    }

    public void SetDpiScale(float scale)
    {
        if (scale <= 0f)
        {
            return;
        }

        _title.Font = new Font("Segoe UI Semibold", 9.5f * scale);
        _details.Font = new Font("Segoe UI", 9f * scale);
        _source.Font = new Font("Segoe UI", 9f * scale);
        _status.Font = new Font("Segoe UI", 9f * scale);
        _play.Font = new Font("Segoe UI Semibold", 9f * scale);
        _export.Font = new Font("Segoe UI", 9f * scale);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _player.Dispose();
        }

        base.Dispose(disposing);
    }

    private static string Time(double seconds)
    {
        if (seconds <= 0)
        {
            return "0:00";
        }

        TimeSpan value = TimeSpan.FromSeconds(seconds);
        return value.TotalHours >= 1
            ? value.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : value.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }
}
