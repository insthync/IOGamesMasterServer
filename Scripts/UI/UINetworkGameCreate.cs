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
    [Header("Match Bot Count")]
    public GameObject containerBotCount;
    public InputField inputBotCount;
    [Header("Match Time")]
    public GameObject containerMatchTime;
    public InputField inputMatchTime;
    [Header("Match Kill")]
    public GameObject containerMatchKill;
    public InputField inputMatchKill;
    [Header("Match Score")]
    public GameObject containerMatchScore;
    public InputField inputMatchScore;
    [Header("Maps")]
    public Image previewImage;
    public Dropdown mapList;
    [Header("Game rules")]
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

        var defaultBotCount = selectedGameRule == null ? 0 : selectedGameRule.DefaultBotCount;
        var defaultMatchTime = selectedGameRule == null ? 0 : selectedGameRule.DefaultMatchTime;
        var defaultMatchKill = selectedGameRule == null ? 0 : selectedGameRule.DefaultMatchKill;
        var defaultMatchScore = selectedGameRule == null ? 0 : selectedGameRule.DefaultMatchScore;
        var botCount = inputBotCount == null ? defaultBotCount : int.Parse(inputBotCount.text);
        var matchTime = inputMatchTime == null ? defaultMatchTime : int.Parse(inputMatchTime.text);
        var matchKill = inputMatchKill == null ? defaultMatchKill : int.Parse(inputMatchKill.text);
        var matchScore = inputMatchScore == null ? defaultMatchScore : int.Parse(inputMatchScore.text);
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
            { BaseNetworkGameRule.MatchKillKey, matchKill.ToString() },
            { BaseNetworkGameRule.MatchScoreKey, matchScore.ToString() },
            { IOGamesModule.GameRuleNameKey, gameRuleName },
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

        if (containerBotCount != null)
            containerBotCount.SetActive(selected.HasOptionBotCount);

        if (containerMatchTime != null)
            containerMatchTime.SetActive(selected.HasOptionMatchTime);

        if (containerMatchKill != null)
            containerMatchKill.SetActive(selected.HasOptionMatchKill);

        if (containerMatchScore != null)
            containerMatchScore.SetActive(selected.HasOptionMatchScore);

        if (inputBotCount != null)
        {
            inputBotCount.contentType = InputField.ContentType.IntegerNumber;
            inputBotCount.text = selected.DefaultBotCount.ToString();
            inputBotCount.onValueChanged.RemoveListener(OnBotCountChanged);
            inputBotCount.onValueChanged.AddListener(OnBotCountChanged);
        }

        if (inputMatchTime != null)
        {
            inputMatchTime.contentType = InputField.ContentType.IntegerNumber;
            inputMatchTime.text = selected.DefaultMatchTime.ToString();
            inputMatchTime.onValueChanged.RemoveListener(OnMatchTimeChanged);
            inputMatchTime.onValueChanged.AddListener(OnMatchTimeChanged);
        }

        if (inputMatchKill != null)
        {
            inputMatchKill.contentType = InputField.ContentType.IntegerNumber;
            inputMatchKill.text = selected.DefaultMatchKill.ToString();
            inputMatchKill.onValueChanged.RemoveListener(OnMatchKillChanged);
            inputMatchKill.onValueChanged.AddListener(OnMatchKillChanged);
        }

        if (inputMatchScore != null)
        {
            inputMatchScore.contentType = InputField.ContentType.IntegerNumber;
            inputMatchScore.text = selected.DefaultMatchScore.ToString();
            inputMatchScore.onValueChanged.RemoveListener(OnMatchScoreChanged);
            inputMatchScore.onValueChanged.AddListener(OnMatchScoreChanged);
        }
    }

    public void OnMaxPlayerChanged(string value)
    {
        int maxPlayer = maxPlayerCustomizable;
        if (!int.TryParse(value, out maxPlayer) || maxPlayer > maxPlayerCustomizable)
            inputMaxPlayer.text = maxPlayer.ToString();
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
            inputMatchTime.text = matchTime.ToString();
    }

    public void OnMatchKillChanged(string value)
    {
        int matchKill = 0;
        if (!int.TryParse(value, out matchKill))
            inputMatchKill.text = matchKill.ToString();
    }

    public void OnMatchScoreChanged(string value)
    {
        int matchScore = 0;
        if (!int.TryParse(value, out matchScore))
            inputMatchScore.text = matchScore.ToString();
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
