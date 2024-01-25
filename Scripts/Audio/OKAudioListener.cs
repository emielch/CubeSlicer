using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OKAudioListener : MonoBehaviour {
    public int cubeID;
    SerialDevice device;

    public List<OKAudioSource> audioSources = new List<OKAudioSource>();
    float[] samples = new float[44100];
    [Range(0.0f, 32767)]
    public float vol = 32767;

    const int samplesInBlock = 128;
    short[] audioData;
    int aDataIdx = 0;

    public float newSampleRate = 44100;
    public float targetQueueFill = 0.7f;
    public float currentQueueFill = 0.6f;
    public float adjustmentSpeed = 3000; // Speed of adjustment

    public float inputIndex = 0;
    int samplesLen = 1;

    private void Awake(){
        OKAudioManager.SetupInstance();
    }

    void Start() {
        audioSources = FindObjectsOfType<OKAudioSource>().ToList();
        audioData = new short[samplesInBlock];
    }

    public void Init(SerialDevice _device) {
        device = _device;
    }

    void Update() {
        if (device == null) return;
        if (device.Stopped() || device.deviceInfo.id != cubeID) {
            device = null;
            return;
        }

        currentQueueFill = device.audioQueueLevel;
        float queueFillDifference = currentQueueFill - targetQueueFill;
        newSampleRate = OKAudioManager.instance.sampleRate - (queueFillDifference * adjustmentSpeed);

        float prevSample = samples[samplesLen - 1];
        Array.Fill(samples, 0f);
        samplesLen = OKAudioManager.GetFL();

        inputIndex %= 1;
        int startCount = 0;
        if (inputIndex != 0) {
            samples[0] = prevSample;
            startCount = 1;
            samplesLen++;
        }

        foreach (var asource in audioSources) {
            for (int i = startCount; i < samplesLen; i++) {
                float dist = Vector3.Distance(asource.transform.position, transform.position);
                float distVol = Mathf.Min(1, asource.range / dist);
                samples[i] += asource.GetSample(i) * vol * distVol;
            }
        }

        float factor = OKAudioManager.instance.sampleRate / newSampleRate;
        for (; inputIndex < samplesLen - 1; inputIndex += factor) {
            int index1 = Mathf.FloorToInt(inputIndex);
            int index2 = Mathf.CeilToInt(inputIndex);

            float fraction = inputIndex - index1;

            audioData[aDataIdx++] = (short)((1 - fraction) * samples[index1] + fraction * samples[index2]);
            if (aDataIdx >= samplesInBlock) {
                aDataIdx = 0;
                device?.SendAudio(audioData);
            }
        }
    }
}
