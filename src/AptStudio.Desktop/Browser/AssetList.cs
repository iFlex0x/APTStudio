using System.ComponentModel;
using System.Drawing;

namespace AptStudio.Desktop.Browser;

internal sealed class AssetList : ScrollableControl
{
    private const int ThumbnailColumnWidth = 38;
    private const int RowPadding = 14;
    private const int CellGap = 8;

    private readonly Font _monoFont = new("Consolas", 8.625f);
    private readonly HashSet<string> _selectedKeys = new(StringComparer.Ordinal);
    private IReadOnlyList<AssetRow> _rows = Array.Empty<AssetRow>();
    private IReadOnlyList<AssetColumn> _columns = Array.Empty<AssetColumn>();
    private int _selectedIndex = -1;
    private int _anchorIndex = -1;
    private string? _currentKey;
    private bool _showThumbnails = true;
    private bool _allowMultiSelect = true;
    private int _scrollY;
    private bool _draggingScroll;
    private int _dragStartY;
    private int _dragStartScroll;
    private int _rowsVersion;
    private int _visibleRowsVersion = -1;
    private int _visibleFirst = -1;
    private int _visibleLast = -1;

    private const int ScrollBarWidth = 10;

    public AssetList()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw
            | ControlStyles.Selectable,
            true);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        TabStop = true;
        AutoScroll = false;
        RowHeight = 52;
    }

    public event EventHandler? SelectionChanged;

    public event EventHandler? OpenRequested;

    public event Action<AssetRow>? ThumbnailRequested;

    public event Action<IReadOnlyList<AssetRow>>? VisibleRowsChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<AssetRow, Bitmap?>? ThumbnailProvider { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<AssetColumn> Columns
    {
        get => _columns;
        set
        {
            _columns = value ?? Array.Empty<AssetColumn>();
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowThumbnails
    {
        get => _showThumbnails;
        set
        {
            _showThumbnails = value;
            _visibleRowsVersion = -1;
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool AllowMultiSelect
    {
        get => _allowMultiSelect;
        set
        {
            _allowMultiSelect = value;
            if (!value)
            {
                KeepOnly(SelectedRow?.Key);
            }
        }
    }

    public IReadOnlyCollection<string> SelectedKeys => _selectedKeys;

    public IReadOnlyList<AssetRow> SelectedRows => _rows
        .Where(row => _selectedKeys.Contains(row.Key))
        .ToList();

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? CurrentKey
    {
        get => _currentKey;
        set
        {
            if (_currentKey == value)
            {
                return;
            }

            _currentKey = value;
            Invalidate();
        }
    }

    public int RowHeight { get; private set; }

    public IReadOnlyList<AssetRow> Rows => _rows;

    public void SetRowHeight(int height)
    {
        int clamped = Math.Clamp(height, 24, 64);
        if (clamped == RowHeight)
        {
            return;
        }

        RowHeight = clamped;
        _scrollY = Math.Clamp(_scrollY, 0, MaxScrollY);
        _visibleRowsVersion = -1;
        Invalidate();
    }

    public AssetRow? SelectedRow =>
        _selectedIndex >= 0 && _selectedIndex < _rows.Count ? _rows[_selectedIndex] : null;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int next = value < 0 || _rows.Count == 0
                ? -1
                : Math.Clamp(value, 0, _rows.Count - 1);
            string? key = next >= 0 ? _rows[next].Key : null;
            if (next == _selectedIndex
                && _selectedKeys.Count == (next >= 0 ? 1 : 0)
                && (key is null || _selectedKeys.Contains(key)))
            {
                return;
            }

            _selectedKeys.Clear();
            if (key is not null)
            {
                _selectedKeys.Add(key);
            }

            _selectedIndex = next;
            _anchorIndex = next;
            _currentKey = key;
            Invalidate();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            EnsureVisible(next);
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int TopIndex
    {
        get => _scrollY / RowHeight;
        set
        {
            ScrollTo(Math.Max(0, value) * RowHeight);
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ScrollOffset
    {
        get => _scrollY;
        set
        {
            ScrollTo(Math.Max(0, value));
            Invalidate();
        }
    }

    private int MaxScrollY => Math.Max(0, _rows.Count * RowHeight - ClientSize.Height);

    private void ScrollTo(int y)
    {
        int clamped = Math.Clamp(y, 0, MaxScrollY);
        if (clamped == _scrollY)
        {
            return;
        }

        _scrollY = clamped;
        Invalidate();
    }

    public void SetRows(
        IReadOnlyList<AssetRow> rows,
        string? keepKey,
        IReadOnlyCollection<string>? keepKeys = null)
    {
        _rows = rows ?? Array.Empty<AssetRow>();
        _rowsVersion++;
        _scrollY = Math.Clamp(_scrollY, 0, MaxScrollY);

        Dictionary<string, int> indexByKey = new(StringComparer.Ordinal);
        for (int index = 0; index < _rows.Count; index++)
        {
            indexByKey.TryAdd(_rows[index].Key, index);
        }

        IReadOnlyCollection<string>? keys = keepKeys;
        if (keys is null && keepKey is not null)
        {
            keys = [keepKey];
        }

        string[] keep = keys?.ToArray() ?? [];
        _selectedKeys.Clear();
        foreach (string key in keep)
        {
            if (indexByKey.ContainsKey(key))
            {
                _selectedKeys.Add(key);
            }
        }

        int anchor = -1;
        if (keepKey is not null && _selectedKeys.Contains(keepKey))
        {
            anchor = indexByKey[keepKey];
        }
        else if (_selectedKeys.Count > 0)
        {
            for (int index = 0; index < _rows.Count; index++)
            {
                if (_selectedKeys.Contains(_rows[index].Key))
                {
                    anchor = index;
                    break;
                }
            }
        }

        _selectedIndex = anchor;
        _anchorIndex = anchor;
        _currentKey = keepKey is not null && indexByKey.ContainsKey(keepKey)
            ? keepKey
            : anchor >= 0 ? _rows[anchor].Key : null;
        if (_rows.Count == 0 && _showThumbnails)
        {
            _visibleRowsVersion = _rowsVersion;
            _visibleFirst = -1;
            _visibleLast = -1;
            VisibleRowsChanged?.Invoke(Array.Empty<AssetRow>());
        }
        Invalidate();
    }

    public void MoveSelection(int delta) => MoveSelection(delta, false);

    public void MoveSelection(int delta, bool extend)
    {
        if (_rows.Count == 0)
        {
            return;
        }

        int next = Math.Clamp(_selectedIndex + delta, 0, _rows.Count - 1);
        if (extend && _allowMultiSelect)
        {
            SelectRange(_anchorIndex >= 0 ? _anchorIndex : _selectedIndex, next);
            return;
        }

        SelectedIndex = next;
    }

    public void ToggleSelection(int index)
    {
        if (index < 0 || index >= _rows.Count || !_allowMultiSelect)
        {
            return;
        }

        string key = _rows[index].Key;
        if (!_selectedKeys.Add(key))
        {
            _selectedKeys.Remove(key);
        }

        if (_selectedKeys.Count == 0)
        {
            _selectedIndex = -1;
            _anchorIndex = -1;
            _currentKey = null;
        }
        else
        {
            _selectedIndex = index;
            _anchorIndex = index;
            _currentKey = key;
        }

        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectAll()
    {
        if (!_allowMultiSelect)
        {
            return;
        }

        _selectedKeys.Clear();
        foreach (AssetRow row in _rows)
        {
            _selectedKeys.Add(row.Key);
        }

        if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
        {
            _selectedIndex = _rows.Count > 0 ? 0 : -1;
        }

        _anchorIndex = _selectedIndex;
        _currentKey = _selectedIndex >= 0 ? _rows[_selectedIndex].Key : null;
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        EnsureVisible(_selectedIndex);
    }

    private void SelectRange(int anchor, int next)
    {
        if (anchor < 0)
        {
            anchor = next;
        }

        int start = Math.Min(anchor, next);
        int end = Math.Max(anchor, next);
        _selectedKeys.Clear();
        for (int index = start; index <= end && index < _rows.Count; index++)
        {
            _selectedKeys.Add(_rows[index].Key);
        }

        _selectedIndex = next;
        _anchorIndex = anchor;
        _currentKey = next >= 0 && next < _rows.Count ? _rows[next].Key : null;
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        EnsureVisible(next);
    }

    private void KeepOnly(string? key)
    {
        _selectedKeys.Clear();
        if (key is not null)
        {
            for (int index = 0; index < _rows.Count; index++)
            {
                if (_rows[index].Key.Equals(key, StringComparison.Ordinal))
                {
                    _selectedKeys.Add(key);
                    _selectedIndex = index;
                    _anchorIndex = index;
                    _currentKey = key;
                    Invalidate();
                    return;
                }
            }
        }

        _selectedIndex = -1;
        _anchorIndex = -1;
        _currentKey = null;
        Invalidate();
    }

    public void ActivateSelected()
    {
        if (SelectedRow is not null)
        {
            OpenRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void EnsureVisible(int index)
    {
        if (index < 0)
        {
            return;
        }

        int top = _scrollY;
        int rowTop = index * RowHeight;
        int rowBottom = rowTop + RowHeight;
        if (rowTop < top)
        {
            ScrollTo(rowTop);
        }
        else if (rowBottom > top + ClientSize.Height)
        {
            ScrollTo(rowBottom - ClientSize.Height);
        }
    }

    internal static int[] MeasureColumns(IReadOnlyList<AssetColumn> columns, int availableWidth)
    {
        int[] widths = new int[columns.Count];
        int flexible = -1;
        int fixedWidth = 0;
        for (int index = 0; index < columns.Count; index++)
        {
            widths[index] = columns[index].Width;
            if (columns[index].Flexible && flexible < 0)
            {
                flexible = index;
            }
            else
            {
                fixedWidth += columns[index].Width;
            }
        }

        if (flexible >= 0)
        {
            int gaps = Math.Max(0, columns.Count - 1) * CellGap;
            widths[flexible] = Math.Max(80, availableWidth - fixedWidth - gaps);
        }

        return widths;
    }

    internal static int MinimumWidth(IReadOnlyList<AssetColumn> columns, bool showThumbnails)
    {
        int x0 = RowPadding + (showThumbnails ? ThumbnailColumnWidth : 0);
        int fixedWidth = 0;
        bool flexible = false;
        foreach (AssetColumn column in columns)
        {
            if (column.Flexible)
            {
                flexible = true;
            }
            else
            {
                fixedWidth += column.Width;
            }
        }

        int gaps = Math.Max(0, columns.Count - 1) * CellGap;
        return x0 + fixedWidth + gaps + (flexible ? 80 : 0) + RowPadding;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.Background);
        if (_rows.Count == 0)
        {
            TextRenderer.DrawText(
                e.Graphics,
                "No rows",
                Font,
                new Rectangle(RowPadding, RowPadding, ClientSize.Width - RowPadding * 2, 24),
                Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            base.OnPaint(e);
            return;
        }

        int scroll = -_scrollY;
        int first = Math.Max(0, (-scroll) / RowHeight);
        int last = Math.Min(_rows.Count - 1, (-scroll + ClientSize.Height) / RowHeight);
        if (_showThumbnails
            && (_rowsVersion != _visibleRowsVersion
                || first != _visibleFirst
                || last != _visibleLast))
        {
            _visibleRowsVersion = _rowsVersion;
            _visibleFirst = first;
            _visibleLast = last;
            VisibleRowsChanged?.Invoke(
                _rows.Skip(first).Take(last - first + 1).ToArray());
        }

        int x0 = RowPadding + (_showThumbnails ? ThumbnailColumnWidth : 0);
        int[] widths = MeasureColumns(_columns, Math.Max(0, ClientSize.Width - x0 - RowPadding));

        using Pen rowLine = new(BrowserVisual.RowBorder);
        using Pen thumbBorder = new(BrowserVisual.BorderStrong);
        for (int index = first; index <= last; index++)
        {
            AssetRow row = _rows[index];
            int y = index * RowHeight + scroll;
            Rectangle bounds = new(0, y, ClientSize.Width, RowHeight);
            bool selected = _selectedKeys.Contains(row.Key) || index == _selectedIndex;
            bool current = string.Equals(_currentKey, row.Key, StringComparison.Ordinal)
                || (_currentKey is null && index == _selectedIndex);
            using SolidBrush background = new(selected ? Theme.Selection : Theme.Background);
            e.Graphics.FillRectangle(background, bounds);

            if (current)
            {
                using SolidBrush accent = new(Theme.Accent);
                e.Graphics.FillRectangle(accent, 0, y, 3, RowHeight);
            }

            if (_showThumbnails)
            {
                int thumbY = y + (RowHeight - BrowserVisual.ThumbnailSize) / 2;
                Rectangle thumb = new(
                    RowPadding, thumbY, BrowserVisual.ThumbnailSize, BrowserVisual.ThumbnailSize);
                Bitmap? thumbnail = ThumbnailProvider?.Invoke(row);
                if (thumbnail is not null)
                {
                    e.Graphics.DrawImage(thumbnail, thumb);
                }

                e.Graphics.DrawRectangle(thumbBorder, thumb);

                if (ThumbnailRequested is not null)
                {
                    ThumbnailRequested(row);
                }
            }

            int x = x0;
            for (int column = 0; column < _columns.Count && column < row.Cells.Length; column++)
            {
                AssetColumn definition = _columns[column];
                int width = widths[column];
                Rectangle cell = new(x, y, width, RowHeight);
                Font font = definition.Mono ? _monoFont : Font;
                string text = row.Cells[column];
                TextFormatFlags flags = TextFormatFlags.VerticalCenter
                    | (definition.Right ? TextFormatFlags.Right : TextFormatFlags.Left)
                    | TextFormatFlags.NoPrefix;
                if (definition.MiddleEllipsis)
                {
                    text = BrowserVisual.EllipsizeMiddle(text, font, Math.Max(20, width - 8));
                }
                else
                {
                    flags |= TextFormatFlags.EndEllipsis;
                }

                TextRenderer.DrawText(
                    e.Graphics,
                    text,
                    font,
                    cell,
                    selected ? Theme.Text : definition.Muted ? Theme.Muted : Theme.Text,
                    flags);
                x += width + CellGap;
            }

            e.Graphics.DrawLine(rowLine, 0, y + RowHeight - 1, ClientSize.Width, y + RowHeight - 1);
        }

        if (Focused && _selectedIndex >= first && _selectedIndex <= last)
        {
            using Pen focus = new(Theme.Accent);
            int y = _selectedIndex * RowHeight + scroll;
            e.Graphics.DrawRectangle(focus, 0, y, ClientSize.Width - 1, RowHeight - 1);
        }

        PaintScrollBar(e.Graphics);
        base.OnPaint(e);
    }

    private void PaintScrollBar(Graphics graphics)
    {
        if (MaxScrollY <= 0)
        {
            return;
        }

        Rectangle track = new(
            ClientSize.Width - ScrollBarWidth, 0, ScrollBarWidth, ClientSize.Height);
        using SolidBrush background = new(Theme.Background);
        graphics.FillRectangle(background, track);
        Rectangle thumb = ScrollThumb();
        using SolidBrush brush = new(BrowserVisual.BorderStrong);
        graphics.FillRectangle(brush, thumb);
    }

    private Rectangle ScrollThumb()
    {
        int track = ClientSize.Height;
        long total = (long)_rows.Count * RowHeight;
        int height = Math.Max(28, (int)(track * ClientSize.Height / Math.Max(1L, total)));
        int span = Math.Max(0, track - height);
        int y = MaxScrollY == 0 ? 0 : (int)((long)span * _scrollY / MaxScrollY);
        return new Rectangle(
            ClientSize.Width - ScrollBarWidth + 2, y, ScrollBarWidth - 4, height);
    }

    protected override bool IsInputKey(Keys keyData)
    {
        // Dialog-key preprocessing eats arrows before OnKeyDown can use them.
        return (keyData & Keys.KeyCode) is Keys.Up or Keys.Down
            || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        bool extend = e.Shift && _allowMultiSelect;
        switch (e.KeyCode)
        {
            case Keys.Up:
                MoveSelection(-1, extend);
                e.Handled = true;
                break;
            case Keys.Down:
                MoveSelection(1, extend);
                e.Handled = true;
                break;
            case Keys.Home:
                if (extend)
                {
                    SelectRange(_anchorIndex >= 0 ? _anchorIndex : 0, 0);
                }
                else
                {
                    SelectedIndex = 0;
                }

                e.Handled = true;
                break;
            case Keys.End:
                if (extend)
                {
                    SelectRange(
                        _anchorIndex >= 0 ? _anchorIndex : _rows.Count - 1,
                        _rows.Count - 1);
                }
                else
                {
                    SelectedIndex = _rows.Count - 1;
                }

                e.Handled = true;
                break;
            case Keys.PageUp:
                MoveSelection(-Math.Max(1, ClientSize.Height / RowHeight), extend);
                e.Handled = true;
                break;
            case Keys.PageDown:
                MoveSelection(Math.Max(1, ClientSize.Height / RowHeight), extend);
                e.Handled = true;
                break;
            case Keys.A when e.Control:
                SelectAll();
                e.Handled = true;
                break;
            case Keys.Space:
                ToggleSelection(_selectedIndex);
                e.Handled = true;
                break;
            case Keys.Enter:
                ActivateSelected();
                e.Handled = true;
                break;
        }

        base.OnKeyDown(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        if (e.Button == MouseButtons.Left
            && MaxScrollY > 0
            && e.X >= ClientSize.Width - ScrollBarWidth)
        {
            Rectangle thumb = ScrollThumb();
            if (!thumb.Contains(e.Location))
            {
                int span = Math.Max(1, ClientSize.Height - thumb.Height);
                long target = ((long)e.Y - thumb.Height / 2) * MaxScrollY / span;
                ScrollTo((int)Math.Clamp(target, 0, MaxScrollY));
            }

            _draggingScroll = true;
            _dragStartY = e.Y;
            _dragStartScroll = _scrollY;
            Capture = true;
            return;
        }

        int index = HitTest(e.Y);
        if (index >= 0)
        {
            bool ctrl = (ModifierKeys & Keys.Control) != 0;
            bool shift = (ModifierKeys & Keys.Shift) != 0;
            if (shift && _allowMultiSelect)
            {
                int anchor = _anchorIndex >= 0
                    ? _anchorIndex
                    : _selectedIndex >= 0 ? _selectedIndex : index;
                SelectRange(anchor, index);
            }
            else if (ctrl && _allowMultiSelect)
            {
                ToggleSelection(index);
            }
            else
            {
                SelectedIndex = index;
            }
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_draggingScroll)
        {
            Rectangle thumb = ScrollThumb();
            int span = Math.Max(1, ClientSize.Height - thumb.Height);
            long delta = ((long)e.Y - _dragStartY) * MaxScrollY / span;
            ScrollTo((int)Math.Clamp(_dragStartScroll + delta, 0, MaxScrollY));
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_draggingScroll)
        {
            _draggingScroll = false;
            Capture = false;
            return;
        }

        base.OnMouseUp(e);
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        if (!Capture)
        {
            _draggingScroll = false;
        }

        base.OnMouseCaptureChanged(e);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left
            && (MaxScrollY <= 0 || e.X < ClientSize.Width - ScrollBarWidth)
            && SelectAt(e.Y) == _selectedIndex)
        {
            ActivateSelected();
        }

        base.OnMouseDoubleClick(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (MaxScrollY > 0)
        {
            int lines = SystemInformation.MouseWheelScrollLines;
            int step = lines < 0 ? Math.Max(1, ClientSize.Height / RowHeight) : Math.Max(1, lines);
            ScrollTo(_scrollY - Math.Sign(e.Delta) * step * RowHeight);
        }

        base.OnMouseWheel(e);
    }

    protected override void OnResize(EventArgs e)
    {
        _scrollY = Math.Clamp(_scrollY, 0, MaxScrollY);
        base.OnResize(e);
    }

    private int SelectAt(int y)
    {
        int index = HitTest(y);
        if (index >= 0)
        {
            SelectedIndex = index;
        }

        return index;
    }

    private int HitTest(int y)
    {
        int index = (y + _scrollY) / RowHeight;
        return index >= 0 && index < _rows.Count ? index : -1;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _monoFont.Dispose();
        }

        base.Dispose(disposing);
    }
}

internal sealed class AssetListHeader : Control
{
    private const int ThumbnailColumnWidth = 38;
    private const int RowPadding = 14;
    private const int CellGap = 8;

    private IReadOnlyList<AssetColumn> _columns = Array.Empty<AssetColumn>();
    private bool _showThumbnails = true;

    public AssetListHeader()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw,
            true);
        BackColor = Theme.Surface;
        ForeColor = BrowserVisual.Faint;
        Height = 30;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<AssetColumn> Columns
    {
        get => _columns;
        set
        {
            _columns = value ?? Array.Empty<AssetColumn>();
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowThumbnails
    {
        get => _showThumbnails;
        set
        {
            _showThumbnails = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.Surface);
        int x0 = RowPadding + (_showThumbnails ? ThumbnailColumnWidth : 0);
        int[] widths = AssetList.MeasureColumns(
            _columns,
            Math.Max(0, ClientSize.Width - x0 - RowPadding));
        int x = x0;
        for (int index = 0; index < _columns.Count; index++)
        {
            AssetColumn column = _columns[index];
            Rectangle bounds = new(x, 0, widths[index], ClientSize.Height);
            TextRenderer.DrawText(
                e.Graphics,
                column.Header.ToUpperInvariant(),
                BrowserVisual.Section,
                bounds,
                BrowserVisual.Faint,
                TextFormatFlags.VerticalCenter
                    | TextFormatFlags.EndEllipsis
                    | (column.Right ? TextFormatFlags.Right : TextFormatFlags.Left));
            x += widths[index] + CellGap;
        }

        using Pen border = new(BrowserVisual.RowBorder);
        e.Graphics.DrawLine(border, 0, ClientSize.Height - 1, ClientSize.Width, ClientSize.Height - 1);
        base.OnPaint(e);
    }
}
