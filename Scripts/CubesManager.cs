using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO.Ports;
using System.IO;
using System.ComponentModel.Composition.Primitives;
using System.Threading;
using System.Linq;
using System;

public class CubesManager : MonoBehaviour {
    public string[] portNames = { };
    public List<SerialDevice> serialDevices;
    public List<CubeSlicer> cubeSlicers;
    public List<OKAudioListener> audioListeners;
    public bool fakeCubes = false;


    void Start() {
        serialDevices = new List<SerialDevice>();
        cubeSlicers = FindObjectsOfType<CubeSlicer>().ToList();
        audioListeners = FindObjectsOfType<OKAudioListener>().ToList();

        if (fakeCubes) {
            foreach (var cubeSlicer in cubeSlicers) {
                SerialDevice newDevice = new SerialDevice();
                DeviceInfo cubeInfo = new DeviceInfo();
                cubeInfo.id = cubeSlicer.cubeID;
                cubeInfo.width = 16;
                cubeInfo.height = 16;
                cubeInfo.depth = 16;
                newDevice.InitFake(cubeInfo);
                serialDevices.Add(newDevice);
                cubeSlicer.Init(newDevice);
            }
        }
    }

    // Update is called once per frame
    void Update() {
        if (fakeCubes) return;
        FindSerialPorts();

        foreach (var serialDevice in serialDevices.Reverse<SerialDevice>()) {
            if (serialDevice.infoUpdated) {
                serialDevice.infoUpdated = false;
                if (serialDevice.Stopped() || serialDevice.deviceInfo.id == -1) {
                    serialDevices.Remove(serialDevice);
                    continue;
                }

                foreach (var cubeSlicer in cubeSlicers) {
                    if (cubeSlicer.cubeID == serialDevice.deviceInfo.id) {
                        cubeSlicer.Init(serialDevice);
                    }
                }

                foreach (var audioListener in audioListeners) {
                    if (audioListener.cubeID == serialDevice.deviceInfo.id) {
                        audioListener.Init(serialDevice);
                    }
                }
            }
        }
    }


    void FindSerialPorts() {
        string[] newPortNames = SerialPort.GetPortNames();
        if (Enumerable.SequenceEqual(newPortNames, portNames)) return;

        foreach (var portName in newPortNames) {
            if (portNames.Contains(portName)) continue;
            SerialDevice newDevice = new SerialDevice();
            newDevice.Init(portName);
            serialDevices.Add(newDevice);
        }
        portNames = newPortNames;
    }

    void OnApplicationQuit() {
        foreach (var serialDevice in serialDevices) {
            serialDevice.Stop();
        }
    }

}
