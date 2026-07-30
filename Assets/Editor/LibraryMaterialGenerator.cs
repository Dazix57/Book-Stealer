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
///   - deja la superficie casi mate: workflow Specular con reflectancia muy baja,
///     smoothness baja, sin reflejos de entorno y sin emision, para que una luz
///     que pase por delante no encienda la estanteria/pared
///   - si la textura tiene alpha, activa Alpha Clipping (los recortes con fondo
///     transparente se ven bien sin pasar a Transparent)
///
/// Si el material ya existia se modifica en sitio: no se recrea el asset, asi que
/// las referencias en escenas y prefabs se mantienen.
///
/// Soporta tres sets de assets con sus propias rutas (estanterias, paredes y
/// puertas), cada uno con su propia entrada en Tools, ademas de una entrada
/// generica por Selection.
/// </summary>
public static class LibraryMaterialGenerator
{
    // ---------- Sets de assets ----------
    // Cada set define de donde salen las texturas y a donde van los materiales.
    // Ambas rutas se verificaron contra el proyecto: "Assets/Art/Textures/BrownLibrary"
    // (la ruta original del fixer viejo) no existe; las texturas reales de las
    // estanterias viven en bookshelf_assets.
    private readonly struct AssetSet
    {
        public readonly string SourceTexturesFolder;
        public readonly string MaterialsFolder;
        public readonly string Label;

        // Donde buscar los materiales YA EN USO para "Fix Existing" (por defecto,
        // la misma MaterialsFolder). Para estanterias es distinta: los .mat reales
        // viven junto a las texturas, dentro de bookshelf_assets, no en
        // Materials/BrownLibrary (que es donde "Generate" crea los nuevos).
        public readonly string ExistingMaterialsFolder;
        // Filtro opcional por nombre para "Fix Existing" ("" = todos los .mat de
        // la carpeta). Necesario para puertas: sus materiales reales viven sueltos
        // dentro de Assets/Materials, junto con materiales de otros sistemas
        // (Concreto, Madera, etc.) que no hay que tocar.
        public readonly string ExistingMaterialsNameFilter;
        // Las puertas no tienen variantes "_variant_a/b/c" como estanterias/paredes:
        // toda su textura repite mucho a lo largo del mapa, asi que siempre usan el
        // BumpScale suave (el mismo que las variantes), no el default de 1.2.
        public readonly bool ForceVariantBumpScale;

        public AssetSet(string sourceTexturesFolder, string materialsFolder, string label,
            string existingMaterialsFolder = null, string existingMaterialsNameFilter = "",
            bool forceVariantBumpScale = false)
        {
            SourceTexturesFolder = sourceTexturesFolder;
            MaterialsFolder = materialsFolder;
            Label = label;
            ExistingMaterialsFolder = existingMaterialsFolder ?? materialsFolder;
            ExistingMaterialsNameFilter = existingMaterialsNameFilter;
            ForceVariantBumpScale = forceVariantBumpScale;
        }
    }

    private static readonly AssetSet ShelfSet = new AssetSet(
        sourceTexturesFolder: "Assets/Resources/bookshelf_assets",
        materialsFolder: "Assets/Resources/Materials/BrownLibrary",
        label: "estanterias",
        existingMaterialsFolder: "Assets/Resources/bookshelf_assets");

    private static readonly AssetSet WallSet = new AssetSet(
        sourceTexturesFolder: "Assets/Resources/wall_assets",
        materialsFolder: "Assets/Resources/wall_materials",
        label: "paredes");

    // Los materiales de puerta realmente usados (Door_Apartment_Brown.mat,
    // Door_Industrial_Dark.mat) viven sueltos en Assets/Materials, mezclados con
    // materiales de otros sistemas — de ahi el filtro por nombre "Door_". Las
    // texturas fuente son el pack completo importado (~20 puertas de color),
    // de las cuales solo 2 tienen material propio por ahora.
    private static readonly AssetSet DoorSet = new AssetSet(
        sourceTexturesFolder: "Assets/Door Texture Pack/Textures",
        materialsFolder: "Assets/Materials",
        label: "puertas",
        existingMaterialsNameFilter: "Door_",
        forceVariantBumpScale: true);

    // Prefijo opcional para el nombre del material ("" = mismo nombre que la textura).
    private const string MaterialPrefix = "";
    // Sufijo de los normal maps generados. Estos archivos se ignoran al recorrer.
    private const string NormalSuffix = "_Normal";

