using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

// Plays a single background track on loop. The track is a very large (~2.5h, 157MB) mp3 that
// Unity can't import as an AudioClip, so it lives verbatim in StreamingAssets and is streamed
// from disk at runtime instead.
public class MusicManager : MonoBehaviour
{
    public string streamingFileName = "music_loop.mp3";
    public float volume = 0.4f;

    private AudioSource source;

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.loop = true;
        source.spatialBlend = 0f;
        source.playOnAwake = false;
        source.volume = volume;
    }

    IEnumerator Start()
    {
        string path = Path.Combine(Application.streamingAssetsPath, streamingFileName);
        if (!File.Exists(path))
        {
            Debug.LogWarning("MusicManager: music file not found at " + path);
            yield break;
        }

        string uri = new Uri(path).AbsoluteUri;   // file:///C:/... form
        using UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.MPEG);
        if (req.downloadHandler is DownloadHandlerAudioClip handler)
            handler.streamAudio = true;           // stream, don't load the whole file into memory

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("MusicManager: failed to load music - " + req.error);
            yield break;
        }

        AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
        if (clip == null)
            yield break;

        source.clip = clip;
        source.Play();
    }
}
