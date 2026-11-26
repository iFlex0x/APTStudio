namespace AptStudio.Core.Materials;

public sealed record MaterialSlotRole(string Authoring, string Binding);

// Slot names follow the game's material slot table; parsing uses the index only.
public static class MaterialSlotRoles
{
    public const int Count = 58;

    private static readonly MaterialSlotRole[] Roles =
    [
        new("Texture", "sDiffuse"),
        new("TextureEnv", "sEnvmap"),
        new("Occlusion", "sBlendMask"),
        new("Normal", "sNormal"),
        new("ShininessLvl", "sSpecular"),
        new("Dirt", "sDirt"),
        new("TextureEnvCube", "sEnv"),
        new("TextureOpacity", "sOpacity"),
        new("UVDestTex", "sUVDistorsion"),
        new("AddTexture", "sAddDiffuse"),
        new("AddNormal", "sAddNormal"),
        new("GlossinessStr", "sGlossiness"),
        new("ZBufferTex", "sZBufferMap"),
        new("NormalRoughMetalTex", "sNormalRoughMetal"),
        new("AddNormalRoughMetalTex", "sAddNormalRoughMetal"),
        new("EmissiveTex", "sEmissive"),
        new("DustTex", "sDust"),
        new("DetailDiffuseTex", "sDetailDiffuse"),
        new("DetailNormalTex", "sDetailNormal"),
        new("RadiosityTex", "sRadiosity"),
        new("DetailTex", "sDetail"),
        new("AddDetailTex", "sAddDetail"),
        new("WetnessAOTex", "sWetnessAO"),
        new("HeightmapTex", "sHeightmap"),
        new("MetalRoughAOTex", "sMetalRoughAO"),
        new("DetailMetalRoughAOTex", "sDetailMetalRoughAO"),
        new("AddMetalRoughAOTex", "sAddMetalRoughAO"),
        new("StretchWrinklesTex", "sStretchWrinkles"),
        new("CompressWrinklesTex", "sCompressWrinkles"),
        new("Extra0WrinklesTex", "sExtra0Wrinkles"),
        new("ParallaxHeightmapTex", "sParallaxHeightmap"),
        new("Custom0Tex", "sCustomMap0"),
        new("Custom1Tex", "sCustomMap1"),
        new("Custom2Tex", "sCustomMap2"),
        new("Custom3Tex", "sCustomMap3"),
        new("NormalRoughMetalLayer2Tex", "sNormalRoughMetalLayer2"),
        new("DiffuseLayer2Tex", "sDiffuseLayer2"),
        new("OcclusionTex", "sOcclusion"),
        new("ClearCoatColorRoughnessTex", "sClearCoatColorRoughness"),
        new("ClearCoatNormalTex", "sClearCoatNormal"),
        new("AnisoDirRoughTex", "sAnisoDirRough"),
        new("AlphaBlendMaskTex", "sAlphaBlendMask"),
        new("WiperMaskTex", "sWiperMask"),
        new("WindshieldDetailNormalTex", "sWindshieldDetailNormal"),
        new("ScratchesNormalTex", "sScratchesNormal"),
        new("NormalRoughMetalLayer3Tex", "sNormalRoughMetalLayer3"),
        new("DiffuseLayer3Tex", "sDiffuseLayer3"),
        new("Custom4Tex", "sCustomMap4"),
        new("Custom5Tex", "sCustomMap5"),
        new("IridescentThicknessTex", "sIridescentThickness"),
        new("sWindshieldInsectsTex", "sWindshieldInsects"),
        new("sWindshieldInsectsMaskTex", "sWindshieldInsectsMask"),
        new("DirtOverlayTex", "sDirtOverlay"),
        new("DirtMetalRoughAOTex", "sDirtMetalRoughAO"),
        new("TireDetailsTex", "sTireDetails"),
        new("TireMudNormalTex", "sTireMudNormal"),
        new("ParallaxHeightmap2Tex", "sParallaxHeightmap2"),
        new("CustomAlbedo0Tex", "sCustomAlbedo0"),
    ];

    public static MaterialSlotRole Role(int slot) =>
        slot >= 0 && slot < Roles.Length
            ? Roles[slot]
            : new MaterialSlotRole("Unknown", "unknown");
}
