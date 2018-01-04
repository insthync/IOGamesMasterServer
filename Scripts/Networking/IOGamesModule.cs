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
        public int roomCount;
        public int playerCount;
        public int roomId;
        public bool isSpawning;

        public RoomCounter()
        {
            roomCount = 0;
            playerCount = 0;
            roomId = 0;
            isSpawning = false;
        }
    }

    public RoomInfo[] roomInfos;
    public float countPlayersToCreateNewRoomDuration = 3;
    private RoomsModule roomsModule;
    private SpawnersModule spawnersModule;
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
            if (roomsModule != null && spawnersModule != null)
            {
                // Clear room counter
                foreach (var roomCount in roomCounts)
                {
                    roomCounts[roomCount.Key].roomCount = 0;
                    roomCounts[roomCount.Key].playerCount = 0;
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
                        roomCounts[sceneName].roomId = 0;
                        SpawnScene(roomInfo, true);
                    }
                    else
                    {
                        // If there are only first room, reset room Id
                        if (roomCounts[sceneName].roomCount == 1)
                            roomCounts[sceneName].roomId = 1;
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
        var sceneName = roomInfo.scene.SceneName;
        if (spawnersModule == null || roomCounts[sceneName].isSpawning)
            return;

        var task = spawnersModule.Spawn(GenerateSceneSpawnInfo(roomInfo, isFirstRoom));
        if (task != null)
        {
            roomCounts[sceneName].isSpawning = true;
            task.WhenDone(t =>
            {
                Logs.Info(roomInfo.scene + " scene spawn status: " + t.Status);
                roomCounts[sceneName].isSpawning = false;
            });
            task.StatusChanged += (SpawnStatus status) =>
            {
                Logs.Info(roomInfo.scene + " Spawn task changed: " + status);
            };
        }
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
            { RoomSpawnTypeKey, RoomSpawnTypeMaster },
        };
    }
}
