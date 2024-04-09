using CSCore;
using CSCore.Codecs.RAW;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-2)]
public class OKAudioSource : MonoBehaviour {
    public AudioClip audioClip;

    private IWaveSource shiftedSource;
    private SoundTouchSource soundTouch;
    MemoryStream memoryStream;
    WaveFormat waveFormat;
    RawDataReader source;

    public bool isSpatial = true;
    [Range(0.001f, 5)]
    public float rolloffScale = 1; // lower number = steeper rolloff (https://www.desmos.com/calculator/vo0iegaksz)
    [Range(0, 20)]
    public float minDistance = 1;
    [Range(0, 100)]
    public float maxDistance = 10;
    float[] samples = new float[10];
    public bool playClip = false;
    public bool loop = false;

    [Range(0, 1)]
    public float vol = 1;

    [Range(-10, 10)]
    public float pitch = 0;
    private float prevPitch = 0;

    public bool playNoise = false;
    public bool playSine = false;
    public float sineFreq = 500f;
    float sineStep = 0.05f;
    float sinePhs = 0;

    System.Random random = new System.Random();

    public Vector3 currPos { get; private set; }

    bool removeWhenFinished = false;

    public Slider volSlider;

    int channelCount;
    int sampleCount;
    float prevLastSample = 0;

    void Awake() {
        OKAudioManager.SetupInstance();
    }

    void Start() {
        if (audioClip == null) return;
        channelCount = audioClip.channels;
        sampleCount = audioClip.samples;
        float[] audioData = new float[sampleCount * channelCount];
        audioClip.GetData(audioData, 0);

        byte[] bytes = new byte[audioData.Length * 2]; // 2 bytes per 16-bit value

        for (int i = 0; i < audioData.Length; i++) {
            short scaledValue = (short)(audioData[i] * 32767); // Scale float value to 16-bit range
            byte[] valueBytes = BitConverter.GetBytes(scaledValue); // Get bytes of 16-bit value
            bytes[i * 2] = valueBytes[0]; // Store first byte
            bytes[i * 2 + 1] = valueBytes[1]; // Store second byte
        }

        memoryStream = new MemoryStream(bytes);
        waveFormat = new WaveFormat(44100, 16, channelCount);
        source = new RawDataReader(memoryStream, waveFormat);
        soundTouch = new SoundTouchSource(source.ToSampleSource(), 10);

        shiftedSource = soundTouch.ToWaveSource(16); // 16-bit PCM format


        if (volSlider) {
            SetVolume(volSlider.value);
            volSlider.onValueChanged.AddListener(SetVolume);
        }
    }

    void Update() {
        currPos = transform.position;

        if (playClip && source.Position >= source.Length) {
            source.SetPosition(TimeSpan.Zero);
            if (!loop) playClip = false;
            if (removeWhenFinished) Destroy(this);
        }

        if (playClip) {
            if (pitch != prevPitch) {
                prevPitch = pitch;
                soundTouch.SetPitch(pitch);
            }

            int FL = OKAudioManager.GetFrameLen() * 2 * channelCount;
            byte[] aData = new byte[FL];
            shiftedSource.Read(aData, 0, aData.Length);

            if (samples.Length < OKAudioManager.GetFrameLen() + 1) {
                samples = new float[OKAudioManager.GetFrameLen() + 1];
            }

            samples[0] = prevLastSample;
            for (int channel = 0; channel < channelCount; channel++) {
                for (int i = 0; i < OKAudioManager.GetFrameLen(); i++) {
                    short value16Bit = BitConverter.ToInt16(aData, (i * channelCount + channel) * 2);
                    if (channel == 0) samples[i + 1] = 0;
                    samples[i + 1] += value16Bit / 65535f / channelCount;
                }
            }
            prevLastSample = samples[OKAudioManager.GetFrameLen()];
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

        if (playClip) sample += samples[id] * vol;

        return sample;
    }

    private void OnDestroy() {
        shiftedSource.Dispose();
        soundTouch.Dispose();
        memoryStream.Dispose();
        source.Dispose();
    }

}
