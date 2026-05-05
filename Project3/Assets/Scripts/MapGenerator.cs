using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural multi-floor tunnel generator for Unity.
/// Designed to be populated with uModeler block prefabs (1x1x1 cubes whose
/// pivot sits at the bottom-center).
///
/// Behavior:
///   - Builds <floorCount> stacked floors of tunnels.
///   - Lowest floor uses the thinnest tunnels; each floor above is wider/more
///     spacious (still feeling like tunnels, not rooms).
///   - Generates a maze per floor via a recursive backtracker that is biased
///     toward STRAIGHT runs, knocks out extra walls to create loops, and
///     prunes dead ends so the result reads as a network of tunnels rather
///     than a maze.
///   - Picks one staircase cell on each non-top floor that connects to a
///     position <stairRun> blocks away on the floor above.
///   - Carves a 1 x stairRun footprint of removed ceilings/floors so a long,
///     gently-sloped stair prefab can pass cleanly between floors.
///   - Uses a separate prefab per floor for floor / wall / ceiling tiles so
///     each floor can have its own texture or material.
///
/// Setup:
///   1. In uModeler, build a 1x1x1 block prefab for each role: FloorBlock,
///      WallBlock, CeilingBlock. Duplicate each one and apply a different
///      material/texture for every floor.
///      The StaircaseBlock should be 1 x floorVerticalSpacing x stairRun
///      (default 1 x 4 x 4) with the steps modeled rising along +Z, the
///      pivot at the bottom-center of the entry cell.
///   2. Drop this script onto an empty GameObject in your scene.
///   3. Assign the prefab arrays in the inspector (one entry per floor).
///   4. Press Play, or right-click the component header and pick "Generate".
/// </summary>
public class TunnelMapGenerator : MonoBehaviour
{
    [Header("uModeler Block Prefabs (one entry per floor, lowest -> highest)")]
    public GameObject[] floorBlockPrefabs;
    public GameObject[] wallBlockPrefabs;
    public GameObject[] ceilingBlockPrefabs;

    [Header("Staircase Prefabs (one entry per transition, optional)")]
    [Tooltip("One staircase prefab per floor transition. If left empty, falls back to staircaseBlockPrefab below.")]
    public GameObject[] staircaseBlockPrefabs;
    [Tooltip("Fallback staircase prefab used when staircaseBlockPrefabs has no entry for a transition.")]
    public GameObject staircaseBlockPrefab;

    [Header("Map Dimensions")]
    public int floorCount = 4;
    [Tooltip("Tunnel grid size in blocks. Larger = bigger maze.")]
    public int gridWidth = 31;
    public int gridDepth = 31;
    public float blockSize = 1f;
    [Tooltip("World-space vertical distance between two floors. Should match the stair prefab's height.")]
    public float floorVerticalSpacing = 4f;
    [Tooltip("Height in world units of the wall prefab. Use 1 for stacked 1x1x1 walls, or set to floorVerticalSpacing if you've made a tall wall prefab (e.g. 1x4x1) for faster generation.")]
    public float wallPrefabHeight = 1f;

    [Header("Tunnel Width Per Floor (lowest -> highest)")]
    [Tooltip("Width in blocks of the tunnels on each floor. Index 0 is the lowest floor and should be the smallest value.")]
    public int[] tunnelWidthPerFloor = new int[] { 1, 2, 3, 3 };

    [Header("Tunnel Shape")]
    [Range(0f, 1f)]
    [Tooltip("Probability the carver continues in the same direction when possible. Higher = longer straight tunnels.")]
    public float straightnessBias = 0.75f;

    [Range(0f, 0.6f)]
    [Tooltip("Chance per wall to be removed after the maze is built. Higher = more loops / multiple routes.")]
    public float loopChance = 0.30f;

    [Range(0, 8)]
    [Tooltip("How many sweeps to seal off dead-end cells. Higher = fewer stubby branches, more through-tunnels.")]
    public int deadEndPruningPasses = 3;

    [Header("Staircase Geometry")]
    [Tooltip("How many blocks long the stair run is (horizontal). Should match the depth of your staircase prefab.")]
    public int stairRun = 4;

    [Header("Random Seed")]
    [Tooltip("Off = same map every generation (controlled by Seed). On = new map every time.")]
    public bool useRandomSeed = false;
    public int seed = 12345;

    [Tooltip("If true, Generate() runs automatically on Play. Turn off after you've saved a map you want to keep.")]
    public bool generateOnPlay = true;

