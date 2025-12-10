using UnityEngine;
using UnityEngine.UIElements;

public class Fader : MonoBehaviour
{
    public UIDocument uiDocument;
    public string faderName = "AmpFader";
    public KickSynth kickSynth; // assign in inspector

    [Header("Targets (enable exactly one)")]
    public bool controlAmpDurationMs = false;
    public bool controlRiseMs = false;
    public bool controlFallMs = false;
    public bool controlDipLevelDb = false;
    public bool controlBounceMs = false;
    public bool controlFadeOutMs = false;
    public bool controlVolume = false;

    [Range(0f, 1f)]
    public float value = 0f;

    private VisualElement fader;
    private bool dragging = false;
    private float startDragY;
    private float startValue;

    void OnValidate()
    {
        // Ensure only one target is active to avoid driving multiple params.
        int active =
            (controlAmpDurationMs ? 1 : 0) +
            (controlRiseMs ? 1 : 0) +
            (controlFallMs ? 1 : 0) +
            (controlDipLevelDb ? 1 : 0) +
            (controlBounceMs ? 1 : 0) +
            (controlFadeOutMs ? 1 : 0) +
            (controlVolume ? 1 : 0);

        if (active > 1)
        {
            // Keep the first enabled, disable the rest.
            bool kept = false;
            if (controlAmpDurationMs) { kept = true; }
            else if (controlRiseMs) { kept = true; controlAmpDurationMs = false; }
            else if (controlFallMs) { kept = true; controlAmpDurationMs = controlRiseMs = false; }
            else if (controlDipLevelDb) { kept = true; controlAmpDurationMs = controlRiseMs = controlFallMs = false; }
            else if (controlBounceMs) { kept = true; controlAmpDurationMs = controlRiseMs = controlFallMs = controlDipLevelDb = false; }
            else if (controlFadeOutMs) { kept = true; controlAmpDurationMs = controlRiseMs = controlFallMs = controlDipLevelDb = controlBounceMs = false; }
            else if (controlVolume) { kept = true; controlAmpDurationMs = controlRiseMs = controlFallMs = controlDipLevelDb = controlBounceMs = controlFadeOutMs = false; }

            // Disable any remaining true flags beyond the first encountered
            if (kept)
            {
                int found = 0;
                if (controlAmpDurationMs) found++;
                else controlAmpDurationMs = false;
                if (controlRiseMs && found == 0) found++; else controlRiseMs = found == 0 && controlRiseMs ? true : false;
                if (controlFallMs && found == 0) found++; else controlFallMs = found == 0 && controlFallMs ? true : false;
                if (controlDipLevelDb && found == 0) found++; else controlDipLevelDb = found == 0 && controlDipLevelDb ? true : false;
                if (controlBounceMs && found == 0) found++; else controlBounceMs = found == 0 && controlBounceMs ? true : false;
                if (controlFadeOutMs && found == 0) found++; else controlFadeOutMs = found == 0 && controlFadeOutMs ? true : false;
                if (controlVolume && found == 0) found++; else controlVolume = found == 0 && controlVolume ? true : false;
            }

            Debug.LogWarning($"{name}: Only one fader target should be enabled. Extra targets were disabled.", this);
        }
    }

    void OnEnable()
    {
        uiDocument.rootVisualElement.RegisterCallback<AttachToPanelEvent>(evt =>
        {
            fader = uiDocument.rootVisualElement.Q<VisualElement>(faderName);
            if (fader == null)
            {
                Debug.LogError($"{name}: Could not find fader named '{faderName}' in the UI document. Check faderName and UXML element name.", this);
                return;
            }
            fader.pickingMode = PickingMode.Position;

            fader.RegisterCallback<PointerDownEvent>(OnPointerDown);
            fader.RegisterCallback<PointerUpEvent>(OnPointerUp);
            fader.RegisterCallback<PointerMoveEvent>(OnPointerMove);

            UpdateVisual();
        });
    }

    void OnPointerDown(PointerDownEvent evt)
    {
        dragging = true;
        startDragY = evt.position.y;
        startValue = value;
        fader.CapturePointer(evt.pointerId);
    }

    void OnPointerUp(PointerUpEvent evt)
    {
        dragging = false;
        fader.ReleasePointer(evt.pointerId);
    }

    void OnPointerMove(PointerMoveEvent evt)
    {
        if (!dragging)
            return;

        float deltaY = startDragY - evt.position.y; // drag up = increase
        float sensitivity = 0.005f;
        value = Mathf.Clamp01(startValue + deltaY * sensitivity);

        UpdateVisual();
        ApplyToSynth();
    }

    private void UpdateVisual()
    {
        if (fader == null) return;
        // Represent as vertical fill from bottom
        float pct = Mathf.Lerp(0f, 1f, value);
        fader.style.backgroundColor = new Color(0.2f, 0.7f, 1f, 0.6f);
        fader.style.height = new Length(100, LengthUnit.Percent); // keep full height
        fader.style.opacity = 1f;
        // Use a bottom border as a simple fill indicator
        fader.style.borderBottomWidth = pct * 4f; // StyleFloat
    }

    private void ApplyToSynth()
    {
        if (kickSynth == null)
            return;

        if (controlAmpDurationMs)
            kickSynth.ampDurationMs = Mathf.Lerp(10f, 400f, value);
        else if (controlRiseMs)
            kickSynth.riseMs = Mathf.Lerp(0f, 30f, value);
        else if (controlFallMs)
            kickSynth.fallMs = Mathf.Lerp(1f, 100f, value);
        else if (controlDipLevelDb)
            kickSynth.dipLevelDb = Mathf.Lerp(-24f, 0f, value);
        else if (controlBounceMs)
            kickSynth.bounceMs = Mathf.Lerp(1f, 100f, value);
        else if (controlFadeOutMs)
            kickSynth.fadeOutMs = Mathf.Lerp(5f, 200f, value);
        else if (controlVolume)
            kickSynth.volume = Mathf.Lerp(0.1f, 1.0f, value);
    }
}

