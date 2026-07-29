using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Genera un Material (URP Lit) por cada textura encontrada, en vez de buscar
/// materiales ya existentes por nombre.
///
/// Por cada textura de color:
///   - crea (o actualiza) Assets/.../<MaterialsFolder>/<nombre>.mat
///   - asigna la textura al Base Map
///   - duplica la textura a "<nombre>_Normal.png" y la reimporta como Normal Map
///     generado desde altura (fake depth), igual que el fixer original
///   - deja Metallic/Smoothness de madera pintada
///   - si la textura tiene alpha, activa Alpha Clipping (los recortes con fondo
///     transparente se ven bien sin pasar a Transparent)
///
/// Si el material ya existia se modifica en sitio: no se recrea el asset, asi que
/// las referencias en escenas y prefabs se mantienen.
/// </summary>
public static class MaterialFixer
{
    // ---------- Rutas ----------
    // Carpeta con los PNG recortados (se recorre incluyendo subcarpetas).
    private const string SourceTexturesFolder = "Assets/Resources/MaterialsMap";
    // Carpeta donde se escriben los .mat.
    private const string MaterialsFolder = "Assets/Resources/MaterialsMap/ShortTextures";
    // Prefijo opcional para el nombre del material ("" = mismo nombre que la textura).
    private const string MaterialPrefix = "";
    // Sufijo de los normal maps generados. Estos archivos se ignoran al recorrer.
    private const string NormalSuffix = "_Normal";

    // ---------- Look ----------
    private const float NormalHeightScale = 0.35f;
    private const float BumpScale = 0.3f;
    private const float Smoothness = 0.25f;
    private const float Metallic = 0f;
    private const float AlphaCutoff = 0.5f;

    // ---------- Opciones ----------
    // Replica la estructura de subcarpetas de las texturas dentro de MaterialsFolder
    // (tall_bookshelf/, short_bookshelf/, ...).
    private const bool MirrorSubfolders = false;
    // Fuerza Point filter + sin compresion en las texturas de color (pixel art).
    private const bool ApplyPixelArtImportSettings = true;
    // Activa Alpha Clipping cuando la textura tiene canal alpha.
    private const bool EnableAlphaClipWhenTextureHasAlpha = true;
    // Si el material ya existia y ya tenia Base Map, lo reemplaza igual.
    private const bool OverwriteExistingBaseMap = true;

    private const string UrpLitShader = "Universal Render Pipeline/Lit";

    [MenuItem("Tools/Book Stealer/Generate Materials From Textures Folder")]
    public static void GenerateFromFolder()
    {
        if (!AssetDatabase.IsValidFolder(SourceTexturesFolder))
        {
            Debug.LogError($"LibraryMaterialGenerator: no existe la carpeta {SourceTexturesFolder}. " +
                           "Ajusta SourceTexturesFolder.");
            return;
        }

        Generate(new[] { SourceTexturesFolder });
    }

