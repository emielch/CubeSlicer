using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using System.Linq;

public class CubeSlicer : MonoBehaviour {
    public int cubeID;
    SerialDevice device;
    public RenderTexture rt;
    byte[] ledData;
    byte[] ledData1;
    byte[] ledData2;
    System.Object ledDataLock = new System.Object();
    public bool[] rigEnabled = new bool[6];
    public List<CubeCamRig> camRigs = new List<CubeCamRig>();
    public GameObject edges;
    public Material edgesEnabledMat;
    public Material edgesDisabledMat;
    [Range(0, 400)]
    public float bri = 100;
    float prevBri = 100;
    [Range(0, 3)]
    public float sat = 1F;
    float cubeScale;
    public MeshRenderer previewPlane;
    private static List<CubeSlicer> instances = new List<CubeSlicer>();

    Thread RTDataProcessThread;
    AutoResetEvent RTData_ResetEvent = new AutoResetEvent(false);
    Queue<byte[]> RTDataQueue = new Queue<byte[]>();
    System.Object RTDataQueueLock = new System.Object();

    public bool autoFrameSkipper = true;
    public int nextRender = 1;
    public int renderCountReset = 2;

    public bool makeListener = true;

    public int overSample = 4;


    void Awake() {
        if (FindObjectOfType<CubesManager>() == null) {
            GameObject cubesManagerGO = new GameObject();
            cubesManagerGO.name = "CubesManager";
            cubesManagerGO.AddComponent<CubesManager>();
        }
        UpdateEdgesMat(edgesDisabledMat);
        nextRender = UnityEngine.Random.Range(1, 20);

        if (makeListener && !gameObject.GetComponent<OKAudioListener>()) {
            OKAudioListener listener = gameObject.AddComponent<OKAudioListener>();
            listener.cubeID = cubeID;
        }

        if (rigEnabled.Length != 6) rigEnabled = new bool[6];
    }

    private void OnEnable() {
        instances.Add(this);
    }
    private void OnDisable() {
        instances.Remove(this);
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
        rig.Init(_rt, ref xpos, ref ypos, w, h, d, cubeScale, overSample);

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
        ledData1 = new byte[device.deviceInfo.width * device.deviceInfo.height * device.deviceInfo.depth * 3];
        ledData2 = new byte[device.deviceInfo.width * device.deviceInfo.height * device.deviceInfo.depth * 3];
        ledData = ledData1;

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientSkyColor = new Color(0, 0, 0);

        int width = device.deviceInfo.width;
        int height = device.deviceInfo.height;
        int depth = device.deviceInfo.depth;

        int xSize = width * depth;
        int ySize = height * 4;
        ySize += depth * (int)Math.Ceiling((float)(width * height) * 2 / (width * depth));

        rt = new RenderTexture(xSize * overSample, ySize * overSample, 24);

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

        device.SendBri(bri);

        if (RTDataProcessThread == null || !RTDataProcessThread.IsAlive) {
            RTDataProcessThread = new Thread(RTDataProcess);
            RTDataProcessThread.Start();
        }
    }

    // Update is called once per frame
    void Update() {
        if (device == null) return;
        if (device.Stopped() || device.deviceInfo.id != cubeID) {
            UpdateEdgesMat(edgesDisabledMat);
            device = null;
            RTDataProcessThread.Abort();
            return;
        }

        if (bri != prevBri) {
            prevBri = bri;
            device.SendBri(bri);
        }

        if (cubeScale != transform.lossyScale.x) {
            cubeScale = transform.lossyScale.x;
            Init(device);
        }

        if (autoFrameSkipper) {
            nextRender--;
            if (nextRender > 0) return;
            renderCountReset = Math.Min(renderCountReset + 1, 10);
            nextRender = renderCountReset;
        }

        RenderSettings.ambientSkyColor = new Color(0, 0, 0);

        for (int i = 0; i < camRigs.Count; i++) {
            if (rigEnabled[i]) camRigs[i].Render();
        }

        RenderSettings.ambientSkyColor = new Color(1, 1, 1);

        var renderTexture = RenderTexture.GetTemporary(rt.width / overSample, rt.height / overSample, 24, RenderTextureFormat.ARGB32);
        renderTexture.filterMode = FilterMode.Bilinear;
        Graphics.Blit(rt, renderTexture);
        AsyncGPUReadback.Request(renderTexture, 0, request => {
            if (request.hasError) {
                Debug.Log("GPU readback error detected.");
            } else {
                lock (RTDataQueueLock) {
                    RTDataQueue.Enqueue(request.GetData<byte>().ToArray());
                    RTData_ResetEvent.Set();
                }
            }
            RenderTexture.ReleaseTemporary(renderTexture);
        });
    }

    void RTDataProcess() {
        while (true) {
            RTData_ResetEvent.WaitOne();
            int queueCount = 1;
            while (queueCount > 0) {
                byte[] nextPacket = new byte[0];
                lock (RTDataQueueLock) {
                    queueCount = RTDataQueue.Count;
                    if (queueCount > 0) {
                        nextPacket = RTDataQueue.Dequeue();
                    }
                }
                if (nextPacket.Length != 0) {
                    FillLEDData(nextPacket);
                }
            }
        }
    }

    void OnApplicationQuit() {
        RTDataProcessThread.Abort();
    }


    void FillLEDData(byte[] rtArray) {
        if (device == null) return;
        if (Monitor.TryEnter(ledDataLock)) {
            try {
                int w = device.deviceInfo.width;
                int h = device.deviceInfo.height;
                int d = device.deviceInfo.depth;

                int id = 0;
                for (int z = 0; z < d; z++)
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++) {
                            Color pix = new();
                            if (rigEnabled[0]) pix += camRigs[0].GetPixel(rtArray, x, y, z);
                            if (rigEnabled[1]) pix += camRigs[1].GetPixel(rtArray, w - 1 - x, y, d - 1 - z);
                            if (rigEnabled[2]) pix += camRigs[2].GetPixel(rtArray, d - 1 - z, y, x);
                            if (rigEnabled[3]) pix += camRigs[3].GetPixel(rtArray, z, y, w - 1 - x);
                            if (rigEnabled[4]) pix += camRigs[4].GetPixel(rtArray, x, z, h - 1 - y);
                            if (rigEnabled[5]) pix += camRigs[5].GetPixel(rtArray, x, d - 1 - z, y);

                            Color.RGBToHSV(pix, out float H, out float S, out float V);
                            pix = Color.HSVToRGB(H, Mathf.Min(1, S * sat), V);

                            float r = pix.r * 255;
                            float g = pix.g * 255;
                            float b = pix.b * 255;

                            ledData[id++] = (r > 255.0f) ? (byte)255 : (byte)r;
                            ledData[id++] = (g > 255.0f) ? (byte)255 : (byte)g;
                            ledData[id++] = (b > 255.0f) ? (byte)255 : (byte)b;
                        }

                device?.SendFrame(ledData);

                if (autoFrameSkipper) {
                    bool areEqual = ledData1.SequenceEqual(ledData2);
                    if (!areEqual) {
                        renderCountReset = 0;
                        nextRender = 0;
                    }
                    if (ledData == ledData1) ledData = ledData2;
                    else ledData = ledData1;
                }
            } finally {
                Monitor.Exit(ledDataLock);
            }
        } else {
            //Debug.Log("unable to enter ledDataLock");
        }
    }
}
