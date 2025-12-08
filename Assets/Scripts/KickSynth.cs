using UnityEngine;
using UnityEngine.InputSystem;

public class KickSynth : MonoBehaviour
{
    [Header("Kick Parameters")]
    public float startFreq = 200f;       // Frequency at start of pitch sweep
    public float endFreq = 40f;          // Frequency at end of pitch sweep
    [Range(0.1f, 1.0f)] public float volume = 1.0f;          // Overall amplitude

    [Header("Pitch Sweep")]
    [Range(10f, 400f)] public float pitchSweepMs = 214.29f; // Duration of pitch sweep in ms
    [Range(0.1f, 0.5f)]
    public float pitchCurve = 0.1f;        // >1 = steeper drop, <1 = slower

    [Header("Amplitude Envelope (ms/dB)")]
    [Range(10f, 400f)] public float ampDurationMs = 180f;   // Main envelope duration (rise+fall+bounce)
    [Range(0f, 30f)] public float riseMs = 5f;       // Attack
    [Range(1, 100f)] public float fallMs = 20f;     // Decay to dip
    [Range(-24f, 0f)] public float dipLevelDb = -12f;// Dip level in dB
    [Range(1f, 100f)] public float bounceMs = 50f;   // Bounce back to full
    [Range(5f, 200f)] public float fadeOutMs = 50f;  // Release (fade out at end)

    [Header("Debug Trigger")]
    public bool triggerKickButton = false; // Inspector button to test

    private double phase = 0.0;
    private double sampleRate;

    // --- Pitch sweep ---
    private double pitchTotalSamples;
    private double currentPitchSample;

    // --- Amplitude envelope ---
    private double ampTotalSamples;
    private double currentAmpSample;
    private double riseSamples;
    private double fallSamples;
    private double bounceSamples;
    private double fadeOutSamples;
    private float dipLevelLin;
    private float ampEnvelope = 0f;

    private bool playing = false;

    void Start()
    {
        sampleRate = AudioSettings.outputSampleRate;
    }

    public void TriggerKick()
    {
        // Reset counters
        currentPitchSample = 0;
        currentAmpSample = 0;

        // Pitch sweep total samples
        pitchTotalSamples = (pitchSweepMs / 1000.0) * sampleRate;

        // Start sine at peak
        phase = Mathf.PI / 2.0;

        // Convert dip level to linear
        dipLevelLin = Mathf.Pow(10f, dipLevelDb / 20f);

        // Scale rise/fall/bounce stages to fit ampDurationMs
        float totalStageMs = riseMs + fallMs + bounceMs;
        float scale = ampDurationMs / totalStageMs;

        riseSamples = (riseMs * scale / 1000.0) * sampleRate;
        fallSamples = (fallMs * scale / 1000.0) * sampleRate;
        bounceSamples = (bounceMs * scale / 1000.0) * sampleRate;

        // FadeOut / release is separate, not scaling main stages
        fadeOutSamples = (fadeOutMs / 1000.0) * sampleRate;

        ampTotalSamples = riseSamples + fallSamples + bounceSamples + fadeOutSamples;

        playing = true;
    }

    void Update()
    {
        // Trigger via spacebar or inspector
        if (Keyboard.current.spaceKey.wasPressedThisFrame || triggerKickButton)
        {
            TriggerKick();
            triggerKickButton = false;
        }
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!playing)
            return;

        int oversample = 4; // 4x oversampling to avoid aliasing

        for (int i = 0; i < data.Length; i += channels)
        {
            // Stop if amplitude envelope finished
            if (currentAmpSample >= ampTotalSamples)
            {
                for (int ch = 0; ch < channels; ch++)
                    data[i + ch] = 0f;

                playing = false;
                continue;
            }

            float sampleSum = 0f;

            for (int os = 0; os < oversample; os++)
            {   
                // --- PITCH SWEEP ---
                float tPitch = (float)(currentPitchSample / pitchTotalSamples);
                tPitch = Mathf.Clamp01(tPitch);
                float tCurved = Mathf.Pow(tPitch, pitchCurve);
                float freq = startFreq * Mathf.Pow(endFreq / startFreq, tCurved);

                //currentPitchSample++; // Increment pitch counter

                // --- AMPLITUDE ENVELOPE ---
                if (currentAmpSample < riseSamples) // Attack
                    ampEnvelope = (float)(currentAmpSample / riseSamples);
                else if (currentAmpSample < riseSamples + fallSamples) // Decay to dip
                {
                    float t = (float)((currentAmpSample - riseSamples) / fallSamples);
                    ampEnvelope = Mathf.Lerp(1f, dipLevelLin, t);
                }
                else if (currentAmpSample < riseSamples + fallSamples + bounceSamples) // Bounce back
                {
                    float t = (float)((currentAmpSample - riseSamples - fallSamples) / bounceSamples);
                    ampEnvelope = Mathf.Lerp(dipLevelLin, 1f, t);
                }
                else // FadeOut / Release
                {
                    float t = (float)((currentAmpSample - riseSamples - fallSamples - bounceSamples) / fadeOutSamples);
                    ampEnvelope = Mathf.Lerp(1f, 0f, t);
                }
                sampleSum += Mathf.Sin((float)phase) * ampEnvelope;


                // Increment phase
                phase += (2.0 * Mathf.PI * freq) / (sampleRate * oversample);
                currentPitchSample += 1.0 / oversample;
                currentAmpSample += 1.0 / oversample;
            }

            float finalSample = sampleSum / oversample * volume;

            // Write sample to all channels
            for (int ch = 0; ch < channels; ch++)
                data[i + ch] = finalSample;
        }
    }
}
