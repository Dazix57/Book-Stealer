using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class LaberintoBuilder
{
    // ---- Parametros configurables ----
    private const float CellSize = 4f;
    private const int MaxCeldasPorLadoGrupo = 4;
    private const float OverheadFactorPasillos = 1.8f;
    private const int SemillaAleatoria = 12345;
    private const float ProbabilidadConexionExtra = 0.08f;
    private const bool PermitirRotacion90EnGrupos = true;
    private const float WallHeight = 3f;
    private const float WallThickness = 0.2f;
    private const float PisoConstruccionYOffset = 0.02f;
    private const float DefaultFootprintSize = 1.5f;
    private const float WallPrefabChance = 0.25f;
    private const bool UsarPrefabsDeParedParaVariedad = true;

    private const string NombreRaizLaberinto = "Laberinto";
    private const string NombreSuelo1 = "Suelo1";
    private const string NombrePrimerPiso = "PrimerPiso";
    private const string NombreSegundoPiso = "SegundoPiso";

    // Cualquier gameobject cuyo nombre propio o el de su prefab de origen contenga
    // alguna de estas palabras (case-insensitive) queda excluido: no se mueve.
    private static readonly string[] ExclusionKeywords =
    {
        "muro", "wall", "door", "puerta", "stairs", "escalera"
    };

    private static readonly string[] MaterialPathsPiso =
    {
        "Assets/Materials/Arquitectura/Materials/M_YFAM_WoodFlooring.mat",
        "Assets/Materials/Arquitectura/Materials/M_YFAM_TilesMarble.mat",
        "Assets/Materials/Arquitectura/Materials/M_YFAM_TilesWorn.mat",
        "Assets/Materials/Arquitectura/Materials/M_YFAM_TilesConcave.mat",
        "Assets/Materials/Arquitectura/Materials/M_YFAM_TilesAcid.mat",
        "Assets/Materials/Arquitectura/Materials/M_YFAM_TilesPatchwork.mat",
        "Assets/Materials/Arquitectura/Materials/M_YFAM_LaminatedWood.mat",
        "Assets/Materials/Arquitectura/Materials/M_YFAM_Plywood.mat",
        "Assets/Materials/Arquitectura/Materials/M_YFAM_RubberDots.mat",
    };

    private static readonly string[] MaterialPathsMuro =
    {
        "Assets/Materials/Arquitectura/Materials/M_YFAM_BricksGray.mat",
        "Assets/Materials/Arquitectura/Materials/M_YFAM_BricksRedRough.mat",
        "Assets/Materials/Arquitectura/Materials/M_YFAM_BricksRedSmooth.mat",
        "Assets/Materials/Arquitectura/Materials/M_YFAM_BricksRough.mat",
        "Assets/Materials/Arquitectura/Materials/M_YFAM_BricksWeathered.mat",
        "Assets/Materials/Arquitectura/Materials/M_YFAM_GabionWall.mat",
        "Assets/Materials/Arquitectura/Materials/M_YFAM_Plasterboard.mat",
        "Assets/Materials/Arquitectura/Materials/M_YFAM_Wallpaper.mat",
    };

    // Modelos FBX sueltos (no hay prefab dedicado) que se reinstancian para dar variedad
    // a algunos tramos de muro nuevo, ademas de los Cube genericos.
    private static readonly string[] PrefabsDeParedConVentana =
    {
        "Assets/MapaNivelAssets/Modelos/SM_Buildings_WallWindowDouble_2x3_01.fbx",
        "Assets/MapaNivelAssets/Modelos/SM_Buildings_WallWindowDouble_5x3_01.fbx",
    };

    private static int siguienteMaterialPiso;
    private static int siguienteMaterialMuro;
    private static Transform holderExcluidosSinMover;

    private class GrupoInfo
    {
        public Transform raiz;
        public Bounds boundsMundo;
        public bool sinRenderers;
        public int celdasAncho;
        public int celdasProfundo;
        public List<Transform> excluidosAnidados;
    }

    private class Arista
    {
        public int nodoA;
        public int nodoB;
        public int x;
        public int y;
        public bool esVertical;
    }

    [MenuItem("Tools/MapaNivelPuyo/Reorganizar Como Laberinto (Dry Run)")]
    private static void ReorganizarComoLaberintoDryRun()
    {
        EjecutarPipeline(dryRun: true);
    }

    [MenuItem("Tools/MapaNivelPuyo/Reorganizar Como Laberinto")]
    private static void ReorganizarComoLaberinto()
    {
        EjecutarPipeline(dryRun: false);
    }

    private static void EjecutarPipeline(bool dryRun)
    {
        GameObject primerPisoGO = GameObject.Find(NombrePrimerPiso);
        GameObject segundoPisoGO = GameObject.Find(NombreSegundoPiso);
        GameObject suelo1GO = GameObject.Find(NombreSuelo1);

        if (primerPisoGO == null && segundoPisoGO == null)
        {
            Debug.LogError("LaberintoBuilder: no se encontraron 'PrimerPiso' ni 'SegundoPiso' en la escena activa. Abre MapaNivelPuyo.unity y vuelve a intentar.");
            return;
        }

        if (suelo1GO == null)
            Debug.LogWarning("LaberintoBuilder: no se encontro 'Suelo1', se usara Y=0 como nivel de suelo.");

        float suelo1Y = suelo1GO != null ? suelo1GO.transform.position.y : 0f;

        // Reiniciar estado estatico por si se corre el pipeline varias veces en la misma sesion de Editor.
        siguienteMaterialPiso = 0;
        siguienteMaterialMuro = 0;
        holderExcluidosSinMover = null;

        System.Random rng = new System.Random(SemillaAleatoria);

        List<Transform> contenedores = new List<Transform>();
        if (primerPisoGO != null) contenedores.Add(primerPisoGO.transform);
        if (segundoPisoGO != null) contenedores.Add(segundoPisoGO.transform);

        List<GrupoInfo> grupos = new List<GrupoInfo>();
        int excluidosTopNivel = 0;
        int desanidadosTotal = 0;

        foreach (Transform contenedor in contenedores)
        {
            Transform[] hijos = new Transform[contenedor.childCount];
            for (int i = 0; i < contenedor.childCount; i++)
                hijos[i] = contenedor.GetChild(i);

            foreach (Transform hijo in hijos)
            {
                if (EsExcluido(hijo.gameObject))
                {
                    excluidosTopNivel++;
                    continue;
                }

                List<Transform> excluidosAnidados = new List<Transform>();
                RecolectarExcluidosAnidados(hijo, excluidosAnidados);
                desanidadosTotal += excluidosAnidados.Count;

                GrupoInfo info = new GrupoInfo { raiz = hijo, excluidosAnidados = excluidosAnidados };
                info.boundsMundo = CalcularBoundsMundo(hijo, excluidosAnidados);
                info.sinRenderers = info.boundsMundo.size.sqrMagnitude <= 0.0001f;
                if (info.sinRenderers)
                    Debug.LogWarning($"LaberintoBuilder: el grupo '{hijo.name}' no tiene Renderers propios, se le asigna un footprint por defecto ({DefaultFootprintSize}m). Revisar manualmente.");

                grupos.Add(info);
            }
        }

        if (grupos.Count == 0)
        {
            Debug.LogWarning("LaberintoBuilder: no quedo ningun grupo movible (todo excluido o vacio). No hay nada que reorganizar.");
            return;
        }

        foreach (GrupoInfo g in grupos)
            CalcularCeldasNecesarias(g);

        int gridW, gridH;
        CalcularTamanoGrid(grupos, out gridW, out gridH);

        // -1 = celda libre; en otro caso, indice de nodo (grupo colocado o celda de pasillo suelta).
        int[,] propietario = new int[gridW, gridH];
        for (int x = 0; x < gridW; x++)
            for (int y = 0; y < gridH; y++)
                propietario[x, y] = -1;

        List<GrupoInfo> ordenPorArea = grupos.OrderByDescending(g => g.celdasAncho * g.celdasProfundo).ToList();
        Dictionary<GrupoInfo, RectInt> asignacion = new Dictionary<GrupoInfo, RectInt>();
        List<GrupoInfo> noUbicados = new List<GrupoInfo>();

        int siguienteNodo = 0;
        foreach (GrupoInfo g in ordenPorArea)
        {
            RectInt? rect = UbicarGrupoEnGrid(g, propietario, gridW, gridH);
            if (rect.HasValue)
            {
                MarcarOcupado(propietario, rect.Value, siguienteNodo);
                asignacion[g] = rect.Value;
                siguienteNodo++;
            }
            else
            {
                noUbicados.Add(g);
                Debug.LogWarning($"LaberintoBuilder: no se encontro hueco en el grid ({gridW}x{gridH}) para el grupo '{g.raiz.name}' ({g.celdasAncho}x{g.celdasProfundo} celdas). Aumenta OverheadFactorPasillos y vuelve a correr.");
            }
        }

        // El resto de celdas libres pasan a ser nodos de pasillo de 1x1, para que todo el grid
        // quede cubierto por nodos y se pueda construir un unico grafo de adyacencia sobre el.
        int totalNodos = siguienteNodo;
        for (int x = 0; x < gridW; x++)
        {
            for (int y = 0; y < gridH; y++)
            {
                if (propietario[x, y] == -1)
                {
                    propietario[x, y] = totalNodos;
                    totalNodos++;
                }
            }
        }

        List<Arista> candidatas = ConstruirAristasCandidatas(propietario, gridW, gridH);
        Mezclar(candidatas, rng);

        bool[,] wallV = new bool[Math.Max(gridW - 1, 0), gridH];
        bool[,] wallH = new bool[gridW, Math.Max(gridH - 1, 0)];
        for (int x = 0; x < wallV.GetLength(0); x++)
            for (int y = 0; y < wallV.GetLength(1); y++)
                wallV[x, y] = true;
        for (int x = 0; x < wallH.GetLength(0); x++)
            for (int y = 0; y < wallH.GetLength(1); y++)
                wallH[x, y] = true;

        // Kruskal aleatorio: como el grafo de celdas de un grid rectangular siempre es conexo,
        // el arbol de expansion resultante garantiza que TODO el laberinto queda transitable
        // sin necesidad de un paso de reparacion de conectividad aparte.
        int[] unionFind = new int[totalNodos];
        for (int i = 0; i < totalNodos; i++) unionFind[i] = i;

        foreach (Arista a in candidatas)
        {
            bool nuevaConexion = UnionFindUnir(unionFind, a.nodoA, a.nodoB);
            bool loopExtra = !nuevaConexion && rng.NextDouble() < ProbabilidadConexionExtra;
            if (nuevaConexion || loopExtra)
            {
                if (a.esVertical) wallV[a.x, a.y] = false;
                else wallH[a.x, a.y] = false;
            }
        }

        int murosCreados = 0;
        if (!dryRun)
        {
            Undo.SetCurrentGroupName("Reorganizar MapaNivelPuyo como laberinto");
            int grupoUndo = Undo.GetCurrentGroup();

            Transform raizLaberinto = ObtenerOCrearRaizLaberinto();
            Vector3 origenGrid = new Vector3(-(gridW * CellSize) * 0.5f, 0f, -(gridH * CellSize) * 0.5f);

            foreach (KeyValuePair<GrupoInfo, RectInt> par in asignacion)
                AplicarGrupoALaberinto(par.Key, par.Value, origenGrid, suelo1Y, raizLaberinto);

            try
            {
                EditorUtility.DisplayProgressBar("Laberinto", "Construyendo muros...", 0f);
                Transform raizEstructura = ObtenerOCrearHijo(raizLaberinto, "Estructura");
                murosCreados += ConstruirMurosVerticales(wallV, gridW, origenGrid, suelo1Y, raizEstructura, rng);
                murosCreados += ConstruirMurosHorizontales(wallH, gridH, origenGrid, suelo1Y, raizEstructura, rng);
                murosCreados += ConstruirPerimetro(gridW, gridH, origenGrid, suelo1Y, raizEstructura);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Undo.CollapseUndoOperations(grupoUndo);
        }

        LogResumen(dryRun, grupos.Count, excluidosTopNivel, desanidadosTotal, asignacion.Count, noUbicados.Count, gridW, gridH, murosCreados);
    }

    // ---------------- Deteccion y exclusion ----------------

    private static bool EsExcluido(GameObject go)
    {
        if (ContieneKeywordExclusion(go.name))
            return true;

        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
        if (!string.IsNullOrEmpty(prefabPath) && ContieneKeywordExclusion(Path.GetFileNameWithoutExtension(prefabPath)))
            return true;

        return false;
    }

    private static bool ContieneKeywordExclusion(string texto)
    {
        if (string.IsNullOrEmpty(texto))
            return false;

        string textoLower = texto.ToLowerInvariant();
        foreach (string palabra in ExclusionKeywords)
        {
            if (textoLower.Contains(palabra))
                return true;
        }

        return false;
    }

    private static void RecolectarExcluidosAnidados(Transform nodo, List<Transform> resultado)
    {
        for (int i = 0; i < nodo.childCount; i++)
        {
            Transform hijo = nodo.GetChild(i);
            if (EsExcluido(hijo.gameObject))
            {
                resultado.Add(hijo);
                continue;
            }

            RecolectarExcluidosAnidados(hijo, resultado);
        }
    }

    // ---------------- Medicion ----------------

    private static Bounds CalcularBoundsMundo(Transform raiz, List<Transform> excluidos)
    {
        HashSet<Transform> excluidosSet = new HashSet<Transform>(excluidos);
        Renderer[] renderers = raiz.GetComponentsInChildren<Renderer>(true);
        Bounds? acumulado = null;

        foreach (Renderer r in renderers)
        {
            if (EstaBajoAlgunExcluido(r.transform, excluidosSet))
                continue;

            if (acumulado.HasValue)
            {
                Bounds b = acumulado.Value;
                b.Encapsulate(r.bounds);
                acumulado = b;
            }
            else
            {
                acumulado = r.bounds;
            }
        }

        return acumulado ?? new Bounds(raiz.position, Vector3.one * DefaultFootprintSize);
    }

    private static bool EstaBajoAlgunExcluido(Transform t, HashSet<Transform> excluidos)
    {
        Transform actual = t;
        while (actual != null)
        {
            if (excluidos.Contains(actual))
                return true;
            actual = actual.parent;
        }

        return false;
    }

    private static void CalcularCeldasNecesarias(GrupoInfo g)
    {
        int ancho = Mathf.Max(1, Mathf.CeilToInt(g.boundsMundo.size.x / CellSize));
        int profundo = Mathf.Max(1, Mathf.CeilToInt(g.boundsMundo.size.z / CellSize));

        if (ancho > MaxCeldasPorLadoGrupo || profundo > MaxCeldasPorLadoGrupo)
        {
            Debug.LogWarning($"LaberintoBuilder: '{g.raiz.name}' mide {g.boundsMundo.size.x:F1}x{g.boundsMundo.size.z:F1}m, se limita a {MaxCeldasPorLadoGrupo}x{MaxCeldasPorLadoGrupo} celdas (puede quedar apretado).");
            ancho = Mathf.Min(ancho, MaxCeldasPorLadoGrupo);
            profundo = Mathf.Min(profundo, MaxCeldasPorLadoGrupo);
        }

        g.celdasAncho = ancho;
        g.celdasProfundo = profundo;
    }

    private static void CalcularTamanoGrid(List<GrupoInfo> grupos, out int gridW, out int gridH)
    {
        int areaCeldas = 0;
        foreach (GrupoInfo g in grupos)
            areaCeldas += g.celdasAncho * g.celdasProfundo;

        int areaConPasillos = Mathf.CeilToInt(areaCeldas * OverheadFactorPasillos);
        int lado = Mathf.Max(4, Mathf.CeilToInt(Mathf.Sqrt(areaConPasillos)));

        gridW = lado;
        gridH = lado;
    }

    // ---------------- Empaquetado (First-Fit Decreasing) ----------------

    private static RectInt? UbicarGrupoEnGrid(GrupoInfo g, int[,] propietario, int gridW, int gridH)
    {
        List<(int w, int d)> orientaciones = new List<(int w, int d)> { (g.celdasAncho, g.celdasProfundo) };
        if (PermitirRotacion90EnGrupos && g.celdasAncho != g.celdasProfundo)
            orientaciones.Add((g.celdasProfundo, g.celdasAncho));

        foreach ((int w, int d) in orientaciones)
        {
            for (int y = 0; y <= gridH - d; y++)
            {
                for (int x = 0; x <= gridW - w; x++)
                {
                    if (RectLibre(propietario, x, y, w, d))
                        return new RectInt(x, y, w, d);
                }
            }
        }

        return null;
    }

    private static bool RectLibre(int[,] propietario, int x, int y, int w, int d)
    {
        for (int i = x; i < x + w; i++)
            for (int j = y; j < y + d; j++)
                if (propietario[i, j] != -1)
                    return false;

        return true;
    }

    private static void MarcarOcupado(int[,] propietario, RectInt rect, int nodo)
    {
        for (int i = rect.x; i < rect.x + rect.width; i++)
            for (int j = rect.y; j < rect.y + rect.height; j++)
                propietario[i, j] = nodo;
    }

    // ---------------- Topologia del laberinto ----------------

    private static List<Arista> ConstruirAristasCandidatas(int[,] propietario, int gridW, int gridH)
    {
        List<Arista> aristas = new List<Arista>();

        for (int x = 0; x < gridW; x++)
        {
            for (int y = 0; y < gridH; y++)
            {
                int actual = propietario[x, y];

                if (x + 1 < gridW)
                {
                    int vecino = propietario[x + 1, y];
                    if (vecino != actual)
                        aristas.Add(new Arista { nodoA = actual, nodoB = vecino, x = x, y = y, esVertical = true });
                }

                if (y + 1 < gridH)
                {
                    int vecino = propietario[x, y + 1];
                    if (vecino != actual)
                        aristas.Add(new Arista { nodoA = actual, nodoB = vecino, x = x, y = y, esVertical = false });
                }
            }
        }

        return aristas;
    }

    private static void Mezclar<T>(List<T> lista, System.Random rng)
    {
        for (int i = lista.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            T temp = lista[i];
            lista[i] = lista[j];
            lista[j] = temp;
        }
    }

    private static int UnionFindBuscar(int[] parent, int i)
    {
        while (parent[i] != i)
            i = parent[i];
        return i;
    }

    private static bool UnionFindUnir(int[] parent, int a, int b)
    {
        int raizA = UnionFindBuscar(parent, a);
        int raizB = UnionFindBuscar(parent, b);
        if (raizA == raizB)
            return false;

        parent[raizA] = raizB;
        return true;
    }

    // ---------------- Aplicacion a la escena ----------------

    private static Transform ObtenerOCrearRaizLaberinto()
    {
        GameObject existente = GameObject.Find(NombreRaizLaberinto);
        if (existente != null)
            return existente.transform;

        GameObject raiz = new GameObject(NombreRaizLaberinto);
        Undo.RegisterCreatedObjectUndo(raiz, "Crear raiz de laberinto");
        return raiz.transform;
    }

    private static Transform ObtenerOCrearHijo(Transform padre, string nombre)
    {
        for (int i = 0; i < padre.childCount; i++)
            if (padre.GetChild(i).name == nombre)
                return padre.GetChild(i);

        GameObject hijo = new GameObject(nombre);
        Undo.RegisterCreatedObjectUndo(hijo, "Crear " + nombre);
        hijo.transform.SetParent(padre, false);
        return hijo.transform;
    }

    private static Transform ObtenerHolderExcluidos(Transform raizLaberinto)
    {
        if (holderExcluidosSinMover != null)
            return holderExcluidosSinMover;

        holderExcluidosSinMover = ObtenerOCrearHijo(raizLaberinto, "ExcluidosSinMover");
        return holderExcluidosSinMover;
    }

    private static void AplicarGrupoALaberinto(GrupoInfo g, RectInt celdas, Vector3 origenGrid, float suelo1Y, Transform raizLaberinto)
    {
        // Desanidar primero cualquier excluido (muro/puerta/escalera) que viviera dentro de este
        // grupo, preservando su posicion de mundo exacta, para que NO se traslade junto al grupo.
        if (g.excluidosAnidados.Count > 0)
        {
            Transform holder = ObtenerHolderExcluidos(raizLaberinto);
            foreach (Transform excluido in g.excluidosAnidados)
                Undo.SetTransformParent(excluido, holder, "Desanidar excluido antes de mover laberinto");
        }

        Vector3 centroCeldaMundo = new Vector3(
            origenGrid.x + (celdas.x + celdas.width * 0.5f) * CellSize,
            suelo1Y + PisoConstruccionYOffset,
            origenGrid.z + (celdas.y + celdas.height * 0.5f) * CellSize);

        Transform piso = CrearPisoConstruccion(raizLaberinto, g.raiz.name, centroCeldaMundo, celdas);

        Undo.SetTransformParent(g.raiz, piso, "Reorganizar como laberinto");

        Vector3 delta = new Vector3(
            centroCeldaMundo.x - g.boundsMundo.center.x,
            suelo1Y - g.boundsMundo.min.y,
            centroCeldaMundo.z - g.boundsMundo.center.z);

        Undo.RecordObject(g.raiz, "Reorganizar como laberinto");
        g.raiz.position += delta;
    }

    private static Transform CrearPisoConstruccion(Transform raizLaberinto, string nombreGrupo, Vector3 centroMundo, RectInt celdas)
    {
        GameObject piso = GameObject.CreatePrimitive(PrimitiveType.Plane);
        Undo.RegisterCreatedObjectUndo(piso, "Crear piso de construccion");
        piso.name = "Piso_" + nombreGrupo;

        Collider colisionador = piso.GetComponent<Collider>();
        if (colisionador != null)
            UnityEngine.Object.DestroyImmediate(colisionador);

        piso.transform.SetParent(raizLaberinto, false);
        piso.transform.position = centroMundo;
        piso.transform.localScale = new Vector3(celdas.width * CellSize / 10f, 1f, celdas.height * CellSize / 10f);

        AplicarMaterialCiclico(piso, MaterialPathsPiso, ref siguienteMaterialPiso);

        return piso.transform;
    }

    private static void AplicarMaterialCiclico(GameObject go, string[] paletaPaths, ref int indice)
    {
        if (paletaPaths.Length == 0)
            return;

        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer == null)
            return;

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(paletaPaths[indice % paletaPaths.Length]);
        indice++;

        if (mat != null)
            renderer.sharedMaterial = mat;
    }

    // ---------------- Muros ----------------

    private static int ConstruirMurosVerticales(bool[,] wallV, int gridW, Vector3 origenGrid, float suelo1Y, Transform raizEstructura, System.Random rng)
    {
        int creados = 0;
        int anchoBoundaries = wallV.GetLength(0);
        int alto = wallV.GetLength(1);

        for (int x = 0; x < anchoBoundaries; x++)
        {
            int y = 0;
            while (y < alto)
            {
                if (!wallV[x, y]) { y++; continue; }

                int yInicio = y;
                while (y < alto && wallV[x, y]) y++;
                int yFin = y; // exclusivo

                float largo = (yFin - yInicio) * CellSize;
                float centroZ = origenGrid.z + (yInicio + (yFin - yInicio) * 0.5f) * CellSize;
                float centroX = origenGrid.x + (x + 1) * CellSize;

                CrearSegmentoMuro(new Vector3(centroX, suelo1Y, centroZ), largo, alongX: false, padre: raizEstructura, rng: rng);
                creados++;
            }
        }

        return creados;
    }

    private static int ConstruirMurosHorizontales(bool[,] wallH, int gridH, Vector3 origenGrid, float suelo1Y, Transform raizEstructura, System.Random rng)
    {
        int creados = 0;
        int ancho = wallH.GetLength(0);
        int altoBoundaries = wallH.GetLength(1);

        for (int y = 0; y < altoBoundaries; y++)
        {
            int x = 0;
            while (x < ancho)
            {
                if (!wallH[x, y]) { x++; continue; }

                int xInicio = x;
                while (x < ancho && wallH[x, y]) x++;
                int xFin = x; // exclusivo

                float largo = (xFin - xInicio) * CellSize;
                float centroX = origenGrid.x + (xInicio + (xFin - xInicio) * 0.5f) * CellSize;
                float centroZ = origenGrid.z + (y + 1) * CellSize;

                CrearSegmentoMuro(new Vector3(centroX, suelo1Y, centroZ), largo, alongX: true, padre: raizEstructura, rng: rng);
                creados++;
            }
        }

        return creados;
    }

    private static int ConstruirPerimetro(int gridW, int gridH, Vector3 origenGrid, float suelo1Y, Transform raizEstructura)
    {
        float anchoTotal = gridW * CellSize;
        float altoTotal = gridH * CellSize;

        CrearSegmentoMuroSimple(new Vector3(origenGrid.x, suelo1Y, origenGrid.z + altoTotal * 0.5f), altoTotal, alongX: false, padre: raizEstructura);
        CrearSegmentoMuroSimple(new Vector3(origenGrid.x + anchoTotal, suelo1Y, origenGrid.z + altoTotal * 0.5f), altoTotal, alongX: false, padre: raizEstructura);
        CrearSegmentoMuroSimple(new Vector3(origenGrid.x + anchoTotal * 0.5f, suelo1Y, origenGrid.z), anchoTotal, alongX: true, padre: raizEstructura);
        CrearSegmentoMuroSimple(new Vector3(origenGrid.x + anchoTotal * 0.5f, suelo1Y, origenGrid.z + altoTotal), anchoTotal, alongX: true, padre: raizEstructura);

        return 4;
    }

    private static void CrearSegmentoMuroSimple(Vector3 centro, float largo, bool alongX, Transform padre)
    {
        GameObject muro = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(muro, "Crear muro perimetral de laberinto");
        muro.name = "MuroPerimetral";
        muro.transform.SetParent(padre, false);
        muro.transform.localScale = alongX
            ? new Vector3(largo, WallHeight, WallThickness)
            : new Vector3(WallThickness, WallHeight, largo);
        muro.transform.position = new Vector3(centro.x, centro.y + WallHeight * 0.5f, centro.z);

        AplicarMaterialCiclico(muro, MaterialPathsMuro, ref siguienteMaterialMuro);
    }

    private static void CrearSegmentoMuro(Vector3 centro, float largo, bool alongX, Transform padre, System.Random rng)
    {
        GameObject muro = null;

        bool intentarPrefab = UsarPrefabsDeParedParaVariedad
            && Mathf.Approximately(largo, CellSize)
            && rng.NextDouble() < WallPrefabChance;

        if (intentarPrefab)
        {
            string path = PrefabsDeParedConVentana[rng.Next(PrefabsDeParedConVentana.Length)];
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset != null)
            {
                muro = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                Undo.RegisterCreatedObjectUndo(muro, "Crear muro de laberinto");
                muro.transform.SetParent(padre, false);
                AjustarEscalaParaCubrir(muro, largo, WallHeight, alongX);
            }
        }

        if (muro == null)
        {
            muro = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(muro, "Crear muro de laberinto");
            muro.transform.SetParent(padre, false);
            muro.transform.localScale = alongX
                ? new Vector3(largo, WallHeight, WallThickness)
                : new Vector3(WallThickness, WallHeight, largo);
            AplicarMaterialCiclico(muro, MaterialPathsMuro, ref siguienteMaterialMuro);
        }

        muro.name = "Muro";
        muro.transform.position = new Vector3(centro.x, centro.y + WallHeight * 0.5f, centro.z);
    }

    private static void AjustarEscalaParaCubrir(GameObject go, float largo, float altura, bool alongX)
    {
        Bounds b = CalcularBoundsMundo(go.transform, new List<Transform>());
        if (b.size.x <= 0.001f || b.size.y <= 0.001f || b.size.z <= 0.001f)
            return;

        Vector3 escala = go.transform.localScale;
        float factorAncho = largo / (alongX ? b.size.x : b.size.z);
        float factorAlto = altura / b.size.y;

        go.transform.localScale = alongX
            ? new Vector3(escala.x * factorAncho, escala.y * factorAlto, escala.z)
            : new Vector3(escala.x, escala.y * factorAlto, escala.z * factorAncho);
    }

    // ---------------- Resumen ----------------

    private static void LogResumen(bool dryRun, int gruposDetectados, int excluidosTopNivel, int desanidados, int movidos, int noUbicados, int gridW, int gridH, int murosCreados)
    {
        string modo = dryRun ? "DRY RUN (no se modifico la escena)" : "APLICADO";
        Debug.Log(
            $"LaberintoBuilder [{modo}]: grid {gridW}x{gridH} celdas de {CellSize}m. " +
            $"Grupos detectados: {gruposDetectados} | movidos: {movidos} | no ubicados: {noUbicados} | " +
            $"excluidos (muro/puerta/escalera) de nivel superior: {excluidosTopNivel} | excluidos anidados desanidados: {desanidados} | " +
            $"muros creados: {murosCreados}. " +
            "Recuerda re-bakear el NavMesh (NavMeshSurface) y revisar WayPoints/spawns tras mover el contenido.");
    }
}
