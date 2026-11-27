using System.Drawing;
using AptStudio.Core.Links;

namespace AptStudio.Desktop.Browser;

internal sealed class SkeletonSkinChooserView : Panel
{
    private readonly Label _selected = new();
    private readonly Button _open = new();
    private readonly DataGridView _grid = new();
    private readonly Font _monoFont = new("Consolas", 8.625f);
    private IReadOnlyList<SkinSkeletonUse> _rows = Array.Empty<SkinSkeletonUse>();
    private int _selectedIndex = -1;
    private bool _suppress;

    public SkeletonSkinChooserView()
    {
        BackColor = Theme.Background;
        Padding = new Padding(0);

        _selected.AutoEllipsis = true;
        _selected.Font = BrowserVisual.RowSmall;
        _selected.ForeColor = BrowserVisual.Faint;
        _selected.Dock = DockStyle.Bottom;
        _selected.Height = 20;
        _selected.Padding = new Padding(0, 2, 0, 0);

        _open.Text = "Open skin";
        _open.FlatStyle = FlatStyle.Flat;
        _open.BackColor = Theme.Accent;
        _open.ForeColor = BrowserVisual.PrimaryText;
        _open.Font = BrowserVisual.UiSemibold;
        _open.AutoSize = false;
        _open.Width = 110;
        _open.Height = 26;
        _open.UseVisualStyleBackColor = false;
        _open.FlatAppearance.BorderSize = 0;
        _open.FlatAppearance.MouseOverBackColor = Color.FromArgb(111, 208, 251);
        _open.Enabled = false;
        _open.Click += (_, _) => OpenSelected();

        FlowLayoutPanel bar = new()
        {
            Dock = DockStyle.Top,
            Height = 38,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Background,
            Padding = new Padding(0, 4, 0, 4),
        };
        bar.Controls.Add(_open);

        ConfigureGrid();
        Controls.Add(_grid);
        Controls.Add(_selected);
        Controls.Add(bar);
    }

    public event EventHandler<SkinSkeletonUse>? OpenRequested;

    public int RowCount => _rows.Count;

    private SkinSkeletonUse? SelectedRow =>
        _selectedIndex >= 0 && _selectedIndex < _rows.Count ? _rows[_selectedIndex] : null;

    public void SetRows(IReadOnlyList<SkinSkeletonUse> rows)
    {
        _rows = rows;
        _suppress = true;
        try
        {
            _grid.Rows.Clear();
            foreach (SkinSkeletonUse row in rows)
            {
                int index = _grid.Rows.Add(
                    $"0x{row.NameHash:X16}",
                    row.ArchiveName);
                _grid.Rows[index].Tag = row;
            }

            _open.Enabled = rows.Count > 0;
            _selected.Text = rows.Count == 0
                ? "No skin is explicitly bound to this skeleton."
                : "Select a skin to open.";
            _selectedIndex = rows.Count == 0 ? -1 : 0;
            if (_grid.Rows.Count > 0)
            {
                _grid.CurrentCell = _grid.Rows[0].Cells[0];
                _grid.Rows[0].Selected = true;
            }
        }
        finally
        {
            _suppress = false;
        }

        UpdateSelection();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _monoFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        BrowserVisual.StyleQuietGrid(_grid);
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "SKIN",
            FillWeight = 52,
            DefaultCellStyle = { Font = _monoFont },
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "ARCHIVE",
            FillWeight = 48,
        });
        _grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 1 || e.Value is not string text)
            {
                return;
            }

            int width = _grid.Columns[1].Width - 20;
            if (width > 0)
            {
                e.Value = BrowserVisual.EllipsizeMiddle(text, _grid.Font, width);
                e.FormattingApplied = true;
            }
        };
        _grid.CellToolTipTextNeeded += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 1
                && _grid.Rows[e.RowIndex].Tag is SkinSkeletonUse row)
            {
                e.ToolTipText = row.ArchiveName;
            }
        };
        _grid.SelectionChanged += (_, _) =>
        {
            if (_suppress)
            {
                return;
            }

            _selectedIndex = _grid.CurrentRow?.Index ?? -1;
            UpdateSelection();
        };
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                OpenSelected();
            }
        };
        _grid.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                OpenSelected();
                e.Handled = true;
            }
        };
    }

    private void UpdateSelection()
    {
        SkinSkeletonUse? row = SelectedRow;
        _open.Enabled = row is not null;
        _selected.Text = row is null
            ? "Select a skin."
            : $"Selected: 0x{row.NameHash:X16} · {row.ArchiveName} · explicitly bound";
    }

    private void OpenSelected()
    {
        if (SelectedRow is { } row)
        {
            OpenRequested?.Invoke(this, row);
        }
    }
}