    [MenuItem("Tools/Book Stealer/Generate Materials From Selection")]
    public static void GenerateFromSelection()
    {
        var roots = new List<string>();
        foreach (string guid in Selection.assetGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path)) roots.Add(path);
        }

        if (roots.Count == 0)
        {
            Debug.LogWarning("LibraryMaterialGenerator: selecciona texturas o carpetas en el Project.");
            return;
        }

        Generate(roots.ToArray());
    }

    private static void Generate(string[] roots)
    {
        var shader = Shader.Find(UrpLitShader);
        if (shader == null)
        {
            Debug.LogError($"LibraryMaterialGenerator: no se encontro el shader \"{UrpLitShader}\". " +
                           "Este script asume URP; si el proyecto usa Built-in, hay que cambiar " +
                           "el shader y los nombres de propiedades (_MainTex en vez de _BaseMap).");
            return;
        }

        List<string> texturePaths = CollectTexturePaths(roots);
        if (texturePaths.Count == 0)
        {
            Debug.LogWarning("LibraryMaterialGenerator: no se encontraron texturas de color.");
            return;
        }

        int created = 0, updated = 0, skipped = 0;
        try
        {
            for (int i = 0; i < texturePaths.Count; i++)
            {
                string colorPath = texturePaths[i];
                if (EditorUtility.DisplayCancelableProgressBar(
                        "Generando materiales",
                        Path.GetFileName(colorPath),
                        (float)i / texturePaths.Count))
                {
                    Debug.LogWarning("LibraryMaterialGenerator: cancelado por el usuario.");
                    break;
                }

                switch (BuildMaterial(colorPath, shader))
                {
                    case Result.Created: created++; break;
                    case Result.Updated: updated++; break;
                    default: skipped++; break;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"LibraryMaterialGenerator: {created} materiales creados, {updated} actualizados, " +
                  $"{skipped} omitidos (de {texturePaths.Count} texturas).");
    }

    private enum Result { Created, Updated, Skipped }

    private static List<string> CollectTexturePaths(string[] roots)
    {
        var found = new List<string>();
        var seen = new HashSet<string>();

        foreach (string root in roots)
        {
            if (AssetDatabase.IsValidFolder(root))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { root }))
                    TryAdd(AssetDatabase.GUIDToAssetPath(guid), found, seen);
            }
            else
            {
                TryAdd(root, found, seen);
            }
        }

        found.Sort();
        return found;
    }

    private static void TryAdd(string path, List<string> found, HashSet<string> seen)
    {
        if (string.IsNullOrEmpty(path) || seen.Contains(path)) return;
        // Los normal maps generados no llevan material propio (evita X_Normal_Normal.png).
        if (Path.GetFileNameWithoutExtension(path).EndsWith(NormalSuffix)) return;
        if (AssetImporter.GetAtPath(path) as TextureImporter == null) return;
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null) return;

        seen.Add(path);
        found.Add(path);
    }

    private static Result BuildMaterial(string colorPath, Shader shader)
    {
        var colorTex = AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath);
        var colorImporter = AssetImporter.GetAtPath(colorPath) as TextureImporter;
        if (colorTex == null || colorImporter == null)
        {
            Debug.LogWarning($"LibraryMaterialGenerator: no se pudo leer {colorPath}, se omite.");
            return Result.Skipped;
        }

        bool hasAlpha = colorImporter.DoesSourceTextureHaveAlpha();

        if (ApplyPixelArtImportSettings)
            ApplyPixelArtSettings(colorImporter, hasAlpha);

        // ---- Material: crear o reutilizar ----
        string matPath = GetMaterialPath(colorPath);
        EnsureFolder(Path.GetDirectoryName(matPath).Replace("\\", "/"));

        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        bool isNew = mat == null;
        if (isNew)
        {
            mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(matPath) };
            AssetDatabase.CreateAsset(mat, matPath);
        }

        if (!mat.HasProperty("_BaseMap"))
        {
            Debug.LogWarning($"LibraryMaterialGenerator: {matPath} usa el shader \"{mat.shader.name}\", " +
                             "que no tiene _BaseMap. Se omite para no dejarlo a medias.");
            return Result.Skipped;
        }

        // Caso heredado: la textura estaba en el slot de detalle en vez de Base Map.
        MigrateDetailSlot(mat);

        if (isNew || OverwriteExistingBaseMap || mat.GetTexture("_BaseMap") == null)
            mat.SetTexture("_BaseMap", colorTex);

        // ---- Normal map generado desde la propia textura (fake depth) ----
        var normalTex = CreateOrUpdateNormalMap(colorPath);
        if (normalTex != null && mat.HasProperty("_BumpMap"))
        {
            mat.SetTexture("_BumpMap", normalTex);
            mat.SetFloat("_BumpScale", BumpScale);
            mat.EnableKeyword("_NORMALMAP");
        }

        // ---- Madera/libros pintados: no metalico, semi-mate ----
        if (mat.HasProperty("_WorkflowMode")) mat.SetFloat("_WorkflowMode", 1f); // Metallic
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", Metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", Smoothness);

        SetAlphaClip(mat, EnableAlphaClipWhenTextureHasAlpha && hasAlpha);

        EditorUtility.SetDirty(mat);
        Debug.Log($"LibraryMaterialGenerator: {(isNew ? "creado" : "actualizado")} {matPath} " +
                  $"-> Base Map = {colorPath}");

        return isNew ? Result.Created : Result.Updated;
    }

    private static string GetMaterialPath(string colorPath)
    {
        string subfolder = "";
        if (MirrorSubfolders && colorPath.StartsWith(SourceTexturesFolder + "/"))
        {
            string relative = colorPath.Substring(SourceTexturesFolder.Length + 1);
            subfolder = Path.GetDirectoryName(relative).Replace("\\", "/");
        }

        string folder = string.IsNullOrEmpty(subfolder)
            ? MaterialsFolder
            : $"{MaterialsFolder}/{subfolder}";

        return $"{folder}/{MaterialPrefix}{Path.GetFileNameWithoutExtension(colorPath)}.mat";
    }

    private static Texture2D CreateOrUpdateNormalMap(string colorPath)
    {
        string normalPath = Path.Combine(
            Path.GetDirectoryName(colorPath),
            Path.GetFileNameWithoutExtension(colorPath) + NormalSuffix + Path.GetExtension(colorPath))
            .Replace("\\", "/");

        if (AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath) == null &&
            !AssetDatabase.CopyAsset(colorPath, normalPath))
        {
            Debug.LogWarning($"LibraryMaterialGenerator: no se pudo copiar {colorPath} a {normalPath} " +
                             "(carpeta de solo lectura?). Se deja el material sin normal map.");
            return null;
        }

        var importer = AssetImporter.GetAtPath(normalPath) as TextureImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(normalPath) as TextureImporter;
        }
        if (importer == null)
        {
            Debug.LogWarning($"LibraryMaterialGenerator: {normalPath} no tiene TextureImporter, se omite.");
            return null;
        }

        bool dirty = false;
        if (importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            dirty = true;
        }
        if (!importer.convertToNormalmap) { importer.convertToNormalmap = true; dirty = true; }
        if (!Mathf.Approximately(importer.heightmapScale, NormalHeightScale))
        {
            importer.heightmapScale = NormalHeightScale;
            dirty = true;
        }
        if (importer.sRGBTexture) { importer.sRGBTexture = false; dirty = true; }
        if (ApplyPixelArtImportSettings &&
            importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            dirty = true;
        }
        if (dirty) importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
    }

    private static void ApplyPixelArtSettings(TextureImporter importer, bool hasAlpha)
    {
        bool dirty = false;
        if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; dirty = true; }
        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            dirty = true;
        }
        if (hasAlpha && !importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true; // evita halos oscuros en los bordes recortados
            dirty = true;
        }
        if (dirty) importer.SaveAndReimport();
    }

    private static void MigrateDetailSlot(Material mat)
    {
        if (!mat.HasProperty("_DetailAlbedoMap")) return;
        if (mat.GetTexture("_DetailAlbedoMap") == null) return;

        mat.SetTextureScale("_BaseMap", mat.GetTextureScale("_DetailAlbedoMap"));
        mat.SetTextureOffset("_BaseMap", mat.GetTextureOffset("_DetailAlbedoMap"));
        mat.SetTexture("_DetailAlbedoMap", null);
        mat.DisableKeyword("_DETAIL_MULX2");
        mat.DisableKeyword("_DETAIL_SCALED");
    }

    private static void SetAlphaClip(Material mat, bool enable)
    {
        if (enable)
        {
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
            if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", AlphaCutoff);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)RenderQueue.AlphaTest;
        }
        else
        {
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 0f);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.renderQueue = -1; // vuelve al queue del shader
        }
    }

    private static void EnsureFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;

        string parent = Path.GetDirectoryName(folder).Replace("\\", "/");
        string leaf = Path.GetFileName(folder);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}