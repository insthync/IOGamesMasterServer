using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Barebones.MasterServer;

public class UINetworkGameCreate : UIBase
{
    [System.Serializable]
    public class MapSelection
    {
        public string mapName;
        public SceneField scene;
        public Sprite previewImage;
    }
    public int maxPlayerCustomizable = 32;
    public InputField inputRoomName;
    public InputField inputMaxPlayer;
    public Image previewImage;
    public MapSelection[] maps;
    public Dropdown mapList;
    public CreateGameProgressUi uiCreateGameProgress;

    public void OnClickCreateGame()
    {
        uiCreateGameProgress = uiCreateGameProgress ?? FindObjectOfType<CreateGameProgressUi>();
        var selectedMap = GetSelectedMap();

        var settings = new Dictionary<string, string> {
            { MsfDictKeys.RoomName, inputRoomName.text },
            { MsfDictKeys.SceneName, selectedMap.scene.SceneName },
            { MsfDictKeys.MapName, selectedMap.scene.SceneName },
            { MsfDictKeys.MaxPlayers, inputMaxPlayer.text },
            { MsfDictKeys.IsPublic, true.ToString() },
            { IOGamesModule.IsFirstRoomKey, false.ToString() },
            { IOGamesModule.RoomSpawnTypeKey, IOGamesModule.RoomSpawnTypeUser },
        };

        Msf.Client.Spawners.RequestSpawn(settings, "", (requestController, errorMsg) =>
        {
            if (requestController == null)
            {
                uiCreateGameProgress.gameObject.SetActive(false);
                Msf.Events.Fire(Msf.EventNames.ShowDialogBox, DialogBoxData.CreateError("Failed to create a game: " + errorMsg));

                Debug.LogError("Failed to create a game: " + errorMsg);
            }
            uiCreateGameProgress.Display(requestController);
        });
    }

    public void OnMapListChange(int value)
    {
        var selected = GetSelectedMap();

        if (selected == null)
        {
            Debug.LogError("Invalid map selection");
            return;
        }

        previewImage.sprite = selected.previewImage;
    }

    public void OnMaxPlayerChanged(string value)
    {
        int maxPlayer = maxPlayerCustomizable;
        if (!int.TryParse(value, out maxPlayer) || maxPlayer > maxPlayerCustomizable)
            inputMaxPlayer.text = maxPlayer.ToString();
    }

    public override void Show()
    {
        base.Show();

        mapList.ClearOptions();
        mapList.AddOptions(maps.Select(m => new Dropdown.OptionData(m.mapName)).ToList());
        mapList.onValueChanged.RemoveListener(OnMapListChange);
        mapList.onValueChanged.AddListener(OnMapListChange);

        inputMaxPlayer.contentType = InputField.ContentType.IntegerNumber;
        inputMaxPlayer.text = maxPlayerCustomizable.ToString();
        inputMaxPlayer.onValueChanged.RemoveListener(OnMaxPlayerChanged);
        inputMaxPlayer.onValueChanged.AddListener(OnMaxPlayerChanged);

        OnMapListChange(0);
    }

    public MapSelection GetSelectedMap()
    {
        var text = mapList.captionText.text;
        return maps.FirstOrDefault(m => m.mapName == text);
    }
}
