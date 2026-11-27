using System.ComponentModel;
using System.Drawing;

namespace AptStudio.Desktop.Browser;

internal sealed class AssetList : ScrollableControl
{
    private const int ThumbnailColumnWidth = 38;
    private const int RowPadding = 8;
    private const int CellGap = 8;

    private readonly Font _monoFont = new("Consolas", 9f);
    private readonly HashSet<string> _selectedKeys = new(StringComparer.Ordinal);
    private IReadOnlyList<AssetRow> _rows = Array.Empty<AssetRow>();
    private IReadOnlyList<AssetColumn> _columns = Array.Empty<AssetColumn>();
    private int _selectedIndex = -1;
    private int _anchorIndex = -1;
    private bool _showThumbnails = true;
    private bool _allowMultiSelect = true;

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
        AutoScroll = true;
        RowHeight = 48;
    }

    public event EventHandler? SelectionChanged;

    public event EventHandler? OpenRequested;

    public event Action<AssetRow>? ThumbnailRequested;

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

    public int RowHeight { get; }

    public IReadOnlyList<AssetRow> Rows => _rows;

    public AssetRow? SelectedRow =>
        _selectedIndex >= 0 && _selectedIndex < _rows.Count ? _rows[_selectedIndex] : null;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int next = _rows.Count == 0 ? -1 : Math.Clamp(value, 0, _rows.Count - 1);
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
            Invalidate();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            EnsureVisible(next);
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int TopIndex
    {
        get => IsHandleCreated ? Math.Max(0, -AutoScrollPosition.Y / RowHeight) : 0;
        set
        {
            if (IsHandleCreated)
            {
                AutoScrollPosition = new Point(0, Math.Max(0, value) * RowHeight);
                Invalidate();
            }
        }
    }

    public void SetRows(
        IReadOnlyList<AssetRow> rows,
        string? keepKey,
        IReadOnlyCollection<string>? keepKeys = null)
    {
        _rows = rows ?? Array.Empty<AssetRow>();
        AutoScrollMinSize = new Size(0, _rows.Count * RowHeight);

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
        }
        else
        {
            _selectedIndex = index;
            _anchorIndex = index;
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

        _selectedIndex = _rows.Count > 0 ? 0 : -1;
        _anchorIndex = _selectedIndex;
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
                    Invalidate();
                    return;
                }
            }
        }

        _selectedIndex = -1;
        _anchorIndex = -1;
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
        if (!IsHandleCreated || index < 0)
        {
            return;
        }

        int top = -AutoScrollPosition.Y;
        int rowTop = index * RowHeight;
        int rowBottom = rowTop + RowHeight;
        if (rowTop < top)
        {
            AutoScrollPosition = new Point(0, rowTop);
        }
        else if (rowBottom > top + ClientSize.Height)
        {
            AutoScrollPosition = new Point(0, rowBottom - ClientSize.Height);
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

        int scroll = AutoScrollPosition.Y;
        int first = Math.Max(0, (-scroll) / RowHeight);
        int last = Math.Min(_rows.Count - 1, (-scroll + ClientSize.Height) / RowHeight);
        int x0 = RowPadding + (_showThumbnails ? ThumbnailColumnWidth : 0);
        int[] widths = MeasureColumns(_columns, Math.Max(0, ClientSize.Width - x0 - RowPadding));

        using Pen border = new(Theme.Border);
        for (int index = first; index <= last; index++)
        {
            AssetRow row = _rows[index];
            int y = index * RowHeight + scroll;
            Rectangle bounds = new(0, y, ClientSize.Width, RowHeight);
            bool selected = _selectedKeys.Contains(row.Key) || index == _selectedIndex;
            using SolidBrush background = new(
                selected ? Theme.Selection : index % 2 == 0 ? Theme.Background : Theme.GridAlt);
            e.Graphics.FillRectangle(background, bounds);

            if (_showThumbnails)
            {
                Bitmap? thumbnail = ThumbnailProvider?.Invoke(row);
                if (thumbnail is not null)
                {
                    e.Graphics.DrawImage(
                        thumbnail,
                        new Rectangle(RowPadding, y + (RowHeight - 32) / 2, 32, 32));
                }

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
                TextFormatFlags flags = TextFormatFlags.VerticalCenter
                    | TextFormatFlags.EndEllipsis
                    | (definition.Right ? TextFormatFlags.Right : TextFormatFlags.Left);
                TextRenderer.DrawText(
                    e.Graphics,
                    row.Cells[column],
                    definition.Mono ? _monoFont : Font,
                    cell,
                    selected ? Theme.Text : definition.Muted ? Theme.Muted : Theme.Text,
                    flags);
                x += width + CellGap;
            }

            e.Graphics.DrawLine(border, 0, y + RowHeight - 1, ClientSize.Width, y + RowHeight - 1);
        }

        if (Focused && _selectedIndex >= first && _selectedIndex <= last)
        {
            using Pen focus = new(Theme.Accent);
            int y = _selectedIndex * RowHeight + scroll;
            e.Graphics.DrawRectangle(focus, 0, y, ClientSize.Width - 1, RowHeight - 1);
        }

        base.OnPaint(e);
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

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && SelectAt(e.Y) == _selectedIndex)
        {
            ActivateSelected();
        }

        base.OnMouseDoubleClick(e);
    }

    protected override void OnScroll(ScrollEventArgs se)
    {
        base.OnScroll(se);
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Invalidate();
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
        int index = (y - AutoScrollPosition.Y) / RowHeight;
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
    private const int RowPadding = 8;
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
        BackColor = Theme.GridHeader;
        ForeColor = Theme.Muted;
        Height = 26;
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
        e.Graphics.Clear(Theme.GridHeader);
        int x0 = RowPadding + (_showThumbnails ? ThumbnailColumnWidth : 0);
        int[] widths = AssetList.MeasureColumns(
            _columns,
            Math.Max(0, ClientSize.Width - x0 - RowPadding));
        using Font font = new("Segoe UI", 8.25f);
        int x = x0;
        for (int index = 0; index < _columns.Count; index++)
        {
            AssetColumn column = _columns[index];
            Rectangle bounds = new(x, 0, widths[index], ClientSize.Height);
            TextRenderer.DrawText(
                e.Graphics,
                column.Header,
                font,
                bounds,
                Theme.Muted,
                TextFormatFlags.VerticalCenter
                    | TextFormatFlags.EndEllipsis
                    | (column.Right ? TextFormatFlags.Right : TextFormatFlags.Left));
            x += widths[index] + CellGap;
        }

        using Pen border = new(Theme.Border);
        e.Graphics.DrawLine(border, 0, ClientSize.Height - 1, ClientSize.Width, ClientSize.Height - 1);
        base.OnPaint(e);
    }
}
