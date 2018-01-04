using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Barebones.MasterServer;

public class IOGamesModule : ServerModuleBehaviour
{
    public const string RoomSpawnTypeKey = "RoomSpawnType";
    public const string IsFirstRoomKey = "SpawnFirstRoom";
    public const string AssignPortKey = "AssignPort";

    public const string RoomSpawnTypeMaster = "Master";
    public const string RoomSpawnTypeUser = "User";

    [System.Serializable]
    public class RoomInfo
    {
        public SceneField scene;
        public string roomName = "Battle-";
        public int maxPlayers = 32;
        public int playersAmountToCreateNewRoom = 24;
    }

    public class RoomCounter
    {
        public int roomCount = 0;
        public int playerCount = 0;

        public void ClearCounter()
        {
            roomCount = 0;
            playerCount = 0;
        }
    }

    public RoomInfo[] roomInfos;
    public float countPlayersToCreateNewRoomDuration = 3;
    public int startPort = 1500;
    private RoomsModule roomsModule;
    private SpawnersModule spawnersModule;
    private bool spawnTaskDone = false;
    private int spawningPort = -1;
    private int portCounter = -1;
    private readonly Queue<int> freePorts = new Queue<int>();
    private readonly Dictionary<string, RoomCounter> roomCounts = new Dictionary<string, RoomCounter>();

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
        roomCounts.Clear();
        foreach (var roomInfo in roomInfos)
        {
            var sceneName = roomInfo.scene.SceneName;
            if (!roomCounts.ContainsKey(sceneName))
            {
                roomCounts[sceneName] = new RoomCounter();
            }
        }

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
                // Clear room counter
                foreach (var roomCount in roomCounts)
                {
                    roomCounts[roomCount.Key].ClearCounter();
                }

                // Count room and players
                var rooms = roomsModule.GetAllRooms().ToList();
                foreach (var room in rooms)
                {
                    var sceneName = room.Options.Properties[MsfDictKeys.SceneName];
                    if (roomCounts.ContainsKey(sceneName))
                    {
                        roomCounts[sceneName].roomCount += 1;
                        roomCounts[sceneName].playerCount += room.OnlineCount;
                    }
                }

                foreach (var roomInfo in roomInfos)
                {
                    var sceneName = roomInfo.scene.SceneName;
                    if (roomCounts[sceneName].roomCount == 0)
                    {
                        SpawnScene(roomInfo, true);
                    }
                    else
                    {
                        if (Mathf.FloorToInt(roomCounts[sceneName].playerCount / rooms.Count) >= roomInfo.playersAmountToCreateNewRoom)
                            SpawnScene(roomInfo, false);
                    }
                }
            }
        }
    }

    public override void Initialize(IServer server)
    {
        roomsModule = server.GetModule<RoomsModule>();
        spawnersModule = server.GetModule<SpawnersModule>();
    }

    public void SpawnScene(RoomInfo roomInfo, bool isFirstRoom)
    {
        if (spawnersModule == null)
            return;

        var task = spawnersModule.Spawn(GenerateSceneSpawnInfo(roomInfo, isFirstRoom));
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
                Logs.Info(roomInfo.scene + " scene spawn status: " + t.Status);
                spawnTaskDone = true;
            });
            task.StatusChanged += (SpawnStatus status) =>
            {
                if (status == SpawnStatus.Killed)
                    FreePort(int.Parse(task.Properties[AssignPortKey]));
            };
        }
    }

    private void FreePort(int port)
    {
        freePorts.Enqueue(port);
    }

    public Dictionary<string, string> GenerateSceneSpawnInfo(RoomInfo info, bool isFirstRoom)
    {
        var roomCount = roomCounts[info.scene.SceneName].roomCount + 1;
        return new Dictionary<string, string>()
        {
            { MsfDictKeys.RoomName, info.roomName + roomCount.ToString("N0") },
            { MsfDictKeys.SceneName, info.scene.SceneName },
            { MsfDictKeys.MapName, info.scene.SceneName },
            { MsfDictKeys.MaxPlayers, info.maxPlayers.ToString() },
            { MsfDictKeys.IsPublic, true.ToString() },
            { IsFirstRoomKey, isFirstRoom.ToString() },
            { AssignPortKey, spawningPort.ToString() },
            { RoomSpawnTypeKey, RoomSpawnTypeMaster },
        };
    }
}
