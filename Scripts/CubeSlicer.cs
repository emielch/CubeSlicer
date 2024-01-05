using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

public class CubeSlicer : MonoBehaviour {
    public int cubeID;
    SerialDevice device;
    public RenderTexture rt;
    byte[] ledData;
    public List<CubeCamRig> camRigs = new List<CubeCamRig>();
    public GameObject edges;
    public Material edgesEnabledMat;
    public Material edgesDisabledMat;
    public float bri = 0.1F;
    public float sat = 1F;
    float cubeScale;
    public MeshRenderer previewPlane;
    private static List<CubeSlicer> instances = new List<CubeSlicer>();

    public int nextRender = 1;
    public int renderCountReset = 2;

    public List<OKAudioSource> audioSources = new List<OKAudioSource>();
    float[] samples = new float[44100];
    [Range(0.0f, 30000)]
    public float vol = 1000;

    int samplesInBlock = 128;
    short[] audioData;
    int aDataIdx = 0;

    void Awake() {
        if (FindObjectOfType<CubesManager>() == null) {
            GameObject cubesManagerGO = new GameObject();
            cubesManagerGO.name = "CubesManager";
            cubesManagerGO.AddComponent<CubesManager>();
        }
        UpdateEdgesMat(edgesDisabledMat);
        nextRender = (int)UnityEngine.Random.Range(1, 20);

        audioSources = FindObjectsOfType<OKAudioSource>().ToList();
    }

    private void OnEnable() {
        instances.Add(this);
    }
    private void OnDisable() {
        instances.Remove(this);
    }

    public static void EnableEdges() {
        foreach (var instance in instances) {
            instance.edges.SetActive(true);
        }
    }

    public static void DisableEdges() {
        foreach (var instance in instances) {
            instance.edges.SetActive(false);
        }
    }

    void UpdateEdgesMat(Material mat) {
        MeshRenderer[] mrs = edges.GetComponentsInChildren<MeshRenderer>();
        foreach (var mr in mrs) {
            mr.material = mat;
        }
    }

    CubeCamRig CreateCamRig(RenderTexture _rt, ref int xpos, ref int ypos, Quaternion angle, int w, int h, int d) {
        GameObject go = new GameObject();
        go.name = "CameraRig";
        go.transform.SetParent(transform);
        go.transform.localPosition = new Vector3(0, 0, 0);
        go.transform.localRotation = angle;
        CubeCamRig rig = go.AddComponent<CubeCamRig>();
        rig.Init(_rt, ref xpos, ref ypos, w, h, d, cubeScale);

        return rig;
    }

    public void Init(SerialDevice _device) {
        foreach (var rig in camRigs) {
            Destroy(rig);
        }
        camRigs.Clear();

        UpdateEdgesMat(edgesEnabledMat);
        device = _device;

        cubeScale = transform.lossyScale.x;
        ledData = new byte[device.deviceInfo.width * device.deviceInfo.height * device.deviceInfo.depth * 3];
        audioData = new short[samplesInBlock];

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientSkyColor = new Color(0, 0, 0);

        int width = device.deviceInfo.width;
        int height = device.deviceInfo.height;
        int depth = device.deviceInfo.depth;

        int xSize = width * depth;
        int ySize = height * 4;
        ySize += depth * (int)Math.Ceiling((float)(width * height) * 2 / (width * depth));

        rt = new RenderTexture(xSize, ySize, 24);

        if (previewPlane != null) {
            previewPlane.material = new Material(Shader.Find("Standard"));
            previewPlane.material.SetColor("_Color", Color.black);
            previewPlane.material.SetColor("_EmissionColor", Color.white);
            previewPlane.material.EnableKeyword("_EMISSION");
            previewPlane.material.SetTexture("_EmissionMap", rt);
        }

        int xpos = 0;
        int ypos = 0;

        // front, back, left, right, top, bottom
        camRigs.Add(CreateCamRig(rt, ref xpos, ref ypos, Quaternion.Euler(0, 0, 0), width, height, depth));
        camRigs.Add(CreateCamRig(rt, ref xpos, ref ypos, Quaternion.Euler(0, 180, 0), width, height, depth));
        camRigs.Add(CreateCamRig(rt, ref xpos, ref ypos, Quaternion.Euler(0, 90, 0), depth, height, width));
        camRigs.Add(CreateCamRig(rt, ref xpos, ref ypos, Quaternion.Euler(0, 270, 0), depth, height, width));
        camRigs.Add(CreateCamRig(rt, ref xpos, ref ypos, Quaternion.Euler(90, 0, 0), width, depth, height));
        camRigs.Add(CreateCamRig(rt, ref xpos, ref ypos, Quaternion.Euler(270, 0, 0), width, depth, height));
    }

