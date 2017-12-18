using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Barebones.MasterServer;

public class UINetworkRoomEntry : MonoBehaviour
{
    [System.Serializable]
    public struct PlayerMeasure
    {
        public string title;
        public Color color;
    }

    public Text textRoomName;
    public Text textSceneName;
    public Text textPlayerCount;
    public Text textPlayerMeasure;
    public string defaultMapName = "";
    public PlayerMeasure playerMeasureMax = new PlayerMeasure() { title = "Max", color = Color.red };
    public PlayerMeasure playerMeasureHigh = new PlayerMeasure() { title = "High", color = Color.red };
    public PlayerMeasure playerMeasureMedium = new PlayerMeasure() { title = "Medium", color = Color.yellow };
    public PlayerMeasure playerMeasureLow = new PlayerMeasure() { title = "Low", color = Color.green };
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
        var rate = (float)data.OnlinePlayers / (float)data.MaxPlayers;
        if (rate >= 1)
            SetPlayerMeasure(playerMeasureMax);
        else if (rate >= 0.6f)
            SetPlayerMeasure(playerMeasureHigh);
        else if (rate >= 0.4f)
            SetPlayerMeasure(playerMeasureMedium);
        else
            SetPlayerMeasure(playerMeasureLow);
    }

    private void SetPlayerMeasure(PlayerMeasure playerMeasure)
    {
        if (textPlayerMeasure != null)
        {
            textPlayerMeasure.text = playerMeasure.title;
            textPlayerMeasure.color = playerMeasure.color;
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