    // ---------- Look ----------
    private const float NormalHeightScale = 0.35f;
    // "_end"/"_start" y cualquier otra textura que no sea variante.
    private const float BumpScale = 1.2f;
    // Los "variant_a/b/c/..." repiten el mismo patron muy seguido en una pared o
    // estanteria larga; a 1.2 el relieve se nota demasiado repetido. Mas suave.
    private const float BumpScaleVariant = 0.3f;
    // Substring que identifica una variante en el nombre del archivo (ej.
    // "06_moldy_green_variant_b"), a diferencia de "_end"/"_start".
    private const string VariantMarker = "_variant";
    private const float AlphaCutoff = 0.5f;

    // Reflectancia. Valor LINEAL, no sRGB: 0.04 es el ~4% de un dielectrico
    // normal (madera barnizada, plastico). 0.02 es deliberadamente mas apagado:
    // madera vieja, seca y con polvo casi no devuelve luz especular.
    private const float SpecularLevel = 0.02f;
    // Muy baja = highlight ancho y difuso en vez de un punto brillante.
    private const float Smoothness = 0.05f;
    // Solo se usa si vuelves a UseSpecularWorkflow = false.
    private const float Metallic = 0f;

    // Specular Map: en vez de dejar el specular/gloss solo en manos del slider
    // (_SpecColor + _Smoothness), se genera UNA textura plana compartida (mismo
    // valor de SpecularLevel en todos lados) y se asigna como _SpecGlossMap. El
    // alpha va en 1 para que el smoothness efectivo lo siga poniendo el slider
    // de abajo (specGloss.a = mapa.a * _Smoothness) — la reflexion queda pareja
    // y minima en toda la superficie, pero nunca en cero.
    private const string GeneratedAssetsFolder = "Assets/Resources/Materials/_Generated";
    private const string FlatSpecularMapFileName = "FlatSpecularMap.png";
    private const int FlatSpecularMapSize = 8;
    private static Texture2D flatSpecularMapCache;

    // ---------- Opciones ----------
    // Replica la estructura de subcarpetas de las texturas dentro de MaterialsFolder
    // (tall_bookshelf/, short_bookshelf/, ...). wall_assets es plano, asi que para
    // paredes esto simplemente no tiene subcarpeta que replicar.
    private const bool MirrorSubfolders = true;
    // Fuerza Point filter + sin compresion en las texturas de color (pixel art).
    private const bool ApplyPixelArtImportSettings = true;
    // Activa Alpha Clipping cuando la textura tiene canal alpha.
    private const bool EnableAlphaClipWhenTextureHasAlpha = true;
    // Si el material ya existia y ya tenia Base Map, lo reemplaza igual.
    private const bool OverwriteExistingBaseMap = true;
    // Workflow Specular (_SpecColor) en vez de Metallic (_Metallic).
    private const bool UseSpecularWorkflow = true;
    // Apaga los reflejos de reflection probe / skybox.
    private const bool DisableEnvironmentReflections = true;
    // Apaga por completo el highlight de las luces directas: mate absoluto.
    // Empieza en false; si con lo demas todavia brilla, pon true.
    private const bool DisableSpecularHighlights = false;
    // Fuerza emision negra (los tiles no deben auto-iluminarse).
    private const bool ForceEmissionOff = true;

    private const string UrpLitShader = "Universal Render Pipeline/Lit";

    [MenuItem("Tools/Book Stealer/Generate Shelf Materials (BrownLibrary)")]
    public static void GenerateShelfMaterials()
    {
        GenerateFromFolder(ShelfSet);
    }

    [MenuItem("Tools/Book Stealer/Generate Wall Materials")]
    public static void GenerateWallMaterials()
    {
        GenerateFromFolder(WallSet);
    }

    [MenuItem("Tools/Book Stealer/Generate Door Materials")]
    public static void GenerateDoorMaterials()
    {
        GenerateFromFolder(DoorSet);
    }

    [MenuItem("Tools/Book Stealer/Generate Materials From Selection (Shelves)")]
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

