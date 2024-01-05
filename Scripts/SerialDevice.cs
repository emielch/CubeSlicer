using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

public class DeviceInfo {
    public int id = -1;
    public int width;
    public int height;
    public int depth;
}

[Serializable]
public class SerialDevice {
    Thread serialRceiveThread;
    SerialPort port;
    bool fakeDevice = false;
    public DeviceInfo deviceInfo;
    public bool infoUpdated = false;
    public bool diff = true;
    public bool Stopped() { return fakeDevice ? false : !port.IsOpen; }

    public void Init(string portName) {
        port = new SerialPort(portName);

        serialRceiveThread = new Thread(ReceiveCubeData);
        serialRceiveThread.Start();
    }

    public void InitFake(DeviceInfo _info) {
        deviceInfo = _info;
        fakeDevice = true;
    }

    DeviceInfo ParseInfo(string info) {
        DeviceInfo newInfo = null;
        string[] splitInfo = info.Split(',');
        if (splitInfo[0] == "CUBE") {
            newInfo = new DeviceInfo();
            newInfo.id = int.Parse(splitInfo[1]);
            newInfo.width = int.Parse(splitInfo[2]);
            newInfo.height = int.Parse(splitInfo[3]);
            newInfo.depth = int.Parse(splitInfo[4]);
        }
        return newInfo;
    }

    public void Stop() {
        infoUpdated = true;
        port.Close();
        serialRceiveThread.Abort();
    }

    public void SendFrame(byte[] data) {
        if (fakeDevice) return;

        lock (port) {
            try {
                port.Write("%");
                port.Write(data, 0, data.Length);
                port.BaseStream.Flush();
            } catch (Exception e) {
                Debug.LogException(e);
                Stop();
            }
        }
    }

    public void SendAudio(short[] data) {
        if (fakeDevice) return;
        byte[] byteArray = new byte[data.Length * 2]; // 2 bytes per short
        for (int i = 0; i < data.Length; i++) {
            byte[] tempBytes = BitConverter.GetBytes(data[i]);
            Array.Copy(tempBytes, 0, byteArray, i * 2, tempBytes.Length);
        }

        lock (port) {
            try {
                port.Write("$");
                port.Write(byteArray, 0, byteArray.Length);
                port.BaseStream.Flush();
            } catch (Exception e) {
                Debug.LogException(e);
                Stop();
            }
        }
    }

    void ReceiveCubeData() {
        // try a couple of times to open the port (not immediately available for opening after inserting USB)
        int tries = 10;
        while (tries > 0) {
            try {
                port.ReadTimeout = 30000;
                port.Open();
                break;
            } catch (Exception e) {
                Debug.Log(e);
                Thread.Sleep(100);
            }
            tries--;
        }
        if (!port.IsOpen) {
            Stop();
            return;
        }


        port.Write("?"); // request cube info

        while (true) {
            if (!port.IsOpen) {
                return;
            }

            try {
                string data = port.ReadLine();
                if (data.StartsWith("CUBE")) {
                    DeviceInfo newInfo = ParseInfo(data);
                    if (deviceInfo == null || newInfo != null) {
                        deviceInfo = newInfo;
                        infoUpdated = true;
                        Debug.Log("Cube found on port: " + port.PortName + "\r\nID: " + deviceInfo.id + ", Width: " + deviceInfo.width + ", Height: " + deviceInfo.height + ", Depth: " + deviceInfo.depth);
                    }
                } else if (data.StartsWith("DIFF")) {
                    string[] splitDiff = data.Split(',');
                    diff = int.Parse(splitDiff[1]) == 1;
                } else {
                    Debug.Log(data);
                }
            } catch (TimeoutException) {

            } catch (ThreadAbortException) {

            } catch (Exception e) {
                Debug.LogException(e);
                Stop();
            }
        }
    }
}