    // Update is called once per frame
    void Update() {
        if (device == null) return;
        if (device.Stopped() || device.deviceInfo.id != cubeID) {
            UpdateEdgesMat(edgesDisabledMat);
            device = null;
            return;
        }

        GenerateAudioData();

        if (cubeScale != transform.lossyScale.x) {
            cubeScale = transform.lossyScale.x;
            Init(device);
        }

        if (device.diff) renderCountReset = 1;

        nextRender--;
        if (nextRender > 0) return;
        renderCountReset = Math.Min(renderCountReset + 1, 10);
        if (device.diff) renderCountReset = 1;
        nextRender = renderCountReset;

        RenderSettings.ambientSkyColor = new Color(0, 0, 0);
        DisableEdges();

        foreach (var rig in camRigs) {
            rig.Render();
        }

        RenderSettings.ambientSkyColor = new Color(1, 1, 1);
        EnableEdges();

        var renderTexture = RenderTexture.GetTemporary(rt.width, rt.height, 24, RenderTextureFormat.ARGB32);
        Graphics.Blit(rt, renderTexture);
        AsyncGPUReadback.Request(renderTexture, 0, request => {
            if (request.hasError) {
                Debug.Log("GPU readback error detected.");
            } else {
                byte[] rtArray = request.GetData<byte>().ToArray();
                Thread fillLEDDataThread = new Thread(() => FillLEDData(rtArray));
                fillLEDDataThread.Start();
            }
        });
        RenderTexture.ReleaseTemporary(renderTexture);
    }

    void GenerateAudioData() {
        Array.Fill(samples, 0f);
        foreach (var asource in audioSources) {
            for (int i = 0; i < OKAudioManager.frameLen; i++) {
                samples[i] += asource.GetSample(i)* vol;
            }
        }

        for (int i = 0; i < OKAudioManager.frameLen; i++) {
            audioData[aDataIdx++] = (short)samples[i];
            if (aDataIdx >= samplesInBlock) {
                aDataIdx = 0;
                device?.SendAudio(audioData);
            }
        }
    }

    void FillLEDData(byte[] rtArray) {
        if (device == null) return;
        int w = device.deviceInfo.width;
        int h = device.deviceInfo.height;
        int d = device.deviceInfo.depth;

        int id = 0;
        for (int z = 0; z < d; z++)
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) {
                    Color pix = new();
                    pix += camRigs[0].GetPixel(rtArray, x, y, z);
                    pix += camRigs[1].GetPixel(rtArray, w - 1 - x, y, d - 1 - z);
                    pix += camRigs[2].GetPixel(rtArray, d - 1 - z, y, x);
                    pix += camRigs[3].GetPixel(rtArray, z, y, w - 1 - x);
                    pix += camRigs[4].GetPixel(rtArray, x, z, h - 1 - y);
                    pix += camRigs[5].GetPixel(rtArray, x, d - 1 - z, y);

                    Color.RGBToHSV(pix, out float H, out float S, out float V);
                    pix = Color.HSVToRGB(H, Mathf.Min(1, S * sat), V);

                    float r = pix.r * 255 * bri;
                    float g = pix.g * 255 * bri;
                    float b = pix.b * 255 * bri;

                    ledData[id++] = (r > 255.0f) ? (byte)255 : (byte)r;
                    ledData[id++] = (g > 255.0f) ? (byte)255 : (byte)g;
                    ledData[id++] = (b > 255.0f) ? (byte)255 : (byte)b;
                }

        device?.SendFrame(ledData);
    }
}
