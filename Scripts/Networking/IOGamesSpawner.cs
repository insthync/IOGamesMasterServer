using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Barebones.Logging;
using Barebones.MasterServer;
using Barebones.Networking;

public class IOGamesSpawner : SpawnerBehaviour
{
    public int startPort = 1500;
    private int spawningPort = -1;
    private int portCounter = -1;
    private readonly Queue<int> freePorts = new Queue<int>();

    private static object _processLock = new object();
    private static Dictionary<int, Process> _processes = new Dictionary<int, Process>();

    protected override void OnConnectedToMaster()
    {
        // If we want to start a spawner (cmd argument was found)
        if (Msf.Args.IsProvided(Msf.Args.Names.StartSpawner))
        {
            spawningPort = startPort;
            portCounter = startPort;
            StartSpawner();
            return;
        }

        if (AutoStartInEditor && Msf.Runtime.IsEditor)
        {
            StartSpawner();
        }
    }

    private void FreePort(int port)
    {
        freePorts.Enqueue(port);
    }

    protected override void HandleSpawnRequest(SpawnRequestPacket packet, IIncommingMessage message)
    {

        var controller = Msf.Server.Spawners.GetController(packet.SpawnerId);

        if (controller == null)
        {
            message.Respond("Failed to spawn a process. Spawner controller not found", ResponseStatus.Failed);
            return;
        }

        if (freePorts.Count > 0)
            spawningPort = freePorts.Dequeue();
        else
            spawningPort = portCounter++;

        var port = spawningPort;

        // Check if we're overriding an IP to master server
        var masterIp = string.IsNullOrEmpty(controller.DefaultSpawnerSettings.MasterIp) ?
            controller.Connection.ConnectionIp : controller.DefaultSpawnerSettings.MasterIp;

        // Check if we're overriding a port to master server
        var masterPort = controller.DefaultSpawnerSettings.MasterPort < 0 ?
            controller.Connection.ConnectionPort : controller.DefaultSpawnerSettings.MasterPort;

        // Machine Ip
        var machineIp = controller.DefaultSpawnerSettings.MachineIp;

        // Path to executable
        var path = controller.DefaultSpawnerSettings.ExecutablePath;
        if (string.IsNullOrEmpty(path))
        {
            path = File.Exists(Environment.GetCommandLineArgs()[0])
                ? Environment.GetCommandLineArgs()[0]
                : Process.GetCurrentProcess().MainModule.FileName;
        }

        // In case a path is provided with the request
        if (packet.Properties.ContainsKey(MsfDictKeys.ExecutablePath))
            path = packet.Properties[MsfDictKeys.ExecutablePath];

        // Get the scene name
        var sceneNameArgument = packet.Properties.ContainsKey(MsfDictKeys.SceneName)
            ? string.Format("{0} {1} ", Msf.Args.Names.LoadScene, packet.Properties[MsfDictKeys.SceneName])
            : "";

        if (!string.IsNullOrEmpty(packet.OverrideExePath))
        {
            path = packet.OverrideExePath;
        }

        // If spawn in batchmode was set and `DontSpawnInBatchmode` arg is not provided
        var spawnInBatchmode = controller.DefaultSpawnerSettings.SpawnInBatchmode
                               && !Msf.Args.DontSpawnInBatchmode;

        var startProcessInfo = new ProcessStartInfo(path)
        {
            CreateNoWindow = false,
            UseShellExecute = false,
            Arguments = " " +
                (spawnInBatchmode ? "-batchmode -nographics " : "") +
                (controller.DefaultSpawnerSettings.AddWebGlFlag ? Msf.Args.Names.WebGl + " " : "") +
                sceneNameArgument +
                string.Format("{0} {1} ", Msf.Args.Names.MasterIp, masterIp) +
                string.Format("{0} {1} ", Msf.Args.Names.MasterPort, masterPort) +
                string.Format("{0} {1} ", Msf.Args.Names.SpawnId, packet.SpawnId) +
                string.Format("{0} {1} ", Msf.Args.Names.AssignedPort, port) +
                string.Format("{0} {1} ", Msf.Args.Names.MachineIp, machineIp) +
                (Msf.Args.DestroyUi ? Msf.Args.Names.DestroyUi + " " : "") +
                string.Format("{0} \"{1}\" ", Msf.Args.Names.SpawnCode, packet.SpawnCode) +
                packet.CustomArgs
        };

        Logger.Debug("Starting process with args: " + startProcessInfo.Arguments);

        var processStarted = false;

        try
        {
            new Thread(() =>
            {
                try
                {
                    Logger.Debug("New thread started");

                    using (var process = Process.Start(startProcessInfo))
                    {
                        Logger.Debug("Process started. Spawn Id: " + packet.SpawnId + ", pid: " + process.Id);
                        processStarted = true;

                        lock (_processLock)
                        {
                            // Save the process
                            _processes[packet.SpawnId] = process;
                        }

                        var processId = process.Id;

                        // Notify server that we've successfully handled the request
                        BTimer.ExecuteOnMainThread(() =>
                        {
                            message.Respond(ResponseStatus.Success);
                            controller.NotifyProcessStarted(packet.SpawnId, processId, startProcessInfo.Arguments);
                        });

                        process.WaitForExit();
                    }
                }
                catch (Exception e)
                {
                    if (!processStarted)
                        BTimer.ExecuteOnMainThread(() => { message.Respond(ResponseStatus.Failed); });

                    Logger.Error("An exception was thrown while starting a process. Make sure that you have set a correct build path. " +
                                 "We've tried to start a process at: '" + path + "'. You can change it at 'SpawnerBehaviour' component");
                    Logger.Error(e);
                }
                finally
                {
                    lock (_processLock)
                    {
                        // Remove the process
                        _processes.Remove(packet.SpawnId);
                    }

                    BTimer.ExecuteOnMainThread(() =>
                    {
                        // Release the port number
                        FreePort(port);

                        Logger.Debug("Notifying about killed process with spawn id: " + packet.SpawnerId);
                        controller.NotifyProcessKilled(packet.SpawnId);
                    });
                }

            }).Start();
        }
        catch (Exception e)
        {
            message.Respond(e.Message, ResponseStatus.Error);
            Logs.Error(e);
        }
    }
}
