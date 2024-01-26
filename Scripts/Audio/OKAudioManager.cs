using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-3)]
public class OKAudioManager : MonoBehaviour {
    public static OKAudioManager instance;

    public float masterVol { get; private set; }
    int prevFrameLen = 0;
    int frameLen = 0;
    public float sampleRate = 44100;
    public List<OKAudioSource> audioSources;
    public Slider volSlider;

    private void Awake() {
        // If there is an instance, and it's not me, delete myself.
        if (instance != null && instance != this) {
            Destroy(this);
        } else {
            instance = this;
        }

        if (volSlider) {
            SetVolume(volSlider.value);
            volSlider.onValueChanged.AddListener(SetVolume);
        }
    }

    void Update() {
        prevFrameLen = frameLen;
        frameLen = Math.Min((int)(sampleRate * Time.deltaTime), (int)sampleRate / 4);
        audioSources = FindObjectsOfType<OKAudioSource>().ToList();
    }

    public void SetVolume(float _vol) {
        masterVol = MathF.Pow(_vol, 3f);
    }

    static public void SetupInstance() {
        if (FindObjectOfType<OKAudioManager>() == null) {
            GameObject audioManagerGO = new GameObject();
            audioManagerGO.name = "OKAudioManager";
            audioManagerGO.AddComponent<OKAudioManager>();
        }
    }

    static public int GetFL() { return instance.frameLen; }
    static public int GetPrevFL() { return instance.prevFrameLen; }

}
