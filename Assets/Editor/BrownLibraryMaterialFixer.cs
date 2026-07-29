using System.IO;
using UnityEditor;
using UnityEngine;

public static class BrownLibraryMaterialFixer
{
    private const float NormalHeightScale = 0.35f;
    private const float BumpScale = 1.2f;
    private const float Smoothness = 0.25f;

    private static readonly string[] MaterialNames =
    {
        "FrontBrown01",
        "SideBrown01",
        "SideBrown02",
        "LittleBrown01",
    };

    [MenuItem("Tools/Book Stealer/Fix BrownLibrary Materials (Fake Depth)")]
    public static void FixMaterials()
    {
        foreach (var name in MaterialNames)
        {
            string matPath = $"Assets/Resources/Materials/BrownLibrary/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Debug.LogWarning($"BrownLibraryMaterialFixer: no se encontro {matPath}");
                continue;
            }

            FixMaterial(mat);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("BrownLibraryMaterialFixer: materiales ajustados (Base Map real + Normal Map generado + Metallic/Smoothness corregidos).");
    }

    private static void FixMaterial(Material mat)
    {
        Texture colorTex = mat.GetTexture("_DetailAlbedoMap");
        if (colorTex == null)
        {
            Debug.LogWarning($"BrownLibraryMaterialFixer: {mat.name} no tiene textura en Detail Albedo Map, se omite.");
            return;
        }

        string colorPath = AssetDatabase.GetAssetPath(colorTex);
        string normalPath = Path.Combine(
            Path.GetDirectoryName(colorPath),
            Path.GetFileNameWithoutExtension(colorPath) + "_Normal.png").Replace("\\", "/");

        if (AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath) == null)
        {
            AssetDatabase.CopyAsset(colorPath, normalPath);
        }

        AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(normalPath);
        importer.textureType = TextureImporterType.NormalMap;
        importer.convertToNormalmap = true;
        importer.heightmapScale = NormalHeightScale;
        importer.sRGBTexture = false;
        importer.SaveAndReimport();

        var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

        // La textura de color estaba en el slot de detalle en vez de Base Map: la movemos.
        mat.SetTexture("_BaseMap", colorTex);
        mat.SetTextureScale("_BaseMap", mat.GetTextureScale("_DetailAlbedoMap"));
        mat.SetTextureOffset("_BaseMap", mat.GetTextureOffset("_DetailAlbedoMap"));

        mat.SetTexture("_DetailAlbedoMap", null);
        mat.DisableKeyword("_DETAIL_MULX2");
        mat.DisableKeyword("_DETAIL_SCALED");

        mat.SetTexture("_BumpMap", normalTex);
        mat.SetFloat("_BumpScale", BumpScale);
        mat.EnableKeyword("_NORMALMAP");

        // Madera/libros pintados: no metalico, semi-mate.
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", Smoothness);

        EditorUtility.SetDirty(mat);
        Debug.Log($"BrownLibraryMaterialFixer: {mat.name} -> Base Map = {colorPath}, Normal Map = {normalPath}");
    }
}
