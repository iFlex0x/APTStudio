using AptStudio.Core.Links;
using AptStudio.Core.Materials;
using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class MaterialChooserTests
{
    [Fact]
    public void ChooserOpensTheChosenSlot()
    {
        MaterialHit hit = new(0, 0xAAAA, 0xAAAA, MaterialHead.MaterialClass, 0);
        MaterialEntry entry = new("APOCALYPSE.DPC", hit);
        MaterialTextureUse use = new(entry, "CODE_EYE", [0, 13]);
        using MaterialChooserView view = new();
        view.SetRows(
            [new MaterialUseRow(use, 0), new MaterialUseRow(use, 13)]);
        MaterialUseRow? opened = null;
        view.OpenRequested += (_, row) => opened = row;

        view.SelectIndex(1);
        view.ActivateSelected();

        Assert.NotNull(opened);
        Assert.Equal(13, opened!.Slot);
        Assert.Equal(0xAAAAUL, opened.Hash);
    }

    [Fact]
    public void EmptyRowsKeepTheCallerMessage()
    {
        using MaterialChooserView view = new();

        view.SetRows([]);

        Assert.Contains("No material uses.", TextValues(view));
    }

    private static IEnumerable<string> TextValues(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child.Text.Length > 0)
            {
                yield return child.Text;
            }

            foreach (string text in TextValues(child))
            {
                yield return text;
            }
        }
    }
}
