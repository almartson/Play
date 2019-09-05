﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Video;
using NAudio.Wave;
using UnityEngine.Networking;
using System.Threading;

public class SingSceneController : MonoBehaviour
{
    public string DefaultSongName;
    public string DefaultPlayerProfileName;

    [TextArea(3, 8)]
    /// Convenience text field to paste and copy song names when debugging.
    public string defaultSongNamePasteBin;

    public RectTransform PlayerUiArea;
    public RectTransform PlayerUiPrefab;

    public SongMeta SongMeta;
    public PlayerProfile PlayerProfile;

    private VideoPlayer VideoPlayer;

    private IWavePlayer m_WaveOutDevice;
    private WaveStream m_MainOutputStream;
    private WaveChannel32 m_VolumeStream;

    private List<ESceneData> loadedData = new List<ESceneData>();

    private double positionInSongMillis;

    public double CurrentBeat
    {
        get
        {
            if (m_MainOutputStream == null)
            {
                return 0;
            }
            else
            {
                double millisInSong = m_MainOutputStream.CurrentTime.TotalMilliseconds;
                var result = BpmUtils.MillisecondInSongToBeat(SongMeta, millisInSong);
                if (result < 0)
                {
                    result = 0;
                }
                return result;
            }
        }
    }

    // The current position in the song in milliseconds.
    public double PositionInSongInMillis
    {
        get
        {
            if (m_MainOutputStream == null)
            {
                return 0;
            }
            else
            {
                return m_MainOutputStream.CurrentTime.TotalMilliseconds;
            }
        }
    }

    // The current position in the song in milliseconds.
    public double PositionInSongInSeconds
    {
        get
        {
            if (m_MainOutputStream == null)
            {
                return 0;
            }
            else
            {
                return m_MainOutputStream.CurrentTime.TotalSeconds;
            }
        }
    }

    void Start()
    {
        Debug.Log("Start: " + loadedData);
        loadedData.Clear();
        SceneDataBus.AwaitData(ESceneData.AllPlayerProfiles, OnAllPlayerProfiledLoaded);
        SceneDataBus.AwaitData(ESceneData.AllSongMetas, OnAllSongMetasLoaded);
    }

    private void StartAfterAllLoaded()
    {
        Debug.Log("StartAfterAllLoaded");
        SongMeta = SceneDataBus.GetData(ESceneData.SelectedSong, GetDefaultSongMeta);
        PlayerProfile = SceneDataBus.GetData(ESceneData.SelectedPlayerProfile, GetDefaultPlayerProfile);

        VideoPlayer = FindObjectOfType<VideoPlayer>();

        Debug.Log($"{PlayerProfile.Name} starts singing of {SongMeta.Title}.");

        var songMetaPath = SongMeta.Directory + Path.DirectorySeparatorChar + SongMeta.Filename;
        var songPath = SongMeta.Directory + Path.DirectorySeparatorChar + SongMeta.Mp3;

        LoadAudio(songPath);
        m_WaveOutDevice.Play();
        if (positionInSongMillis > 0)
        {
            m_MainOutputStream.CurrentTime = TimeSpan.FromMilliseconds(positionInSongMillis);
        }

        // Create player ui for each player (currently there is only one player)
        CreatePlayerUi();

        // Start any associated video
        Invoke("StartVideoPlayback", SongMeta.VideoGap);

        // Go to next scene when the song finishes
        Invoke("CheckSongFinished", m_MainOutputStream.TotalTime.Seconds);
    }

    private void OnAllPlayerProfiledLoaded()
    {
        loadedData.Add(ESceneData.AllPlayerProfiles);
        if (loadedData.Contains(ESceneData.AllPlayerProfiles) && loadedData.Contains(ESceneData.AllSongMetas))
        {
            StartAfterAllLoaded();
        }
    }

    private void OnAllSongMetasLoaded()
    {
        loadedData.Add(ESceneData.AllSongMetas);
        if (loadedData.Contains(ESceneData.AllPlayerProfiles) && loadedData.Contains(ESceneData.AllSongMetas))
        {
            StartAfterAllLoaded();
        }
    }

    void OnDisable()
    {
        if (m_MainOutputStream != null)
        {
            positionInSongMillis = m_MainOutputStream.CurrentTime.TotalMilliseconds;
            UnloadAudio();
        }
        if (VideoPlayer != null)
        {
            VideoPlayer.Stop();
        }
    }

    void OnEnable()
    {
        if (m_MainOutputStream == null && positionInSongMillis > 0)
        {
            Debug.Log("reloading");
            Start();
        }
    }

