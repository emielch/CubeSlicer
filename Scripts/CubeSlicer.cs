using Leap.Unity;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

public class CubeCam : MonoBehaviour {
    public int cubeID;
    CubeDevice device;
    public RenderTexture rt;
    byte[] ledData;
    public List<CubeCamRig> camRigs = new List<CubeCamRig>();
    public GameObject edges;
    public Material edgesEnabledMat;
    public Material edgesDisabledMat;
    public float bri = 0.1F;
    public float sat = 1F;
    public int pos = 0;
    public float cubeScale;
    public Material rtMat;

    void Start() {
        if (FindObjectOfType<CubesManager>() == null) {
            GameObject cubesManagerGO = new GameObject();
            cubesManagerGO.name = "CubesManager";
            cubesManagerGO.AddComponent<CubesManager>();
        }
        updateEdges(edgesDisabledMat);
    }

    void updateEdges(Material mat) {
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

    public void Init(CubeDevice _device) {
        foreach (var rig in camRigs) {
            Destroy(rig);
        }
        camRigs.Clear();

        updateEdges(edgesEnabledMat);
        device = _device;

        cubeScale = transform.lossyScale.x;
        ledData = new byte[device.cubeInfo.width * device.cubeInfo.height * device.cubeInfo.depth * 3];

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientSkyColor = new Color(0, 0, 0);

        int width = device.cubeInfo.width;
        int height = device.cubeInfo.height;
        int depth = device.cubeInfo.depth;

        int xSize = width * depth;
        int ySize = height * 4;
        ySize += depth * (int)Math.Ceiling((float)(width * height) * 2 / (width * depth));

        rt = new RenderTexture(xSize, ySize, 24);

        if (rtMat != null)
            rtMat.SetTexture("_EmissionMap", rt);

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
        if (device.Stopped() || device.cubeInfo.id != cubeID) {
            updateEdges(edgesDisabledMat);
            device = null;
            return;
        }

        if (cubeScale != transform.lossyScale.x) {
            cubeScale = transform.lossyScale.x;
            Init(device);
        }

        RenderSettings.ambientSkyColor = new Color(0, 0, 0);
        edges.SetActive(false);

        foreach (var rig in camRigs) {
            rig.Render();
        }

        RenderSettings.ambientSkyColor = new Color(1, 1, 1);
        edges.SetActive(true);

        //Texture2D texture2D = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        //RenderTexture.active = rt;
        //texture2D.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        //RenderTexture.active = null; // JC: added to avoid errors
        //FillLEDData(texture2D.GetRawTextureData());

        var renderTexture = RenderTexture.GetTemporary(rt.width, rt.height, 24, RenderTextureFormat.ARGB32);
        Graphics.Blit(rt, renderTexture);
        // Readback
        // https://issuetracker.unity3d.com/issues/asyncgpureadback-dot-requestintonativearray-crashes-unity-when-trying-to-request-a-copy-to-the-same-nativearray-multiple-times
        // https://issuetracker.unity3d.com/issues/asyncgpureadback-dot-requestintonativearray-causes-invalidoperationexception-on-nativearray
        AsyncGPUReadback.Request(renderTexture, 0, request => {
            if (request.hasError) {
                Debug.Log("GPU readback error detected.");
            } else {
                // Extract the color components from the output array
                byte[] rtArray = request.GetData<byte>().ToArray();
                Thread fillLEDDataThread = new Thread(() => FillLEDData(rtArray, device.cubeInfo.width, device.cubeInfo.height, device.cubeInfo.depth));
                fillLEDDataThread.Start();
            }
        });
        // Release
        RenderTexture.ReleaseTemporary(renderTexture);
    }

    void FillLEDData(byte[] rtArray, int w, int h, int d) {
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

        device.Send(ledData);
    }
}
