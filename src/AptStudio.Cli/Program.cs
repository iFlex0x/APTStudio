using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using AptStudio.Core.Animations;
using AptStudio.Core.Assets;
using AptStudio.Core.Audio;
using AptStudio.Core.Containers;
using AptStudio.Core.Games;
using AptStudio.Core.Links;
using AptStudio.Core.Materials;
using AptStudio.Core.Meshes;
using AptStudio.Core.Skeletons;
using AptStudio.Core.Skins;
using AptStudio.Core.Textures;

namespace AptStudio.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 2 && string.Equals(args[0], "scan", StringComparison.OrdinalIgnoreCase))
        {
            return Scan(args[1]);
        }

        if ((args.Length == 2 || args.Length == 3)
            && string.Equals(args[0], "list", StringComparison.OrdinalIgnoreCase))
        {
            bool rebuild = false;
            if (args.Length == 3)
            {
                if (!string.Equals(args[2], "--rebuild", StringComparison.OrdinalIgnoreCase))
                {
                    Usage();
                    return 1;
                }

                rebuild = true;
            }

            return List(args[1], rebuild);
        }

        if (args.Length == 2 && string.Equals(args[0], "assets", StringComparison.OrdinalIgnoreCase))
        {
            return Assets(args[1]);
        }

        if (args.Length == 3 && string.Equals(args[0], "mesh", StringComparison.OrdinalIgnoreCase))
        {
            return Mesh(args[1], args[2]);
        }

        if ((args.Length == 2 || args.Length == 3)
            && string.Equals(args[0], "materials", StringComparison.OrdinalIgnoreCase))
        {
            bool rebuild = args.Length == 3
                && string.Equals(args[2], "--rebuild", StringComparison.OrdinalIgnoreCase);
            if (args.Length == 3 && !rebuild)
            {
                Usage();
                return 1;
            }

            return Materials(args[1], rebuild);
        }

        if (args.Length == 3 && string.Equals(args[0], "material", StringComparison.OrdinalIgnoreCase))
        {
            return Material(args[1], args[2]);
        }

        if (args.Length == 2 && string.Equals(args[0], "skins", StringComparison.OrdinalIgnoreCase))
        {
            return Skins(args[1]);
        }

        if (args.Length == 3 && string.Equals(args[0], "skin", StringComparison.OrdinalIgnoreCase))
        {
            return Skin(args[1], args[2]);
        }

        if (args.Length == 3 && string.Equals(args[0], "animation", StringComparison.OrdinalIgnoreCase))
        {
            return Animation(args[1], args[2]);
        }

        if ((args.Length == 3 || args.Length == 4)
            && string.Equals(args[0], "animclip", StringComparison.OrdinalIgnoreCase))
        {
            return AnimationClipCommand(args[1], args[2], args.Length == 4 ? args[3] : null);
        }

        if ((args.Length == 2 || args.Length == 3)
            && string.Equals(args[0], "skeletons", StringComparison.OrdinalIgnoreCase))
        {
            bool rebuild = false;
            if (args.Length == 3)
            {
                if (!string.Equals(args[2], "--rebuild", StringComparison.OrdinalIgnoreCase))
                {
                    Usage();
                    return 1;
                }

                rebuild = true;
            }

            return Skeletons(args[1], rebuild);
        }

        if (args.Length == 3 && string.Equals(args[0], "skeleton", StringComparison.OrdinalIgnoreCase))
        {
            return Skeleton(args[1], args[2]);
        }

        if (args.Length == 2 && string.Equals(args[0], "audio", StringComparison.OrdinalIgnoreCase))
        {
            return Audio(args[1]);
        }

        if (args.Length == 2 && string.Equals(args[0], "links", StringComparison.OrdinalIgnoreCase))
        {
            return Links(args[1]);
        }

        if (args.Length == 2 && string.Equals(args[0], "skelbind", StringComparison.OrdinalIgnoreCase))
        {
            return SkeletonBindings(args[1]);
        }

        if ((args.Length == 5 || args.Length == 6)
            && string.Equals(args[0], "pose", StringComparison.OrdinalIgnoreCase))
        {
            return Pose(
                args[1], args[2], args[3], args[4], args.Length == 6 ? args[5] : "z");
        }

        if (args.Length == 5 && string.Equals(args[0], "audioconvert", StringComparison.OrdinalIgnoreCase))
        {
            return AudioConvert(args[1], args[2], args[3], args[4]);
        }

        if (args.Length == 4 && string.Equals(args[0], "export", StringComparison.OrdinalIgnoreCase))
        {
            return Export(args[1], args[2], args[3]);
        }

        Usage();
        return 1;
    }

    private static void Usage()
    {
        Console.Error.WriteLine("usage:");
        Console.Error.WriteLine("  aptstudio scan <gameRoot>");
        Console.Error.WriteLine("  aptstudio list <gameRoot> [--rebuild]");
        Console.Error.WriteLine("  aptstudio assets <gameRoot>");
        Console.Error.WriteLine("  aptstudio mesh <gameRoot> <nameHash>");
        Console.Error.WriteLine("  aptstudio materials <gameRoot> [--rebuild]");
        Console.Error.WriteLine("  aptstudio material <gameRoot> <nameHash>");
        Console.Error.WriteLine("  aptstudio skins <gameRoot>");
        Console.Error.WriteLine("  aptstudio skin <gameRoot> <nameHash>");
        Console.Error.WriteLine("  aptstudio animation <gameRoot> <nameHash>");
        Console.Error.WriteLine("  aptstudio animclip <gameRoot> <nameHash> [frame]");
        Console.Error.WriteLine("  aptstudio skeletons <gameRoot> [--rebuild]");
        Console.Error.WriteLine("  aptstudio skeleton <gameRoot> <nameHash>");
        Console.Error.WriteLine("  aptstudio audio <gameRoot>");
        Console.Error.WriteLine("  aptstudio links <gameRoot>");
        Console.Error.WriteLine("  aptstudio skelbind <gameRoot>");
        Console.Error.WriteLine("  aptstudio pose <gameRoot> <skinHash> <boneHash> <degrees> [x|y|z]");
        Console.Error.WriteLine("  aptstudio audioconvert <gameRoot> <pack> <mediaId> <output.ogg>");
        Console.Error.WriteLine("  aptstudio export <gameRoot> <nameHash> <outputPath>");
    }

    private static int Assets(string gameRoot)
    {
        if (!GameProfile.TryDetect(gameRoot, out GameProfile? profile, out string? error) || profile is null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        Stopwatch watch = Stopwatch.StartNew();
        AssetCatalog catalog;
        try
        {
            catalog = AssetCatalog.Load(profile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        watch.Stop();
        foreach (AssetClassCount group in catalog.Classes)
        {
            Console.WriteLine($"{group.Name,-20} {N(group.Count),8}");
        }

        Console.WriteLine(
            $"assets: {N(catalog.Entries.Count)}, bitmap rows: {N(catalog.BitmapCount)}, previewable rows: {N(catalog.PreviewableCount)}, archives: {N(catalog.Archives.Count)}, files: {N(catalog.FileCount)}, body errors: {N(catalog.BodyErrorCount)}, failed: {N(catalog.Failures.Count)}, {N(watch.ElapsedMilliseconds)} ms");

        foreach (CatalogFailure failure in catalog.Failures)
        {
            Console.Error.WriteLine(failure.Message);
        }

        foreach (AssetEntry entry in catalog.Entries
            .Where(item => item.BodyError is not null)
            .Take(20))
        {
            Console.Error.WriteLine(entry.BodyError);
        }

        return catalog.Failures.Count == 0 && catalog.BodyErrorCount == 0 ? 0 : 2;
    }

    private static int Materials(string gameRoot, bool rebuild)
    {
        if (!GameProfile.TryDetect(gameRoot, out GameProfile? profile, out string? error) || profile is null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        Stopwatch watch = Stopwatch.StartNew();
        MaterialCatalog materials;
        AssetCatalog assets;
        try
        {
            materials = MaterialCatalog.Load(profile, rebuild);
            assets = AssetCatalog.Load(profile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        Dictionary<ulong, AssetEntry> bitmaps = BitmapByName(assets);
        long slots = 0;
        long resolved = 0;
        int failed = 0;
        Dictionary<string, int> codes = new();
        Dictionary<ulong, int> classes = new();
        List<string> failures = new();
        foreach (IGrouping<string, MaterialEntry> group in materials.Entries.GroupBy(
            entry => entry.ArchivePath))
        {
            try
            {
                using FileStream stream = new(
                    group.Key, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                foreach (MaterialEntry entry in group)
                {
                    try
                    {
                        MaterialData material = MaterialDecoder.Read(stream, entry.ArchivePath, entry.Hit);
                        classes[material.Head.ClassHash] = classes.GetValueOrDefault(material.Head.ClassHash) + 1;
                        codes[material.Code] = codes.GetValueOrDefault(material.Code) + 1;
                        slots += material.Slots.Count;
                        resolved += material.Slots.Count(slot => bitmaps.ContainsKey(slot.TextureHash));
                    }
                    catch (InvalidDataException ex)
                    {
                        failed++;
                        if (failures.Count < 10)
                        {
                            failures.Add(ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed += group.Count();
                if (failures.Count < 10)
                {
                    failures.Add(ex.Message);
                }
            }
        }

        watch.Stop();
        int animation = classes.GetValueOrDefault(MaterialHead.AnimClass);
        Console.WriteLine(
            $"materials: {N(materials.Entries.Count)} (Material_Z {N(materials.Entries.Count - animation)}, MaterialAnim_Z {N(animation)}), archives: {N(materials.Archives.Count)}, slots: {N(slots)}, resolved: {N(resolved)}, failed: {N(failed)}, scanned: {N(materials.Scanned)}, cached: {N(materials.Cached)}, {N(watch.ElapsedMilliseconds)} ms");
        foreach ((string code, int count) in codes.OrderByDescending(pair => pair.Value).Take(12))
        {
            Console.WriteLine($"  {code,-32} {N(count),8}");
        }

        foreach (CatalogFailure failure in materials.Failures)
        {
            Console.Error.WriteLine(failure.Message);
        }

        foreach (string failure in failures)
        {
            Console.Error.WriteLine(failure);
        }

        return failed == 0 && materials.Failures.Count == 0 && resolved == slots ? 0 : 2;
    }

    private static int Material(string gameRoot, string hashText)
    {
        if (!TryHash(hashText, out ulong nameHash))
        {
            Console.Error.WriteLine($"'{hashText}' is not a 64-bit hex hash.");
            return 1;
        }

        if (!GameProfile.TryDetect(gameRoot, out GameProfile? profile, out string? error) || profile is null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        try
        {
            MaterialCatalog materials = MaterialCatalog.Load(profile);
            AssetCatalog assets = AssetCatalog.Load(profile);
            Dictionary<ulong, AssetEntry> bitmaps = BitmapByName(assets);
            List<MaterialEntry> matches = materials.Entries
                .Where(entry => entry.NameHash == nameHash)
                .ToList();
            if (matches.Count == 0)
            {
                Console.Error.WriteLine($"no Material_Z row named 0x{nameHash:X16}.");
                return 1;
            }

            foreach (IGrouping<string, MaterialEntry> group in matches.GroupBy(
                entry => entry.ArchivePath))
            {
                using FileStream stream = new(
                    group.Key, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                foreach (MaterialEntry entry in group)
                {
                    MaterialData material = MaterialDecoder.Read(stream, entry.ArchivePath, entry.Hit);
                    Console.WriteLine(
                        $"{Path.GetFileName(entry.ArchivePath)} 0x{entry.NameHash:X16} offset {entry.Offset}");
                    Console.WriteLine(
                        $"  {material.Head.ClassHash switch { MaterialHead.MaterialClass => "Material_Z", _ => "MaterialAnim_Z" }}, version {material.Version}, {material.Code}, {material.Slots.Count} slots, mask 0x{material.Mask:X16}");
                    foreach (MaterialSlot slot in material.Slots)
                    {
                        string texture = bitmaps.TryGetValue(slot.TextureHash, out AssetEntry? bitmap)
                            ? $"{Path.GetFileName(bitmap.ArchivePath)} 0x{slot.TextureHash:X16}"
                            : $"missing 0x{slot.TextureHash:X16}";
                        Console.WriteLine(
                            $"  {slot.Index,2} {slot.Role.Authoring,-28} {slot.Role.Binding,-22} {texture}");
                    }
                }
            }

            return 0;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static Dictionary<ulong, AssetEntry> BitmapByName(AssetCatalog catalog) =>
        catalog.Entries
            .Where(entry => entry.IsBitmap)
            .GroupBy(entry => entry.NameHash)
            .ToDictionary(group => group.Key, group => group.First());

    private static Func<ulong, int?> MeshVertexLookup(AssetCatalog assets)
    {
        Dictionary<ulong, AssetEntry> rows = assets.Entries
            .Where(entry => entry.ClassHash == AssetClass.Mesh)
            .GroupBy(entry => entry.NameHash)
            .ToDictionary(group => group.Key, group => group.First());
        Dictionary<ulong, int?> cache = new();
        return mesh =>
        {
            if (cache.TryGetValue(mesh, out int? known))
            {
                return known;
            }

            int? result = null;
            if (rows.TryGetValue(mesh, out AssetEntry? row))
            {
                try
                {
                    using FileStream stream = new(
                        row.BodyArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    MeshData data = MeshDecoder.Read(stream, row.BodyArchivePath, row.Row);
                    result = data.Streams.Count > 0 ? data.Streams[0].VertexCount : 0;
                }
                catch (InvalidDataException)
                {
                    result = null;
                }
            }

            cache[mesh] = result;
            return result;
        };
    }

    private static int Skins(string gameRoot)
    {
        if (!GameProfile.TryDetect(gameRoot, out GameProfile? profile, out string? error) || profile is null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        try
        {
            Stopwatch watch = Stopwatch.StartNew();
            AssetCatalog assets = AssetCatalog.Load(profile);
            int rows = assets.Entries.Count(entry => entry.ClassHash == AssetClass.Skin);
            Func<ulong, int?> vertices = MeshVertexLookup(assets);
            HashSet<(string Path, long Offset)> seen = new();
            Dictionary<ushort, int> versions = new();
            Dictionary<int, int> strides = new();
            List<string> failures = new();
            int bodies = 0;
            int parts = 0;
            int skinned = 0;
            int packets = 0;
            int bones = 0;
            int failed = 0;
            long verticesTotal = 0;
            foreach (AssetEntry entry in assets.Entries)
            {
                if (entry.ClassHash != AssetClass.Skin
                    || !seen.Add((entry.BodyArchivePath, entry.Row.BodyOffset)))
                {
                    continue;
                }

                bodies++;
                try
                {
                    using FileStream stream = new(
                        entry.BodyArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    SkinData skin = SkinDecoder.Read(
                        stream, entry.BodyArchivePath, entry.Row, vertices);
                    versions[skin.Head.Version] = versions.GetValueOrDefault(skin.Head.Version) + 1;
                    parts += skin.Parts.Length;
                    bones += skin.Bones.Length;
                    foreach (SkinPart part in skin.Parts)
                    {
                        if (part.VertexCount == 0)
                        {
                            continue;
                        }

                        skinned++;
                        verticesTotal += part.VertexCount;
                        strides[part.Stride] = strides.GetValueOrDefault(part.Stride) + 1;
                        if (part.PacketsParsed)
                        {
                            packets++;
                        }
                    }
                }
                catch (InvalidDataException ex)
                {
                    failed++;
                    if (failures.Count < 10)
                    {
                        failures.Add(ex.Message);
                    }
                }
            }

            watch.Stop();
            Console.WriteLine(
                $"skins: {N(rows)} rows, {N(bodies)} bodies, parts: {N(parts)}, skinned: {N(skinned)}, vertices: {N(verticesTotal)}, packets: {N(packets)}, bones: {N(bones)}, versions: {string.Join(", ", versions.OrderBy(p => p.Key).Select(p => $"v{p.Key}={N(p.Value)}"))}, strides: {string.Join(", ", strides.OrderBy(p => p.Key).Select(p => $"{p.Key}={N(p.Value)}"))}, failed: {N(failed)}, {N(watch.ElapsedMilliseconds)} ms");
            foreach (string failure in failures)
            {
                Console.Error.WriteLine(failure);
            }

            return failed == 0 ? 0 : 2;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int Skin(string gameRoot, string hashText)
    {
        if (!TryHash(hashText, out ulong nameHash))
        {
            Console.Error.WriteLine($"'{hashText}' is not a 64-bit hex hash.");
            return 1;
        }

        if (!GameProfile.TryDetect(gameRoot, out GameProfile? profile, out string? error) || profile is null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        try
        {
            AssetCatalog assets = AssetCatalog.Load(profile);
            List<AssetEntry> matches = assets.Entries
                .Where(entry => entry.ClassHash == AssetClass.Skin && entry.NameHash == nameHash)
                .ToList();
            if (matches.Count == 0)
            {
                Console.Error.WriteLine($"no Skin_Z row named 0x{nameHash:X16}.");
                return 1;
            }

            Func<ulong, int?> vertices = MeshVertexLookup(assets);

            foreach (AssetEntry entry in matches)
            {
                using FileStream stream = new(
                    entry.BodyArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                SkinData skin = SkinDecoder.Read(
                    stream, entry.BodyArchivePath, entry.Row, vertices);
                string external = entry.IsExternal ? " body COMMON.DPC" : string.Empty;
                Console.WriteLine(
                    $"{Path.GetFileName(entry.ArchivePath)} 0x{skin.Head.NameHash:X16} offset {entry.Row.Offset}{external}");
                Console.WriteLine(
                    $"  version {skin.Head.Version}, {skin.Head.Storage}, body {N(skin.Head.BodySize)}, stored {N(skin.Head.StoredSize)}, meshes {skin.MeshLinks.Length}, parts {skin.Parts.Length}, bones {skin.Bones.Length}");
                SkinBinding? binding = null;
                try
                {
                    binding = SkinBinding.Read(stream, entry.BodyArchivePath, entry.Row);
                }
                catch (InvalidDataException)
                {
                }

                string skeleton = binding is null
                    ? "unreadable"
                    : binding.HasSkeleton ? $"0x{binding.SkeletonRoot:X16}" : "none";
                Console.WriteLine($"  skeleton {skeleton}");
                foreach (SkinPart part in skin.Parts)
                {
                    string packets = part.PacketsParsed
                        ? $"{part.PacketCount} packet{(part.PacketCount == 1 ? string.Empty : "s")} 0x{part.MaterialHash:X16}"
                        : "packets not measured";
                    Console.WriteLine(
                        $"  part {part.Index,2}: mesh 0x{part.MeshHash:X16}, {N(part.VertexCount)} vertices, stride {part.Stride}, {packets}");
                }
            }

            return 0;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int Animation(string gameRoot, string hashText)
    {
        if (!TryHash(hashText, out ulong nameHash))
        {
            Console.Error.WriteLine($"'{hashText}' is not a 64-bit hex hash.");
            return 1;
        }

        if (!GameProfile.TryDetect(gameRoot, out GameProfile? profile, out string? error) || profile is null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        try
        {
            AssetCatalog assets = AssetCatalog.Load(profile);
            List<AssetEntry> matches = assets.Entries
                .Where(entry => entry.ClassHash == AssetClass.Animation && entry.NameHash == nameHash)
                .ToList();
            if (matches.Count == 0)
            {
                Console.Error.WriteLine($"no Animation_Z row named 0x{nameHash:X16}.");
                return 1;
            }

            foreach (IGrouping<(string Path, long Offset), AssetEntry> group in matches
                .GroupBy(entry => (entry.BodyArchivePath, entry.Row.BodyOffset)))
            {
                AssetEntry entry = group.First();
                using FileStream stream = new(
                    entry.BodyArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                AnimationData animation = AnimationDecoder.Read(stream, entry.BodyArchivePath, entry.Row);
                string external = entry.IsExternal ? " body COMMON.DPC" : string.Empty;
                Console.WriteLine(
                    $"{Path.GetFileName(entry.ArchivePath)} 0x{animation.Head.NameHash:X16} offset {entry.Row.Offset}{external}");
                Console.WriteLine(
                    $"  rows {group.Count()} across {group.Select(item => item.ArchivePath).Distinct().Count()} archives");
                Console.WriteLine(
                    $"  duration {animation.Duration:0.###}s, keys {N(animation.KeyCount)}, {animation.Encoding}, packed bits {N(animation.PackedBits)}, flags 0x{animation.Flags:X4}");
                if (animation.Encoding == AnimationEncoding.Packed)
                {
                    Console.WriteLine(
                        $"  bones {animation.Bones.Count} ({animation.AnimatedBones} animated, {animation.BaseBones} base), stream at 0x{animation.PackedStreamOffset:X}, extra {animation.Head.ExtraSize}");
                }
                else
                {
                    Console.WriteLine(
                        $"  frame entries {N(animation.FrameEntries)}, tracks {N(animation.FrameTracks)}, extra {animation.Head.ExtraSize}");
                }

                foreach (AnimationBone bone in animation.Bones.Take(32))
                {
                    Console.WriteLine(
                        $"  bone 0x{bone.NameHash:X16} bits {bone.BitOffset} +{bone.BitLength}");
                }
            }

            return 0;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int AnimationClipCommand(string gameRoot, string hashText, string? frameText)
    {
        if (!TryHash(hashText, out ulong nameHash))
        {
            Console.Error.WriteLine($"'{hashText}' is not a 64-bit hex hash.");
            return 1;
        }

        int frame = 0;
        if (frameText is not null
            && !int.TryParse(frameText, NumberStyles.Integer, CultureInfo.InvariantCulture, out frame))
        {
            Console.Error.WriteLine($"'{frameText}' is not a frame number.");
            return 1;
        }

        if (!GameProfile.TryDetect(gameRoot, out GameProfile? profile, out string? error) || profile is null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        try
        {
            AssetCatalog assets = AssetCatalog.Load(profile);
            AssetEntry? entry = assets.Entries
                .Where(row => row.ClassHash == AssetClass.Animation && row.NameHash == nameHash)
                .GroupBy(row => (row.BodyArchivePath, row.Row.BodyOffset))
                .Select(group => group
                    .OrderBy(row => row.ArchivePath, StringComparer.OrdinalIgnoreCase)
                    .First())
                .FirstOrDefault();
            if (entry is null)
            {
                Console.Error.WriteLine($"no Animation_Z row named 0x{nameHash:X16}.");
                return 1;
            }

            using FileStream stream = new(
                entry.BodyArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            AnimationBody body = AnimationDecoder.ReadBody(stream, entry.BodyArchivePath, entry.Row);
            AnimationClip clip = AnimationClipReader.Read(body.Data, body.Body);
            int at = Math.Clamp(frame, 0, clip.FrameCount - 1);
            Console.WriteLine(
                $"animclip: 0x{nameHash:X16} {clip.Duration:0.###}s, keys {N(clip.KeyCount)}, groups {clip.GroupCount}, frames {N(clip.FrameCount)}, bones {N(clip.Bones.Count)}, unreadable {N(clip.UnreadableBones)}");
            int shown = 0;
            foreach (AnimationClipBone bone in clip.Bones)
            {
                IReadOnlyList<Quaternion> rotations = bone.Rotations;
                Quaternion q = rotations[Math.Min(at, rotations.Count - 1)];
                if (q == Quaternion.Identity)
                {
                    continue;
                }

                Console.WriteLine(
                    $"  bone 0x{bone.NameHash:X16} B {bone.ValueBits} keys {N(rotations.Count)} frame {at}: ({q.X:F4},{q.Y:F4},{q.Z:F4},{q.W:F4})");
                if (++shown == 8)
                {
                    break;
                }
            }

            if (shown == 0)
            {
                Console.WriteLine("  all sampled rotations are identity.");
            }

            return 0;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int Pose(
        string gameRoot,
        string skinText,
        string boneText,
        string angleText,
        string axisText)
    {
        if (!TryHash(skinText, out ulong skinHash) || !TryHash(boneText, out ulong boneHash))
        {
            Console.Error.WriteLine("skin and bone must be 64-bit hex hashes.");
            return 1;
        }

        if (!float.TryParse(angleText, NumberStyles.Float, CultureInfo.InvariantCulture, out float degrees))
        {
            Console.Error.WriteLine($"'{angleText}' is not an angle.");
            return 1;
        }

        if (!float.IsFinite(degrees))
        {
            Console.Error.WriteLine("angle must be finite.");
            return 1;
        }

        Vector3 axis = axisText.ToLowerInvariant() switch
        {
            "x" => Vector3.UnitX,
            "y" => Vector3.UnitY,
            "z" => Vector3.UnitZ,
            _ => new Vector3(float.NaN),
        };
        if (!float.IsFinite(axis.X))
        {
            Console.Error.WriteLine($"axis '{axisText}' is not x, y, or z.");
            return 1;
        }

        if (!GameProfile.TryDetect(gameRoot, out GameProfile? profile, out string? error) || profile is null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        try
        {
            Stopwatch watch = Stopwatch.StartNew();
            AssetCatalog assets = AssetCatalog.Load(profile);
            AssetEntry? skinRow = assets.Entries
                .Where(entry => entry.ClassHash == AssetClass.Skin && entry.NameHash == skinHash)
                .GroupBy(entry => (entry.BodyArchivePath, entry.Row.BodyOffset))
                .Select(group => group
                    .OrderBy(entry => entry.ArchivePath, StringComparer.OrdinalIgnoreCase)
                    .First())
                .FirstOrDefault();
            if (skinRow is null)
            {
                Console.Error.WriteLine($"no Skin_Z row named 0x{skinHash:X16}.");
                return 1;
            }

            Dictionary<ulong, AssetEntry> meshRows = new();
            foreach (AssetEntry entry in assets.Entries)
            {
                if (entry.ClassHash == AssetClass.Mesh)
                {
                    meshRows.TryAdd(entry.NameHash, entry);
                }
            }

            Dictionary<ulong, MeshGeometry?> meshes = new();
            MeshGeometry? Geometry(ulong hash)
            {
                if (meshes.TryGetValue(hash, out MeshGeometry? cached))
                {
                    return cached;
                }

                if (!meshRows.TryGetValue(hash, out AssetEntry? row))
                {
                    return null;
                }

                using FileStream stream = new(
                    row.BodyArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                MeshData data = MeshDecoder.Read(stream, row.BodyArchivePath, row.Row);
                MeshGeometry geometry = data.Streams.Count == 0
                    ? new MeshGeometry([], [], Vector3.Zero, Vector3.Zero)
                    : data.ToGeometry();
                meshes.Add(hash, geometry);
                return geometry;
            }

            int? Verts(ulong hash) => Geometry(hash)?.Positions.Length;

            SkinData skin;
            SkinBinding binding;
            using (FileStream stream = new(
                skinRow.BodyArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                skin = SkinDecoder.Read(stream, skinRow.BodyArchivePath, skinRow.Row, Verts);
                binding = SkinBinding.Read(stream, skinRow.BodyArchivePath, skinRow.Row);
            }

            if (!binding.HasSkeleton)
            {
                Console.Error.WriteLine($"skin 0x{skinHash:X16} has no skeleton.");
                return 1;
            }

            SkeletonCatalog skeletons = SkeletonCatalog.Load(profile);
            SkeletonEntry? skeletonRow = skeletons.Entries
                .Where(entry => entry.NameHash == binding.SkeletonRoot)
                .OrderBy(entry => entry.ArchivePath, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (skeletonRow is null)
            {
                Console.Error.WriteLine($"skeleton 0x{binding.SkeletonRoot:X16} is not in the catalog.");
                return 1;
            }

            SkeletonData skeleton;
            using (FileStream stream = new(
                skeletonRow.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                skeleton = SkeletonDecoder.Read(stream, skeletonRow.ArchivePath, skeletonRow.Hit);
            }

            SkinDeformer deformer = SkinDeformer.Create(skin, skeleton, Geometry);
            SkinPaletteBone? target = deformer.Bones.FirstOrDefault(
                bone => bone.NameHash == boneHash);
            if (target is null)
            {
                Console.Error.WriteLine($"bone 0x{boneHash:X16} is not in skin 0x{skinHash:X16}.");
                return 1;
            }

            MeshGeometry bind = deformer.Deform(null);
            MeshGeometry posed = deformer.Deform(new Dictionary<int, Quaternion>
            {
                [target.SkeletonBone] = Quaternion.CreateFromAxisAngle(
                    axis, degrees * MathF.PI / 180f),
            });
            int moved = 0;
            double max = 0;
            for (int index = 0; index < bind.Positions.Length; index++)
            {
                double distance = Vector3.Distance(bind.Positions[index], posed.Positions[index]);
                if (distance > 1e-4)
                {
                    moved++;
                }

                max = Math.Max(max, distance);
            }

            Console.WriteLine(
                $"pose: skin 0x{skin.Head.NameHash:X16} skeleton 0x{skeleton.Head.NameHash:X16}");
            Console.WriteLine(
                $"  parts {N(deformer.Parts.Count)} of {N(skin.Parts.Length)}, bones {N(deformer.Bones.Count)} of {N(skin.Bones.Length)}");
            Console.WriteLine(
                $"  bone 0x{boneHash:X16} skeleton index {target.SkeletonBone}, axis {axisText.ToLowerInvariant()}, angle {degrees:F1}");
            Console.WriteLine(
                $"  bind bounds ({bind.Min.X:F3},{bind.Min.Y:F3},{bind.Min.Z:F3})..({bind.Max.X:F3},{bind.Max.Y:F3},{bind.Max.Z:F3})");
            Console.WriteLine(
                $"  posed bounds ({posed.Min.X:F3},{posed.Min.Y:F3},{posed.Min.Z:F3})..({posed.Max.X:F3},{posed.Max.Y:F3},{posed.Max.Z:F3}), moved {N(moved)} of {N(bind.Positions.Length)}, max {max:F3}, {N(watch.ElapsedMilliseconds)} ms");
            return 0;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int Skeletons(string gameRoot, bool rebuild)
    {
        if (!GameProfile.TryDetect(gameRoot, out GameProfile? profile, out string? error) || profile is null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        try
        {
            Stopwatch watch = Stopwatch.StartNew();
            SkeletonCatalog skeletons = SkeletonCatalog.Load(profile, rebuild);
            int bones = 0;
            int maxBones = 0;
            long tails = 0;
            int failed = 0;
            List<string> failures = new();
            foreach (SkeletonEntry entry in skeletons.Entries)
            {
                try
                {
                    using FileStream stream = new(
                        entry.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    SkeletonData data = SkeletonDecoder.Read(
                        stream, entry.ArchivePath, entry.Hit);
                    bones += data.Bones.Count;
                    maxBones = Math.Max(maxBones, data.Bones.Count);
                    tails += data.TailLength;
                }
                catch (InvalidDataException ex)
                {
                    failed++;
                    if (failures.Count < 10)
                    {
                        failures.Add(ex.Message);
                    }
                }
            }

            watch.Stop();
            Console.WriteLine(
                $"skeletons: {N(skeletons.Entries.Count)} objects, {N(skeletons.UniqueNameCount)} names, archives: {N(skeletons.Archives.Count)}, bones: {N(bones)}, max: {N(maxBones)}, tail: {N(tails)} bytes, failed: {N(failed)}, scanned: {N(skeletons.Scanned)}, cached: {N(skeletons.Cached)}, {N(watch.ElapsedMilliseconds)} ms");
            foreach (string failure in failures)
            {
                Console.Error.WriteLine(failure);
            }

            return failed == 0 ? 0 : 2;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int Skeleton(string gameRoot, string hashText)
    {
        if (!TryHash(hashText, out ulong nameHash))
        {
            Console.Error.WriteLine($"'{hashText}' is not a 64-bit hex hash.");
            return 1;
        }

        if (!GameProfile.TryDetect(gameRoot, out GameProfile? profile, out string? error) || profile is null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        try
        {
            SkeletonCatalog skeletons = SkeletonCatalog.Load(profile);
            List<SkeletonEntry> matches = skeletons.Entries
                .Where(entry => entry.NameHash == nameHash)
                .ToList();
            if (matches.Count == 0)
            {
                Console.Error.WriteLine($"no Skel_Z object named 0x{nameHash:X16}.");
                return 1;
            }

            foreach (SkeletonEntry entry in matches)
            {
                Console.WriteLine(
                    $"{Path.GetFileName(entry.ArchivePath)} 0x{entry.NameHash:X16} offset {entry.Offset} body {N(entry.Hit.BodySize)} stored {N(entry.Hit.StoredSize)}");
            }

            SkeletonEntry first = matches[0];
            using FileStream stream = new(
                first.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            SkeletonData data = SkeletonDecoder.Read(stream, first.ArchivePath, first.Hit);
            Console.WriteLine(
                $"  version {data.Head.Version}, bones {N(data.Bones.Count)}, tail {N(data.TailLength)}");
            int shown = Math.Min(12, data.Bones.Count);
            for (int index = 0; index < shown; index++)
            {
                SkeletonBone bone = data.Bones[index];
                var position = bone.Position;
                Console.WriteLine(
                    $"  bone {index,3}: 0x{bone.NameHash:X16} parent {bone.ParentIndex,4} first child {bone.FirstChildIndex,4} at ({position.X:F3}, {position.Y:F3}, {position.Z:F3})");
            }

            return 0;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int SkeletonBindings(string gameRoot)
    {
        if (!GameProfile.TryDetect(gameRoot, out GameProfile? profile, out string? error) || profile is null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        try
        {
            Stopwatch watch = Stopwatch.StartNew();
            AssetCatalog assets = AssetCatalog.Load(profile);
            SkinSkeletonIndex skins = SkinSkeletonIndex.Load(assets.Entries);
            watch.Stop();
            Console.WriteLine(
                $"skin skeletons: {N(skins.Entries.Count)} bodies, {N(skins.BoundCount)} bound, {N(skins.NoSkeleton)} none, {N(skins.BySkeleton.Count)} skeletons used, {skins.Failures.Count} failed, {N(watch.ElapsedMilliseconds)} ms");
            Console.WriteLine(
                "  kinds: "
                + string.Join(", ", skins.Entries
                    .GroupBy(use => use.Kind)
                    .OrderByDescending(group => group.Count())
                    .Select(group => $"{group.Key}->{N(group.Count())}")));
            foreach (KeyValuePair<ulong, IReadOnlyList<SkinSkeletonUse>> pair in skins.BySkeleton
                .OrderByDescending(pair => pair.Value.Count)
                .ThenBy(pair => pair.Key)
                .Take(5))
            {
                Console.WriteLine($"  skeleton 0x{pair.Key:X16} skins {N(pair.Value.Count)}");
            }

            foreach (CatalogFailure failure in skins.Failures.Take(10))
            {
                Console.Error.WriteLine(failure.Message);
            }

            return skins.Failures.Count == 0 ? 0 : 2;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int Links(string gameRoot)
    {
        if (!GameProfile.TryDetect(gameRoot, out GameProfile? profile, out string? error) || profile is null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        try
        {
            Stopwatch watch = Stopwatch.StartNew();
            AssetCatalog assets = AssetCatalog.Load(profile);
            SkinLinkIndex skins = SkinLinkIndex.Load(assets.Entries);
            long skinMs = watch.ElapsedMilliseconds;
            watch.Restart();
            MaterialCatalog materials = MaterialCatalog.Load(profile);
            MaterialTextureIndex textures = MaterialTextureIndex.Load(materials);
            long materialMs = watch.ElapsedMilliseconds;

            int sharedMeshes = skins.ByMesh.Count(pair => pair.Value.Count > 1);
            int maxSkins = skins.ByMesh.Count == 0 ? 0 : skins.ByMesh.Max(pair => pair.Value.Count);
            int materialEdges = textures.ByTexture.Values.Sum(list => list.Count);
            int maxMaterials = textures.ByTexture.Count == 0
                ? 0
                : textures.ByTexture.Max(pair => pair.Value.Count);
            Console.WriteLine(
                $"skins: {N(skins.Entries.Count)} bodies, {N(skins.ByMesh.Count)} meshes, {N(skins.Entries.Sum(entry => entry.Links.MeshLinks.Distinct().Count()))} links, {N(sharedMeshes)} shared, max {N(maxSkins)} skins/mesh, {skins.Failures.Count} failed, {N(skinMs)} ms");
            Console.WriteLine(
                $"materials: {N(textures.Materials)} bodies, {N(textures.Slots)} slots, {N(textures.ByTexture.Count)} textures, {N(materialEdges)} edges, max {N(maxMaterials)} materials/texture, {textures.Failures.Count} failed, {N(materialMs)} ms");
            foreach (KeyValuePair<ulong, IReadOnlyList<MaterialTextureUse>> pair in textures.ByTexture
                .OrderByDescending(pair => pair.Value.Count)
                .ThenBy(pair => pair.Key)
                .Take(5))
            {
                Console.WriteLine($"  texture 0x{pair.Key:X16} materials {N(pair.Value.Count)}");
            }

            foreach (CatalogFailure failure in skins.Failures.Concat(textures.Failures))
            {
                Console.Error.WriteLine($"{Path.GetFileName(failure.Path)}: {failure.Message}");
            }

            return skins.Failures.Count == 0 && textures.Failures.Count == 0 ? 0 : 2;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int Audio(string gameRoot)
    {
        if (!GameProfile.TryDetect(gameRoot, out GameProfile? profile, out string? error) || profile is null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        try
        {
            Stopwatch watch = Stopwatch.StartNew();
            AudioCatalog catalog = AudioCatalog.Load(profile);
            watch.Stop();
            Dictionary<string, int> channels = new();
            Dictionary<string, int> codecs = new();
            Dictionary<string, int> languages = new();
            int named = 0;
            double seconds = 0;
            foreach (AudioEntry entry in catalog.Entries)
            {
                string key = $"{entry.Info.Channels}ch {entry.Info.SampleRate} Hz";
                channels[key] = channels.GetValueOrDefault(key) + 1;
                codecs[entry.Info.Codec] = codecs.GetValueOrDefault(entry.Info.Codec) + 1;
                languages[entry.Language] = languages.GetValueOrDefault(entry.Language) + 1;
                named += entry.Name is null ? 0 : 1;
                seconds += entry.DurationSeconds;
            }

            Console.WriteLine(
                $"audio: {N(catalog.Entries.Count)} media, {N(catalog.Archives.Count)} packs with media, {N(catalog.Packs)} packs, {N(catalog.Previews)} previews skipped, {N(catalog.Plugins)} plugin skipped, {catalog.Failures.Count} failed, {N(watch.ElapsedMilliseconds)} ms");
            Console.WriteLine($"  duration {TimeSpan.FromSeconds(seconds):c}, named {N(named)}");
            Console.WriteLine($"  codecs {FormatCounts(codecs)}");
            Console.WriteLine($"  channels {FormatCounts(channels)}");
            Console.WriteLine($"  languages {FormatCounts(languages)}");
            foreach (AudioArchive archive in catalog.Archives)
            {
                Console.WriteLine($"  {Path.GetFileName(archive.Path),-30} {N(archive.Media)}");
            }

            foreach (CatalogFailure failure in catalog.Failures)
            {
                Console.Error.WriteLine($"{Path.GetFileName(failure.Path)}: {failure.Message}");
            }

            return catalog.Failures.Count == 0 ? 0 : 2;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int AudioConvert(string gameRoot, string packText, string idText, string outputPath)
    {
        if (!TryHash(idText, out ulong id) || id > uint.MaxValue)
        {
            Console.Error.WriteLine($"'{idText}' is not a 32-bit media id.");
            return 1;
        }

        if (!GameProfile.TryDetect(gameRoot, out GameProfile? profile, out string? error) || profile is null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        string full = Path.GetFullPath(outputPath);
        if (GameFolderGuard.IsInside(profile.Root, Path.GetDirectoryName(full)!))
        {
            Console.Error.WriteLine("refusing to write inside the game folder.");
            return 1;
        }

        try
        {
            AudioCatalog catalog = AudioCatalog.Load(profile);
            string pack = NormalizePack(packText);
            List<AudioEntry> matches = catalog.Entries
                .Where(entry => entry.Id == id
                    && string.Equals(NormalizePack(entry.PackName), pack, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(entry => entry.Size)
                .ToList();
            if (matches.Count == 0)
            {
                Console.Error.WriteLine($"no audio 0x{id:X8} in pack {packText}.");
                return 1;
            }

            AudioEntry media = matches[0];
            byte[] wem = new byte[media.Size];
            using (FileStream stream = new(
                media.PackPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                stream.Seek(media.Offset, SeekOrigin.Begin);
                stream.ReadExactly(wem);
            }

            byte[] ogg = WemConverter.ToOgg(wem);
            string? directory = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temp, ogg);
                File.Move(temp, full, overwrite: true);
            }
            finally
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }

            Console.WriteLine(
                $"converted {media.PackName} 0x{media.Id:X8} -> {full} ({N(ogg.Length)} bytes, {media.DurationSeconds:F2}s, {media.Info.Channels}ch {media.Info.SampleRate} Hz, {media.Info.Codec})");
            if (matches.Count > 1)
            {
                Console.WriteLine($"  {N(matches.Count)} copies in the pack, wrote the largest.");
            }

            return 0;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string NormalizePack(string text)
    {
        string name = Path.GetFileName(text.Trim());
        foreach (string suffix in new[] { ".PC.PCK", ".PCK", ".PC" })
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return name[..^suffix.Length];
            }
        }

        return name;
    }

    private static string FormatCounts(Dictionary<string, int> counts) =>
        string.Join(", ", counts
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={N(pair.Value)}"));

    private static bool TryHash(string text, out ulong value)
    {
        string digits = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? text[2..]
            : text;
        return ulong.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    private static int Mesh(string gameRoot, string hashText)
    {
        if (!TryHash(hashText, out ulong nameHash))
        {
            Console.Error.WriteLine($"'{hashText}' is not a 64-bit hex hash.");
            return 1;
        }

        if (!GameProfile.TryDetect(gameRoot, out GameProfile? profile, out string? error) || profile is null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        AssetCatalog catalog;
        try
        {
            catalog = AssetCatalog.Load(profile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        List<AssetEntry> matches = catalog.Entries
            .Where(entry => entry.ClassHash == AssetClass.Mesh && entry.NameHash == nameHash)
            .ToList();
        if (matches.Count == 0)
        {
            Console.Error.WriteLine($"no Mesh_Z row named 0x{nameHash:X16}.");
            return 1;
        }

        int failed = 0;
        foreach (AssetEntry entry in matches)
        {
            try
            {
                using FileStream stream = new(
                    entry.BodyArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                MeshData mesh = MeshDecoder.Read(stream, entry.BodyArchivePath, entry.Row);
                Console.WriteLine(
                    $"{Path.GetFileName(entry.ArchivePath)} 0x{entry.NameHash:X16} offset {entry.Row.Offset}{(entry.IsExternal ? " body COMMON.DPC" : "")}");
                int groupCount = 0;
                foreach (MeshIndexSet item in mesh.IndexSets)
                {
                    groupCount += item.Groups.Count;
                }

                Console.WriteLine(
                    $"  version {mesh.Head.Version}, {mesh.Head.Storage}, body {N(mesh.Head.BodySize)}, streams {mesh.Streams.Count}, index sets {mesh.IndexSets.Count}, groups {groupCount}");
                for (int index = 0; index < mesh.Streams.Count; index++)
                {
                    MeshStream item = mesh.Streams[index];
                    Console.WriteLine(
                        $"  stream {index}: {N(item.VertexCount)} vertices, stride {item.Stride}, flags 0x{item.Flags:X4}");
                }

                for (int index = 0; index < mesh.IndexSets.Count; index++)
                {
                    MeshIndexSet item = mesh.IndexSets[index];
                    Console.WriteLine(
                        $"  indices {index}: {N(item.IndexCount)} ({N(item.IndexCount / 3)} triangles), u{item.IndexSize * 8}, flags 0x{item.Flags:X4}");
                }

                for (int setIndex = 0; setIndex < mesh.IndexSets.Count; setIndex++)
                {
                    for (int groupIndex = 0; groupIndex < mesh.IndexSets[setIndex].Groups.Count; groupIndex++)
                    {
                        MeshGroup item = mesh.IndexSets[setIndex].Groups[groupIndex];
                        Console.WriteLine(
                            $"  group {setIndex}.{groupIndex}: {N(item.VertexCount)} vertices, {N(item.FaceCount)} triangles, offsets {N(item.VertexOffset)}/{N(item.IndexOffset)}");
                    }
                }

                if (mesh.Streams.Count == 0)
                {
                    Console.WriteLine("  empty mesh (no geometry)");
                    continue;
                }

                MeshGeometry geometry = mesh.ToGeometry();
                Console.WriteLine(
                    $"  bounds min ({F(geometry.Min.X)}, {F(geometry.Min.Y)}, {F(geometry.Min.Z)}) max ({F(geometry.Max.X)}, {F(geometry.Max.Y)}, {F(geometry.Max.Z)})");
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException
                or UnauthorizedAccessException)
            {
                failed++;
                Console.Error.WriteLine($"{Path.GetFileName(entry.ArchivePath)} 0x{entry.NameHash:X16}: {ex.Message}");
            }
        }

        return failed == 0 ? 0 : 2;
    }

    private static string F(float value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private static int Scan(string gameRoot)
    {
        if (!TryFindDpcs(gameRoot, out string[] files))
        {
            return 1;
        }

        Stopwatch watch = Stopwatch.StartNew();
        List<(string Path, DpcArchive Archive)> parsed = new();
        List<string> failures = new();
        foreach (string file in files)
        {
            try
            {
                DpcArchive archive = DpcArchive.Open(file);
                parsed.Add((file, archive));
                Console.WriteLine(
                    $"{Path.GetFileName(file),-28} {archive.Header.Kind,-7} {N(archive.Entries.Count),7} + {N(archive.SecondaryEntries.Count),6} rows  {archive.BodyLocation,-8} {N(archive.Length),14} B");
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                failures.Add(ex.Message);
                Console.WriteLine($"{Path.GetFileName(file),-28} ERROR");
            }
        }

        DpcArchive? common = parsed
            .Where(item => string.Equals(Path.GetFileName(item.Path), "COMMON.DPC", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Archive)
            .FirstOrDefault();

        if (common is not null)
        {
            int external = 0;
            int outside = 0;
            foreach ((string path, DpcArchive archive) in parsed)
            {
                if (archive.BodyLocation != BodyLocation.External)
                {
                    continue;
                }

                foreach (DpcEntry entry in archive.Entries.Concat(archive.SecondaryEntries))
                {
                    external++;
                    if (entry.BodyOffset + entry.CompressedSize > common.Length)
                    {
                        outside++;
                        failures.Add($"{Path.GetFileName(path)}: row name 0x{entry.NameHash:X16} is outside COMMON.");
                    }
                }
            }

            Console.WriteLine(
                $"external extents checked against COMMON: {N(external)}, outside: {N(outside)}");
        }
        else if (parsed.Exists(item => item.Archive.BodyLocation == BodyLocation.External))
        {
            failures.Add("COMMON.DPC not found; external extents were not checked.");
        }

        watch.Stop();
        Console.WriteLine(
            $"files: {N(files.Length)}, parsed: {N(parsed.Count)}, failed: {N(failures.Count)}, {N(watch.ElapsedMilliseconds)} ms");
        foreach (string failure in failures)
        {
            Console.Error.WriteLine(failure);
        }

        return failures.Count == 0 ? 0 : 2;
    }

    private static int List(string gameRoot, bool rebuildCache)
    {
        if (!GameProfile.TryDetect(gameRoot, out GameProfile? profile, out string? error) || profile is null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        Stopwatch watch = Stopwatch.StartNew();
        TextureCatalog catalog;
        try
        {
            catalog = TextureCatalog.Load(profile, rebuildCache);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        Dictionary<string, int> byFormat = new(StringComparer.Ordinal);
        int volumes = 0;
        int unknownCodec = 0;
        foreach (CatalogArchive archive in catalog.Archives)
        {
            string name = Path.GetFileName(archive.Path);
            if (archive.IsExternal)
            {
                if (archive.ExternalBitmapReferences > 0)
                {
                    Console.WriteLine(
                        $"{name,-28} {N(archive.ExternalBitmapReferences),7} bitmap refs in another archive (not listed)");
                }

                continue;
            }

            foreach (TextureEntry texture in archive.Textures)
            {
                BitmapHead head = texture.Head;
                string format = head.Format.DisplayName;
                byFormat[format] = byFormat.GetValueOrDefault(format) + 1;
                if (head.Kind == BitmapKind.Volume)
                {
                    volumes++;
                }

                if (head.Format.Codec == TextureCodec.Unknown)
                {
                    unknownCodec++;
                }

                string dimensions = head.Kind == BitmapKind.Volume
                    ? $"volume w={head.Width}"
                    : $"{head.Width}x{head.Height}";
                Console.WriteLine(
                    $"{name,-28} 0x{head.NameHash:X16} {format,-16} {dimensions,-14} {head.MipCount,2} mips {head.Surfaces} surfaces {(head.Storage == BitmapStorage.Raw ? "raw" : "lz4")}");
            }
        }

        watch.Stop();
        Console.WriteLine();
        foreach (CatalogArchive archive in catalog.Archives
            .Where(item => !item.IsExternal && item.TextureCount > 0)
            .OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                $"{Path.GetFileName(archive.Path),-28} {N(archive.TextureCount),7} textures {(archive.FromCache ? "cached" : "scanned")}");
        }

        Console.WriteLine("formats:");
        foreach ((string format, int count) in byFormat.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"  {format,-18} {N(count),7}");
        }

        int cachedArchives = catalog.Archives.Count(
            item => !item.IsExternal && item.TextureCount > 0 && item.FromCache);
        int scannedArchives = catalog.Archives.Count(
            item => !item.IsExternal && item.TextureCount > 0 && !item.FromCache);
        int externalArchives = catalog.Archives.Count(item => item.IsExternal);
        int externalReferences = catalog.Archives.Sum(item => item.ExternalBitmapReferences);
        Console.WriteLine(
            $"textures: {N(catalog.Textures.Count)}, previewable: {N(catalog.PreviewableCount)}, volumes: {N(volumes)}, unknown codec: {N(unknownCodec)}, external archives: {N(externalArchives)}, external bitmap refs: {N(externalReferences)}, cached: {N(cachedArchives)}, scanned: {N(scannedArchives)}, files: {N(catalog.FileCount)}, failed: {N(catalog.Failures.Count)}, {N(watch.ElapsedMilliseconds)} ms");
        foreach (CatalogFailure failure in catalog.Failures)
        {
            Console.Error.WriteLine(failure.Message);
        }

        return catalog.Failures.Count == 0 ? 0 : 2;
    }

    private static int Export(string gameRoot, string hashText, string outputPath)
    {
        string text = hashText.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? hashText[2..]
            : hashText;
        if (!ulong.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong name))
        {
            Console.Error.WriteLine($"invalid texture hash '{hashText}'");
            return 1;
        }

        if (!TryFindDpcs(gameRoot, out string[] files))
        {
            return 1;
        }

        string fullOutput = Path.GetFullPath(outputPath);
        if (GameFolderGuard.IsInside(gameRoot, Path.GetDirectoryName(fullOutput)!))
        {
            Console.Error.WriteLine("refusing to write inside the game folder.");
            return 1;
        }

        List<TextureEntry> matches = new();
        int failedArchives = 0;
        string? externalMatch = null;
        foreach (string file in files)
        {
            string archiveName = Path.GetFileName(file);
            try
            {
                DpcArchive archive = DpcArchive.Open(file);
                if (archive.BodyLocation != BodyLocation.Local)
                {
                    if (archive.Entries.Concat(archive.SecondaryEntries).Any(entry =>
                        entry.ClassHash == BitmapHead.ClassHash && entry.NameHash == name))
                    {
                        externalMatch ??= archiveName;
                    }

                    continue;
                }

                matches.AddRange(TextureCache.LoadOrScan(archive, false, out _)
                    .Where(texture => texture.Head.NameHash == name));
            }
            catch (InvalidDataException ex)
            {
                failedArchives++;
                Console.Error.WriteLine(ex.Message);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failedArchives++;
                Console.Error.WriteLine($"{archiveName}: {ex.Message}");
            }
        }

        if (matches.Count == 0)
        {
            if (externalMatch is not null)
            {
                Console.Error.WriteLine(
                    $"{externalMatch}: texture 0x{name:X16} resolves into another archive; external bodies are not decoded.");
                return 2;
            }

            Console.Error.WriteLine($"texture 0x{name:X16} not found");
            return failedArchives == 0 ? 1 : 2;
        }

        List<TextureEntry> decodable = matches
            .Where(TextureDecoder.CanDecodeMip0)
            .ToList();
        if (decodable.Count == 0)
        {
            try
            {
                TextureEntry first = matches[0];
                using FileStream stream = new(
                    first.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                TextureDecoder.DecodeMip0(stream, first);
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException
                or IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine(ex.Message);
            }

            return 2;
        }

        TextureEntry chosen = decodable[0];
        if (matches.Count > 1)
        {
            Console.WriteLine(
                $"note: {matches.Count} rows share 0x{name:X16}; exporting {Path.GetFileName(chosen.ArchivePath)} {chosen.Head.Format.DisplayName} {chosen.Head.Width}x{chosen.Head.Height}");
        }

        try
        {
            DecodedTexture image = TextureExport.ExportPng(chosen, outputPath);
            long bytes = new FileInfo(Path.GetFullPath(outputPath)).Length;
            Console.WriteLine(
                $"{Path.GetFileName(chosen.ArchivePath)} 0x{name:X16} {image.Width}x{image.Height} {chosen.Head.Format.DisplayName} {N(bytes)} B -> {outputPath}");
            return 0;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException or NotSupportedException)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
    }

    private static string N(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static bool TryFindDpcs(string gameRoot, out string[] files)
    {
        files = Array.Empty<string>();
        if (!GameProfile.TryDetect(gameRoot, out GameProfile? profile, out string? error) || profile is null)
        {
            Console.Error.WriteLine(error);
            return false;
        }

        try
        {
            files = profile.DataRoots
                .SelectMany(root => Directory.EnumerateFiles(
                    Path.Combine(profile.Root, root),
                    "*.DPC",
                    SearchOption.AllDirectories))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return false;
        }
    }
}
