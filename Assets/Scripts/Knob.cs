using UnityEngine;
using UnityEngine.UIElements;

public class Knob : MonoBehaviour
{
    public UIDocument uiDocument;
    public string knobName = "PitchKnob";
    public KickSynth kickSynth;       // assign your KickSynth component in inspector
    public bool controlPitchCurve = true; // toggle for PitchKnob
    public bool controlPitchSweepMs = false; // toggle for Pitch Sweep knob


    [Range(0f, 1f)]
    public float value = 0f;

    private VisualElement knob;
    private bool dragging = false;
    private float startDragY;
    private float startValue;

    private float minAngle = -135f;
    private float maxAngle = 135f;

    void OnValidate()
    {
        // Make sure only one target is active to avoid driving both params.
        if (controlPitchCurve && controlPitchSweepMs)
        {
            controlPitchSweepMs = false;
            Debug.LogWarning($"{name}: Both controlPitchCurve and controlPitchSweepMs were enabled. Disabling pitch sweep to keep knob single-purpose.", this);
        }
    }

    void OnEnable()
    {
        uiDocument.rootVisualElement.RegisterCallback<AttachToPanelEvent>(evt =>
        {
            knob = uiDocument.rootVisualElement.Q<VisualElement>(knobName);
            if (knob == null)
            {
                Debug.LogError($"{name}: Could not find knob named '{knobName}' in the UI document. Check knobName and UXML element name.", this);
                return;
            }
            knob.pickingMode = PickingMode.Position;

            knob.RegisterCallback<PointerDownEvent>(OnPointerDown);
            knob.RegisterCallback<PointerUpEvent>(OnPointerUp);
            knob.RegisterCallback<PointerMoveEvent>(OnPointerMove);

            UpdateRotation();
        });
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
            if (controlPitchSweepMs)
            {
                // Map knob value (0‑1) to pitchSweepMs range (10‑400 ms)
                kickSynth.pitchSweepMs = Mathf.Lerp(10f, 400f, value);
            }
        }
    }


    private void UpdateRotation()
    {
        float angle = Mathf.Lerp(minAngle, maxAngle, value);
        knob.style.rotate = new StyleRotate(new Rotate(new Angle(angle, AngleUnit.Degree)));
    }
}
