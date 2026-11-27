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
    private readonly Label _summary = new();
    private readonly Label _source = new();
    private readonly Label _selected = new();
    private readonly Button _open = new();
    private readonly DataGridView _grid = new();
    private readonly Font _monoFont = new("Consolas", 9f);
    private IReadOnlyList<MaterialUseRow> _rows = Array.Empty<MaterialUseRow>();
    private int _selectedIndex = -1;
    private bool _suppress;

    public MaterialChooserView()
    {
        BackColor = Theme.Background;
        Padding = new Padding(0);

        _summary.AutoEllipsis = true;
        _summary.Font = new Font("Segoe UI Semibold", 9.5f);
        _summary.ForeColor = Theme.Text;
        _summary.Dock = DockStyle.Top;
        _summary.Height = 22;
        _summary.Padding = new Padding(0, 2, 0, 0);

        _source.AutoEllipsis = true;
        _source.ForeColor = Theme.Muted;
        _source.Dock = DockStyle.Top;
        _source.Height = 20;

        _selected.AutoEllipsis = true;
        _selected.ForeColor = Theme.Muted;
        _selected.Dock = DockStyle.Bottom;
        _selected.Height = 22;
        _selected.Padding = new Padding(0, 3, 0, 0);

        _open.Text = "Open material";
        _open.FlatStyle = FlatStyle.Flat;
        _open.BackColor = Theme.Accent;
        _open.ForeColor = Color.FromArgb(6, 32, 43);
        _open.Font = new Font("Segoe UI Semibold", 9f);
        _open.AutoSize = false;
        _open.Width = 112;
        _open.Height = 26;
        _open.UseVisualStyleBackColor = false;
        _open.FlatAppearance.BorderSize = 0;
        _open.Enabled = false;
        _open.Click += (_, _) => OpenSelected();

        FlowLayoutPanel bar = new()
        {
            Dock = DockStyle.Top,
            Height = 32,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Background,
            Padding = new Padding(0, 2, 0, 2),
        };
        bar.Controls.Add(_open);

        ConfigureGrid();
        Controls.Add(_grid);
        Controls.Add(_selected);
        Controls.Add(bar);
        Controls.Add(_source);
        Controls.Add(_summary);
    }

    public event EventHandler? SelectionChanged;

    public event EventHandler<MaterialUseRow>? OpenRequested;

    public MaterialUseRow? SelectedRow =>
        _selectedIndex >= 0 && _selectedIndex < _rows.Count ? _rows[_selectedIndex] : null;

    public int RowCount => _rows.Count;

    public void SetRows(IReadOnlyList<MaterialUseRow> rows, ulong sourceHash, int archiveCount)
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

            _summary.Text = $"All archives · {rows.Count:N0} uses in {archiveCount:N0} archives";
            _source.Text = $"Source texture 0x{sourceHash:X16}";
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
        _grid.BackgroundColor = Theme.Background;
        _grid.BorderStyle = BorderStyle.None;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.GridColor = Theme.Border;
        _grid.RowHeadersVisible = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.ReadOnly = true;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.ColumnHeadersHeight = 26;
        _grid.RowTemplate.Height = 26;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.GridHeader;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Muted;
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.GridHeader;
        _grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Theme.Muted;
        _grid.DefaultCellStyle.BackColor = Theme.Background;
        _grid.DefaultCellStyle.ForeColor = Theme.Text;
        _grid.DefaultCellStyle.SelectionBackColor = Theme.Selection;
        _grid.DefaultCellStyle.SelectionForeColor = Theme.Text;
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Theme.GridAlt;
        _grid.AlternatingRowsDefaultCellStyle.ForeColor = Theme.Text;
        _grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = Theme.Selection;
        _grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = Theme.Text;
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Material",
            FillWeight = 34,
            DefaultCellStyle = { Font = _monoFont },
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Code",
            FillWeight = 22,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Archive",
            FillWeight = 26,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Slot",
            FillWeight = 10,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight },
        });
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
