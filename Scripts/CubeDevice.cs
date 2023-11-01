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

public class CubeDevice {
    Thread serialRceiveThread;
    Thread serialSendThread;
    SerialPort port;
    public CubeInfo cubeInfo;
    public bool infoUpdated = false;
    byte[] sendData;
    public bool Stopped() { return !port.IsOpen; }

    public void Init(string portName) {
        port = new SerialPort(portName, 9600);
        StartReceiving();
        port.Write("?");

        //CubeInfo newInfo = ParseInfo(data);
        //if (newInfo.id != -1) {
        //    cubeInfo = newInfo;
        //    Debug.Log("Cube found on port: " + portName + "\r\nID: " + cubeInfo.id + ", Width: " + cubeInfo.width + ", Height: " + cubeInfo.height + ", Depth: " + cubeInfo.depth);

        //    return true;
        //}
        //return false;
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
        port.Close();
        serialRceiveThread.Abort();
    }

    public void Send(byte[] data) {
        if (serialSendThread != null && serialSendThread.ThreadState == ThreadState.Running) {
            serialSendThread.Join(500);
            if (serialSendThread.ThreadState == ThreadState.Running) return;  // if the thread has still not ended, return
        }

        sendData = data;
        serialSendThread = new Thread(SendSerial);
        serialSendThread.Start();
    }
    void SendSerial() {
        try {
            port.Write("%");
            port.Write(sendData, 0, sendData.Length);
            port.BaseStream.Flush();
        } catch (Exception e) {
            Debug.LogException(e);
            Stop();
        }
    }

    void StartReceiving() {
        port.ReadTimeout = 30000;
        port.Open();

        serialRceiveThread = new Thread(ReceiveCubeData);
        serialRceiveThread.Start();
    }

    void ReceiveCubeData() {
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