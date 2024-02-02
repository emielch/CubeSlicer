using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

[DefaultExecutionOrder(-1)]
public class OKAudioListener : MonoBehaviour {
    public int cubeID;
    SerialDevice device;

    float[] samples = new float[44100];
    [Range(0.0f, 32767)]
    public float vol = 32767;

    const int samplesInBlock = 128;
    short[] audioData;
    int aDataIdx = 0;

    System.Object audioDataLock = new System.Object();

    public float newSampleRate = 44100;
    public float targetQueueFill = 0.7f;
    public float currentQueueFill = 0.6f;
    public float adjustmentSpeed = 3000; // Speed of adjustment

    public float inputIndex = 0;
    int samplesLen = 1;

    Vector3 currPos;

    private void Awake() {
        OKAudioManager.SetupInstance();
    }

    void Start() {
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

        currPos = transform.position;

        Thread processAudioThread = new Thread(() => ProcessAudio());
        processAudioThread.Start();
    }

    void ProcessAudio() {
        if (Monitor.TryEnter(audioDataLock)) {
            try {
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

                foreach (var aSource in OKAudioManager.instance.audioSources) {
                    if (!aSource.HasSamples()) continue;
                    float dist = Vector3.Distance(aSource.currPos, currPos);
                    if (dist > aSource.maxDistance) continue;
                    float distVol = aSource.isSpatial ? Mathf.Min(1, aSource.rolloffScale / (Mathf.Max(aSource.minDistance, dist) - aSource.minDistance + aSource.rolloffScale)) : 1;
                    float _vol = vol * distVol * OKAudioManager.instance.masterVol;
                    for (int i = startCount; i < samplesLen; i++) {
                        samples[i] += aSource.GetSample(i) * _vol;
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
            } finally {
                Monitor.Exit(audioDataLock);
            }
        } else {
            Debug.Log("unable to enter audioDataLock");
        }
    }
}
