using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace AptStudio.Desktop;

internal enum ChannelView
{
    Rgb,
    Alpha,
    Red,
    Green,
    Blue,
}

internal sealed class PreviewControl : Control
{
    private Bitmap? _image;
    private ImageAttributes _attributes;
    private TextureBrush? _checkerBrush;
    private Bitmap? _checkerTile;
    private int _checkerTileSize;
    private string? _message = "Open the game folder.";
    private bool _messageIsError;
    private float _scale = 1f;
    private float _dpiScale = 1f;
    private PointF _origin;
    private bool _fit = true;
    private bool _panning;
    private Point _panStart;
    private PointF _panOrigin;

    public PreviewControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw
            | ControlStyles.Selectable,
            true);
        BackColor = Theme.Background;
        TabStop = true;
        _attributes = BuildAttributes(ChannelView.Rgb);
    }

    public event EventHandler? ZoomChanged;

    public float Zoom => _scale;

    public bool IsFit => _fit;

    public bool HasImage => _image is not null;

    public void SetImage(Bitmap image)
    {
        ArgumentNullException.ThrowIfNull(image);
        Bitmap? previous = _image;
        _image = image;
        _message = null;
        _messageIsError = false;
        previous?.Dispose();
        Fit();
        Invalidate();
    }

    public void ShowMessage(string text, bool error)
    {
        Bitmap? previous = _image;
        _image = null;
        previous?.Dispose();
        _message = text;
        _messageIsError = error;
        _fit = true;
        _scale = 1f;
        ZoomChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    internal void SetDpiScale(float scale)
    {
        if (scale <= 0f)
        {
            return;
        }

        _dpiScale = scale;
        _checkerBrush?.Dispose();
        _checkerBrush = null;
        _checkerTile?.Dispose();
        _checkerTile = null;
        _checkerTileSize = 0;
        if (_fit)
        {
            Fit();
        }
        else
        {
            Invalidate();
        }
    }

    public void SetChannel(ChannelView view)
    {
        ImageAttributes previous = _attributes;
        _attributes = BuildAttributes(view);
        previous.Dispose();
        Invalidate();
    }

    public void Fit()
    {
        if (_image is null)
        {
            return;
        }

        float padding = 16f * DeviceDpi / 96f * _dpiScale;
        float scaleX = (ClientSize.Width - padding * 2) / _image.Width;
        float scaleY = (ClientSize.Height - padding * 2) / _image.Height;
        _scale = Math.Clamp(Math.Min(scaleX, scaleY), 0.01f, 16f);
        Center();
        _fit = true;
        ZoomChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ActualSize()
    {
        if (_image is null)
        {
            return;
        }

        _scale = 1f;
        Center();
        _fit = false;
        ZoomChanged?.Invoke(this, EventArgs.Empty);
    }

    public PointF Origin => _origin;

    internal void SetView(float zoom, PointF origin)
    {
        if (_image is null)
        {
            return;
        }

        _scale = Math.Clamp(zoom, 0.02f, 32f);
        _origin = origin;
        _fit = false;
        ZoomChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_fit)
        {
            Fit();
        }
        else
        {
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && _image is not null)
        {
            Focus();
            _panning = true;
            _panStart = e.Location;
            _panOrigin = _origin;
            Capture = true;
            Cursor = Cursors.SizeAll;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_panning)
        {
            return;
        }

        _origin = new PointF(
            _panOrigin.X + e.X - _panStart.X,
            _panOrigin.Y + e.Y - _panStart.Y);
        if (_fit)
        {
            _fit = false;
            ZoomChanged?.Invoke(this, EventArgs.Empty);
        }

        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left)
        {
            _panning = false;
            Capture = false;
            Cursor = Cursors.Default;
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_image is null)
        {
            return;
        }

        float imageX = (e.X - _origin.X) / _scale;
        float imageY = (e.Y - _origin.Y) / _scale;
        float factor = MathF.Pow(1.15f, e.Delta / 120f);
        float next = Math.Clamp(_scale * factor, 0.02f, 32f);
        if (Math.Abs(next - _scale) < 0.0001f)
        {
            return;
        }

        _scale = next;
        _origin = new PointF(e.X - imageX * _scale, e.Y - imageY * _scale);
        _fit = false;
        ZoomChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    protected override void OnDoubleClick(EventArgs e)
    {
        base.OnDoubleClick(e);
        Fit();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.Clear(Theme.Background);
        DrawCheckerboard(graphics);
        if (_image is null)
        {
            DrawMessage(graphics);
            return;
        }

        float width = _image.Width * _scale;
        float height = _image.Height * _scale;
        RectangleF destination = new(_origin.X, _origin.Y, width, height);
        RectangleF visible = RectangleF.Intersect(destination, ClientRectangle);
        if (visible.Width < 0.5f || visible.Height < 0.5f)
        {
            return;
        }

        float sourceX = Math.Clamp((visible.X - destination.X) / _scale, 0, _image.Width);
        float sourceY = Math.Clamp((visible.Y - destination.Y) / _scale, 0, _image.Height);
        float sourceWidth = Math.Min(visible.Width / _scale, _image.Width - sourceX);
        float sourceHeight = Math.Min(visible.Height / _scale, _image.Height - sourceY);
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            return;
        }

        graphics.InterpolationMode = _scale < 1f
            ? InterpolationMode.HighQualityBilinear
            : InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.DrawImage(
            _image,
            Rectangle.Round(visible),
            sourceX,
            sourceY,
            sourceWidth,
            sourceHeight,
            GraphicsUnit.Pixel,
            _attributes);
        using Pen border = new(Theme.Border);
        graphics.DrawRectangle(
            border, destination.X, destination.Y, destination.Width - 1, destination.Height - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _image?.Dispose();
            _attributes.Dispose();
            _checkerBrush?.Dispose();
            _checkerTile?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Center()
    {
        if (_image is null)
        {
            return;
        }

        _origin = new PointF(
            (ClientSize.Width - _image.Width * _scale) / 2f,
            (ClientSize.Height - _image.Height * _scale) / 2f);
        Invalidate();
    }

    private void DrawCheckerboard(Graphics graphics)
    {
        int expected = Math.Max(8, (int)Math.Round(8 * DeviceDpi / 96f * _dpiScale));
        if (_checkerBrush is null || _checkerTileSize != expected)
        {
            _checkerBrush?.Dispose();
            _checkerTile?.Dispose();
            _checkerTileSize = expected;
            int tileSize = expected * 2;
            _checkerTile = new Bitmap(tileSize, tileSize);
            using (Graphics tile = Graphics.FromImage(_checkerTile))
            {
                tile.Clear(Theme.CheckerDark);
                using SolidBrush light = new(Theme.CheckerLight);
                tile.FillRectangle(light, 0, 0, expected, expected);
                tile.FillRectangle(light, expected, expected, expected, expected);
            }

            _checkerBrush = new TextureBrush(_checkerTile);
        }

        graphics.FillRectangle(_checkerBrush, ClientRectangle);
    }

    private void DrawMessage(Graphics graphics)
    {
        if (string.IsNullOrEmpty(_message))
        {
            return;
        }

        Rectangle bounds = Rectangle.Inflate(ClientRectangle, -24, -24);
        TextRenderer.DrawText(
            graphics,
            _message,
            Font,
            bounds,
            _messageIsError ? Theme.Error : Theme.Muted,
            TextFormatFlags.HorizontalCenter
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.WordBreak);
    }

    private static ImageAttributes BuildAttributes(ChannelView view)
    {
        // gdi+ maps rows to source channels and adds the last row as a constant.
        float[][] rows = view switch
        {
            ChannelView.Rgb =>
            [
                [1, 0, 0, 0, 0],
                [0, 1, 0, 0, 0],
                [0, 0, 1, 0, 0],
                [0, 0, 0, 0, 0],
                [0, 0, 0, 1, 1],
            ],
            ChannelView.Alpha =>
            [
                [0, 0, 0, 0, 0],
                [0, 0, 0, 0, 0],
                [0, 0, 0, 0, 0],
                [1, 1, 1, 0, 0],
                [0, 0, 0, 1, 1],
            ],
            ChannelView.Red =>
            [
                [1, 1, 1, 0, 0],
                [0, 0, 0, 0, 0],
                [0, 0, 0, 0, 0],
                [0, 0, 0, 0, 0],
                [0, 0, 0, 1, 1],
            ],
            ChannelView.Green =>
            [
                [0, 0, 0, 0, 0],
                [1, 1, 1, 0, 0],
                [0, 0, 0, 0, 0],
                [0, 0, 0, 0, 0],
                [0, 0, 0, 1, 1],
            ],
            ChannelView.Blue =>
            [
                [0, 0, 0, 0, 0],
                [0, 0, 0, 0, 0],
                [1, 1, 1, 0, 0],
                [0, 0, 0, 0, 0],
                [0, 0, 0, 1, 1],
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(view)),
        };

        ImageAttributes attributes = new();
        attributes.SetColorMatrix(
            new ColorMatrix(rows), ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        return attributes;
    }
}
