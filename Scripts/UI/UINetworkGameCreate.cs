using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Barebones.MasterServer;

public class UINetworkGameCreate : UIBase
{
    public int maxPlayerCustomizable = 32;
    public InputField inputRoomName;
    public InputField inputMaxPlayer;
    public InputField inputBotCount;
    public InputField inputMatchTime;
    public Image previewImage;
    [Header("Map list")]
    public Dropdown mapList;
    [Header("Game rule list")]
    public Dropdown gameRuleList;
    public CreateGameProgressUi uiCreateGameProgress;

    private IOGamesModule.MapSelection[] maps;
    private BaseNetworkGameRule[] gameRules;

    public void OnClickCreateGame()
    {
        uiCreateGameProgress = uiCreateGameProgress ?? FindObjectOfType<CreateGameProgressUi>();

        var selectedMap = GetSelectedMap();
        var selectedGameRule = GetSelectedGameRule();

        if (selectedMap == null)
            return;

        var botCount = inputBotCount == null ? 0 : int.Parse(inputBotCount.text);
        var matchTime = inputMatchTime == null ? 0 : int.Parse(inputMatchTime.text);
        var gameRuleName = selectedGameRule == null ? "" : selectedGameRule.name;

        var settings = new Dictionary<string, string> {
            { MsfDictKeys.RoomName, inputRoomName == null ? "" : inputRoomName.text },
            { MsfDictKeys.SceneName, selectedMap.scene.SceneName },
            { MsfDictKeys.MapName, selectedMap.scene.SceneName },
            { MsfDictKeys.MaxPlayers, inputMaxPlayer == null ? "0" : inputMaxPlayer.text },
            { MsfDictKeys.IsPublic, true.ToString() },
            { IOGamesModule.IsFirstRoomKey, false.ToString() },
            { IOGamesModule.RoomSpawnTypeKey, IOGamesModule.RoomSpawnTypeUser },
            { BaseNetworkGameRule.BotCountKey, botCount.ToString() },
            { BaseNetworkGameRule.MatchTimeKey, matchTime.ToString() },
            { IOGamesModule.GameRuleKey, gameRuleName },
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
        if (gameRuleList != null)
            gameRuleList.ClearOptions();

        var selected = GetSelectedMap();

        if (selected == null)
        {
            Debug.LogError("Invalid map selection");
            return;
        }

        previewImage.sprite = selected.previewImage;
        gameRules = selected.availableGameRules;

        if (gameRuleList != null)
        {
            gameRuleList.AddOptions(gameRules.Select(a => new Dropdown.OptionData(a.Title)).ToList());
            gameRuleList.onValueChanged.RemoveListener(OnGameRuleListChange);
            gameRuleList.onValueChanged.AddListener(OnGameRuleListChange);
        }

        OnGameRuleListChange(0);
    }

    public void OnGameRuleListChange(int value)
    {
        var selected = GetSelectedGameRule();

        if (selected == null)
        {
            Debug.LogError("Invalid game rule selection");
            return;
        }
    }

    public void OnBotCountChanged(string value)
    {
        int botCount = 0;
        if (!int.TryParse(value, out botCount))
            inputBotCount.text = botCount.ToString();
    }

    public void OnMatchTimeChanged(string value)
    {
        int matchTime = 0;
        if (!int.TryParse(value, out matchTime))
            inputBotCount.text = matchTime.ToString();
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
        
        var ioGamesModule = FindObjectOfType<IOGamesModule>();
        maps = ioGamesModule.maps;

        if (mapList != null)
        {
            mapList.ClearOptions();
            mapList.AddOptions(maps.Select(m => new Dropdown.OptionData(m.mapName)).ToList());
            mapList.onValueChanged.RemoveListener(OnMapListChange);
            mapList.onValueChanged.AddListener(OnMapListChange);
        }

        if (inputMaxPlayer != null)
        {
            inputMaxPlayer.contentType = InputField.ContentType.IntegerNumber;
            inputMaxPlayer.text = maxPlayerCustomizable.ToString();
            inputMaxPlayer.onValueChanged.RemoveListener(OnMaxPlayerChanged);
            inputMaxPlayer.onValueChanged.AddListener(OnMaxPlayerChanged);
        }

        if (inputBotCount != null)
        {
            inputBotCount.contentType = InputField.ContentType.IntegerNumber;
            inputBotCount.text = "0";
            inputBotCount.onValueChanged.RemoveListener(OnBotCountChanged);
            inputBotCount.onValueChanged.AddListener(OnBotCountChanged);
        }

        if (inputMatchTime != null)
        {
            inputMatchTime.contentType = InputField.ContentType.IntegerNumber;
            inputMatchTime.text = "0";
            inputMatchTime.onValueChanged.RemoveListener(OnMatchTimeChanged);
            inputMatchTime.onValueChanged.AddListener(OnMatchTimeChanged);
        }

        OnMapListChange(0);
    }

    public IOGamesModule.MapSelection GetSelectedMap()
    {
        var text = mapList.captionText.text;
        return maps.FirstOrDefault(m => m.mapName == text);
    }

    public BaseNetworkGameRule GetSelectedGameRule()
    {
        var text = gameRuleList.captionText.text;
        return gameRules.FirstOrDefault(m => m.Title == text);
    }
}
