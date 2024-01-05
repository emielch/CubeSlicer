using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1)]
public class OKAudioSource : MonoBehaviour {
    public AudioClip audioClip;
    List<float[]> samples;
    [Range(0, 1)]
    public double playFac = 0;
    public bool play = false;
    public bool loop = false;
    [Range(0, 1)]
    public float vol = 1;
    [Range(0, 1)]
    public float[] channelVols;

    int playHead = 0;

    void Awake() {
        if (FindObjectOfType<OKAudioManager>() == null) {
            GameObject audioManagerGO = new GameObject();
            audioManagerGO.name = "OKAudioManager";
            audioManagerGO.AddComponent<OKAudioManager>();
        }
    }

    void Start() {
        if (audioClip == null) return;
        int channelCount = audioClip.channels;
        int sampleCount = audioClip.samples;
        float[] audioData = new float[sampleCount * channelCount];
        audioClip.GetData(audioData, 0);

        samples = new List<float[]>(channelCount);

        for (int channel = 0; channel < channelCount; channel++) {
            float[] channelData = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++) {
                // Extract channel data
                channelData[i] = audioData[i * channelCount + channel];
            }

            samples.Add(channelData);
        }

        channelVols = new float[channelCount];
        Array.Fill(channelVols, 1);
    }

    void Update() {
        if (!play) return;
        playFac += (double)OKAudioManager.frameLen / audioClip.samples;
        playHead = (int)(playFac * audioClip.samples);
        if (playHead >= audioClip.samples) {
            playFac = (double)OKAudioManager.frameLen / audioClip.samples;
            playHead = (int)(playFac * audioClip.samples);
            if (!loop) play = false;
        }
    }

    public float GetSample(int id) {
        if (play) {
            float sample = 0;
            for (int i = 0; i < samples.Count; i++) {
                sample += samples[i][playHead - OKAudioManager.frameLen + id] * channelVols[i] * vol;
            }
            return sample;
        }
        return 0;
    }
}
