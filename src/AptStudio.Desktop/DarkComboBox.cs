namespace AptStudio.Desktop;

internal sealed class DarkComboBox : ComboBox
{
    public DarkComboBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        DropDownStyle = ComboBoxStyle.DropDownList;
        FlatStyle = FlatStyle.Flat;
        BackColor = Theme.Surface;
        ForeColor = Theme.Text;
        ItemHeight = 20;
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ItemHeight = Math.Max(18, Font.Height + 4);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using (SolidBrush back = new(selected ? Theme.Selection : Theme.Surface))
        {
            e.Graphics.FillRectangle(back, e.Bounds);
        }

        if (e.Index < 0)
        {
            return;
        }

        TextRenderer.DrawText(
            e.Graphics,
            GetItemText(Items[e.Index]),
            Font,
            e.Bounds,
            Theme.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}