    // -- internal state ----------------------------------------------------
    private bool[,,] walkable;          // [floor, x, z]
    private Vector2Int[] stairCells;    // entry cell on the LOWER floor for each transition
    private int[] stairDirections;      // 0=+Z, 1=+X, 2=-Z, 3=-X
    private System.Random rng;

    private static readonly int[] DX = { 0, 1, 0, -1 };
    private static readonly int[] DZ = { 1, 0, -1, 0 };

    void Start()
    {
        if (generateOnPlay) Generate();
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        ClearChildren();
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        ClearChildren();

        if (useRandomSeed) seed = System.Environment.TickCount;
        rng = new System.Random(seed);

        if (tunnelWidthPerFloor == null || tunnelWidthPerFloor.Length < floorCount)
        {
            Debug.LogError("tunnelWidthPerFloor must have at least floorCount entries.");
            return;
        }

        walkable = new bool[floorCount, gridWidth, gridDepth];
        stairCells = new Vector2Int[floorCount];
        stairDirections = new int[floorCount];

        // 1. Carve each floor as its own maze with extra loops, then prune dead ends.
        for (int f = 0; f < floorCount; f++)
            CarveFloor(f, Mathf.Max(1, tunnelWidthPerFloor[f]));

        // 2. Decide where staircases go, choose a direction for the run, and
        //    carve the stair footprint plus landings on both floors.
        for (int f = 0; f < floorCount - 1; f++)
        {
            stairCells[f] = PickStairCell(f);
            stairDirections[f] = ChooseStairDirection(stairCells[f]);
            CarveStairOpening(f, stairCells[f], stairDirections[f], false); // lower
            CarveStairOpening(f + 1, stairCells[f], stairDirections[f], true);  // upper
        }

        // 3. Instantiate floor / wall / ceiling tiles.
        for (int f = 0; f < floorCount; f++)
            BuildFloorBlocks(f);

        // 4. Drop the staircase prefabs into the openings.
        for (int f = 0; f < floorCount - 1; f++)
            PlaceStaircase(f, stairCells[f]);
    }

    // ====================================================================
    // Maze carving
    // ====================================================================
    private void CarveFloor(int floorIndex, int tunnelWidth)
    {
        int cellStep = tunnelWidth + 1;
        int cellsX = (gridWidth - 1) / cellStep;
        int cellsZ = (gridDepth - 1) / cellStep;
        if (cellsX < 2 || cellsZ < 2) { Debug.LogWarning("Grid too small for tunnel width on floor " + floorIndex); return; }

        bool[,] visited = new bool[cellsX, cellsZ];
        bool[,,] edge = new bool[cellsX, cellsZ, 4];

        // ---- Recursive backtracker with straightness bias ------------------
        Stack<Vector2Int> posStack = new Stack<Vector2Int>();
        Stack<int> dirStack = new Stack<int>();
        Vector2Int start = new Vector2Int(rng.Next(cellsX), rng.Next(cellsZ));
        visited[start.x, start.y] = true;
        posStack.Push(start);
        dirStack.Push(-1);

        while (posStack.Count > 0)
        {
            Vector2Int cur = posStack.Peek();
            int prevDir = dirStack.Peek();

            List<int> dirs = new List<int>(4);
            for (int d = 0; d < 4; d++)
            {
                int nx = cur.x + DX[d], nz = cur.y + DZ[d];
                if (nx >= 0 && nx < cellsX && nz >= 0 && nz < cellsZ && !visited[nx, nz])
                    dirs.Add(d);
            }
            if (dirs.Count == 0) { posStack.Pop(); dirStack.Pop(); continue; }

            int chosenDir;
            if (prevDir >= 0 && dirs.Contains(prevDir) && rng.NextDouble() < straightnessBias)
                chosenDir = prevDir;
            else
                chosenDir = dirs[rng.Next(dirs.Count)];

            int ax = cur.x + DX[chosenDir], az = cur.y + DZ[chosenDir];
            visited[ax, az] = true;
            edge[cur.x, cur.y, chosenDir] = true;
            edge[ax, az, (chosenDir + 2) % 4] = true;
            posStack.Push(new Vector2Int(ax, az));
            dirStack.Push(chosenDir);
        }

        // ---- Knock out extra walls so multiple routes exist ----------------
        for (int x = 0; x < cellsX; x++)
            for (int z = 0; z < cellsZ; z++)
            {
                if (!visited[x, z]) continue;
                for (int d = 0; d < 2; d++)
                {
                    if (edge[x, z, d]) continue;
                    int nx = x + DX[d], nz = z + DZ[d];
                    if (nx < 0 || nx >= cellsX || nz < 0 || nz >= cellsZ) continue;
                    if (!visited[nx, nz]) continue;
                    if (rng.NextDouble() < loopChance)
                    {
                        edge[x, z, d] = true;
                        edge[nx, nz, (d + 2) % 4] = true;
                    }
                }
            }

        // ---- Seal off dead ends so the result reads as tunnels -------------
        for (int pass = 0; pass < deadEndPruningPasses; pass++)
        {
            bool anyChanged = false;
            for (int x = 0; x < cellsX; x++)
                for (int z = 0; z < cellsZ; z++)
                {
                    if (!visited[x, z]) continue;
                    int openCount = 0; int onlyDir = -1;
                    for (int d = 0; d < 4; d++)
                        if (edge[x, z, d]) { openCount++; onlyDir = d; }
                    if (openCount <= 1)
                    {
                        if (onlyDir >= 0)
                        {
                            int nx = x + DX[onlyDir], nz = z + DZ[onlyDir];
                            edge[x, z, onlyDir] = false;
                            if (nx >= 0 && nx < cellsX && nz >= 0 && nz < cellsZ)
                                edge[nx, nz, (onlyDir + 2) % 4] = false;
                        }
                        visited[x, z] = false;
                        anyChanged = true;
                    }
                }
            if (!anyChanged) break;
        }

        // ---- Rasterize cells/edges into the block grid ---------------------
        for (int cx = 0; cx < cellsX; cx++)
            for (int cz = 0; cz < cellsZ; cz++)
            {
                if (!visited[cx, cz]) continue;
                int bx0 = 1 + cx * cellStep;
                int bz0 = 1 + cz * cellStep;
                CarveRect(floorIndex, bx0, bz0, tunnelWidth, tunnelWidth);
                if (cx + 1 < cellsX && edge[cx, cz, 1])
                    CarveRect(floorIndex, bx0 + tunnelWidth, bz0, 1, tunnelWidth);
                if (cz + 1 < cellsZ && edge[cx, cz, 0])
                    CarveRect(floorIndex, bx0, bz0 + tunnelWidth, tunnelWidth, 1);
            }
    }

