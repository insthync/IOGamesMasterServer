using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Barebones.MasterServer;

public class UINetworkRoomEntry : MonoBehaviour
{
    public Text textRoomName;
    public Text textSceneName;
    public Text textPlayerCount;
    public string defaultMapName = "";
    private GameInfoPacket _data;
    public void SetData(GameInfoPacket data)
    {
        _data = data;
        if (textRoomName != null)
            textRoomName.text = data.Name;
        if (textSceneName != null)
        {
            textSceneName.text = data.Properties.ContainsKey(MsfDictKeys.MapName)
                ? data.Properties[MsfDictKeys.MapName] : defaultMapName;
        }
        if (textPlayerCount != null)
        {
            if (data.MaxPlayers > 0)
                textPlayerCount.text = data.OnlinePlayers + "/" + data.MaxPlayers;
            else
                textPlayerCount.text = data.OnlinePlayers.ToString();
        }
    }

    public void OnClickJoinButton()
    {
        Msf.Client.Rooms.GetAccess(_data.Id, OnPassReceived);
    }

    protected virtual void OnPassReceived(RoomAccessPacket packet, string errorMessage)
    {
        if (packet == null)
        {
            Msf.Events.Fire(Msf.EventNames.ShowDialogBox, DialogBoxData.CreateError(errorMessage));
            Logs.Error(errorMessage);
            return;
        }

        // Hope something handles the event
    }
}
