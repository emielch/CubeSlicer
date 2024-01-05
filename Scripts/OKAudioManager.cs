using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-2)]
public class OKAudioManager : MonoBehaviour
{
    public static int frameLen = 0;
    public static float sampleRate = 44200;

    void Start()
    {
        
    }

    
    void Update()
    {
        frameLen = Math.Min((int)(sampleRate * Time.deltaTime), (int)sampleRate / 4);
    }
}
