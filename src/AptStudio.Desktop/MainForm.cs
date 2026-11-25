namespace AptStudio.Desktop;

internal sealed class MainForm : Form
{
    public MainForm()
    {
        Text = "APTStudio";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1100, 720);
        MinimumSize = new Size(800, 500);
    }
}
