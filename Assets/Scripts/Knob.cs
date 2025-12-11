using UnityEngine;
using UnityEngine.UIElements;

public class Knob : MonoBehaviour
{
    public UIDocument uiDocument;
    public string knobName = "PitchKnob";
    public KickSynth kickSynth;       // assign your KickSynth component in inspector
    public bool controlPitchCurve = true; // toggle for PitchKnob
    public bool controlPitchSweepMs = false; // toggle for Pitch Sweep knob
    public bool controlStartFreq = false; // toggle for Start Freq knob
    public bool controlEndFreq = false;   // toggle for End Freq knob


    [Range(0f, 1f)]
    public float value = 0f;

    private VisualElement knob;
    private VisualElement root;
    private bool bound = false;
    private bool dragging = false;
    private float startDragY;
    private float startValue;

    private float minAngle = -135f;
    private float maxAngle = 135f;

    void OnValidate()
    {
        // Make sure only one target is active to avoid driving multiple params.
        int active =
            (controlPitchCurve ? 1 : 0) +
            (controlPitchSweepMs ? 1 : 0) +
            (controlStartFreq ? 1 : 0) +
            (controlEndFreq ? 1 : 0);

        if (active > 1)
        {
            // Keep the first enabled, disable the rest.
            bool kept = false;
            if (controlPitchCurve) { kept = true; }
            else if (controlPitchSweepMs) { kept = true; controlPitchCurve = false; }
            else if (controlStartFreq) { kept = true; controlPitchCurve = controlPitchSweepMs = false; }
            else if (controlEndFreq) { kept = true; controlPitchCurve = controlPitchSweepMs = controlStartFreq = false; }

            // Disable any remaining true flags beyond the first encountered
            if (kept)
            {
                int found = 0;
                if (controlPitchCurve && found == 0) found++; else controlPitchCurve = found == 0 && controlPitchCurve ? true : false;
                if (controlPitchSweepMs && found == 0) found++; else controlPitchSweepMs = found == 0 && controlPitchSweepMs ? true : false;
                if (controlStartFreq && found == 0) found++; else controlStartFreq = found == 0 && controlStartFreq ? true : false;
                if (controlEndFreq && found == 0) found++; else controlEndFreq = found == 0 && controlEndFreq ? true : false;
            }

            Debug.LogWarning($"{name}: Only one knob target should be enabled. Extra targets were disabled.", this);
        }
    }

    void OnEnable()
    {
        TryBind();
        if (uiDocument != null && uiDocument.rootVisualElement != null)
            uiDocument.rootVisualElement.RegisterCallback<AttachToPanelEvent>(_ => TryBind());
    }

    void Update()
    {
        // Fallback binding for player builds if AttachToPanelEvent is late
        if (!bound)
            TryBind();
    }

    private void TryBind()
    {
        if (bound)
            return;

        root = uiDocument != null ? uiDocument.rootVisualElement : null;
        if (root == null)
            return;

        knob = root.Q<VisualElement>(knobName);
        if (knob == null)
            return;

        knob.pickingMode = PickingMode.Position;

        knob.RegisterCallback<PointerDownEvent>(OnPointerDown);
        knob.RegisterCallback<PointerUpEvent>(OnPointerUp);
        knob.RegisterCallback<PointerMoveEvent>(OnPointerMove);

        // Initialize value from synth if available
        if (kickSynth != null)
        {
            if (controlPitchCurve)
                value = Mathf.InverseLerp(0.1f, 0.5f, kickSynth.pitchCurve);
            else if (controlPitchSweepMs)
                value = Mathf.InverseLerp(10f, 400f, kickSynth.pitchSweepMs);
            else if (controlStartFreq)
                value = Mathf.InverseLerp(400f, 22000f, kickSynth.startFreq);
            else if (controlEndFreq)
                value = Mathf.InverseLerp(5f, 400f, kickSynth.endFreq);
        }

        UpdateRotation();
        bound = true;
        Debug.Log($"{name}: Knob bound to '{knobName}'", this);
    }

    void OnPointerDown(PointerDownEvent evt)
    {
        dragging = true;
        startDragY = evt.position.y;
        startValue = value;
        knob.CapturePointer(evt.pointerId);
    }

    void OnPointerUp(PointerUpEvent evt)
    {
        dragging = false;
        knob.ReleasePointer(evt.pointerId);
    }

    void OnPointerMove(PointerMoveEvent evt)
    {
        if (!dragging)
            return;

        float deltaY = startDragY - evt.position.y; // drag up = increase
        float sensitivity = 0.005f;
        value = Mathf.Clamp01(startValue + deltaY * sensitivity);

        UpdateRotation();

        if (kickSynth != null)
        {
            if (controlPitchCurve)
            {
                // Map knob value (0‑1) to pitchCurve range (0.1‑0.5)
                kickSynth.pitchCurve = Mathf.Lerp(0.1f, 0.5f, value);
            }
            else if (controlPitchSweepMs)
            {
                // Map knob value (0‑1) to pitchSweepMs range (10‑400 ms)
                kickSynth.pitchSweepMs = Mathf.Lerp(10f, 400f, value);
            }
            else if (controlStartFreq)
            {
                // Map knob value (0‑1) to startFreq range (400‑22000 Hz)
                kickSynth.startFreq = Mathf.Lerp(400f, 22000f, value);
            }
            else if (controlEndFreq)
            {
                // Map knob value (0‑1) to endFreq range (5‑400 Hz)
                kickSynth.endFreq = Mathf.Lerp(5f, 400f, value);
            }
        }
    }


    private void UpdateRotation()
    {
        float angle = Mathf.Lerp(minAngle, maxAngle, value);
        knob.style.rotate = new StyleRotate(new Rotate(new Angle(angle, AngleUnit.Degree)));
    }
}
