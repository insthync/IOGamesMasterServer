using System;
using Barebones.Logging;
using Barebones.MasterServer;
using Barebones.Networking;
using UnityEngine;
using UnityEngine.Networking;

public class IOGamesServerStarter : MonoBehaviour
{
    #region Unity's inspector

    public HelpBox _header = new HelpBox()
    {
        Text = "Starts the Unet server if conditions are met.",
        Type = HelpBoxType.Info
    };

    public bool warnIfNoConnectionObject = true;

    public LogLevel logLevel = LogLevel.Info;

    public SimpleLanNetworkManager networkManager;

    [Header("If Spawned Process")]
    [Tooltip("If set to true and if this process is spawned, it will try to " +
             "automatically start the game server. ")]
    public bool autoStartSpawned = true;

    [Header("If Testing In Editor")]
    public bool autoStartInEditor = true;
    public bool startServerAsHost = true;
    public bool autoJoinRoom = true;
    public bool startMaster = true;
    public MasterServerBehaviour masterServer;

    #endregion

    protected bool isStartingEditorServer;

    public BmLogger logger = Msf.Create.Logger(typeof(UnetServerStarter).Name);

    protected virtual void Awake()
    {
        logger.LogLevel = logLevel;

        masterServer = masterServer ?? FindObjectOfType<MasterServerBehaviour>();
        networkManager = networkManager ?? FindObjectOfType<SimpleLanNetworkManager>();

        var connection = GetConnection();

        // Listen to the connected event
        connection.AddConnectionListener(OnConnectedToMaster, true);

        if (warnIfNoConnectionObject && FindObjectOfType<ConnectionToMaster>() == null)
        {
            logger.Warn("No connection object was found in the scene. Ignore the warning, if you're connecting " +
                        "to server manually.");
        }

        // Listen to when a room is registered
        Msf.Server.Rooms.RoomRegistered += OnRoomRegistered;
    }

    protected virtual void Start()
    {
        if (ShouldStartServerInEditor() && startMaster)
        {
            // If we need to start the master server
            if (masterServer == null)
            {
                logger.Error("You have selected to start a master server, but there's no " +
                             "master server object in the scene");
                return;
            }

            // Enable master server object
            masterServer.gameObject.SetActive(true);

            // If auto start in editor is not selected
            if (!masterServer.AutoStartInEditor)
                masterServer.StartServer();
        }
    }

    protected virtual void OnConnectedToMaster()
    {
        // Start the server if it's a spawned process
        if (autoStartSpawned && Msf.Server.Spawners.IsSpawnedProccess)
        {
            // If this is a spawned process, and we want the game server to be started automatically
            StartSpawned();
            return;
        }

        if (ShouldStartServerInEditor())
        {
            isStartingEditorServer = true;

            // If we haven't received any access yet, we assume that we wan't to start a game server
            Logs.Info("AutoStartInEditor set to true and there's no Room access - " +
                      "we're assuming that you want to start a game server in editor.");

            StartInEditor();
            return;
        }
    }

    protected virtual bool ShouldStartServerInEditor()
    {
        return !Msf.Client.Rooms.ForceClientMode
            && Msf.Runtime.IsEditor
               && autoStartInEditor
               && Msf.Client.Rooms.LastReceivedAccess == null;
    }

    protected virtual void StartSpawned()
    {
        Msf.Server.Spawners.RegisterSpawnedProcess(Msf.Args.SpawnId, Msf.Args.SpawnCode, (controller, error) =>
        {
            if (controller == null)
            {
                logger.Error("Failed to register a spawned process: " + error);
                throw new Exception("Failed to register a spawned process: " + error);
            }

            // Set the static object, so that it can be used when creating a room
            IOGamesRoom.SpawnTaskController = controller;

            if (Msf.Args.IsProvided(Msf.Args.Names.WebGl))
                networkManager.useWebSockets = true;

            // Use the assigned port from cmd args
            networkManager.networkPort = Msf.Args.AssignedPort;

            // Setup game rules/configs
            var prop = controller.Properties;
            var ioGamesModule = FindObjectOfType<IOGamesModule>();
            BaseNetworkGameRule gameRule = null;
            if (prop.ContainsKey(IOGamesModule.GameRuleKey))
            {
                gameRule = ioGamesModule.GetGameRule(prop[IOGamesModule.GameRuleKey]);
                if (gameRule != null)
                    gameRule.ReadConfigs(prop);
            }
            networkManager.gameRule = gameRule;

            // Start the server
            networkManager.StartServerAndQuitIfCannotListen();
        });
        return;
    }

    protected virtual void StartInEditor()
    {
        // Connections will be managed by room accesses
        // Maximize this just in case
        networkManager.maxConnections = 999;

        if (startServerAsHost)
            networkManager.StartHostAndQuitIfCannotListen();
        else
            networkManager.StartServerAndQuitIfCannotListen();
    }

    /// <summary>
    /// This will be called when a room is created and registered to "master"
    /// </summary>
    protected virtual void OnRoomRegistered(RoomController controller)
    {
        if (isStartingEditorServer && autoJoinRoom)
        {
            //-------------------------
            // 1. Log into the server
            logger.Debug("Logging in as guest...");
            Msf.Client.Auth.LogInAsGuest((accInfo, loginError) =>
            {
                if (accInfo == null)
                {
                    logger.Error("Failed to log in: " + loginError);
                    return;
                }

                logger.Debug("Logged in successfully");

                //-------------------------
                // 2. Get access to join the room
                logger.Debug("Retrieving room access ...");
                Msf.Client.Rooms.GetAccess(controller.RoomId, (access, accessError) =>
                {
                    if (access == null)
                    {
                        logger.Error("Failed to get the access to server: " + accessError);
                        return;
                    }

                    // We have the access, try to connect to room
                    logger.Debug("Access received: " + access);

                    if (RoomConnector.Instance == null)
                    {
                        logger.Warn("RoomConnector was not found in the scene. Hopefully, " +
                                    "you handle the  'Msf.Client.Rooms.AccessReceived' " +
                                    "event manually.");
                    }

                });
            });
        }
    }

    protected virtual IClientSocket GetConnection()
    {
        return Msf.Connection;
    }

    void OnDestroy()
    {
        Msf.Server.Rooms.RoomRegistered -= OnRoomRegistered;

        // Remove listeners
        GetConnection().RemoveConnectionListener(OnConnectedToMaster);
    }
}
