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

    public SceneField scene;
    public bool spawnSceneInEditor = false;
    public string roomName = "Battle-";
    public int maxPlayers = 2;
    public int playersAmountToCreateNewRoom = 1;
    private RoomsModule roomsModule;
    private SpawnersModule spawnersModule;
    private bool sceneSpawned = false;
    private uint sceneCounter = 0;
    private bool spawnFirstRoom = false;
    private bool spawnTaskDone = false;

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
            yield return new WaitForSeconds(1);
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
            task.WhenDone(t => {
                Logs.Info(scene + " scene spawn status: " + t.Status);
                spawnTaskDone = true;
            });
            spawnFirstRoom = true;
        }
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
            { IsFirstRoomKey, (!spawnFirstRoom).ToString() }
        };
    }
}
