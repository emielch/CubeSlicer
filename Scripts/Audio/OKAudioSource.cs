using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-2)]
public class OKAudioSource : MonoBehaviour {
    public AudioClip audioClip;
    [Range(0, 10)]
    public float range = 1;
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

    public bool insertNoise = false;
    public bool insertSine = false;
    public float sineFreq = 500f;
    float sineStep = 0.05f;
    float sinePhs = 0;

    public Vector3 currPos;

    void Awake() {
        OKAudioManager.SetupInstance();
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
        currPos = transform.position;
        if (play) {
            playFac += (double)OKAudioManager.GetFL() / audioClip.samples;
            playHead = (int)(playFac * audioClip.samples);
            if (playHead >= audioClip.samples) {
                playFac = (double)OKAudioManager.GetFL() / audioClip.samples;
                playHead = (int)(playFac * audioClip.samples);
                if (!loop) play = false;
            }
        }

        if (insertSine) {
            sineStep = sineFreq / OKAudioManager.instance.sampleRate * Mathf.PI * 2;
            sinePhs += sineStep * OKAudioManager.GetPrevFL();
            sinePhs %= Mathf.PI * 2;
        }
    }

    public float GetSample(int id) {
        float sample = 0;

        if (insertNoise) sample += UnityEngine.Random.Range(-1f, 1f) * vol * 0.1f;
        if (insertSine) sample += MathF.Sin((float)(sinePhs + id * sineStep)) * vol * 0.1f;

        if (play)
            for (int i = 0; i < samples.Count; i++) {
                sample += samples[i][playHead - OKAudioManager.GetFL() + id] * channelVols[i] * vol;
            }
        return sample;
    }

}
