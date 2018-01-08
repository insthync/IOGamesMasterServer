using System;
using System.Collections;
using System.Collections.Generic;
using Barebones.Logging;
using Barebones.MasterServer;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// This script automatically creates a room in "master" server,
/// when <see cref="OnStartServer"/> is called (most likely by Network Manager
/// , when server is started).
/// 
/// After room is created, it also checks if this game server was "spawned", and 
/// if so - it finalizes the spawn task
/// </summary>
public class IOGamesRoom : NetworkBehaviour
{
    public static SpawnTaskController SpawnTaskController;

    /// <summary>
    /// Unet msg type 
    /// </summary>
    public const short AccessMsgType = 3000;

    public struct SimplePeerInfo
    {
        public int peerId;
        public NetworkConnection connection;
    }

    public HelpBox _header = new HelpBox()
    {
        Text = "Waits for the Unet game server to start," +
               "and then automatically creates a Room for it " +
               "(registers server to 'Master').",
        Type = HelpBoxType.Info
    };

    [Header("General")]
    public LogLevel logLevel = LogLevel.Warn;

    [Header("Room options")]
    [Tooltip("This address will be sent to clients with an access token")]
    public string publicIp = "xxx.xxx.xxx.xxx";
    public string roomName = "Room Name";
    public int maxPlayers = 5;
    public bool isPublic = true;
    public string password = "";
    public bool allowUsersRequestAccess = true;

    [Header("Room properties")]
    public string mapName = "Amazing Map";

    [Header("Room Terminator")]
    [Tooltip("Terminates server if first player doesn't join in a given number of seconds")]
    public float firstPlayerTimeoutSecs = 25;
    [Tooltip("Terminates if room is not registered in a given number of seconds")]
    public float roomRegistrationTimeoutSecs = 15;
    [Tooltip("Once every given number of seconds checks if the room is empty." +
             " If it is - terminates it")]
    public float terminateEmptyOnIntervals = 60;
    [Tooltip("Each second, will check if connected to master. If not - quits the application")]
    public bool terminateIfNotConnected = true;
    [Tooltip("If true, quit the application immediately, when the last player quits")]
    public bool terminateWhenLastPlayerQuits = true;
    private bool hasFirstPlayerShowedUp = false;

    [Header("Other")]
    public bool quitAppIfDisconnected = true;

    public BmLogger logger = Msf.Create.Logger(typeof(UnetGameRoom).Name);

    protected Dictionary<int, SimplePeerInfo> peersByConnectionId;
    protected Dictionary<int, SimplePeerInfo> peersByPeerId;

    public event Action<SimplePeerInfo> onPlayerJoined;
    public event Action<SimplePeerInfo> onPlayerLeft;

    public NetworkManager networkManager;
    public RoomController roomController;

    protected bool isFirstRoom;
    protected string roomType;
    protected string gameRuleName;

    protected virtual void Awake()
    {
        networkManager = networkManager ?? FindObjectOfType<NetworkManager>();

        logger.LogLevel = logLevel;
        
        peersByConnectionId = new Dictionary<int, SimplePeerInfo>();
        peersByPeerId = new Dictionary<int, SimplePeerInfo>();

        NetworkServer.RegisterHandler(AccessMsgType, HandleReceivedAccess);

        Msf.Server.Rooms.Connection.Disconnected += OnDisconnectedFromMaster;
    }

    protected virtual void Start()
    {
        if (!Msf.Args.IsProvided(Msf.Args.Names.SpawnCode))
        {
            // If this game server was not spawned by a spawner
            Destroy(gameObject);
            return;
        }

        if (roomRegistrationTimeoutSecs > 0)
            StartCoroutine(StartStartedTimeout(roomRegistrationTimeoutSecs));

        if (firstPlayerTimeoutSecs > 0)
            StartCoroutine(StartFirstPlayerTimeout(firstPlayerTimeoutSecs));

        if (terminateEmptyOnIntervals > 0)
            StartCoroutine(StartEmptyIntervalsCheck(terminateEmptyOnIntervals));

        if (terminateIfNotConnected)
            StartCoroutine(StartWaitingForConnectionLost());
    }

    public bool IsRoomRegistered { get; protected set; }

    /// <summary>
    /// This will be called, when game server starts
    /// </summary>
    public override void OnStartServer()
    {
        // Find the manager, in case it was inaccessible on awake
        networkManager = networkManager ?? FindObjectOfType<NetworkManager>();

        // The Unet server is started, we need to register a Room
        BeforeRegisteringRoom();
        RegisterRoom();
    }

