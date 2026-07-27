using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Genera clusters cuadrados conectables reutilizando el vocabulario de props de MapaNivel.unity
// (Estanterias, escritorios, sofas, cajas, barreras) y calibra la escala contra la altura real
// de la camara/cabeza del jugador definida en Prueba.unity, en vez de asumir un valor fijo.
public static class LevelPrefabClusterBuilder
{
    private const string OutputFolder = "Assets/PreFabs/LevelPreFabs";
    private const string SinEscalarFolder = "Assets/PreFabs/sinEscalar";
    private const string PruebaScenePath = "Assets/Scenes/Prueba.unity";

    // Tamano base de una casilla (coincide con el Plane por defecto de Unity: 10x10 a escala 1,
    // el mismo primitivo que usa Suelo1 en MapaNivel). El pie de la casilla se mantiene igual
    // entre lotes para que todos los clusters generados sigan siendo conectables entre si;
    // solo el contenido (paredes, puertas, props) se recalibra a escala humana realista.
    private const float BaseTileSize = 10f;
    private const float BaseWallThickness = 0.2f;

    // Altura de ojos "de diseno" contra la que se calibra el tamano de la casilla (estandar FPS 1.6m).
    private const float DesignEyeHeight = 1.6f;

    // La camara en Prueba.unity representa los ojos, no la coronilla: un humano promedio tiene
    // los ojos a ~93% de su estatura total. De ahi se deriva la estatura del personaje, que es
    // la referencia real contra la que se calibran paredes/puertas/muebles/estanterias.
    private const float EyeToHeightRatio = 0.93f;

    // Proporciones "ideales" de cada categoria respecto a la estatura del personaje.
    private const float WallHeightRatio = 1.45f;        // techo claramente mas alto que el personaje
    private const float DoorClearHeightRatio = 1.15f;   // puerta un poco mas alta que el personaje
    private const float DoorWidthRatio = 0.68f;
    private const float ChairHeightRatio = 0.47f;       // silla claramente mas baja que el personaje
    private const float TableHeightRatio = 0.40f;
    private const float CouchHeightRatio = 0.45f;
    private const float TrashHeightRatio = 0.30f;
    private const float BoxHeightRatio = 0.28f;
    private const float BarrierHeightRatio = 0.30f;
    private const float LightPoleHeightRatio = 1.90f;
    private const float ShelfHeightRatio = 1.70f;              // estanteria pequena/estante de tienda
    private const float EstanteriaMedianaHeightRatio = 2.00f;
    private const float EstanteriaGrandeHeightRatio = 2.50f;   // estanteria "mucho" mas alta
    private const float TorreHeightRatio = 5.50f;              // torre central, referencia de varios pisos

    private static readonly string[] MaterialPathsPiso =
    {
        "Assets/Materials/Arquitectura/Materials/M_YFAM_WoodFlooring.mat",
        "Assets/Materials/Arquitectura/Materials/M_YFAM_TilesMarble.mat",
        "Assets/Materials/Arquitectura/Materials/M_YFAM_TilesWorn.mat",
    };

    private static readonly string[] MaterialPathsMuro =
    {
        "Assets/Materials/Arquitectura/Materials/M_YFAM_BricksGray.mat",
        "Assets/Materials/Arquitectura/Materials/M_YFAM_Plasterboard.mat",
        "Assets/Materials/Arquitectura/Materials/M_YFAM_BricksRedRough.mat",
    };

    private const string PrefabEstanteriaMediana = "Assets/PreFabs/Estanterias/EstanteriaMediana.prefab";
    // El nombre real en disco usa una "n" + tilde combinante (NFD), no la "n" precompuesta (NFC),
    // asi que se resuelve por busqueda en vez de una ruta literal con el caracter acentuado.
    private static readonly string PrefabEstanteriaPequena = ResolverRutaPorNombre("EstanteriaPeque", "Estanterias/EstanteriaPeque");
    private const string PrefabEstanteriaGrande = "Assets/PreFabs/Estanterias/EstanteriaGrande.prefab";
    private const string PrefabDesk = "Assets/MapaNivelAssets/Prefabs/SM_Prop_ShopInterior_Desk_02.prefab";
    private const string PrefabCouch = "Assets/PreFabs/ObjetosAmbientacion/SM_Prop_Couch_01.prefab";
    private const string PrefabBarrier = "Assets/PreFabs/ObjetosAmbientacion/SM_Prop_Barrier_01.prefab";
    private const string PrefabBoxA = "Assets/PreFabs/ObjetosAmbientacion/SM_Prop_CardboardBox_02.prefab";
    private const string PrefabBoxB = "Assets/PreFabs/ObjetosAmbientacion/SM_Prop_CardboardBox_04 (3).prefab";
    private const string PrefabLightPole = "Assets/PreFabs/ObjetosAmbientacion/SM_Prop_LightPole_Base_01.prefab";