    private void CarveRect(int f, int x0, int z0, int sx, int sz)
    {
        for (int x = x0; x < x0 + sx; x++)
            for (int z = z0; z < z0 + sz; z++)
                if (x >= 0 && x < gridWidth && z >= 0 && z < gridDepth)
                    walkable[f, x, z] = true;
    }

    // ====================================================================
    // Staircase placement
    // ====================================================================
    private Vector2Int PickStairCell(int floorIndex)
    {
        int margin = stairRun + 2;
        margin = Mathf.Min(margin, Mathf.Min(gridWidth, gridDepth) / 3);
        for (int attempts = 0; attempts < 500; attempts++)
        {
            int x = rng.Next(margin, gridWidth - margin);
            int z = rng.Next(margin, gridDepth - margin);
            if (walkable[floorIndex, x, z]) return new Vector2Int(x, z);
        }
        for (int x = margin; x < gridWidth - margin; x++)
            for (int z = margin; z < gridDepth - margin; z++)
                if (walkable[floorIndex, x, z]) return new Vector2Int(x, z);
        return new Vector2Int(gridWidth / 2, gridDepth / 2);
    }

    private int ChooseStairDirection(Vector2Int cell)
    {
        // Pick a direction where both the run end and the back-landing fit on the grid.
        List<int> ok = new List<int>(4);
        for (int d = 0; d < 4; d++)
        {
            int endX = cell.x + DX[d] * stairRun;
            int endZ = cell.y + DZ[d] * stairRun;
            int backX = cell.x - DX[d];
            int backZ = cell.y - DZ[d];
            if (endX >= 1 && endX < gridWidth - 1 && endZ >= 1 && endZ < gridDepth - 1 &&
                backX >= 1 && backX < gridWidth - 1 && backZ >= 1 && backZ < gridDepth - 1)
                ok.Add(d);
        }
        return ok.Count == 0 ? 0 : ok[rng.Next(ok.Count)];
    }

    private void CarveStairOpening(int f, Vector2Int start, int dir, bool isUpperFloor)
    {
        // 1. Carve the stair footprint walkable so adjacent walls render correctly.
        for (int step = 0; step < stairRun; step++)
        {
            int cx = start.x + DX[dir] * step;
            int cz = start.y + DZ[dir] * step;
            if (cx >= 0 && cx < gridWidth && cz >= 0 && cz < gridDepth)
                walkable[f, cx, cz] = true;
        }

        // 2. Carve a small landing so the stair connects to the maze:
        //    - lower floor: a 3x3 patch one step BEHIND the entry (approach side)
        //    - upper floor: a 3x3 patch one step BEYOND the exit (step-off side)
        int landingStep = isUpperFloor ? stairRun : -1;
        int lx = start.x + DX[dir] * landingStep;
        int lz = start.y + DZ[dir] * landingStep;
        for (int ox = -1; ox <= 1; ox++)
            for (int oz = -1; oz <= 1; oz++)
            {
                int x = lx + ox, z = lz + oz;
                if (x > 0 && x < gridWidth - 1 && z > 0 && z < gridDepth - 1)
                    walkable[f, x, z] = true;
            }
    }

