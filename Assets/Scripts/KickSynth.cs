using UnityEngine;
using UnityEngine.InputSystem;

public class KickSynth : MonoBehaviour
{
    public System.Action onKickStart;
    public System.Action<float[]> onKickComplete; // optional for snapshot consumers

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
    [Range(0f, 20f)] public float riseMs = 5f;       // Attack
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
    private double riseEnd;
    private double fallEnd;
    private double bounceEnd;
    private double fadeEnd;

    private bool playing = false;

    // --- Oscilloscope buffer ---
    private readonly object scopeLock = new object();
    private float[] scopeBuffer = new float[2048];
    private int scopeWriteIndex = 0;
    private System.Collections.Generic.List<float> snapshot = new System.Collections.Generic.List<float>(4096);
    private bool snapshotDone = false;

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

        // All stages are independent and use their raw ms values
        // They are sequential: rise -> fall -> bounce (each starts when previous ends)
        riseSamples = (riseMs / 1000.0) * sampleRate;
        fallSamples = (fallMs / 1000.0) * sampleRate;
        bounceSamples = (bounceMs / 1000.0) * sampleRate;
        fadeOutSamples = (fadeOutMs / 1000.0) * sampleRate;

        // Define stage end positions for envelope
        riseEnd = riseSamples;
        fallEnd = riseEnd + fallSamples;
        bounceEnd = fallEnd + bounceSamples;

        // ampDurationMs: signal plays at full gain (1.0) after bounce until this point
        double ampDurationSamples = (ampDurationMs / 1000.0) * sampleRate;

        // fadeOutMs: release stage added AFTER ampDurationMs completes (like synth release)
        // Fades from 1.0 to 0.0 over fadeOutMs
        fadeEnd = ampDurationSamples + fadeOutSamples;

        // Total playback = ampDurationMs + fadeOutMs
        ampTotalSamples = fadeEnd;


        // Reset oscilloscope buffer and notify listeners for sync
        lock (scopeLock)
        {
            scopeWriteIndex = 0;
        }
        snapshotDone = false;
        snapshot.Clear();
        onKickStart?.Invoke();

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
                if (!snapshotDone)
                {
                    snapshotDone = true;
                    var finalSnap = snapshot.ToArray();
                    onKickComplete?.Invoke(finalSnap);
                }
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
                double ampDurationSamples = (ampDurationMs / 1000.0) * sampleRate;
                
                if (currentAmpSample < riseEnd)
                {
                    ampEnvelope = (float)(currentAmpSample / riseSamples);
                }
                else if (currentAmpSample < fallEnd)
                {
                    float t = (float)((currentAmpSample - riseEnd) / fallSamples);
                    ampEnvelope = Mathf.Lerp(1f, dipLevelLin, t);
                }
                else if (currentAmpSample < bounceEnd)
                {
                    float t = (float)((currentAmpSample - fallEnd) / bounceSamples);
                    ampEnvelope = Mathf.Lerp(dipLevelLin, 1f, t);
                }
                else if (currentAmpSample < ampDurationSamples)
                {
                    // After bounce, hold at full gain (1.0) until ampDurationMs completes
                    ampEnvelope = 1f;
                }
                else if (currentAmpSample < fadeEnd)
                {
                    // Release stage: fadeOut starts AFTER ampDurationMs, fades from 1.0 to 0.0
                    float t = (float)((currentAmpSample - ampDurationSamples) / fadeOutSamples);
                    ampEnvelope = Mathf.Lerp(1f, 0f, t);
                }
                else
                {
                    ampEnvelope = 0f;
                }


                sampleSum += Mathf.Sin((float)phase) * ampEnvelope;