    // Segundo lote: mesas, estanterias adicionales y basura, con piso forzado a madera.
    private const string FloorMaterialMadera = "Assets/Materials/Madera/Materials/M_YFFlM_03.mat";
    private const string PrefabTable02 = "Assets/MapaNivelAssets/Prefabs/SM_Prop_Table_02.prefab";
    private const string PrefabShopTable = "Assets/PreFabs/ObjetosAmbientacion/SM_Prop_ShopInterior_Table_01 (1).prefab";
    private const string PrefabPicnicTable = "Assets/MapaNivelAssets/Prefabs/SM_Prop_PicnicTable_01.prefab";
    private const string PrefabChair = "Assets/PreFabs/ObjetosAmbientacion/SM_Prop_ShopInterior_Chair_01 (4).prefab";
    private const string PrefabShelf01 = "Assets/PreFabs/ObjetosAmbientacion/SM_Prop_ShopInterior_Shelf_01 (1).prefab";
    private const string PrefabShelf02 = "Assets/PreFabs/ObjetosAmbientacion/SM_Prop_ShopInterior_Shelf_02.prefab";
    private const string PrefabTrashbin = "Assets/MapaNivelAssets/Prefabs/SM_Prop_Trashbin_01.prefab";

    private static float scaleFactor = 1f;
    private static float characterHeight = DesignEyeHeight / EyeToHeightRatio;
    private static int nextPisoMat;
    private static int nextMuroMat;