    // ====================================================================
    // Block instantiation
    // ====================================================================
    private void BuildFloorBlocks(int floorIndex)
    {
        float y = floorIndex * floorVerticalSpacing;
        GameObject parentGO = new GameObject("Floor_" + floorIndex);
        Transform parent = parentGO.transform;
        parent.SetParent(transform, false);

        // Suppress per-object editor updates during the spawn loop.
        parentGO.SetActive(false);

        float wallH = Mathf.Max(0.001f, wallPrefabHeight);
        int wallStacks = Mathf.Max(1, Mathf.RoundToInt(floorVerticalSpacing / wallH));

        GameObject floorPrefab = PickPrefab(floorBlockPrefabs, floorIndex);
        GameObject wallPrefab = PickPrefab(wallBlockPrefabs, floorIndex);
        GameObject ceilingPrefab = PickPrefab(ceilingBlockPrefabs, floorIndex);

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridDepth; z++)
            {
                if (walkable[floorIndex, x, z])
                {
                    if (!IsStairOpeningFromBelow(floorIndex, x, z))
                        SpawnBlock(floorPrefab, x, y, z, parent);

                    if (!IsStairOpeningGoingUp(floorIndex, x, z))
                        SpawnBlock(ceilingPrefab, x, y + floorVerticalSpacing - blockSize, z, parent);
                }
                else if (HasWalkableNeighbor(floorIndex, x, z))
                {
                    for (int h = 0; h < wallStacks; h++)
                        SpawnBlock(wallPrefab, x, y + h * wallH, z, parent);
                }
            }
        }

        parentGO.SetActive(true);
    }

    private bool IsStairOpeningGoingUp(int f, int x, int z)
    {
        if (f >= floorCount - 1) return false;
        return IsInStairFootprint(stairCells[f], stairDirections[f], x, z);
    }

    private bool IsStairOpeningFromBelow(int f, int x, int z)
    {
        if (f == 0) return false;
        return IsInStairFootprint(stairCells[f - 1], stairDirections[f - 1], x, z);
    }

    private bool IsInStairFootprint(Vector2Int start, int dir, int x, int z)
    {
        for (int step = 0; step < stairRun; step++)
            if (start.x + DX[dir] * step == x && start.y + DZ[dir] * step == z) return true;
        return false;
    }

    private bool HasWalkableNeighbor(int f, int x, int z)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0) continue;
                int nx = x + dx, nz = z + dz;
                if (nx >= 0 && nx < gridWidth && nz >= 0 && nz < gridDepth && walkable[f, nx, nz])
                    return true;
            }
        return false;
    }

    private void SpawnBlock(GameObject prefab, int gx, float worldY, int gz, Transform parent)
    {
        if (prefab == null) return;
        Vector3 pos = transform.position + new Vector3(gx * blockSize, worldY, gz * blockSize);
        Instantiate(prefab, pos, Quaternion.identity, parent);
    }

    private void PlaceStaircase(int floorIndex, Vector2Int cell)
    {
        GameObject prefab = PickPrefab(staircaseBlockPrefabs, floorIndex, staircaseBlockPrefab);
        if (prefab == null) return;

        float y = floorIndex * floorVerticalSpacing;
        Vector3 pos = transform.position + new Vector3(cell.x * blockSize, y, cell.y * blockSize);

        int dir = stairDirections[floorIndex];
        float[] yaw = { 0f, 90f, 180f, 270f };
        Quaternion rot = Quaternion.Euler(0, yaw[dir], 0);

        GameObject stair = Instantiate(prefab, pos, rot, transform);
        stair.name = "Staircase_F" + floorIndex + "_to_F" + (floorIndex + 1);
    }

    // ====================================================================
    // Utility
    // ====================================================================
    private GameObject PickPrefab(GameObject[] arr, int index, GameObject fallback = null)
    {
        if (arr != null && arr.Length > index && arr[index] != null)
            return arr[index];
        return fallback;
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
                Destroy(transform.GetChild(i).gameObject);
            else
                DestroyImmediate(transform.GetChild(i).gameObject);
        }
    }
}