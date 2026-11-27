using System.Drawing;
using AptStudio.Core.Materials;

namespace AptStudio.Desktop.Browser;

internal sealed class MaterialSlotsView : Panel
{
    private readonly DataGridView _grid = new();
    private readonly Font _monoFont = new("Consolas", 8.625f);
    private MaterialData? _data;
    private int _selectedSlot = -1;
    private bool _suppress;

    public MaterialSlotsView()
    {
        BackColor = Theme.Background;
        ConfigureGrid();
        Controls.Add(_grid);
    }

    public event EventHandler? SelectionChanged;

    public event EventHandler<int>? OpenTextureRequested;

    public int SelectedSlot => _selectedSlot;

    public void SetMaterial(
        MaterialData data,
        int sourceSlot,
        Func<ulong, int> copyCount)
    {
        _data = data;
        _suppress = true;
        try
        {
            _grid.Rows.Clear();
            foreach (MaterialSlot slot in data.Slots)
            {
                int index = _grid.Rows.Add(
                    slot.Index,
                    slot.Role.Authoring,
                    slot.Role.Binding,
                    $"0x{slot.TextureHash:X16}",
                    copyCount(slot.TextureHash));
                _grid.Rows[index].Tag = slot.Index;
            }

            _selectedSlot = sourceSlot;
            int row = RowForSlot(sourceSlot);
            if (row >= 0)
            {
                _grid.CurrentCell = _grid.Rows[row].Cells[0];
                _grid.Rows[row].Selected = true;
            }
        }
        finally
        {
            _suppress = false;
        }
    }

    public void SelectSlot(int slot)
    {
        int row = RowForSlot(slot);
        if (row < 0)
        {
            return;
        }

        _suppress = true;
        try
        {
            _grid.CurrentCell = _grid.Rows[row].Cells[0];
            _grid.Rows[row].Selected = true;
            _selectedSlot = slot;
        }
        finally
        {
            _suppress = false;
        }
    }

    public void FocusList()
    {
        _grid.Focus();
        if (_grid.CurrentRow is not null)
        {
            _grid.CurrentCell = _grid.CurrentRow.Cells[0];
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _monoFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private int RowForSlot(int slot)
    {
        for (int row = 0; row < _grid.Rows.Count; row++)
        {
            if (_grid.Rows[row].Tag is int value && value == slot)
            {
                return row;
            }
        }

        return -1;
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
            HeaderText = "SLOT",
            FillWeight = 8,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight },
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "ROLE",
            FillWeight = 20,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "BINDING",
            FillWeight = 18,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "TEXTURE",
            FillWeight = 42,
            DefaultCellStyle = { Font = _monoFont },
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "COPIES",
            FillWeight = 12,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight },
        });
        _grid.SelectionChanged += (_, _) =>
        {
            if (_suppress || _grid.CurrentRow?.Tag is not int slot)
            {
                return;
            }

            _selectedSlot = slot;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        };
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && _grid.Rows[e.RowIndex].Tag is int slot)
            {
                OpenTextureRequested?.Invoke(this, slot);
            }
        };
        _grid.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && _selectedSlot >= 0)
            {
                OpenTextureRequested?.Invoke(this, _selectedSlot);
                e.Handled = true;
            }
        };
    }
}
