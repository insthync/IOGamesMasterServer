using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Barebones.MasterServer;
using Barebones.Networking;

public class UINetworkRoom : UIBase
{
    public UINetworkRoomEntry entryPrefab;
    public Transform gameListContainer;
    private readonly List<UINetworkRoomEntry> entries = new List<UINetworkRoomEntry>();

    protected IClientSocket Connection = Msf.Connection;

    protected override void Awake()
    {
        base.Awake();
        ClearEntries();
        Connection.Connected += OnConnectedToMaster;
    }

    void OnDestroy()
    {
        Msf.Connection.Connected -= OnConnectedToMaster;
    }

    void OnEnable()
    {
        if (Connection.IsConnected)
            RequestRooms();
    }

    void OnConnectedToMaster()
    {
        // Get rooms, if at the time of connecting the lobby is visible
        if (gameObject.activeSelf)
            RequestRooms();
    }

    void ClearEntries()
    {
        for (var i = gameListContainer.childCount - 1; i >= 0; --i)
        {
            var child = gameListContainer.GetChild(i);
            Destroy(child.gameObject);
        }
        entries.Clear();
    }

    void RequestRooms()
    {
        if (!Connection.IsConnected)
        {
            Logs.Error("Tried to request rooms, but no connection was set");
            return;
        }

        var loadingPromise = Msf.Events.FireWithPromise(Msf.EventNames.ShowLoading, "Retrieving Rooms list...");

        Msf.Client.Matchmaker.FindGames(games =>
        {
            loadingPromise.Finish();
            ClearEntries();
            foreach (var game in games)
            {
                var newEntry = Instantiate(entryPrefab, gameListContainer);
                newEntry.SetData(game);
                newEntry.gameObject.SetActive(true);
                entries.Add(newEntry);
            }
        });
    }

    public void OnClickRefresh()
    {
        RequestRooms();
    }

    public override void Show()
    {
        base.Show();
        OnClickRefresh();
    }
}