    private void CheckSongFinished()
    {
        if (m_MainOutputStream == null)
        {
            return;
        }

        double totalMillis = m_MainOutputStream.TotalTime.TotalMilliseconds;
        double currentMillis = m_MainOutputStream.CurrentTime.TotalMilliseconds;
        double missingMillis = totalMillis - currentMillis;
        if (missingMillis <= 0)
        {
            Invoke("FinishScene", 1f);
        }
        else
        {
            Invoke("CheckSongFinished", (float)(missingMillis / 1000.0));
        }
    }

    private void FinishScene()
    {
        SceneNavigator.Instance.LoadScene(EScene.SongSelectScene);
    }

    private void StartVideoPlayback()
    {
        var videoPath = SongMeta.Directory + Path.DirectorySeparatorChar + SongMeta.Video;
        if (File.Exists(videoPath))
        {
            VideoPlayer.url = "file://" + videoPath;
            InvokeRepeating("SyncVideoWithMusic", 5f, 10f);
        }
        else
        {
            VideoPlayer.enabled = false;
            // TODO: Use cover or background tag as fallback
        }
    }

    private void SyncVideoWithMusic()
    {
        if (m_MainOutputStream == null)
        {
            return;
        }

        var secondsInSong = m_MainOutputStream.CurrentTime.TotalSeconds;
        if (VideoPlayer.length > secondsInSong)
        {
            VideoPlayer.time = secondsInSong;
        }
    }

    private void CreatePlayerUi()
    {
        // Remove old player ui
        foreach (RectTransform oldPlayerUi in PlayerUiArea)
        {
            GameObject.Destroy(oldPlayerUi.gameObject);
        }

        // Create new player ui for each player.
        var playerUi = GameObject.Instantiate(PlayerUiPrefab);
        playerUi.SetParent(PlayerUiArea);

        // Associate a LyricsDisplayer with the SentenceDisplayer
        var sentenceDisplayer = playerUi.GetComponentInChildren<SentenceDisplayer>();
        var lyricsDisplayer = FindObjectOfType<LyricsDisplayer>();
        sentenceDisplayer.LyricsDisplayer = lyricsDisplayer;

        // Load the voice for the SentenceDisplayer of the PlayerUi
        sentenceDisplayer.LoadVoice(SongMeta, null);
    }

    private PlayerProfile GetDefaultPlayerProfile()
    {
        var allPlayerProfiles = SceneDataBus.GetData(ESceneData.AllPlayerProfiles, new List<PlayerProfile>());
        var defaultPlayerProfiles = allPlayerProfiles.Where(it => it.Name == DefaultPlayerProfileName);
        if (defaultPlayerProfiles.Count() == 0)
        {
            throw new Exception("The default player profile was not found.");
        }
        return defaultPlayerProfiles.First();
    }

    private SongMeta GetDefaultSongMeta()
    {
        SongMetaManager.ScanFiles();
        var defaultSongMetas = SongMetaManager.GetSongMetas().Where(it => it.Title == DefaultSongName);
        if (defaultSongMetas.Count() == 0)
        {
            throw new Exception("The default song was not found.");
        }
        return defaultSongMetas.First();
    }

    private bool LoadAudioFromData(byte[] data)
    {
        try
        {
            MemoryStream tmpStr = new MemoryStream(data);
            m_MainOutputStream = new Mp3FileReader(tmpStr);
            m_VolumeStream = new WaveChannel32(m_MainOutputStream);

            m_WaveOutDevice = new WaveOutEvent();
            m_WaveOutDevice.Init(m_VolumeStream);

            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error Loading Audio: " + ex.Message);
        }

        return false;
    }

    private void LoadAudio(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes == null)
        {
            throw new Exception("Loading the mp3 data failed.");
        }

        if (!LoadAudioFromData(bytes))
        {
            throw new Exception("Loading the audio from the mp3 data failed.");
        }

        Resources.UnloadUnusedAssets();
    }

    private void UnloadAudio()
    {
        if (m_WaveOutDevice != null)
        {
            m_WaveOutDevice.Stop();
        }

        if (m_MainOutputStream != null)
        {
            // this one really closes the file and ACM conversion
            m_VolumeStream.Close();
            m_VolumeStream = null;

            // this one does the metering stream
            m_MainOutputStream.Close();
            m_MainOutputStream = null;
        }
        if (m_WaveOutDevice != null)
        {
            m_WaveOutDevice.Dispose();
            m_WaveOutDevice = null;
        }
    }

}