                // Increment phase
                phase += (2.0 * Mathf.PI * freq) / (sampleRate * oversample);
                currentPitchSample += 1.0 / oversample;
                currentAmpSample += 1.0 / oversample;
            }

            float finalSample = sampleSum / oversample * volume;

            // Store for oscilloscope (single channel snapshot)
            lock (scopeLock)
            {
                scopeBuffer[scopeWriteIndex] = finalSample;
                scopeWriteIndex = (scopeWriteIndex + 1) % scopeBuffer.Length;
            }
            if (!snapshotDone)
            {
                snapshot.Add(finalSample);
            }

            // Write sample to all channels
            for (int ch = 0; ch < channels; ch++)
                data[i + ch] = finalSample;
        }
    }

    /// <summary>
    /// Copy the most recent audio samples into target. Returns number copied.
    /// </summary>
    public int CopyScopeData(float[] target)
    {
        if (target == null || target.Length == 0)
            return 0;

        lock (scopeLock)
        {
            int count = Mathf.Min(target.Length, scopeBuffer.Length);
            int start = (scopeWriteIndex - count + scopeBuffer.Length) % scopeBuffer.Length;
            for (int i = 0; i < count; i++)
            {
                target[i] = scopeBuffer[(start + i) % scopeBuffer.Length];
            }
            return count;
        }
    }

    /// <summary>
    /// Generate a full preview of the kick waveform using current parameters (offline),
    /// resampled to a fixed output length for consistent drawing.
    /// </summary>
        public float[] GeneratePreviewWaveform(int outputSamples = 8192)
        {
            double sr = sampleRate > 0 ? sampleRate : AudioSettings.outputSampleRate;
            if (sr <= 0) return System.Array.Empty<float>();

            int oversample = 4;

            // --- Calculate pitch sweep ---
            double localPitchTotalSamples = (pitchSweepMs / 1000.0) * sr;

            // --- Calculate amplitude envelope stages ---
            // All stages are independent and use their raw ms values
            // They are sequential: rise -> fall -> bounce (each starts when previous ends)
            double localRise = (riseMs / 1000.0) * sr;
            double localFall = (fallMs / 1000.0) * sr;
            double localBounce = (bounceMs / 1000.0) * sr;
            double localFade = (fadeOutMs / 1000.0) * sr;

            // Stage end positions
            double riseEnd = localRise;
            double fallEnd = riseEnd + localFall;
            double bounceEnd = fallEnd + localBounce;

            // ampDurationMs: signal plays at full gain (1.0) after bounce until this point
            double localAmpDurationSamples = (ampDurationMs / 1000.0) * sr;

            // fadeOutMs: release stage added AFTER ampDurationMs completes (like synth release)
            // Fades from 1.0 to 0.0 over fadeOutMs
            double fadeEnd = localAmpDurationSamples + localFade;

            // Total playback = ampDurationMs + fadeOutMs
            double localAmpTotal = fadeEnd;

            double localPhase = Mathf.PI / 2.0; // Start sine at peak
            double localPitchSample = 0;
            double localAmpSample = 0;
            float localDip = Mathf.Pow(10f, dipLevelDb / 20f);

            var list = new System.Collections.Generic.List<float>();
            float previewAmpEnv = 0f; // local to avoid touching shared ampEnvelope

            // Generate full waveform at oversampled resolution
            while (localAmpSample < localAmpTotal)
            {
                float sampleSum = 0f;

                for (int os = 0; os < oversample; os++)
                {
                    float tPitch = (float)(localPitchSample / localPitchTotalSamples);
                    tPitch = Mathf.Clamp01(tPitch);
                    float tCurved = Mathf.Pow(tPitch, pitchCurve);
                    float freq = startFreq * Mathf.Pow(endFreq / startFreq, tCurved);

                    // --- Amplitude Envelope using stage ends ---
                    if (localAmpSample < riseEnd)
                    {
                        previewAmpEnv = (float)(localAmpSample / localRise);
                    }
                    else if (localAmpSample < fallEnd)
                    {
                        float t = (float)((localAmpSample - riseEnd) / localFall);
                        previewAmpEnv = Mathf.Lerp(1f, localDip, t);
                    }
                    else if (localAmpSample < bounceEnd)
                    {
                        float t = (float)((localAmpSample - fallEnd) / localBounce);
                        previewAmpEnv = Mathf.Lerp(localDip, 1f, t);
                    }
                    else if (localAmpSample < localAmpDurationSamples)
                    {
                        // After bounce, hold at full gain (1.0) until ampDurationMs completes
                        previewAmpEnv = 1f;
                    }
                    else if (localAmpSample < fadeEnd)
                    {
                        // Release stage: fadeOut starts AFTER ampDurationMs, fades from 1.0 to 0.0
                        float t = (float)((localAmpSample - localAmpDurationSamples) / localFade);
                        previewAmpEnv = Mathf.Lerp(1f, 0f, t);
                    }
                    else
                    {
                        previewAmpEnv = 0f;
                    }

                    sampleSum += Mathf.Sin((float)localPhase) * previewAmpEnv;

                    // Increment phase and counters
                    localPhase += (2.0 * Mathf.PI * freq) / (sr * oversample);
                    localPitchSample += 1.0 / oversample;
                    localAmpSample += 1.0 / oversample;
                }

                float finalSample = sampleSum / oversample * volume;
                list.Add(finalSample);
            }

            // Resample to fixed output length
            if (list.Count == 0 || outputSamples <= 1)
                return new float[outputSamples > 0 ? outputSamples : 0];

            var outArr = new float[outputSamples];
            int srcCount = list.Count;

            for (int i = 0; i < outputSamples; i++)
            {
                float t = (outputSamples == 1) ? 0f : i / (float)(outputSamples - 1);
                float srcPos = t * (srcCount - 1);
                int idx0 = Mathf.FloorToInt(srcPos);
                int idx1 = Mathf.Min(idx0 + 1, srcCount - 1);
                float frac = srcPos - idx0;
                outArr[i] = Mathf.Lerp(list[idx0], list[idx1], frac);
            }

            return outArr;
        }

}