        // La mirroria de subcarpetas y el destino usan el set de estanterias: esta
        // entrada existe para probar con pocas texturas antes de lanzar la carpeta
        // completa, igual que antes.
        Generate(roots.ToArray(), ShelfSet);
    }

    // Los GameObjects de la escena (paredes y estanterias, ProBuilder con varias
    // caras/materiales cada una) NO usan los materiales que "Generate" crea por
    // nombre de textura: usan materiales que ya existian de antes en otro lado
    // (ej. bookshelf_assets/MugLibrary/06_moldy_green_start.mat, o
    // wall_materials/WallMaterial01.mat), con nombres que no matchean ninguna
    // textura especifica. Reasignar por nombre nunca iba a encontrar nada.
    //
    // Esto en cambio no toca lo que el GameObject referencia: toma cada material
    // YA ASIGNADO en la carpeta indicada, mira que textura tiene en _BaseMap (o
    // _DetailAlbedoMap si quedo en el slot heredado) y le aplica el mismo
    // tratamiento que BuildMaterial hace con los nuevos (normal map fake depth +
    // reflectancia mate + alpha clip), en el material existente, en el mismo path.
    // El GameObject nunca cambia de referencia porque el asset que ya tenia
    // asignado es justamente el que se actualiza.
    [MenuItem("Tools/Book Stealer/Fix Existing Shelf Materials (In Place)")]
    public static void FixExistingShelfMaterials()
    {
        FixExistingMaterials(ShelfSet);
    }

    [MenuItem("Tools/Book Stealer/Fix Existing Wall Materials (In Place)")]
    public static void FixExistingWallMaterials()
    {
        FixExistingMaterials(WallSet);
    }

    [MenuItem("Tools/Book Stealer/Fix Existing Door Materials (In Place)")]
    public static void FixExistingDoorMaterials()
    {
        FixExistingMaterials(DoorSet);
    }

    private static void FixExistingMaterials(AssetSet set)
    {
        string folder = set.ExistingMaterialsFolder;
        if (!AssetDatabase.IsValidFolder(folder))
        {
            Debug.LogError($"LibraryMaterialGenerator: no existe la carpeta {folder} ({set.Label}).");
            return;
        }

        // El filtro por nombre evita tocar materiales de otros sistemas que viven
        // en la misma carpeta (ej. Assets/Materials tiene de todo, no solo puertas).
        string searchFilter = string.IsNullOrEmpty(set.ExistingMaterialsNameFilter)
            ? "t:Material"
            : $"{set.ExistingMaterialsNameFilter} t:Material";

        string[] matGuids = AssetDatabase.FindAssets(searchFilter, new[] { folder });
        if (matGuids.Length == 0)
        {
            Debug.LogWarning($"LibraryMaterialGenerator: no se encontraron materiales en {folder} ({set.Label}).");
            return;
        }

        int updated = 0, skipped = 0;
        try
        {
            for (int i = 0; i < matGuids.Length; i++)
            {
                string matPath = AssetDatabase.GUIDToAssetPath(matGuids[i]);
                if (EditorUtility.DisplayCancelableProgressBar(
                        $"Actualizando materiales existentes ({set.Label})",
                        Path.GetFileName(matPath),
                        (float)i / matGuids.Length))
                {
                    Debug.LogWarning("LibraryMaterialGenerator: cancelado por el usuario.");
                    break;
                }

                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (UpdateExistingMaterial(mat, set)) updated++;
                else skipped++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"LibraryMaterialGenerator ({set.Label}): {updated} materiales existentes actualizados, " +
                  $"{skipped} omitidos (de {matGuids.Length} encontrados en {folder}).");
    }

    // Aplica el mismo tratamiento que BuildMaterial pero sobre un material que YA
    // EXISTE y ya tiene asignada una textura (no crea nada nuevo, no toca el path).
    // Devuelve false si no se pudo hacer nada (sin _BaseMap utilizable en el shader,
    // o sin ninguna textura asignada de la que generar el normal map).
    private static bool UpdateExistingMaterial(Material mat, AssetSet set)
    {
        if (mat == null || !mat.HasProperty("_BaseMap")) return false;

        // Caso heredado: la textura estaba en el slot de detalle en vez de Base Map.
        MigrateDetailSlot(mat);

        if (!(mat.GetTexture("_BaseMap") is Texture2D baseTex)) return false;

        string colorPath = AssetDatabase.GetAssetPath(baseTex);
        if (string.IsNullOrEmpty(colorPath)) return false;

        var colorImporter = AssetImporter.GetAtPath(colorPath) as TextureImporter;
        bool hasAlpha = colorImporter != null && colorImporter.DoesSourceTextureHaveAlpha();
        if (ApplyPixelArtImportSettings && colorImporter != null)
            ApplyPixelArtSettings(colorImporter, hasAlpha);

        var normalTex = CreateOrUpdateNormalMap(colorPath);
        if (normalTex != null && mat.HasProperty("_BumpMap"))
        {
            mat.SetTexture("_BumpMap", normalTex);
            mat.SetFloat("_BumpScale", GetBumpScale(colorPath, set));
            mat.EnableKeyword("_NORMALMAP");
        }

        ConfigureReflectance(mat);
        SetAlphaClip(mat, EnableAlphaClipWhenTextureHasAlpha && hasAlpha);

        EditorUtility.SetDirty(mat);
        return true;
    }

    private static void GenerateFromFolder(AssetSet set)
    {
        if (!AssetDatabase.IsValidFolder(set.SourceTexturesFolder))
        {
            Debug.LogError($"LibraryMaterialGenerator: no existe la carpeta {set.SourceTexturesFolder} " +
                           $"({set.Label}). Ajusta la ruta correspondiente en el script.");
            return;
        }

        Generate(new[] { set.SourceTexturesFolder }, set);
    }

    private static void Generate(string[] roots, AssetSet set)
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
            Debug.LogWarning($"LibraryMaterialGenerator: no se encontraron texturas de color ({set.Label}).");
            return;
        }

        int created = 0, updated = 0, skipped = 0;
        try
        {
            for (int i = 0; i < texturePaths.Count; i++)
            {
                string colorPath = texturePaths[i];
                if (EditorUtility.DisplayCancelableProgressBar(
                        $"Generando materiales ({set.Label})",
                        Path.GetFileName(colorPath),
                        (float)i / texturePaths.Count))
                {
                    Debug.LogWarning("LibraryMaterialGenerator: cancelado por el usuario.");
                    break;
                }

                switch (BuildMaterial(colorPath, shader, set))
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
        Debug.Log($"LibraryMaterialGenerator ({set.Label}): {created} materiales creados, {updated} actualizados, " +
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

    private static Result BuildMaterial(string colorPath, Shader shader, AssetSet set)
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
        string matPath = GetMaterialPath(colorPath, set);
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
            mat.SetFloat("_BumpScale", GetBumpScale(colorPath, set));
            mat.EnableKeyword("_NORMALMAP");
        }

        // ---- Reflexion especular minima ----
        ConfigureReflectance(mat);

        SetAlphaClip(mat, EnableAlphaClipWhenTextureHasAlpha && hasAlpha);

        EditorUtility.SetDirty(mat);
        Debug.Log($"LibraryMaterialGenerator ({set.Label}): {(isNew ? "creado" : "actualizado")} {matPath} " +
                  $"-> Base Map = {colorPath}");

        return isNew ? Result.Created : Result.Updated;
    }

    // Las variantes ("_variant_a/b/c/...") se repiten muy seguido a lo largo de
    // una pared/estanteria larga; con el mismo relieve que "_end"/"_start" el
    // patron se nota demasiado, asi que llevan un BumpScale mas bajo. Las puertas
    // (set.ForceVariantBumpScale) no tienen ese sufijo en el nombre pero repiten
    // igual de seguido, asi que van siempre con el valor suave.
    private static float GetBumpScale(string colorPath, AssetSet set)
    {
        if (set.ForceVariantBumpScale) return BumpScaleVariant;

        string name = Path.GetFileNameWithoutExtension(colorPath);
        return name.Contains(VariantMarker) ? BumpScaleVariant : BumpScale;
    }

    private static string GetMaterialPath(string colorPath, AssetSet set)
    {
        string subfolder = "";
        if (MirrorSubfolders && colorPath.StartsWith(set.SourceTexturesFolder + "/"))
        {
            string relative = colorPath.Substring(set.SourceTexturesFolder.Length + 1);
            subfolder = Path.GetDirectoryName(relative).Replace("\\", "/");
        }

        string folder = string.IsNullOrEmpty(subfolder)
            ? set.MaterialsFolder
            : $"{set.MaterialsFolder}/{subfolder}";

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

    // Genera (una sola vez; despues se reutiliza) una textura chica y plana con
    // color RGB = SpecularLevel y alpha = 1, para usar como _SpecGlossMap. Vive
    // fuera de una carpeta Editor porque los materiales que la referencian se
    // usan en juego, no solo en el editor.
    private static Texture2D GetOrCreateFlatSpecularMap()
    {
        if (flatSpecularMapCache != null) return flatSpecularMapCache;

        string path = $"{GeneratedAssetsFolder}/{FlatSpecularMapFileName}";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null)
        {
            flatSpecularMapCache = existing;
            return existing;
        }

        EnsureFolder(GeneratedAssetsFolder);

        var tex = new Texture2D(FlatSpecularMapSize, FlatSpecularMapSize, TextureFormat.RGBA32, false, false);
        var fill = new Color32(
            (byte)Mathf.RoundToInt(SpecularLevel * 255f),
            (byte)Mathf.RoundToInt(SpecularLevel * 255f),
            (byte)Mathf.RoundToInt(SpecularLevel * 255f),
            255);
        var pixels = new Color32[FlatSpecularMapSize * FlatSpecularMapSize];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = fill;
        tex.SetPixels32(pixels);
        tex.Apply();

        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        File.WriteAllBytes(path, png);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        flatSpecularMapCache = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        return flatSpecularMapCache;
    }

    /// <summary>
    /// Todo lo que aporta brillo, al minimo. Son cinco fuentes distintas y
    /// bajar solo una deja la superficie brillando igual:
    ///   1. reflectancia especular del material (_SpecColor / _Metallic)
    ///   2. el Specular Map (si no se controla, tapa al slider de arriba)
    ///   3. smoothness (concentra o dispersa el highlight)
    ///   4. reflejos de entorno (reflection probe / skybox)
    ///   5. emision (auto-iluminacion)
    /// </summary>
    private static void ConfigureReflectance(Material mat)
    {
        if (mat.HasProperty("_MetallicGlossMap")) mat.SetTexture("_MetallicGlossMap", null);

        // Sacar el smoothness del alpha del albedo seria un desastre en los
        // recortes con fondo transparente: alpha 0/1 -> smoothness 0/1, o sea
        // espejo en toda la silueta. Se fuerza al canal del mapa/slider.
        if (mat.HasProperty("_SmoothnessTextureChannel")) mat.SetFloat("_SmoothnessTextureChannel", 0f);
        mat.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");

        if (UseSpecularWorkflow)
        {
            if (mat.HasProperty("_WorkflowMode")) mat.SetFloat("_WorkflowMode", 0f); // 0 = Specular
            mat.EnableKeyword("_SPECULAR_SETUP");
            // Importante: _SpecColor arranca en blanco. En workflow Specular eso
            // es una superficie 100% reflectante, justo lo que hay que evitar.
            if (mat.HasProperty("_SpecColor"))
                mat.SetColor("_SpecColor", new Color(SpecularLevel, SpecularLevel, SpecularLevel, 1f));

            // Specular Map: una textura plana muy oscura (no negra) en vez de
            // dejar el specular/gloss solo en el slider de arriba. El alpha del
            // mapa es 1, asi que el smoothness efectivo lo sigue poniendo el
            // _Smoothness de abajo (specGloss.a = mapa.a * _Smoothness) — la
            // reflexion queda pareja y minima en toda la superficie, sin llegar
            // nunca a cero.
            if (mat.HasProperty("_SpecGlossMap"))
                mat.SetTexture("_SpecGlossMap", GetOrCreateFlatSpecularMap());
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        else
        {
            if (mat.HasProperty("_WorkflowMode")) mat.SetFloat("_WorkflowMode", 1f); // 1 = Metallic
            mat.DisableKeyword("_SPECULAR_SETUP");
            // El Specular Map solo aplica al workflow Specular; en Metallic el
            // brillo lo controla _MetallicGlossMap (ya limpiado arriba).
            if (mat.HasProperty("_SpecGlossMap")) mat.SetTexture("_SpecGlossMap", null);
            mat.DisableKeyword("_METALLICSPECGLOSSMAP");
        }

        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", Metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", Smoothness);

        if (mat.HasProperty("_EnvironmentReflections"))
            mat.SetFloat("_EnvironmentReflections", DisableEnvironmentReflections ? 0f : 1f);
        if (DisableEnvironmentReflections) mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        else mat.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");

        if (mat.HasProperty("_SpecularHighlights"))
            mat.SetFloat("_SpecularHighlights", DisableSpecularHighlights ? 0f : 1f);
        if (DisableSpecularHighlights) mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        else mat.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");

        if (ForceEmissionOff)
        {
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
            mat.DisableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }
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
