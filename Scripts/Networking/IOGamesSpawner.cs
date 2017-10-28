using Barebones.Logging;
using Barebones.MasterServer;
using Barebones.Networking;
using UnityEngine;

public class IOGamesSpawner : SpawnerBehaviour
{
    protected override void OnConnectedToMaster()
    {
        StartSpawner();
    }
}
