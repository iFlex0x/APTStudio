using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using AptStudio.Core.Meshes;

namespace AptStudio.Desktop;

internal sealed class MeshView : Control
{
    private static readonly Vector3 LightDirection = Vector3.Normalize(new Vector3(0.35f, 0.8f, 0.5f));

    private MeshGeometry? _mesh;
    private Vector3[] _points = [];
    private int[] _indices = [];
    private Vector3 _center;
    private float _radius = 1f;
    private float _yaw = 0.65f;
    private float _pitch = 0.35f;
    private float _zoom = 1f;
    private PointF _pan;
    private bool _wireframe;
    private bool _rotating;
    private bool _panning;
    private Point _lastMouse;
    private string? _message = "Select a mesh.";
    private bool _messageIsError;
    private Bitmap? _target;
    private int[] _pixels = [];
    private float[] _depth = [];
    private bool _dirty = true;

    public MeshView()
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
    }

    public void SetMesh(MeshGeometry mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        _mesh = mesh;
        _points = mesh.Positions;
        _indices = mesh.Indices;
        _center = (mesh.Min + mesh.Max) * 0.5f;
        _radius = MathF.Max((mesh.Max - mesh.Min).Length() * 0.5f, 1e-6f);
        _message = null;
        _messageIsError = false;
        Fit();
    }

    public void SetPositions(Vector3[] positions)
    {
        ArgumentNullException.ThrowIfNull(positions);
        if (_mesh is null)
        {
            return;
        }

        if (positions.Length != _points.Length)
        {
            throw new ArgumentException(
                $"expected {_points.Length} positions, got {positions.Length}.",
                nameof(positions));
        }

        _points = positions;
        _dirty = true;
        Invalidate();
    }

    public void ShowMessage(string text, bool error)
    {
        _mesh = null;
        _points = [];
        _indices = [];
        _message = text;
        _messageIsError = error;
        _dirty = true;
        Invalidate();
    }

    public void Fit()
    {
        _yaw = 0.65f;
        _pitch = 0.35f;
        _zoom = 1f;
        _pan = PointF.Empty;
        _dirty = true;
        Invalidate();
    }

    public void SetWireframe(bool wireframe)
    {
        if (_wireframe == wireframe)
        {
            return;
        }

        _wireframe = wireframe;
        _dirty = true;
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _target?.Dispose();
        _target = null;
        _dirty = true;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (_mesh is null)
        {
            return;
        }

        Focus();
        _lastMouse = e.Location;
        _rotating = e.Button == MouseButtons.Left;
        _panning = e.Button is MouseButtons.Right or MouseButtons.Middle;
        Capture = _rotating || _panning;
        Cursor = _rotating ? Cursors.SizeAll : _panning ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_rotating && !_panning)
        {
            return;
        }

        int dx = e.X - _lastMouse.X;
        int dy = e.Y - _lastMouse.Y;
        _lastMouse = e.Location;
        if (_rotating)
        {
            _yaw += dx * 0.01f;
            _pitch = Math.Clamp(_pitch + dy * 0.01f, -1.45f, 1.45f);
        }
        else
        {
            _pan = new PointF(_pan.X + dx, _pan.Y + dy);
        }

        _dirty = true;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _rotating = false;
        _panning = false;
        Capture = false;
        Cursor = Cursors.Default;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_mesh is null)
        {
            return;
        }

        _zoom = Math.Clamp(_zoom * MathF.Pow(1.15f, e.Delta / 120f), 0.15f, 12f);
        _dirty = true;
        Invalidate();
    }

    protected override void OnDoubleClick(EventArgs e)
    {
        base.OnDoubleClick(e);
        Fit();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.Clear(Theme.Background);
        if (_mesh is null)
        {
            DrawMessage(graphics);
            return;
        }

        int width = ClientSize.Width;
        int height = ClientSize.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (_target is null || _target.Width != width || _target.Height != height)
        {
            _target?.Dispose();
            _target = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            _pixels = new int[width * height];
            _depth = new float[width * height];
            _dirty = true;
        }

        if (_dirty)
        {
            Render(width, height);
            _dirty = false;
        }

        graphics.DrawImageUnscaled(_target, 0, 0);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _target?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Render(int width, int height)
    {
        Array.Fill(_pixels, Theme.Background.ToArgb());
        Array.Fill(_depth, float.PositiveInfinity);

        float aspect = width / (float)height;
        Matrix4x4 rotation = Matrix4x4.CreateRotationY(_yaw) * Matrix4x4.CreateRotationX(_pitch);
        Matrix4x4 matrix = Matrix4x4.CreateTranslation(-_center)
            * Matrix4x4.CreateScale(1f / _radius)
            * rotation
            * Matrix4x4.CreateTranslation(
                _pan.X * 0.004f / _zoom, -_pan.Y * 0.004f / _zoom, 0f)
            * Matrix4x4.CreateTranslation(0f, 0f, -3f / _zoom)
            * Matrix4x4.CreatePerspectiveFieldOfView(0.7f, aspect, 0.05f, 20f);

        int count = _points.Length;
        float[] sx = new float[count];
        float[] sy = new float[count];
        float[] sz = new float[count];
        bool[] valid = new bool[count];
        for (int index = 0; index < count; index++)
        {
            Vector4 clip = Vector4.Transform(new Vector4(_points[index], 1f), matrix);
            if (clip.W <= 0.0001f)
            {
                continue;
            }

            float inverse = 1f / clip.W;
            sx[index] = (clip.X * inverse * 0.5f + 0.5f) * width;
            sy[index] = (0.5f - clip.Y * inverse * 0.5f) * height;
            sz[index] = clip.Z * inverse;
            valid[index] = sz[index] is >= 0f and <= 1f;
        }

        for (int triangle = 0; triangle + 2 < _indices.Length; triangle += 3)
        {
            int i0 = _indices[triangle];
            int i1 = _indices[triangle + 1];
            int i2 = _indices[triangle + 2];
            if (!valid[i0] || !valid[i1] || !valid[i2])
            {
                continue;
            }

            Vector3 p0 = _points[i0];
            Vector3 p1 = _points[i1];
            Vector3 p2 = _points[i2];
            Vector3 normal = Vector3.Cross(p1 - p0, p2 - p0);
            if (normal.LengthSquared() < 1e-12f)
            {
                continue;
            }

            normal = Vector3.Normalize(Vector3.TransformNormal(normal, rotation));
            float light = 0.45f + 0.55f * MathF.Abs(Vector3.Dot(normal, LightDirection));
            float shade = light * (1f - 0.25f * ((sz[i0] + sz[i1] + sz[i2]) / 3f));
            int color = Shade(shade);
            Rasterize(
                (sx[i0], sy[i0], sz[i0]),
                (sx[i1], sy[i1], sz[i1]),
                (sx[i2], sy[i2], sz[i2]),
                color,
                depthOnly: _wireframe,
                width,
                height);
        }

        if (_wireframe)
        {
            int line = Theme.Accent.ToArgb();
            for (int triangle = 0; triangle + 2 < _indices.Length; triangle += 3)
            {
                int i0 = _indices[triangle];
                int i1 = _indices[triangle + 1];
                int i2 = _indices[triangle + 2];
                if (!valid[i0] || !valid[i1] || !valid[i2])
                {
                    continue;
                }

                DrawLine(sx[i0], sy[i0], sz[i0], sx[i1], sy[i1], sz[i1], line, width, height);
                DrawLine(sx[i1], sy[i1], sz[i1], sx[i2], sy[i2], sz[i2], line, width, height);
                DrawLine(sx[i2], sy[i2], sz[i2], sx[i0], sy[i0], sz[i0], line, width, height);
            }
        }

        BitmapData data = _target!.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);
        Marshal.Copy(_pixels, 0, data.Scan0, _pixels.Length);
        _target.UnlockBits(data);
    }

    private void Rasterize(
        (float X, float Y, float Z) a,
        (float X, float Y, float Z) b,
        (float X, float Y, float Z) c,
        int color,
        bool depthOnly,
        int width,
        int height)
    {
        float area = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        if (MathF.Abs(area) < 0.001f)
        {
            return;
        }

        int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))));
        int maxX = Math.Min(width - 1, (int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))));
        int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y))));
        int maxY = Math.Min(height - 1, (int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y))));
        if (minX > maxX || minY > maxY)
        {
            return;
        }

        float inverseArea = 1f / area;
        for (int y = minY; y <= maxY; y++)
        {
            float py = y + 0.5f;
            int row = y * width;
            for (int x = minX; x <= maxX; x++)
            {
                float px = x + 0.5f;
                float w0 = (c.X - b.X) * (py - b.Y) - (c.Y - b.Y) * (px - b.X);
                float w1 = (a.X - c.X) * (py - c.Y) - (a.Y - c.Y) * (px - c.X);
                float w2 = (b.X - a.X) * (py - a.Y) - (b.Y - a.Y) * (px - a.X);
                float l0 = w0 * inverseArea;
                float l1 = w1 * inverseArea;
                float l2 = w2 * inverseArea;
                if (l0 < 0f || l1 < 0f || l2 < 0f)
                {
                    continue;
                }

                float z = l0 * a.Z + l1 * b.Z + l2 * c.Z;
                int index = row + x;
                if (z >= _depth[index])
                {
                    continue;
                }

                _depth[index] = z;
                if (!depthOnly)
                {
                    _pixels[index] = color;
                }
            }
        }
    }

    private void DrawLine(
        float x0, float y0, float z0,
        float x1, float y1, float z1,
        int color, int width, int height)
    {
        int steps = (int)MathF.Max(MathF.Abs(x1 - x0), MathF.Abs(y1 - y0));
        if (steps <= 0)
        {
            return;
        }

        for (int step = 0; step <= steps; step++)
        {
            float t = step / (float)steps;
            int x = (int)MathF.Round(x0 + (x1 - x0) * t);
            int y = (int)MathF.Round(y0 + (y1 - y0) * t);
            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                continue;
            }

            float z = z0 + (z1 - z0) * t;
            int index = y * width + x;
            if (z <= _depth[index] + 0.0005f)
            {
                _pixels[index] = color;
            }
        }
    }

    private static int Shade(float light)
    {
        byte value = (byte)Math.Clamp(light * 210f, 0f, 255f);
        int red = (int)(value * 0.75f);
        int green = (int)(value * 0.85f);
        return unchecked((int)0xFF000000) | (red << 16) | (green << 8) | value;
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
}
