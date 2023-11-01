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
    public List<CubeDevice> cubeDevices;
    public List<CubeCam> cubeCams;


    void Start() {
        cubeDevices = new List<CubeDevice>();
        cubeCams = FindObjectsOfType<CubeCam>().ToList();
    }

    // Update is called once per frame
    void Update() {
        FindSerialPorts();

        foreach (var cubeDevice in cubeDevices.Reverse<CubeDevice>()) {
            if (cubeDevice.infoUpdated) {
                cubeDevice.infoUpdated = false;
                if (cubeDevice.cubeInfo.id == -1) {
                    cubeDevices.Remove(cubeDevice);
                    continue;
                }

                foreach (var cubeCam in cubeCams) {
                    if (cubeCam.cubeID == cubeDevice.cubeInfo.id) {
                        cubeCam.Init(cubeDevice);
                        break;
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
            CubeDevice newCube = new CubeDevice();
            newCube.Init(portName);
            cubeDevices.Add(newCube);
        }
        portNames = newPortNames;
    }

    void OnApplicationQuit() {
        foreach (var cubeDevice in cubeDevices) {
            cubeDevice.Stop();
        }
    }

}
