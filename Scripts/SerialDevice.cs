using Leap.Unity.Interaction;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
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
    Thread receiveThread;
    SerialPort port;
    bool fakeDevice = false;
    public DeviceInfo deviceInfo;
    public bool infoUpdated = false;
    public float audioQueueLevel = 0;
    public bool Stopped() { return fakeDevice ? false : !port.IsOpen; }

    Thread sendThread;
    AutoResetEvent send_ResetEvent = new AutoResetEvent(false);
    Queue<byte[]> sendQueue = new Queue<byte[]>();
    System.Object sendQueueLock = new System.Object();

    public void Init(string portName) {
        port = new SerialPort(portName);

        receiveThread = new Thread(ReceiveCubeData);
        receiveThread.Start();

        sendThread = new Thread(DataSender);
        sendThread.Start();
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
        if (!port.IsOpen) return;
        port.Close();
        Debug.Log("port " + port.PortName + " closed");
    }

    public void SendFrame(byte[] data) {
        byte[] preData = new byte[1];
        preData[0] = (byte)'%';

        lock (sendQueueLock) {
            sendQueue.Enqueue(preData);
            sendQueue.Enqueue(data);
            send_ResetEvent.Set();
        }
    }

    public void SendAudio(short[] data) {

        byte[] audioByteArray = new byte[data.Length * 2 + 1]; // 2 bytes per short
        audioByteArray[0] = (byte)'$';
        for (int i = 0; i < data.Length; i++) {
            byte[] tempBytes = BitConverter.GetBytes(data[i]);
            Array.Copy(tempBytes, 0, audioByteArray, i * 2 +1, tempBytes.Length);
        }

        lock (sendQueueLock) {
            sendQueue.Enqueue(audioByteArray);
            send_ResetEvent.Set();
        }
    }

    public void SendBri(float bri) {
        string formattedString = "b" + $"{bri:F1}".PadLeft(5);
        byte[] data = Encoding.ASCII.GetBytes(formattedString);

        lock (sendQueueLock) {
            sendQueue.Enqueue(data);
            send_ResetEvent.Set();
        }
    }

    private void DataSender() {
        while (true) {
            send_ResetEvent.WaitOne();
            int queueCount = 1;
            while (queueCount > 0) {
                byte[] nextPacket = new byte[0];
                lock (sendQueueLock) {
                    queueCount = sendQueue.Count;
                    if (queueCount > 0) {
                        nextPacket = sendQueue.Dequeue();
                    }
                }
                if (nextPacket.Length != 0) {
                    if (!port.IsOpen) continue;
                    port.Write(nextPacket, 0, nextPacket.Length);
                }
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
                } else if (data.StartsWith("AQ")) {
                    string[] splitDiff = data.Split(',');
                    audioQueueLevel = float.Parse(splitDiff[1]);
                } else {
                    Debug.Log(port.PortName + ": " + data);
                }
            } catch (TimeoutException) {

            } catch (ThreadAbortException) {

            } catch (IOException e) {
                Debug.Log(e);
                Stop();
            } catch (Exception e) {
                Debug.LogException(e);
                Stop();
            }
        }
    }
}