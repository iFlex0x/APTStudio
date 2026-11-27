using AptStudio.Core.Skins;

namespace AptStudio.Desktop.Browser;

internal static class SkinSkeletonText
{
    public static string Text(SkinBinding? binding, string? bindingError)
    {
        if (bindingError is not null)
        {
            return "Skin binding unavailable";
        }

        return binding is { HasSkeleton: true }
            ? $"skeleton 0x{binding.SkeletonRoot:X16}"
            : "No explicit skeleton binding";
    }

    public static string Missing(ulong root) =>
        $"Skeleton 0x{root:X16} was not found.";
}
