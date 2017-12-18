using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Barebones.MasterServer;
using Barebones.Networking;

public class IOGamesModule : ServerModuleBehaviour
{
    public const string IsFirstRoomKey = "SpawnFirstRoom";
    public const string AssignPortKey = "AssignPort";

    public SceneField scene;
    public string roomName = "Battle-";
    public int maxPlayers = 2;
    public int playersAmountToCreateNewRoom = 1;
    public float countPlayersToCreateNewRoomDuration = 3;
    public int startPort = 1500;
    private RoomsModule roomsModule;
    private SpawnersModule spawnersModule;
    private uint sceneCounter = 0;
    private bool spawnFirstRoom = false;
    private bool spawnTaskDone = false;
    private int spawningPort = -1;
    private int portCounter = -1;
    private Queue<int> freePorts = new Queue<int>();

    private void Awake()
    {
        // Destroy this game object if it already exists
        if (DestroyIfExists())
        {
            Destroy(gameObject);
            return;
        };

        // Don't destroy the module on load
        DontDestroyOnLoad(gameObject);

        // Register dependencies
        AddDependency<RoomsModule>();
        AddDependency<SpawnersModule>();
        spawningPort = startPort;
        portCounter = startPort;
    }

    private void Start()
    {
        spawnTaskDone = true;
        if (Msf.Args.IsProvided(Msf.Args.Names.LoadScene))
            SceneManager.LoadScene(Msf.Args.LoadScene);
        else
            StartCoroutine(StartSpawnServerRoutine());
    }

    IEnumerator StartSpawnServerRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(countPlayersToCreateNewRoomDuration);
            if (roomsModule != null && spawnersModule != null && spawnTaskDone)
            {
                var rooms = roomsModule.GetAllRooms().ToList();
                if (rooms.Count == 0)
                {
                    sceneCounter = 0;
                    spawnFirstRoom = false;
                    SpawnScene();
                }
                else
                {
                    if (rooms.Count == 1)
                        sceneCounter = 0;
                    var totalPlayers = 0;
                    foreach (var room in rooms)
                        totalPlayers += room.OnlineCount;
                    if (Mathf.FloorToInt(totalPlayers / rooms.Count) >= playersAmountToCreateNewRoom)
                        SpawnScene();
                }
            }
        }
    }

    public override void Initialize(IServer server)
    {
        roomsModule = server.GetModule<RoomsModule>();
        spawnersModule = server.GetModule<SpawnersModule>();
    }

    void SpawnScene()
    {
        if (spawnersModule == null)
            return;

        var task = spawnersModule.Spawn(GenerateSceneSpawnInfo(scene));
        if (task != null)
        {
            spawnTaskDone = false;
            if (freePorts.Count > 0)
                spawningPort = freePorts.Dequeue();
            else
            {
                ++portCounter;
                spawningPort = portCounter;
            }
            task.WhenDone(t =>
            {
                Logs.Info(scene + " scene spawn status: " + t.Status);
                spawnTaskDone = true;
            });
            task.StatusChanged += (SpawnStatus status) =>
            {
                if (status == SpawnStatus.Killed)
                    FreePort(int.Parse(task.Properties[AssignPortKey]));
            };
            spawnFirstRoom = true;
        }
    }

    private void FreePort(int port)
    {
        freePorts.Enqueue(port);
    }

    public Dictionary<string, string> GenerateSceneSpawnInfo(string sceneName)
    {
        return new Dictionary<string, string>()
        {
            { MsfDictKeys.RoomName, roomName + (++sceneCounter) },
            { MsfDictKeys.SceneName, sceneName },
            { MsfDictKeys.MapName, sceneName },
            { MsfDictKeys.MaxPlayers, maxPlayers.ToString() },
            { MsfDictKeys.IsPublic, "true" },
            { IsFirstRoomKey, (!spawnFirstRoom).ToString() },
            { AssignPortKey, spawningPort.ToString() },
        };
    }
}
