namespace AptStudio.Desktop.Browser;

internal static class BrowserLayout
{
    public const int CollapseWidth = 1100;
    public const int SidebarMinimum = 180;
    public const int InspectorMinimum = 320;
    public const int ResultsMinimum = 360;

    public static bool ShouldCollapseSidebar(int clientWidth) =>
        clientWidth < CollapseWidth;

    public static BrowserResizeAction PlanResize(FormWindowState state, bool wasMinimized)
    {
        if (state == FormWindowState.Minimized)
        {
            return BrowserResizeAction.Ignore;
        }

        return wasMinimized ? BrowserResizeAction.Reapply : BrowserResizeAction.Apply;
    }

    public static int ClampSidebar(int width, int clientWidth) =>
        Math.Clamp(width, SidebarMinimum, Math.Max(SidebarMinimum, clientWidth - 760));

    public static int ClampInspector(int width, int clientWidth) =>
        Math.Clamp(width, InspectorMinimum, Math.Max(InspectorMinimum, clientWidth - 680));
}

internal enum BrowserResizeAction
{
    Ignore,
    Apply,
    Reapply,
}