    private static string ResolverRutaPorNombre(string filtroBusqueda, string debeContener)
    {
        string[] guids = AssetDatabase.FindAssets(filtroBusqueda + " t:Prefab");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains(debeContener) && !path.Contains("(") && !path.Contains(" 1."))
                return path;
        }
        return guids.Length > 0 ? AssetDatabase.GUIDToAssetPath(guids[0]) : null;
    }

    [MenuItem("Tools/MapaNivelPuyo/Generar 10 Clusters Cuadrados")]
    public static void GenerarClusters()
    {
        CalcularEscalaDesdeCamaraDePrueba();
        nextPisoMat = 0;
        nextMuroMat = 0;

        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder("Assets/PreFabs", "LevelPreFabs");

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject buildRoot = new GameObject("ClustersBuilder");

        CrearVacio(buildRoot.transform);
        CrearEsquina(buildRoot.transform);
        CrearRectoNS(buildRoot.transform);
        CrearRectoEO(buildRoot.transform);
        CrearTInterseccion(buildRoot.transform);
        CrearCallejonSinSalida(buildRoot.transform);
        CrearCruce(buildRoot.transform);
        CrearSalaEstanterias(buildRoot.transform);
        CrearSalaTrabajo(buildRoot.transform);
        CrearSalaEstar(buildRoot.transform);

        int guardados = GuardarHijosComoPrefabs(buildRoot);

        Object.DestroyImmediate(buildRoot);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        AssetDatabase.SaveAssets();
        Debug.Log($"LevelPrefabClusterBuilder: {guardados}/10 clusters guardados en {OutputFolder}. " +
                  $"Estatura de personaje calibrada: {characterHeight:F3}m. Pared: {WallHeight:F2}m, puerta: {DoorClearHeight:F2}m, silla: {HeightChair:F2}m, estanteria grande: {HeightEstanteriaGrande:F2}m.");
    }

    [MenuItem("Tools/MapaNivelPuyo/Generar 10 Clusters Cuadrados (Lote 2 - Mesas y Basura)")]
    public static void GenerarClusters2()
    {
        CalcularEscalaDesdeCamaraDePrueba();
        nextMuroMat = 0;

        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder("Assets/PreFabs", "LevelPreFabs");

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject buildRoot = new GameObject("ClustersBuilder2");

        CrearMesasComedor(buildRoot.transform);
        CrearMesasPicnic(buildRoot.transform);
        CrearEstanteriasAltas(buildRoot.transform);
        CrearEstanteriasMixtas(buildRoot.transform);
        CrearZonaBasura(buildRoot.transform);
        CrearPasilloMesas(buildRoot.transform);
        CrearInterseccionEstante(buildRoot.transform);
        CrearCallejonBasura(buildRoot.transform);
        CrearSalaTrabajoMesas(buildRoot.transform);
        CrearCrucePlaza(buildRoot.transform);

        int guardados = GuardarHijosComoPrefabs(buildRoot);

        Object.DestroyImmediate(buildRoot);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        AssetDatabase.SaveAssets();
        Debug.Log($"LevelPrefabClusterBuilder: {guardados}/10 clusters (lote 2) guardados en {OutputFolder}. Piso forzado a {FloorMaterialMadera}. " +
                  $"Estatura de personaje calibrada: {characterHeight:F3}m.");
    }

    // Escala cada prefab compuesto de Assets/PreFabs/sinEscalar (grupos extraidos tal cual de
    // MapaNivel.unity, con su escala original sin tocar) a la altura "ideal" de su categoria,
    // y lo mueve a LevelPreFabs. Se mide la altura combinada de todos sus Renderers y se reescala
    // la raiz uniformemente, preservando la disposicion interna de cada grupo.
    [MenuItem("Tools/MapaNivelPuyo/Escalar y Mover Prefabs de sinEscalar")]
    public static void EscalarYMoverSinEscalar()
    {
        CalcularEscalaDesdeCamaraDePrueba();

        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder("Assets/PreFabs", "LevelPreFabs");

        Dictionary<string, float> alturaObjetivoPorNombre = new Dictionary<string, float>
        {
            { "EstanteriaDiagonalOri", HeightEstanteriaMediana },
            { "EscritoriosRecepcion", HeightTable },
            { "AltaOcciental", HeightEstanteriaGrande },
            { "MesaEscritorioOcci", HeightTable },
            { "BibliotecaCircularOcci", HeightShelf },
            { "SofasOcci", HeightCouch },
            { "CajasOcci", HeightBox },
            { "EstanteriaOccidenEntrada", HeightEstanteriaMediana },
            { "TorreCentral", TorreHeightRatio * characterHeight },
            { "EscalerasOcci", WallHeight },
            { "EstantesRecepcion", HeightEstanteriaMediana },
            { "EstanteDiagoOcci2", HeightEstanteriaMediana },
            { "MurosOccidental", WallHeight },
            { "MesasDiagOri", HeightTable },
            { "EscalerasOri", WallHeight },
            { "MurosBordeLateralOri", WallHeight },
            { "MesasTrabajoFondoOri", HeightTable },
        };

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { SinEscalarFolder });
        int procesados = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string nombre = Path.GetFileNameWithoutExtension(path);

            if (!alturaObjetivoPorNombre.TryGetValue(nombre, out float alturaObjetivo))
            {
                Debug.LogWarning($"LevelPrefabClusterBuilder: '{nombre}' no tiene categoria de escala asignada, se omite (no se mueve).");
                continue;
            }

            GameObject raiz = PrefabUtility.LoadPrefabContents(path);
            float alturaActual = MedirAlturaMundo(raiz);
            if (alturaActual > 0.0001f)
            {
                float mul = alturaObjetivo / alturaActual;
                raiz.transform.localScale *= mul;
                PrefabUtility.SaveAsPrefabAsset(raiz, path);
                Debug.Log($"LevelPrefabClusterBuilder: '{nombre}' escalado de {alturaActual:F2}m a {alturaObjetivo:F2}m (x{mul:F3}).");
            }
            else
            {
                Debug.LogWarning($"LevelPrefabClusterBuilder: '{nombre}' no tiene Renderers, no se pudo calibrar su altura.");
            }
            PrefabUtility.UnloadPrefabContents(raiz);

            string destino = $"{OutputFolder}/{nombre}.prefab";
            string error = AssetDatabase.ValidateMoveAsset(path, destino);
            if (string.IsNullOrEmpty(error))
            {
                AssetDatabase.MoveAsset(path, destino);
                procesados++;
            }
            else
            {
                Debug.LogError($"LevelPrefabClusterBuilder: no se pudo mover '{nombre}' a {destino}: {error}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"LevelPrefabClusterBuilder: {procesados}/{guids.Length} prefabs de sinEscalar escalados y movidos a {OutputFolder}.");
    }

    // Arma un mapa de GridSize x GridSize casillas en una escena nueva, eligiendo un cluster al
    // azar (de OutputFolder, sin contar la subcarpeta objetos) para cada casilla y esparciendo
    // unos cuantos prefabs de ObjetosFolder dentro de cada una. La seleccion es puramente
    // aleatoria por casilla: no hay logica de conectividad, asi que puertas/muros de casillas
    // vecinas no necesariamente coinciden.
    private const string ObjetosFolder = "Assets/PreFabs/LevelPreFabs/objetos";
    private const string MapaEscaladoScenePath = "Assets/Scenes/MapaEscalado.unity";
    private const int GridSize = 10;
    private const int ObjetosPorCluster = 3;
    private const int SemillaMapa = 20260727;

    [MenuItem("Tools/MapaNivelPuyo/Generar Mapa Escalado 10x10")]
    public static void GenerarMapaEscalado()
    {
        CalcularEscalaDesdeCamaraDePrueba();
        ConstruirMapa(GridSize, ObtenerClusterPaths(), ObtenerObjetoPaths(), ObjetosPorCluster, SemillaMapa, MapaEscaladoScenePath, "MapaEscalado");
    }

    // Mapa de prueba: una sola casilla de cluster repetida (sin variedad), util para revisar
    // como luce/conecta un cluster especifico antes de mezclarlo en el mapa grande.
    [MenuItem("Tools/MapaNivelPuyo/Generar Mapa 5x5 (Cluster_Pasillo_Mesas)")]
    public static void GenerarMapa5x5PasilloMesas()
    {
        CalcularEscalaDesdeCamaraDePrueba();

        string clusterPath = $"{OutputFolder}/Cluster_Pasillo_Mesas.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(clusterPath) == null)
        {
            Debug.LogError($"LevelPrefabClusterBuilder: no se encontro '{clusterPath}'.");
            return;
        }

        ConstruirMapa(5, new List<string> { clusterPath }, new List<string>(), 0, SemillaMapa,
            "Assets/Scenes/MapaEscalado_5x5_PasilloMesas.unity", "MapaEscalado_5x5_PasilloMesas");
    }

    private static List<string> ObtenerClusterPaths()
    {
        List<string> clusterPaths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { OutputFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.Contains("/objetos/") && Path.GetFileNameWithoutExtension(path).StartsWith("Cluster_"))
                clusterPaths.Add(path);
        }
        return clusterPaths;
    }

    private static List<string> ObtenerObjetoPaths()
    {
        List<string> objetoPaths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { ObjetosFolder }))
            objetoPaths.Add(AssetDatabase.GUIDToAssetPath(guid));
        return objetoPaths;
    }

    private static void ConstruirMapa(int gridSize, List<string> clusterPaths, List<string> objetoPaths, int objetosPorCluster, int semilla, string escenaPath, string nombreRaiz)
    {
        if (clusterPaths.Count == 0)
        {
            Debug.LogError($"LevelPrefabClusterBuilder: no se encontraron prefabs 'Cluster_*' en {OutputFolder}. Genera los clusters primero.");
            return;
        }

        // Cluster_Esquina trae muros en su lado Sur y Oeste de fabrica (ver CrearEsquina). Para
        // que las 4 esquinas del mapa siempre queden con los muros hacia afuera y las aperturas
        // hacia el resto de la grilla, cada esquina necesita su propio giro de 90 grados.
        string clusterEsquinaPath = $"{OutputFolder}/Cluster_Esquina.prefab";
        bool hayClusterEsquina = AssetDatabase.LoadAssetAtPath<GameObject>(clusterEsquinaPath) != null;

        System.Random rng = new System.Random(semilla);
        Scene escena = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        GameObject mapaRoot = new GameObject(nombreRaiz);

        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                bool esEsquina = hayClusterEsquina && gridSize > 1 &&
                    (i == 0 || i == gridSize - 1) && (j == 0 || j == gridSize - 1);

                string clusterPath = esEsquina ? clusterEsquinaPath : clusterPaths[rng.Next(clusterPaths.Count)];
                float rotacionY = 0f;
                if (esEsquina)
                {
                    if (i == 0 && j == 0) rotacionY = 0f;
                    else if (i == gridSize - 1 && j == 0) rotacionY = 270f;
                    else if (i == 0 && j == gridSize - 1) rotacionY = 90f;
                    else rotacionY = 180f; // i == gridSize-1 && j == gridSize-1
                }

                GameObject clusterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(clusterPath);
                GameObject clusterInstancia = (GameObject)PrefabUtility.InstantiatePrefab(clusterAsset);
                clusterInstancia.name = $"{Path.GetFileNameWithoutExtension(clusterPath)}_{i}_{j}";
                clusterInstancia.transform.SetParent(mapaRoot.transform, false);
                clusterInstancia.transform.localRotation = Quaternion.Euler(0f, rotacionY, 0f);
                clusterInstancia.transform.localPosition = new Vector3(
                    (i - (gridSize - 1) * 0.5f) * Full,
                    0f,
                    (j - (gridSize - 1) * 0.5f) * Full);

                float margen = Half * 0.7f;
                for (int k = 0; k < objetosPorCluster && objetoPaths.Count > 0; k++)
                {
                    string objetoPath = objetoPaths[rng.Next(objetoPaths.Count)];
                    GameObject objetoAsset = AssetDatabase.LoadAssetAtPath<GameObject>(objetoPath);
                    GameObject objetoInstancia = (GameObject)PrefabUtility.InstantiatePrefab(objetoAsset);
                    objetoInstancia.name = $"{Path.GetFileNameWithoutExtension(objetoPath)}_{k}";
                    objetoInstancia.transform.SetParent(clusterInstancia.transform, false);
                    objetoInstancia.transform.localPosition = new Vector3(
                        ((float)rng.NextDouble() * 2f - 1f) * margen,
                        0f,
                        ((float)rng.NextDouble() * 2f - 1f) * margen);
                    objetoInstancia.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                }
            }
        }

        EditorSceneManager.SaveScene(escena, escenaPath);
        Debug.Log($"LevelPrefabClusterBuilder: mapa {gridSize}x{gridSize} generado en {escenaPath} " +
                  $"({clusterPaths.Count} tipos de cluster, {objetoPaths.Count} tipos de objeto, {objetosPorCluster} objetos/cluster).");
    }

    private static int GuardarHijosComoPrefabs(GameObject buildRoot)
    {
        int guardados = 0;
        for (int i = 0; i < buildRoot.transform.childCount; i++)
        {
            GameObject cluster = buildRoot.transform.GetChild(i).gameObject;
            string path = $"{OutputFolder}/{cluster.name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(cluster, path, out bool exito);
            if (exito) guardados++;
            else Debug.LogError($"LevelPrefabClusterBuilder: fallo al guardar '{cluster.name}' en {path}");
        }
        return guardados;
    }

    private static void CalcularEscalaDesdeCamaraDePrueba()
    {
        EditorSceneManager.OpenScene(PruebaScenePath, OpenSceneMode.Single);
        GameObject camaraGO = GameObject.Find("MainCamera");
        if (camaraGO == null)
        {
            Camera cam = Object.FindAnyObjectByType<Camera>();
            camaraGO = cam != null ? cam.gameObject : null;
        }

        float eyeHeight;
        if (camaraGO == null)
        {
            Debug.LogWarning("LevelPrefabClusterBuilder: no se encontro la camara en Prueba.unity, se usa altura de ojos de referencia (1.6m).");
            eyeHeight = DesignEyeHeight;
        }
        else
        {
            eyeHeight = camaraGO.transform.position.y;
        }

        scaleFactor = eyeHeight / DesignEyeHeight;
        characterHeight = eyeHeight / EyeToHeightRatio;
        Debug.Log($"LevelPrefabClusterBuilder: altura de camara/cabeza en Prueba.unity = {eyeHeight:F4}m -> estatura de personaje calibrada = {characterHeight:F4}m.");
    }

    // ---------------- Bloques de construccion ----------------

    private static GameObject NuevoCluster(Transform padre, string nombre)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        return go;
    }

    private static float Full => BaseTileSize * scaleFactor;
    private static float Half => Full * 0.5f;
    private static float WallHeight => WallHeightRatio * characterHeight;
    private static float WallThickness => BaseWallThickness * scaleFactor;
    private static float DoorClearHeight => DoorClearHeightRatio * characterHeight;
    private static float DoorWidth => DoorWidthRatio * characterHeight;

    private static float HeightChair => ChairHeightRatio * characterHeight;
    private static float HeightTable => TableHeightRatio * characterHeight;
    private static float HeightCouch => CouchHeightRatio * characterHeight;
    private static float HeightTrash => TrashHeightRatio * characterHeight;
    private static float HeightBox => BoxHeightRatio * characterHeight;
    private static float HeightBarrier => BarrierHeightRatio * characterHeight;
    private static float HeightLightPole => LightPoleHeightRatio * characterHeight;
    private static float HeightShelf => ShelfHeightRatio * characterHeight;
    private static float HeightEstanteriaMediana => EstanteriaMedianaHeightRatio * characterHeight;
    private static float HeightEstanteriaGrande => EstanteriaGrandeHeightRatio * characterHeight;

    private static void AddFloor(Transform cluster, string materialPathOverride = null)
    {
        GameObject piso = GameObject.CreatePrimitive(PrimitiveType.Plane);
        piso.name = "Piso";
        piso.transform.SetParent(cluster, false);
        piso.transform.localScale = Vector3.one * scaleFactor;

        if (materialPathOverride != null)
        {
            Renderer renderer = piso.GetComponent<Renderer>();
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPathOverride);
            if (mat != null) renderer.sharedMaterial = mat;
        }
        else
        {
            AplicarMaterialCiclico(piso, MaterialPathsPiso, ref nextPisoMat);
        }
    }

    private static void AddWallCore(Transform cluster, string name, float centerX, float centerY, float centerZ, float length, float height, bool alongX)
    {
        GameObject muro = GameObject.CreatePrimitive(PrimitiveType.Cube);
        muro.name = name;
        muro.transform.SetParent(cluster, false);
        muro.transform.localPosition = new Vector3(centerX, centerY, centerZ);
        muro.transform.localScale = alongX
            ? new Vector3(length, height, WallThickness)
            : new Vector3(WallThickness, height, length);
        AplicarMaterialCiclico(muro, MaterialPathsMuro, ref nextMuroMat);
    }

    private static void AddWall(Transform cluster, string name, float centerX, float centerZ, float length, bool alongX)
    {
        AddWallCore(cluster, name, centerX, WallHeight * 0.5f, centerZ, length, WallHeight, alongX);
    }

    // side: 0=Norte(+Z) 1=Sur(-Z) 2=Este(+X) 3=Oeste(-X)
    private static void AddFullWall(Transform cluster, int side)
    {
        switch (side)
        {
            case 0: AddWall(cluster, "Muro_N", 0f, Half, Full, alongX: true); break;
            case 1: AddWall(cluster, "Muro_S", 0f, -Half, Full, alongX: true); break;
            case 2: AddWall(cluster, "Muro_E", Half, 0f, Full, alongX: false); break;
            case 3: AddWall(cluster, "Muro_O", -Half, 0f, Full, alongX: false); break;
        }
    }

    // Deja un hueco de puerta (DoorWidth x DoorClearHeight) y cierra el dintel por encima
    // hasta la altura completa del muro, para que la puerta no quede abierta hasta el techo.
    private static void AddWallWithDoorway(Transform cluster, int side)
    {
        float segmento = (Full - DoorWidth) * 0.5f;
        float offset = (DoorWidth + segmento) * 0.5f;
        float dintelAltura = WallHeight - DoorClearHeight;
        float dintelCentroY = DoorClearHeight + dintelAltura * 0.5f;

        switch (side)
        {
            case 0:
                AddWall(cluster, "Muro_N_a", -offset, Half, segmento, alongX: true);
                AddWall(cluster, "Muro_N_b", offset, Half, segmento, alongX: true);
                AddWallCore(cluster, "Muro_N_Dintel", 0f, dintelCentroY, Half, DoorWidth, dintelAltura, alongX: true);
                break;
            case 1:
                AddWall(cluster, "Muro_S_a", -offset, -Half, segmento, alongX: true);
                AddWall(cluster, "Muro_S_b", offset, -Half, segmento, alongX: true);
                AddWallCore(cluster, "Muro_S_Dintel", 0f, dintelCentroY, -Half, DoorWidth, dintelAltura, alongX: true);
                break;
            case 2:
                AddWall(cluster, "Muro_E_a", Half, -offset, segmento, alongX: false);
                AddWall(cluster, "Muro_E_b", Half, offset, segmento, alongX: false);
                AddWallCore(cluster, "Muro_E_Dintel", Half, dintelCentroY, 0f, DoorWidth, dintelAltura, alongX: false);
                break;
            case 3:
                AddWall(cluster, "Muro_O_a", -Half, -offset, segmento, alongX: false);
                AddWall(cluster, "Muro_O_b", -Half, offset, segmento, alongX: false);
                AddWallCore(cluster, "Muro_O_Dintel", -Half, dintelCentroY, 0f, DoorWidth, dintelAltura, alongX: false);
                break;
        }
    }

    private static float MedirAlturaMundo(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return 0f;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        return b.size.y;
    }

    // Instancia el prefab, lo escala uniformemente para que su altura real coincida con
    // targetHeight (calibrada contra la estatura del personaje) y luego lo posiciona.
    // La rotacion se aplica antes de medir porque un giro en Y no altera la altura.
    private static GameObject AddProp(Transform cluster, string prefabPath, string name, float localX, float localZ, float localRotY, float targetHeight)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (asset == null)
        {
            Debug.LogWarning($"LevelPrefabClusterBuilder: no se encontro el prefab '{prefabPath}', se omite '{name}'.");
            return null;
        }

        GameObject instancia = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        instancia.name = name;
        instancia.transform.SetParent(cluster, false);
        instancia.transform.localRotation = Quaternion.Euler(0f, localRotY, 0f);

        float alturaActual = MedirAlturaMundo(instancia);
        if (alturaActual > 0.0001f)
            instancia.transform.localScale *= targetHeight / alturaActual;
        else
            Debug.LogWarning($"LevelPrefabClusterBuilder: '{name}' no tiene Renderers, no se pudo calibrar su altura.");

        instancia.transform.localPosition = new Vector3(localX, 0f, localZ);
        return instancia;
    }

    private static void AplicarMaterialCiclico(GameObject go, string[] paletaPaths, ref int indice)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer == null || paletaPaths.Length == 0)
            return;

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(paletaPaths[indice % paletaPaths.Length]);
        indice++;
        if (mat != null)
            renderer.sharedMaterial = mat;
    }

    // ---------------- Los 10 clusters (lote 1) ----------------

    private static void CrearVacio(Transform padre)
    {
        GameObject c = NuevoCluster(padre, "Cluster_Vacio");
        AddFloor(c.transform);
    }

    private static void CrearEsquina(Transform padre)
    {
        GameObject c = NuevoCluster(padre, "Cluster_Esquina");
        AddFloor(c.transform);
        AddFullWall(c.transform, 1); // Sur
        AddFullWall(c.transform, 3); // Oeste
        AddProp(c.transform, PrefabLightPole, "Poste", -Half * 0.6f, -Half * 0.6f, 0f, HeightLightPole);
    }

    private static void CrearRectoNS(Transform padre)
    {
        GameObject c = NuevoCluster(padre, "Cluster_Recto_NS");
        AddFloor(c.transform);
        AddFullWall(c.transform, 2); // Este
        AddFullWall(c.transform, 3); // Oeste
    }

    private static void CrearRectoEO(Transform padre)
    {
        GameObject c = NuevoCluster(padre, "Cluster_Recto_EO");
        AddFloor(c.transform);
        AddFullWall(c.transform, 0); // Norte
        AddFullWall(c.transform, 1); // Sur
    }

    private static void CrearTInterseccion(Transform padre)
    {
        GameObject c = NuevoCluster(padre, "Cluster_T_Interseccion");
        AddFloor(c.transform);
        AddFullWall(c.transform, 1); // Sur (unico lado cerrado)
    }

    private static void CrearCallejonSinSalida(Transform padre)
    {
        GameObject c = NuevoCluster(padre, "Cluster_CallejonSinSalida");
        AddFloor(c.transform);
        AddFullWall(c.transform, 0); // Norte
        AddFullWall(c.transform, 2); // Este
        AddFullWall(c.transform, 3); // Oeste
        AddProp(c.transform, PrefabBoxA, "Caja", 0f, Half * 0.5f, 0f, HeightBox);
    }

    private static void CrearCruce(Transform padre)
    {
        GameObject c = NuevoCluster(padre, "Cluster_Cruce");
        AddFloor(c.transform);
        float esquina = Half * 0.85f;
        AddProp(c.transform, PrefabLightPole, "Poste_NE", esquina, esquina, 0f, HeightLightPole);
        AddProp(c.transform, PrefabLightPole, "Poste_NO", -esquina, esquina, 0f, HeightLightPole);
        AddProp(c.transform, PrefabLightPole, "Poste_SE", esquina, -esquina, 0f, HeightLightPole);
        AddProp(c.transform, PrefabLightPole, "Poste_SO", -esquina, -esquina, 0f, HeightLightPole);
    }

    private static void CrearSalaEstanterias(Transform padre)
    {
        GameObject c = NuevoCluster(padre, "Cluster_Sala_Estanterias");
        AddFloor(c.transform);
        AddFullWall(c.transform, 1);
        AddFullWall(c.transform, 2);
        AddFullWall(c.transform, 3);
        AddWallWithDoorway(c.transform, 0);

        float z = Half * 0.7f;
        AddProp(c.transform, PrefabEstanteriaGrande, "Estanteria_1", -Half * 0.55f, z, 180f, HeightEstanteriaGrande);
        AddProp(c.transform, PrefabEstanteriaGrande, "Estanteria_2", 0f, z, 180f, HeightEstanteriaGrande);
        AddProp(c.transform, PrefabEstanteriaGrande, "Estanteria_3", Half * 0.55f, z, 180f, HeightEstanteriaGrande);
        AddProp(c.transform, PrefabEstanteriaPequena, "Estanteria_Baja_1", -Half * 0.6f, -Half * 0.3f, 90f, HeightShelf);
        AddProp(c.transform, PrefabEstanteriaPequena, "Estanteria_Baja_2", Half * 0.6f, -Half * 0.3f, -90f, HeightShelf);
    }

    private static void CrearSalaTrabajo(Transform padre)
    {
        GameObject c = NuevoCluster(padre, "Cluster_Sala_Trabajo");
        AddFloor(c.transform);
        AddFullWall(c.transform, 1);
        AddFullWall(c.transform, 2);
        AddFullWall(c.transform, 3);
        AddWallWithDoorway(c.transform, 0);

        AddProp(c.transform, PrefabDesk, "Escritorio_1", -Half * 0.5f, Half * 0.5f, 0f, HeightTable);
        AddProp(c.transform, PrefabDesk, "Escritorio_2", Half * 0.5f, Half * 0.5f, 0f, HeightTable);
        AddProp(c.transform, PrefabDesk, "Escritorio_3", 0f, -Half * 0.5f, 180f, HeightTable);
        AddProp(c.transform, PrefabEstanteriaMediana, "Estanteria_Fondo", 0f, Half * 0.85f, 180f, HeightEstanteriaMediana);
    }

    private static void CrearSalaEstar(Transform padre)
    {
        GameObject c = NuevoCluster(padre, "Cluster_Sala_Estar");
        AddFloor(c.transform);
        AddFullWall(c.transform, 1);
        AddFullWall(c.transform, 2);
        AddFullWall(c.transform, 3);
        AddWallWithDoorway(c.transform, 0);

        AddProp(c.transform, PrefabCouch, "Sofa_1", -Half * 0.5f, -Half * 0.3f, 0f, HeightCouch);
        AddProp(c.transform, PrefabCouch, "Sofa_2", Half * 0.5f, -Half * 0.3f, 0f, HeightCouch);
        AddProp(c.transform, PrefabBarrier, "Barrera_1", -Half * 0.4f, Half * 0.55f, 0f, HeightBarrier);
        AddProp(c.transform, PrefabBoxB, "Caja_1", Half * 0.35f, Half * 0.6f, 0f, HeightBox);
        AddProp(c.transform, PrefabBoxA, "Caja_2", Half * 0.55f, Half * 0.6f, 0f, HeightBox);
    }

    // ---------------- Los 10 clusters (lote 2: mesas / estanterias / basura) ----------------

    private static void CrearMesasComedor(Transform padre)
    {
        GameObject c = NuevoCluster(padre, "Cluster_Mesas_Comedor");
        AddFloor(c.transform, FloorMaterialMadera);
        AddFullWall(c.transform, 1);
        AddFullWall(c.transform, 2);
        AddFullWall(c.transform, 3);
        AddWallWithDoorway(c.transform, 0);

        AddProp(c.transform, PrefabTable02, "Mesa_1", -Half * 0.45f, 0f, 0f, HeightTable);
        AddProp(c.transform, PrefabTable02, "Mesa_2", Half * 0.45f, 0f, 0f, HeightTable);
        AddProp(c.transform, PrefabChair, "Silla_1", -Half * 0.45f, -Half * 0.35f, 0f, HeightChair);
        AddProp(c.transform, PrefabChair, "Silla_2", -Half * 0.45f, Half * 0.35f, 180f, HeightChair);
        AddProp(c.transform, PrefabChair, "Silla_3", Half * 0.45f, -Half * 0.35f, 0f, HeightChair);
        AddProp(c.transform, PrefabChair, "Silla_4", Half * 0.45f, Half * 0.35f, 180f, HeightChair);
    }

    private static void CrearMesasPicnic(Transform padre)
    {
        GameObject c = NuevoCluster(padre, "Cluster_Mesas_Picnic");
        AddFloor(c.transform, FloorMaterialMadera);
        AddFullWall(c.transform, 1);
        AddWallWithDoorway(c.transform, 3);

        AddProp(c.transform, PrefabPicnicTable, "MesaPicnic_1", 0f, Half * 0.2f, 0f, HeightTable);
        AddProp(c.transform, PrefabShopTable, "MesaAuxiliar", Half * 0.55f, -Half * 0.5f, 90f, HeightTable);
        AddProp(c.transform, PrefabLightPole, "Poste", -Half * 0.6f, -Half * 0.6f, 0f, HeightLightPole);
    }

    private static void CrearEstanteriasAltas(Transform padre)
    {
        GameObject c = NuevoCluster(padre, "Cluster_Estanterias_Altas");
        AddFloor(c.transform, FloorMaterialMadera);
        AddFullWall(c.transform, 2);
        AddFullWall(c.transform, 3);
        AddWallWithDoorway(c.transform, 1);

        float x = Half * 0.55f;
        AddProp(c.transform, PrefabEstanteriaGrande, "Estanteria_N1", x, Half * 0.6f, -90f, HeightEstanteriaGrande);
        AddProp(c.transform, PrefabEstanteriaGrande, "Estanteria_N2", -x, Half * 0.6f, 90f, HeightEstanteriaGrande);
        AddProp(c.transform, PrefabEstanteriaGrande, "Estanteria_S1", x, -Half * 0.1f, -90f, HeightEstanteriaGrande);
        AddProp(c.transform, PrefabEstanteriaGrande, "Estanteria_S2", -x, -Half * 0.1f, 90f, HeightEstanteriaGrande);
    }

    private static void CrearEstanteriasMixtas(Transform padre)
    {
        GameObject c = NuevoCluster(padre, "Cluster_Estanterias_Mixtas");
        AddFloor(c.transform, FloorMaterialMadera);
        AddFullWall(c.transform, 1);
        AddFullWall(c.transform, 2);
        AddFullWall(c.transform, 3);
        AddWallWithDoorway(c.transform, 0);

        AddProp(c.transform, PrefabEstanteriaMediana, "Estanteria_Mediana", -Half * 0.55f, Half * 0.7f, 180f, HeightEstanteriaMediana);
        AddProp(c.transform, PrefabEstanteriaPequena, "Estanteria_Pequena", Half * 0.1f, Half * 0.7f, 180f, HeightShelf);
        AddProp(c.transform, PrefabShelf01, "Estante_01", Half * 0.6f, -Half * 0.4f, -90f, HeightShelf);
        AddProp(c.transform, PrefabShelf02, "Estante_02", -Half * 0.6f, -Half * 0.4f, 90f, HeightShelf);
    }

    private static void CrearZonaBasura(Transform padre)
    {
        GameObject c = NuevoCluster(padre, "Cluster_Zona_Basura");
        AddFloor(c.transform, FloorMaterialMadera);
        AddFullWall(c.transform, 1);
        AddFullWall(c.transform, 3);

        AddProp(c.transform, PrefabTrashbin, "Basurero_1", -Half * 0.55f, -Half * 0.55f, 0f, HeightTrash);
        AddProp(c.transform, PrefabTrashbin, "Basurero_2", -Half * 0.25f, -Half * 0.6f, 0f, HeightTrash);
        AddProp(c.transform, PrefabBoxA, "Caja_1", -Half * 0.6f, -Half * 0.2f, 0f, HeightBox);
        AddProp(c.transform, PrefabBoxB, "Caja_2", -Half * 0.3f, -Half * 0.25f, 0f, HeightBox);
    }

    private static void CrearPasilloMesas(Transform padre)
    {
        GameObject c = NuevoCluster(padre, "Cluster_Pasillo_Mesas");
        AddFloor(c.transform, FloorMaterialMadera);
        AddFullWall(c.transform, 2);
        AddFullWall(c.transform, 3);

        AddProp(c.transform, PrefabShopTable, "Mesa_Lateral", -Half * 0.6f, 0f, 90f, HeightTable);
        AddProp(c.transform, PrefabChair, "Silla_Lateral", -Half * 0.45f, Half * 0.3f, -90f, HeightChair);
    }

    private static void CrearInterseccionEstante(Transform padre)
    {
        GameObject c = NuevoCluster(padre, "Cluster_Interseccion_Estante");
        AddFloor(c.transform, FloorMaterialMadera);
        AddFullWall(c.transform, 1); // unico lado cerrado

        AddProp(c.transform, PrefabShelf02, "Estante_Fondo", 0f, -Half * 0.7f, 0f, HeightShelf);
    }

    private static void CrearCallejonBasura(Transform padre)
    {
        GameObject c = NuevoCluster(padre, "Cluster_Callejon_Basura");
        AddFloor(c.transform, FloorMaterialMadera);
        AddFullWall(c.transform, 0);
        AddFullWall(c.transform, 2);
        AddFullWall(c.transform, 3);

        AddProp(c.transform, PrefabTrashbin, "Basurero_1", -Half * 0.5f, Half * 0.5f, 0f, HeightTrash);
        AddProp(c.transform, PrefabTrashbin, "Basurero_2", Half * 0.5f, Half * 0.5f, 0f, HeightTrash);
        AddProp(c.transform, PrefabBoxA, "Caja_1", 0f, Half * 0.6f, 0f, HeightBox);
    }

    private static void CrearSalaTrabajoMesas(Transform padre)
    {
        GameObject c = NuevoCluster(padre, "Cluster_Sala_Trabajo_Mesas");
        AddFloor(c.transform, FloorMaterialMadera);
        AddFullWall(c.transform, 1);
        AddFullWall(c.transform, 2);
        AddFullWall(c.transform, 3);
        AddWallWithDoorway(c.transform, 0);

        AddProp(c.transform, PrefabDesk, "Escritorio", -Half * 0.55f, Half * 0.55f, 0f, HeightTable);
        AddProp(c.transform, PrefabTable02, "Mesa", Half * 0.5f, -Half * 0.1f, 0f, HeightTable);
        AddProp(c.transform, PrefabChair, "Silla_1", Half * 0.5f, Half * 0.15f, 180f, HeightChair);
        AddProp(c.transform, PrefabShelf01, "Estante", -Half * 0.6f, -Half * 0.5f, 90f, HeightShelf);
    }

    private static void CrearCrucePlaza(Transform padre)
    {
        GameObject c = NuevoCluster(padre, "Cluster_Cruce_Plaza");
        AddFloor(c.transform, FloorMaterialMadera);

        AddProp(c.transform, PrefabShopTable, "Mesa_Central", 0f, 0f, 0f, HeightTable);
        AddProp(c.transform, PrefabChair, "Silla_1", -Half * 0.2f, Half * 0.2f, 45f, HeightChair);
        AddProp(c.transform, PrefabChair, "Silla_2", Half * 0.2f, -Half * 0.2f, -135f, HeightChair);
    }
}
