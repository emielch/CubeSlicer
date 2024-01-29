using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-2)]
public class OKAudioSource : MonoBehaviour {
    public AudioClip audioClip;
    public bool isSpatial = true;
    [Range(0.1f, 5)]
    public float rolloffScale = 1; // lower number = steeper rolloff
    [Range(0, 20)]
    public float minDistance = 1;
    [Range(0, 100)]
    public float maxDistance = 100;
    List<float[]> samples;
    [Range(0, 1)]
    public double playFac = 0;
    public bool playClip = false;
    public bool loop = false;
    int playHead = 0;
    [Range(0, 1)]
    public float vol = 1;
    [Range(0, 1)]
    public float[] channelVols;

    public bool playNoise = false;
    public bool playSine = false;
    public float sineFreq = 500f;
    float sineStep = 0.05f;
    float sinePhs = 0;

    System.Random random = new System.Random();

    public Vector3 currPos { get; private set; }

    bool removeWhenFinished = false;

    public Slider volSlider;

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

        if (volSlider) {
            SetVolume(volSlider.value);
            volSlider.onValueChanged.AddListener(SetVolume);
        }
    }

    void Update() {
        currPos = transform.position;
        if (playClip) {
            playFac += (double)OKAudioManager.GetFL() / audioClip.samples;
            playHead = (int)(playFac * audioClip.samples);
            if (playHead >= audioClip.samples) {
                playFac = (double)OKAudioManager.GetFL() / audioClip.samples;
                playHead = (int)(playFac * audioClip.samples);
                if (!loop) playClip = false;
                if (removeWhenFinished) Destroy(this);
            }
        }

        if (playSine) {
            sineStep = sineFreq / OKAudioManager.instance.sampleRate * Mathf.PI * 2;
            sinePhs += sineStep * OKAudioManager.GetPrevFL();
            sinePhs %= Mathf.PI * 2;
        }
    }
    public void SetVolume(float _vol) {
        vol = MathF.Pow(_vol, 3f);
    }

    public void PlayOneShot(AudioClip _clip, float _vol) {
        audioClip = _clip;
        vol = _vol;
        removeWhenFinished = true;
        playClip = true;
    }

    public bool HasSamples() {
        return playNoise || playSine || playClip;
    }

    public float GetSample(int id) {
        float sample = 0;

        if (playNoise) sample += ((float)random.NextDouble() * 2 - 1) * vol * 0.1f;
        if (playSine) sample += MathF.Sin((float)(sinePhs + id * sineStep)) * vol * 0.1f;

        if (playClip)
            for (int i = 0; i < samples.Count; i++) {
                sample += samples[i][playHead - OKAudioManager.GetFL() + id] * channelVols[i] * vol;
            }
        return sample;
    }

}
