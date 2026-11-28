// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Drawing;
using AptStudio.Core.Materials;

namespace AptStudio.Desktop.Browser;

internal sealed class MaterialSlotsView : Panel
{
    private readonly DataGridView _grid = new();
    private readonly TableLayoutPanel _detail = new();
    private readonly Label _notice = new();
    private readonly Label _role = new();
    private readonly Label _binding = new();
    private readonly Font _monoFont = new("Consolas", 8.625f);
    private MaterialData? _data;
    private int _selectedSlot = -1;
    private bool _suppress;

    public MaterialSlotsView()
    {
        BackColor = Theme.Background;
        ConfigureGrid();
        ConfigureDetail();

        _notice.Dock = DockStyle.Fill;
        _notice.TextAlign = ContentAlignment.MiddleCenter;
        _notice.Font = BrowserVisual.Ui;
        _notice.ForeColor = Theme.Muted;
        _notice.BackColor = Theme.Background;
        _notice.AutoEllipsis = true;
        _notice.Padding = new Padding(24, 0, 24, 0);
        _notice.Visible = false;

        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Background,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(_grid, 0, 0);
        root.Controls.Add(_notice, 0, 0);
        root.Controls.Add(_detail, 0, 1);
        Controls.Add(root);
        Resize += (_, _) => UpdateDetailWidth();
        UpdateDetailWidth();
    }

    public event EventHandler? SelectionChanged;

    public event EventHandler<int>? OpenTextureRequested;

    public int SelectedSlot => _selectedSlot;

    internal bool NoticeActive { get; private set; }

    internal string NoticeText => _notice.Text;

    public void ShowNotice(string message, bool error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        _data = null;
        _selectedSlot = -1;
        _suppress = true;
        try
        {
            _grid.Rows.Clear();
        }
        finally
        {
            _suppress = false;
        }

        _grid.Visible = false;
        _notice.Text = message;
        _notice.ForeColor = error ? Theme.Error : Theme.Muted;
        _notice.Visible = true;
        _notice.BringToFront();
        _role.Text = "";
        _binding.Text = "";
        NoticeActive = true;
    }

    public void SetMaterial(
        MaterialData data,
        int sourceSlot,
        Func<ulong, int> copyCount)
    {
        _data = data;
        NoticeActive = false;
        _notice.Visible = false;
        _grid.Visible = true;
        _grid.BringToFront();
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

        UpdateDetail();
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

        UpdateDetail();
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

    private void ConfigureDetail()
    {
        _detail.AutoSize = true;
        _detail.Dock = DockStyle.Fill;
        _detail.ColumnCount = 2;
        _detail.RowCount = 2;
        _detail.BackColor = Theme.Surface;
        _detail.Padding = new Padding(8, 6, 8, 6);
        _detail.Margin = new Padding(0);
        _detail.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68));
        _detail.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddDetailRow(0, "Role", _role);
        AddDetailRow(1, "Binding", _binding);
        _detail.Paint += (_, e) =>
        {
            using Pen border = new(BrowserVisual.RowBorder);
            e.Graphics.DrawLine(border, 0, 0, _detail.Width, 0);
        };
    }

    private void AddDetailRow(int row, string name, Label value)
    {
        _detail.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _detail.Controls.Add(new Label
        {
            Text = name,
            Font = BrowserVisual.RowSmall,
            ForeColor = BrowserVisual.Faint,
            AutoSize = true,
            Margin = new Padding(0, 2, 6, 2),
        }, 0, row);
        value.Font = BrowserVisual.UiSmall;
        value.ForeColor = Theme.Text;
        value.AutoSize = true;
        value.MaximumSize = new Size(120, 0);
        value.Margin = new Padding(0, 2, 0, 2);
        _detail.Controls.Add(value, 1, row);
    }

    private void UpdateDetailWidth()
    {
        int width = Math.Max(80, ClientSize.Width - 96);
        _role.MaximumSize = new Size(width, 0);
        _binding.MaximumSize = new Size(width, 0);
    }

    private void UpdateDetail()
    {
        if (_grid.CurrentRow is null)
        {
            _role.Text = "";
            _binding.Text = "";
            return;
        }

        _role.Text = _grid.CurrentRow.Cells[1].Value?.ToString() ?? "";
        _binding.Text = _grid.CurrentRow.Cells[2].Value?.ToString() ?? "";
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
            UpdateDetail();
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
