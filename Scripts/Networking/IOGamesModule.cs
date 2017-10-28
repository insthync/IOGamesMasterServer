using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Barebones.MasterServer;
using Barebones.Networking;

public class IOGamesModule : ServerModuleBehaviour
{
    public const string SpawnFirstRoomKey = "SpawnFirstRoom";

    public SceneField scene;
    public bool spawnSceneInEditor = false;
    public string roomName = "Battle-";
    public int maxPlayers = 2;
    public int playersAmountToCreateNewRoom = 1;
    private SpawnersModule spawnersModule;
    private bool sceneSpawned = false;
    private uint sceneCounter = 0;
    private bool spawnFirstRoom = false;

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
        AddDependency<SpawnersModule>();
    }

    private void Start()
    {
        if (Msf.Args.IsProvided(Msf.Args.Names.LoadScene))
            SceneManager.LoadScene(Msf.Args.LoadScene);
    }

    public override void Initialize(IServer server)
    {
        spawnersModule = server.GetModule<SpawnersModule>();
        //----------------------------------------------
        // Spawn game servers (zones)

        // Find a spawner 
        var spawner = spawnersModule.GetSpawners().FirstOrDefault();

        if (spawner != null)
        {
            // We found a spawner we can use
            SpawnScene();
        }
        else
        {
            // Spawners are not yet registered to the master, 
            // so let's listen to an event and wait for them
            spawnersModule.SpawnerRegistered += registeredSpawner =>
            {
                // Ignore if zones are already spawned
                if (sceneSpawned) return;
                // Spawn the zones
                SpawnScene();
                sceneSpawned = true;
            };
        }
    }

    void SpawnScene()
    {
        spawnersModule.Spawn(GenerateSceneSpawnInfo(scene)).WhenDone(task => Logs.Info(scene + " scene spawn status: " + task.Status));
        spawnFirstRoom = true;
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
            { SpawnFirstRoomKey, spawnFirstRoom.ToString() }
        };
    }
}
