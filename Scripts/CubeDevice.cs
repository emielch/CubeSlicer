using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

public class CubeInfo {
    public int id = -1;
    public int width;
    public int height;
    public int depth;
}

[Serializable]
public class CubeDevice {
    Thread serialRceiveThread;
    SerialPort port;
    public CubeInfo cubeInfo;
    public bool infoUpdated = false;
    public bool Stopped() { return !port.IsOpen; }

    public void Init(string portName) {
        port = new SerialPort(portName, 9600);

        serialRceiveThread = new Thread(ReceiveCubeData);
        serialRceiveThread.Start();
    }

    CubeInfo ParseInfo(string info) {
        CubeInfo newInfo = null;
        string[] splitInfo = info.Split(',');
        if (splitInfo[0] == "CUBE") {
            newInfo = new CubeInfo();
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

    public void Send(byte[] data) {
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
                CubeInfo newInfo = ParseInfo(data);
                Debug.Log(data);
                if (cubeInfo == null || newInfo != null) {
                    cubeInfo = newInfo;
                    infoUpdated = true;
                    Debug.Log("Cube found on port: " + port.PortName + "\r\nID: " + cubeInfo.id + ", Width: " + cubeInfo.width + ", Height: " + cubeInfo.height + ", Depth: " + cubeInfo.depth);
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