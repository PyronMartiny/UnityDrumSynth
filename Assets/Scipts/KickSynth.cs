using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.InputSystem;

public class KickSynth : MonoBehaviour
{
    [Header("Kick Paramaters")]
    public float startFreq = 200f;
    public float endFreq = 40f;
    public float durationMs = 214.29f;
    public float volume = 1.0f;

    private double phase = 0.0;
    private double sampleRate;
    private double totalSamples;
    private double currentSample;

    private bool playing = false;

    void Start()
    {
        sampleRate = AudioSettings.outputSampleRate;
        totalSamples = (durationMs / 1000.0) * sampleRate;
    }

    public void TriggerKick()
    {
        currentSample = 0.0;
        playing = true;
    }

    void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            TriggerKick();
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!playing)
            return;

        for (int i = 0; i < data.Length; i += channels)
        {
            if (currentSample >= totalSamples)
            {
                // Fill the rest of the buffer with silence
                for (int ch = 0; ch < channels; ch++)
                    data[i + ch] = 0f;
                playing = false;
                continue;
            }

            float t = (float)(currentSample / totalSamples);
            float freq = Mathf.Lerp(startFreq, endFreq, t); // pitch sweep

            phase += (2.0 * Mathf.PI * freq) / sampleRate;
            float sample = Mathf.Sin((float)phase) * volume; // full amplitude

            for (int ch = 0; ch < channels; ch++)
                data[i + ch] = sample;

            currentSample++;
        }
    }


}