    /// <summary>
    /// This method is called before creating a room. It can be used to
    /// extract some parameters from cmd args or from span task properties
    /// </summary>
    protected virtual void BeforeRegisteringRoom()
    {
        if (SpawnTaskController != null)
        {
            logger.Debug("Reading spawn task properties to override some of the room options");

            // If this server was spawned, try to read some of the properties
            var prop = SpawnTaskController.Properties;

            // Room name
            if (prop.ContainsKey(MsfDictKeys.RoomName))
                roomName = prop[MsfDictKeys.RoomName];

            if (prop.ContainsKey(MsfDictKeys.MaxPlayers))
                maxPlayers = int.Parse(prop[MsfDictKeys.MaxPlayers]);

            if (prop.ContainsKey(MsfDictKeys.RoomPassword))
                password = prop[MsfDictKeys.RoomPassword];

            if (prop.ContainsKey(MsfDictKeys.MapName))
                mapName = prop[MsfDictKeys.MapName];

            if (prop.ContainsKey(IOGamesModule.IsFirstRoomKey))
                isFirstRoom = bool.Parse(prop[IOGamesModule.IsFirstRoomKey]);

            if (prop.ContainsKey(IOGamesModule.RoomSpawnTypeKey))
                roomType = prop[IOGamesModule.RoomSpawnTypeKey];

            if (prop.ContainsKey(IOGamesModule.GameRuleNameKey))
                gameRuleName = prop[IOGamesModule.GameRuleNameKey];
        }

        // Override the public address
        if (Msf.Args.IsProvided(Msf.Args.Names.MachineIp) && networkManager != null)
        {
            publicIp = Msf.Args.MachineIp;
            logger.Debug("Overriding rooms public IP address to: " + publicIp);
        }
    }

    public virtual void RegisterRoom()
    {
        var isUsingLobby = Msf.Args.IsProvided(Msf.Args.Names.LobbyId);

        // 1. Create options object
        var options = new RoomOptions()
        {
            RoomIp = publicIp,
            RoomPort = networkManager.networkPort,
            Name = roomName,
            MaxPlayers = maxPlayers,

            // Lobby rooms should be private, because they are accessed differently
            IsPublic = isUsingLobby ? false : isPublic,
            AllowUsersRequestAccess = isUsingLobby ? false : allowUsersRequestAccess,

            Password = password,

            Properties = new Dictionary<string, string>()
            {
                { MsfDictKeys.MapName, mapName },
                { MsfDictKeys.SceneName, SceneManager.GetActiveScene().name },
                { IOGamesModule.RoomSpawnTypeKey, roomType },
                { IOGamesModule.GameRuleNameKey, gameRuleName },
            }
        };

        // 2. Send a request to create a room
        Msf.Server.Rooms.RegisterRoom(options, (controller, error) =>
        {
            if (controller == null)
            {
                logger.Error("Failed to create a room: " + error);
                return;
            }

            // Save the controller
            roomController = controller;
            logger.Debug("Room Created successfully. Room ID: " + controller.RoomId);
            OnRoomRegistered(controller);
        });
    }

    /// <summary>
    /// Called when room is registered to the "master server"
    /// </summary>
    /// <param name="roomController"></param>
    public void OnRoomRegistered(RoomController roomController)
    {
        IsRoomRegistered = true;

        // Set access provider (Optional)
        roomController.SetAccessProvider(CreateAccess);

        // If this room was spawned
        if (SpawnTaskController != null)
            SpawnTaskController.FinalizeTask(CreateSpawnFinalizationData());
    }

    /// <summary>
    /// Override, if you want to manually handle creation of access'es
    /// </summary>
    /// <param name="callback"></param>
    public virtual void CreateAccess(UsernameAndPeerIdPacket requester, RoomAccessProviderCallback callback)
    {
        callback.Invoke(new RoomAccessPacket()
        {
            RoomIp = roomController.Options.RoomIp,
            RoomPort = roomController.Options.RoomPort,
            Properties = roomController.Options.Properties,
            RoomId = roomController.RoomId,
            SceneName = SceneManager.GetActiveScene().name,
            Token = Guid.NewGuid().ToString()
        }, null);
    }

