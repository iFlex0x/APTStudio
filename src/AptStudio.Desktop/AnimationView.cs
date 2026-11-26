using System.Globalization;

namespace AptStudio.Desktop;

internal sealed record AnimationBoneRow(ulong NameHash, uint BitOffset, uint BitLength);

internal sealed class AnimationView : Panel
{
    private readonly Label _title = new();
    private readonly Label _subtitle = new();
    private readonly Label _note = new();
    private readonly DataGridView _bones = new();

    public AnimationView()
    {
        BackColor = Theme.Background;
        DoubleBuffered = true;

        _title.AutoEllipsis = true;
        _title.Font = new Font("Segoe UI Semibold", 9.5f);
        _title.ForeColor = Theme.Text;
        _title.TextAlign = ContentAlignment.MiddleLeft;
        _title.Padding = new Padding(8, 0, 8, 0);
        _title.Dock = DockStyle.Top;
        _title.Height = 23;

        _subtitle.AutoEllipsis = true;
        _subtitle.ForeColor = Theme.Muted;
        _subtitle.TextAlign = ContentAlignment.MiddleLeft;
        _subtitle.Padding = new Padding(8, 0, 8, 0);
        _subtitle.Dock = DockStyle.Top;
        _subtitle.Height = 20;

        Panel header = new() { Dock = DockStyle.Fill, BackColor = Theme.Background };
        header.Controls.Add(_subtitle);
        header.Controls.Add(_title);

        ConfigureBones();

        _note.AutoEllipsis = true;
        _note.ForeColor = Theme.Muted;
        _note.TextAlign = ContentAlignment.MiddleLeft;
        _note.Dock = DockStyle.Bottom;
        _note.Height = 24;
        _note.Padding = new Padding(8, 0, 8, 0);
        _note.Text = "packed key stream is measured but not decoded yet";

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Background,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(_bones, 0, 1);
        layout.Controls.Add(_note, 0, 2);
        Controls.Add(layout);
    }

    public void SetAnimation(
        string title,
        string subtitle,
        IReadOnlyList<AnimationBoneRow> bones,
        bool packed)
    {
        _title.Text = title;
        _subtitle.Text = subtitle;
        _note.Text = packed
            ? "packed key stream is measured but not decoded yet"
            : "frame table is measured; key values are not decoded yet";
        _bones.Rows.Clear();
        foreach (AnimationBoneRow bone in bones)
        {
            _bones.Rows.Add(
                bone.NameHash.ToString("X16", CultureInfo.InvariantCulture),
                bone.BitOffset.ToString(CultureInfo.InvariantCulture),
                bone.BitLength.ToString(CultureInfo.InvariantCulture));
        }
    }

    public void Reset()
    {
        _title.Text = "";
        _subtitle.Text = "";
        _bones.Rows.Clear();
    }

    public void SetDpiScale(float scale)
    {
        if (scale <= 0f)
        {
            return;
        }

        _title.Font = new Font("Segoe UI Semibold", 9.5f * scale);
        _bones.RowTemplate.Height = (int)Math.Round(20 * scale);
        _bones.ColumnHeadersHeight = (int)Math.Round(22 * scale);
        _bones.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8.5f * scale);
        foreach (DataGridViewColumn column in _bones.Columns)
        {
            if (column.Tag is int width)
            {
                column.Width = (int)Math.Round(width * scale);
            }
        }
    }

    private void ConfigureBones()
    {
        _bones.Dock = DockStyle.Fill;
        _bones.BackgroundColor = Theme.Background;
        _bones.GridColor = Theme.Border;
        _bones.BorderStyle = BorderStyle.None;
        _bones.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _bones.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _bones.EnableHeadersVisualStyles = false;
        _bones.ColumnHeadersHeight = 22;
        _bones.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _bones.RowHeadersVisible = false;
        _bones.AllowUserToAddRows = false;
        _bones.AllowUserToDeleteRows = false;
        _bones.AllowUserToResizeRows = false;
        _bones.ReadOnly = true;
        _bones.MultiSelect = false;
        _bones.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _bones.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _bones.RowTemplate.Height = 20;
        _bones.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.GridHeader,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.GridHeader,
            SelectionForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 8.5f),
            Padding = new Padding(4, 0, 4, 0),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
        };
        _bones.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Background,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.Selection,
            SelectionForeColor = Color.White,
            Padding = new Padding(4, 0, 4, 0),
        };
        _bones.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.GridAlt,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.Selection,
            SelectionForeColor = Color.White,
        };

        _bones.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Bone",
            Tag = 260,
            Width = 260,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });
        _bones.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Bit Offset",
            Tag = 100,
            Width = 100,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });
        _bones.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Bits",
            Tag = 80,
            Width = 80,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });
    }
}
