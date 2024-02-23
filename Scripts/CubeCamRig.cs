using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CubeCamRig : MonoBehaviour {
    public List<Camera> cams;
    public Vector2Int[,,] rtPosLUT;
    public Light camLight;
    public RenderTexture rt;
    public int rtWidth;
    int overSample;

    public void Init(RenderTexture _rt, ref int xpos, ref int ypos, int w, int h, int d, float scale, int os) {
        rt = _rt;
        rtWidth = rt.width;
        overSample = os;

        GameObject lightObj = new GameObject();
        lightObj.name = "Light";
        lightObj.transform.SetParent(transform);
        lightObj.transform.localPosition = new Vector3(0, 0, 0);
        lightObj.transform.localRotation = Quaternion.Euler(0, 0, 0);
        camLight = lightObj.AddComponent<Light>();
        camLight.type = LightType.Directional;
        camLight.color = new Color(1, 1, 1);
        lightObj.SetActive(false);

        cams = new List<Camera>();
        rtPosLUT = new Vector2Int[w, h, d];

        int edgesLayer = 31; // LayerMask.NameToLayer("CubeEdges");

        for (int j = 0; j < d; j++) {
            GameObject camObj = new GameObject();
            camObj.name = "Camera";
            camObj.transform.SetParent(transform);
            Camera cam = camObj.AddComponent(typeof(Camera)) as Camera;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0);
            cam.orthographic = true;
            cam.orthographicSize = 0.5F * scale;
            cam.nearClipPlane = (1 + (1F / d) * j) * scale;
            cam.farClipPlane = (1 + (1F / d) * (j + 1)) * scale;
            cam.useOcclusionCulling = false;
            cam.transform.localRotation = Quaternion.Euler(0, 0, 0);
            cam.transform.localPosition = new Vector3(0, 0, -1.5F * scale);
            cam.cullingMask &= ~(1 << edgesLayer);

            for (int x = 0; x < w; x++) {
                for (int y = 0; y < w; y++) {
                    rtPosLUT[x, y, j] = new Vector2Int(xpos / overSample + x, ypos / overSample + y);
                }
            }

            cam.targetTexture = rt;
            cam.rect = new Rect(xpos / (float)rt.width, ypos / (float)rt.height, (w * overSample) / (float)rt.width, (h * overSample) / (float)rt.height);
            xpos += w * overSample;
            if (xpos >= rt.width) {
                xpos = 0;
                ypos += h * overSample;
            }

            camObj.SetActive(false);
            cams.Add(cam);
        }
    }

    public void Render() {
        camLight.gameObject.SetActive(true);
        foreach (var cam in cams) {
            cam.Render();
        }
        camLight.gameObject.SetActive(false);
    }

    public Color GetPixel(byte[] rtArray, int x, int y, int z) {
        int pos = GetRTArrPos(x, y, z);
        return new Color(rtArray[pos] / 255f, rtArray[pos + 1] / 255f, rtArray[pos + 2] / 255f);
    }

    public int GetRTArrPos(int x, int y, int z) {
        Vector2Int pos = rtPosLUT[x, y, z];
        return (pos.x + pos.y * rtWidth / overSample) * 4;
    }
}