    /// <summary>
    /// This dictionary will be sent to "master server" when we want 
    /// notify "master" server that Spawn Process is completed
    /// </summary>
    /// <returns></returns>
    public virtual Dictionary<string, string> CreateSpawnFinalizationData()
    {
        return new Dictionary<string, string>()
        {
            // Add room id, so that whoever requested to spawn this game server,
            // knows which rooms access to request
            {MsfDictKeys.RoomId, roomController.RoomId.ToString()},

            // Add room password, so that creator can request an access to a 
            // password-protected room
            {MsfDictKeys.RoomPassword, roomController.Options.Password}
        };
    }

    /// <summary>
    /// This should be called when client leaves the game server.
    /// This method will remove player object from lookups
    /// </summary>
    /// <param name="connection"></param>
    public void ClientDisconnected(NetworkConnection connection)
    {
        SimplePeerInfo peer;
        if (!peersByConnectionId.TryGetValue(connection.connectionId, out peer))
            return;

        OnPlayerLeft(peer);
    }

    protected virtual void HandleReceivedAccess(NetworkMessage netmsg)
    {
        var token = netmsg.ReadMessage<StringMessage>().value;

        roomController.ValidateAccess(token, (validatedAccess, error) =>
        {
            if (validatedAccess == null)
            {
                logger.Error("Failed to confirm access token:" + error);
                // Confirmation failed, disconnect the user
                netmsg.conn.Disconnect();
                return;
            }

            logger.Debug("Confirmed token access for peer: " + validatedAccess);
            var peerInfo = new SimplePeerInfo()
            {
                peerId = validatedAccess.PeerId,
                connection = netmsg.conn,
            };
            OnPlayerJoined(peerInfo);
        });
    }

    protected virtual void OnPlayerJoined(SimplePeerInfo peerInfo)
    {
        // Add to lookups
        peersByConnectionId.Add(peerInfo.connection.connectionId, peerInfo);
        peersByPeerId.Add(peerInfo.peerId, peerInfo);

        if (onPlayerJoined != null)
            onPlayerJoined.Invoke(peerInfo);

        hasFirstPlayerShowedUp = true;
    }

    protected virtual void OnPlayerLeft(SimplePeerInfo peerInfo)
    {
        // Remove from lookups
        peersByConnectionId.Remove(peerInfo.connection.connectionId);
        peersByPeerId.Remove(peerInfo.peerId);

        if (!isFirstRoom && terminateWhenLastPlayerQuits && peersByPeerId.Count == 0)
            Application.Quit();

        if (onPlayerLeft != null)
            onPlayerLeft.Invoke(peerInfo);
        
        // Notify controller that the player has left
        roomController.PlayerLeft(peerInfo.peerId);
    }

    private void OnDisconnectedFromMaster()
    {
        if (quitAppIfDisconnected)
            Application.Quit();
    }

    protected virtual void OnDestroy()
    {
        Msf.Server.Rooms.Connection.Disconnected -= OnDisconnectedFromMaster;
    }

    #region Terminator Functions
    /// <summary>
    ///     Each second checks if we're still connected, and if we are not,
    ///     terminates game server
    /// </summary>
    /// <returns></returns>
    private IEnumerator StartWaitingForConnectionLost()
    {
        // Wait at least 5 seconds until first check
        yield return new WaitForSeconds(5);

        while (true)
        {
            yield return new WaitForSeconds(1);
            if (!isFirstRoom && !Msf.Connection.IsConnected)
            {
                Logs.Error("Terminating game server, no connection");
                Application.Quit();
            }
        }
    }

    /// <summary>
    ///     Each time, after the amount of seconds provided passes, checks
    ///     if the server is empty, and if it is - terminates application
    /// </summary>
    /// <param name="timeout"></param>
    /// <returns></returns>
    private IEnumerator StartEmptyIntervalsCheck(float timeout)
    {
        while (true)
        {
            yield return new WaitForSeconds(timeout);
            if (!isFirstRoom && peersByPeerId.Count <= 0)
            {
                Logs.Error("Terminating game server because it's empty at the time of an interval check.");
                Application.Quit();
            }
        }
    }

    /// <summary>
    ///     Waits a number of seconds, and checks if the game room was registered
    ///     If not - terminates the application
    /// </summary>
    /// <returns></returns>
    private IEnumerator StartStartedTimeout(float timeout)
    {
        yield return new WaitForSeconds(timeout);
        if (!isFirstRoom && !IsRoomRegistered)
            Application.Quit();
    }

    private IEnumerator StartFirstPlayerTimeout(float timeout)
    {
        yield return new WaitForSeconds(timeout);
        if (!isFirstRoom && !hasFirstPlayerShowedUp)
        {
            Logs.Error("Terminated game server because first player didn't show up");
            Application.Quit();
        }
    }
    #endregion
}