using System.Drawing;
using AptStudio.Core.Links;

namespace AptStudio.Desktop.Browser;

internal sealed record MaterialUseRow(MaterialTextureUse Use, int Slot)
{
    public ulong Hash => Use.NameHash;

    public string Archive => Use.ArchiveName;

    public string Code => Use.Code;
}

internal sealed class MaterialChooserView : Panel
{
    private readonly Label _selected = new();
    private readonly Button _open = new();
    private readonly DataGridView _grid = new();
    private readonly Font _monoFont = new("Consolas", 8.625f);
    private IReadOnlyList<MaterialUseRow> _rows = Array.Empty<MaterialUseRow>();
    private int _selectedIndex = -1;
    private bool _suppress;

    public MaterialChooserView()
    {
        BackColor = Theme.Background;
        Padding = new Padding(0);

        _selected.AutoEllipsis = true;
        _selected.Font = BrowserVisual.RowSmall;
        _selected.ForeColor = BrowserVisual.Faint;
        _selected.Dock = DockStyle.Bottom;
        _selected.Height = 20;
        _selected.Padding = new Padding(0, 2, 0, 0);

        _open.Text = "Open material";
        _open.FlatStyle = FlatStyle.Flat;
        _open.BackColor = Theme.Accent;
        _open.ForeColor = BrowserVisual.PrimaryText;
        _open.Font = BrowserVisual.UiSemibold;
        _open.AutoSize = false;
        _open.Width = 120;
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

    public event EventHandler? SelectionChanged;

    public event EventHandler<MaterialUseRow>? OpenRequested;

    public MaterialUseRow? SelectedRow =>
        _selectedIndex >= 0 && _selectedIndex < _rows.Count ? _rows[_selectedIndex] : null;

    public int RowCount => _rows.Count;

    public void SetRows(IReadOnlyList<MaterialUseRow> rows)
    {
        _rows = rows;
        _suppress = true;
        try
        {
            _grid.Rows.Clear();
            foreach (MaterialUseRow row in rows)
            {
                int index = _grid.Rows.Add(
                    $"0x{row.Hash:X16}",
                    row.Code,
                    row.Archive,
                    row.Slot);
                _grid.Rows[index].Tag = row;
            }

            _open.Enabled = false;
            _selected.Text = rows.Count == 0 ? "No material uses." : "Select a material occurrence.";
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

    public void FocusList()
    {
        _grid.Focus();
        if (_grid.CurrentRow is not null)
        {
            _grid.CurrentCell = _grid.CurrentRow.Cells[0];
        }
    }

    internal void SelectIndex(int index)
    {
        if (index < 0 || index >= _rows.Count)
        {
            return;
        }

        _selectedIndex = index;
        _suppress = true;
        try
        {
            if (_grid.Rows.Count > index)
            {
                _grid.CurrentCell = _grid.Rows[index].Cells[0];
                _grid.Rows[index].Selected = true;
            }
        }
        finally
        {
            _suppress = false;
        }

        UpdateSelection();
    }

    internal void ActivateSelected()
    {
        OpenSelected();
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
            HeaderText = "MATERIAL",
            FillWeight = 40,
            DefaultCellStyle = { Font = _monoFont },
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "CODE",
            FillWeight = 24,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "ARCHIVE",
            FillWeight = 28,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "SLOT",
            FillWeight = 8,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight },
        });
        _grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 2 || e.Value is not string text)
            {
                return;
            }

            int width = _grid.Columns[2].Width - 20;
            if (width <= 0)
            {
                return;
            }

            e.Value = BrowserVisual.EllipsizeMiddle(text, _grid.Font, width);
            e.FormattingApplied = true;
        };
        _grid.CellToolTipTextNeeded += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 2
                && _grid.Rows[e.RowIndex].Tag is MaterialUseRow row)
            {
                e.ToolTipText = row.Archive;
            }
        };
        _grid.SelectionChanged += (_, _) =>
        {
            if (!_suppress)
            {
                _selectedIndex = _grid.CurrentRow?.Index ?? -1;
                UpdateSelection();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
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
        MaterialUseRow? row = SelectedRow;
        _open.Enabled = row is not null;
        _selected.Text = row is null
            ? "Select a material occurrence."
            : $"Selected: 0x{row.Hash:X16} · {row.Code} · {row.Archive} · slot {row.Slot}";
    }

    private void OpenSelected()
    {
        MaterialUseRow? row = SelectedRow;
        if (row is null)
        {
            return;
        }

        OpenRequested?.Invoke(this, row);
    }
}
