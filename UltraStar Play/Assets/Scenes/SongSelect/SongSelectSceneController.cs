﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Xml.Linq;
using System;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Linq;

public class SongSelectSceneController : MonoBehaviour
{
    public RectTransform songListContent;
    public RectTransform songButtonPrefab;

    public RectTransform playerProfileListContent;
    public RectTransform playerProfileButtonPrefab;

    private List<SongMeta> songs;

    private PlayerProfile selectedPlayerProfile;

    void OnEnable()
    {
    }

    void Start()
    {
        SceneDataBus.AwaitData(ESceneData.AllSongMetas, OnAllSongsLoaded);
        SceneDataBus.AwaitData(ESceneData.AllPlayerProfiles, OnAllPlayerProfilesLoaded);
    }

    private void OnAllSongsLoaded()
    {
        PopulateSongList();
    }

    private void OnAllPlayerProfilesLoaded()
    {
        var playerProfiles = SceneDataBus.GetData(ESceneData.AllPlayerProfiles, new List<PlayerProfile>());
        PopulatePlayerProfileList(playerProfiles);
    }

    private void PopulatePlayerProfileList(List<PlayerProfile> playerProfiles)
    {
        // Remove old buttons.
        foreach (RectTransform element in playerProfileListContent)
        {
            GameObject.Destroy(element.gameObject);
        }

        // Create new buttons. One for each profile.
        foreach (var playerProfile in playerProfiles)
        {
            AddPlayerProfileButton(playerProfile);
        }
    }

    private void AddPlayerProfileButton(PlayerProfile playerProfile)
    {
        var newButton = RectTransform.Instantiate(playerProfileButtonPrefab);
        newButton.SetParent(playerProfileListContent);

        newButton.GetComponentInChildren<Text>().text = playerProfile.Name;
        newButton.GetComponent<Button>().onClick.AddListener(() => OnPlayerProfileButtonClicked(playerProfile));
    }

    private void PopulateSongList()
    {
        // Remove old buttons.
        foreach (RectTransform element in songListContent)
        {
            GameObject.Destroy(element.gameObject);
        }

        // Create new song buttons. One for each loaded song.
        var songMetas = SongMetaManager.GetSongMetas();
        foreach (var songMeta in songMetas)
        {
            AddSongButton(songMeta);
        }
    }

    private void AddSongButton(SongMeta songMeta)
    {
        var newButton = RectTransform.Instantiate(songButtonPrefab);
        newButton.SetParent(songListContent);

        newButton.GetComponentInChildren<Text>().text = songMeta.Title;
        newButton.GetComponent<Button>().onClick.AddListener(() => OnSongButtonClicked(songMeta));
    }

    private void OnSongButtonClicked(SongMeta songMeta)
    {
        Debug.Log($"Clicked on song button: {songMeta.Title}");

        SceneDataBus.PutData(ESceneData.SelectedSong, songMeta);
        var allPlayerProfiles = SceneDataBus.GetData(ESceneData.AllPlayerProfiles, new List<PlayerProfile>());
        var defaultPlayerProfile = allPlayerProfiles[0];
        SceneDataBus.PutData(ESceneData.SelectedPlayerProfile, selectedPlayerProfile.OrElse(defaultPlayerProfile));

        SceneNavigator.Instance.LoadScene(EScene.SingScene);
    }

    private void OnPlayerProfileButtonClicked(PlayerProfile playerProfile)
    {
        Debug.Log($"Clicked on player profile button: {playerProfile.Name}");
        selectedPlayerProfile = playerProfile;
    }

}
