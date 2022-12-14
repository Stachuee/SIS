using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class MapManager : MonoBehaviour
{
    public static MapManager mapManager;

    [SerializeField]
    MeshFilter terrainMesh;
    [SerializeField]
    MeshRenderer terrainMeshRenderer;

    [SerializeField]
    Texture heatMap;

    [SerializeField]
    public Vector2Int mapSize;
    [SerializeField]
    public float blockSize;
    
    Texture2D texture;


    public NativeArray<bool> map;


    public NativeArray<float4> mapPheromones;
    public NativeArray<int> crowdedness;

    private void Awake()
    {
        mapManager = this;

        mapPheromones = new NativeArray<float4>(mapSize.x * mapSize.y, Allocator.Persistent);
        map = new NativeArray<bool>(mapSize.x * mapSize.y, Allocator.Persistent);
        crowdedness = new NativeArray<int>(mapSize.x * mapSize.y, Allocator.Persistent);
        GenerateMap();
        DrawMap();
    }

    private void Start()
    {
        texture = new Texture2D(mapSize.x, mapSize.y, TextureFormat.ARGB32, false);
        texture.filterMode = FilterMode.Point;
        InvokeRepeating("TextureFromPheromonMap", 0.1f, 0.1f);
        InvokeRepeating("GetCrowdedness", 0f, 0.1f);

        for (int i = 0; i < mapPheromones.Length; i++)
        {
            mapPheromones[i] = new float4(0,0,0,0);
        }
    }
    private void OnDestroy()
    {
        mapPheromones.Dispose();
        map.Dispose();
        crowdedness.Dispose();
    }

    private void Update()
    {

    }


    [SerializeField]
    GameObject[] buildings;
    private void GenerateMap()
    {
        for (int x = 0; x < mapSize.x; x++)
        {
            for (int y = 0; y < mapSize.y; y++)
            {
                map[x + y * mapSize.x] = true;
                //if (x % 3 == 0 || y % 3 == 0) map[x + y * mapSize.x] = true;
                //else map[x + y * mapSize.x] = false;
            }
        }
        char[,] mapChar = new char[mapSize.x, mapSize.y];

        int2 cityCenter = new int2(UnityEngine.Random.Range((mapSize.x / 2) - (mapSize.x / 10), (mapSize.x / 2) + (mapSize.x / 10)), UnityEngine.Random.Range((mapSize.y / 2) - (mapSize.y / 10), (mapSize.y / 2) + (mapSize.y / 10)));

        for (int x = 1; x < mapSize.x - 1; x++)
        {
            for (int y = 1; y < mapSize.y - 1; y++)
            {
                if (mapChar[x, y] != 'b' && mapChar[x - 1, y] != 'b' && mapChar[x, y - 1] != 'b' && mapChar[x - 1, y - 1] != 'b' && mapChar[x, y] != 'r')
                {
                    if (UnityEngine.Random.Range(0f, 1f) > 0.9f) continue;
                    float distanceToCenter = Vector2.Distance(new Vector2(cityCenter.x, cityCenter.y), new Vector2(x, y));
                    float building = 1 - (distanceToCenter / (Mathf.Max(mapSize.x, mapSize.y)));

                    int2 buildingSize = new int2(Mathf.Clamp(Mathf.Clamp(Mathf.CeilToInt(UnityEngine.Random.Range(2, 5) * building), 2, 5), 0, mapSize.x - x), Mathf.Clamp(Mathf.Clamp(Mathf.CeilToInt(UnityEngine.Random.Range(2, 5) * building), 2, 5), 0, mapSize.y - y));
                    for(int z = 0; z < buildingSize.x; z++)
                    {
                        for(int w = 0; w < buildingSize.y; w++)
                        {
                            mapChar[x + z, y + w] = 'b'; 
                        }
                    }
                }
            }
        }

        if (cityCenter.x - (mapSize.x / 2) > 0) for (int i = cityCenter.x; i >= 0; i--) for (int j = 0; j <= 1; j++) mapChar[cityCenter.x - i, cityCenter.y + j] = 'r';
        else for (int i = cityCenter.x; i < mapSize.x; i++) for (int j = 0; j <= 1; j++) mapChar[i, cityCenter.y + j] = 'r';
        if (cityCenter.y - (mapSize.y / 2) > 0) for (int i = cityCenter.y; i >= 0; i--) for (int j = 0; j <= 1; j++) mapChar[cityCenter.x + j, cityCenter.y - i] = 'r';
        else for (int i = cityCenter.y; i < mapSize.y; i++) for (int j = 0; j <= 1; j++) mapChar[cityCenter.x + j, i] = 'r';


        for (int x = 1; x < mapSize.x - 1; x++)
        {
            for (int y = 1; y < mapSize.y - 1; y++)
            {
                if(mapChar[x, y] == 'b')
                {
                    int size = math.min(SetBuilding(x, y, mapChar), 5);
                    for (int i = 0; i < size - 1; i++) for (int j = 0; j < size - 1; j++) mapChar[x + i, y + j] = 'o';
                    GameObject temp = Instantiate(buildings[size - 2], new Vector3(x * blockSize - mapSize.x * blockSize / 2,0, y * blockSize - mapSize.y * blockSize / 2) , quaternion.identity); // new Vector3((x + size / 2) * blockSize - blockSize / 2, 0, (y + size / 2) * blockSize - blockSize / 2) - new Vector3((mapSize.x * blockSize) / 2 ,0, (mapSize.y * blockSize) / 2)
                    temp.transform.position += new Vector3(1, 0, 1) * (blockSize * (size - 1)) / 2;
                    //temp.transform.position += new Vector3(1,0,1) * (size - 1) * blockSize;
                    temp.transform.localScale *= blockSize; // * blockSize 
                }
            }
        }

        for (int x = 0; x < mapSize.x; x++)
        {
            for (int y = 0; y < mapSize.y; y++)
            {
                if (!(x > 0 && y > 0 && x < mapSize.x - 1 && y < mapSize.y - 1)) map[x + y * mapSize.x] = false;
                else if(mapChar[x,y] == 'r') map[x + y * mapSize.x] = true;
                else if (mapChar[x, y] == 'b' || mapChar[x, y] == 'o') map[x + y * mapSize.x] = false;
            }
        }
    }

    int SetBuilding(int x, int y, char[,] mapChar)
    {
        int size = 0;
        bool fits = true;
        do
        {
            size++;
            for (int i = x; i < x + size; i++)
            {
                if (i > mapSize.x - 1 || y + size - 1 > mapSize.y - 1 || mapChar[i, y + size - 1] != 'b') fits = false;
            }
            for(int j = y; j < y + size; j++)
            {
                if (j > mapSize.y - 1 || x + size - 1 > mapSize.x - 1 || mapChar[x + size - 1, j] != 'b') fits = false;
            }
        } 
        while (fits);

        return size;
    }


    [SerializeField] bool drawCrowdness;
    [SerializeField] bool drawAlertness;
    [SerializeField] bool drawHumanSmell;
    [SerializeField] bool drawBlood;

    [SerializeField] bool drawWalkable;

    void TextureFromPheromonMap()
    {

        NativeArray<float4> copyPheromones = new NativeArray<float4>(mapPheromones, Allocator.TempJob);
        NativeArray<float4> secondCopyPheromones = new NativeArray<float4>(mapPheromones, Allocator.TempJob);

        CalculatePheromonesSpread calc = new CalculatePheromonesSpread()
        {
            pheromones = copyPheromones,
            copyPheromones = secondCopyPheromones,
            mapSizeX = mapSize.x,
            mapSizeY = mapSize.y,
            pheromonTransmitionLoss = new float4(0.1f, 0.1f, 0.1f, 0.02f),
            delta = 0.1f,
            walkable = map,
        };
        calc.Run();

        for (int i = 0; i < copyPheromones.Length; i++)
        {
            mapPheromones[i] = copyPheromones[i];
        }

        copyPheromones.Dispose();
        secondCopyPheromones.Dispose();



        if (drawBlood || drawCrowdness || drawAlertness || drawHumanSmell)
        {
            for (int x = 0; x < mapSize.x; x++)
            {
                for (int y = 0; y < mapSize.y; y++)
                {

                    Color color = new Color(0, 0, 0, 1);
                    if (drawCrowdness)
                    {
                        color.g = mapPheromones[x + y * mapSize.y].x;
                    }
                    else if (drawAlertness)
                    {
                        color.g = mapPheromones[x + y * mapSize.y].y;
                    }
                    else if (drawHumanSmell)
                    {
                        color.g = mapPheromones[x + y * mapSize.y].z;
                    }
                    else if (drawBlood)
                    {
                        color.g = mapPheromones[x + y * mapSize.y].w;
                    }

                    texture.SetPixel(x, y, color);
                }
            }
        }
        else if (drawWalkable)
        {
            for (int x = 0; x < mapSize.x; x++)
            {
                for (int y = 0; y < mapSize.y; y++)
                {
                    Color color = new Color(0, 0, 0, 1);
                    if (map[x + y * mapSize.x]) color = Color.clear;
                    else color = Color.red;
                    texture.SetPixel(x, y, color);
                }
            }
        }
        else
        {
            for (int x = 0; x < mapSize.x; x++)
            {
                for (int y = 0; y < mapSize.y; y++)
                {
                    Color color = new Color(0, 0, 0, 0);
                    texture.SetPixel(x, y, color);
                }
            }
        }

        texture.Apply();
        heatMap = texture;
        terrainMeshRenderer.material.SetTexture("_BaseMap", texture);
    }

    public void DrawMap()
    {
        Mesh newterrainMesh = new Mesh();

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();

        int2 vertMapSize = new int2(mapSize.x + 1, mapSize.y + 1);

        for (int x = 0; x < vertMapSize.x; x++)
        {
            for (int y = 0; y < vertMapSize.y; y++)
            {
                verts.Add(new Vector3(x * blockSize + blockSize / 2 - (vertMapSize.x * blockSize) / 2, 1, y * blockSize + blockSize / 2 - (vertMapSize.y * blockSize) / 2));
                uvs.Add(new Vector2((float)x / (vertMapSize.x - 1), (float)y / (vertMapSize.y - 1)));
            }
        }

        List<int> triangles = new List<int>();

        for (int x = 0; x < vertMapSize.x - 1; x++)
        {
            for (int y = 0; y < vertMapSize.y - 1; y++)
            {
                triangles.Add(y + x * vertMapSize.y);
                triangles.Add((y + 1) + x * vertMapSize.y);
                triangles.Add(y + (x + 1) * vertMapSize.y);

                triangles.Add((y + 1) + x * vertMapSize.y);
                triangles.Add((y + 1) + (x + 1) * vertMapSize.y);
                triangles.Add(y + (x + 1) * vertMapSize.y);
            }
        }

        newterrainMesh.vertices = verts.ToArray();
        newterrainMesh.triangles = triangles.ToArray();
        newterrainMesh.uv = uvs.ToArray();

        terrainMesh.mesh = newterrainMesh;

    }
    void GetCrowdedness()
    {
        NativeArray<AIManager.Zombie> zombies = new NativeArray<AIManager.Zombie>(AIManager.aIManager.zombies.ToArray(), Allocator.TempJob);
        NativeArray<int> count = new NativeArray<int>(mapSize.x * mapSize.y, Allocator.TempJob);
        GetCrowdednessMap mapCrowdeness = new GetCrowdednessMap()
        {
            blockSize = blockSize,
            mapSizeX = mapSize.x,
            mapSizeY = mapSize.y,
            zombies = zombies,
            count = count
        };
        mapCrowdeness.Run();

        int maxCrowd = 0;
        for (int i = 0; i < mapSize.x * mapSize.y; i++)
        {
            crowdedness[i] = map[i] ? count[i] : int.MaxValue;
            if (maxCrowd < count[i]) maxCrowd = count[i];
        }

        zombies.Dispose();
        count.Dispose();
    }

    [BurstCompile]
    struct GetCrowdednessMap : IJob
    {
        [ReadOnly] public NativeArray<AIManager.Zombie> zombies;
        public NativeArray<int> count;
        public float blockSize;
        public int mapSizeX;
        public int mapSizeY;

        public void Execute()
        {
            for (int i = 0; i < zombies.Length; i++)
            {
                int2 pos = GetPositionOnGrid(zombies[i].position);
                count[pos.x + pos.y * mapSizeX]++;
            }
        }

        int2 GetPositionOnGrid(float2 pos)
        {
            return new int2((int)math.floor(pos.x / blockSize + mapSizeX / 2), (int)math.floor(pos.y / blockSize + mapSizeY / 2));
        }
    }

    [BurstCompile]
    struct CalculatePheromonesSpread : IJob
    {
        public NativeArray<float4> pheromones;
        public NativeArray<float4> copyPheromones;
        [ReadOnly] public NativeArray<bool> walkable;
        public int mapSizeX;
        public int mapSizeY;
        public float4 pheromonTransmitionLoss;
        public float delta;
        public void Execute()
        {
            float4 sumPheromones = new float4(0,0,0,0);
            float4 sumPheromonesAfter = new float4(0, 0, 0,0);

            for (int x = 0; x < mapSizeX; x++)
            {
                for (int y = 0; y < mapSizeY; y++)
                {
                    if (!walkable[x + y * mapSizeX]) continue;
                    float4 p = GetPheromones(x, y);
                    pheromones[x + y * mapSizeX] = p + delta * (GetAveragePheromones(x,y) - p);
                    sumPheromones += copyPheromones[x + y * mapSizeX];
                    sumPheromonesAfter += pheromones[x + y * mapSizeX];
                }
            }

            //sumPheromones /= (sumPheromonesAfter + 1);

            for (int x = 0; x < mapSizeX; x++)
            {
                for (int y = 0; y < mapSizeY; y++)
                {
                    pheromones[x + y * mapSizeX] *=  (1 - pheromonTransmitionLoss * delta); // sumPheromones *
                }
            }
        }
        public float4 GetAveragePheromones(int x, int y)
        {
            int neighCount = 0;
            float4 pheromones = new float4(0,0,0,0);

            if (x > 0 && walkable[x - 1 + y * mapSizeX]) // 
            {
                neighCount++;
                pheromones += GetPheromones(x - 1, y);
            }
            if (x < mapSizeX - 1) // 
            {
                neighCount++;
                pheromones += GetPheromones(x + 1, y);
            }
            if (y > 0 && walkable[x + (y - 1) * mapSizeX]) // 
            {
                neighCount++;
                pheromones += GetPheromones(x, y - 1);
            }
            if (y < mapSizeY - 1 && walkable[x + (y + 1) * mapSizeX]) //
            {
                neighCount++;
                pheromones += GetPheromones(x, y + 1);
            }
            return pheromones / neighCount;
        }

        public float4 GetPheromones(int x, int y)
        {
            return copyPheromones[x + y * mapSizeX];
        }
    }

    


    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(-mapSize.x, 0, mapSize.y) * blockSize / 2, new Vector3(mapSize.x, 0, mapSize.y) * blockSize / 2);
        Gizmos.DrawLine(new Vector3(mapSize.x, 0, mapSize.y) * blockSize / 2, new Vector3(mapSize.x, 0, -mapSize.y) * blockSize / 2);
        Gizmos.DrawLine(new Vector3(mapSize.x, 0, -mapSize.y) * blockSize / 2, new Vector3(-mapSize.x, 0, -mapSize.y) * blockSize / 2);
        Gizmos.DrawLine(new Vector3(-mapSize.x, 0, -mapSize.y) * blockSize / 2, new Vector3(-mapSize.x, 0, mapSize.y) * blockSize / 2);
    }
}
