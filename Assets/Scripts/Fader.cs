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
    private VisualElement crossbar;
    private VisualElement hitArea;
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
                if (controlAmpDurationMs && found == 0) found++; else controlAmpDurationMs = found == 0 && controlAmpDurationMs ? true : false;
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

            // Find crossbar and hit area for sliders
            if (fader.ClassListContains("slider"))
            {
                hitArea = fader.Q<VisualElement>(className: "slider-hit-area");
                if (hitArea != null)
                {
                    crossbar = hitArea.Q<VisualElement>(className: "slider-crossbar");
                    // Make hit area draggable
                    hitArea.pickingMode = PickingMode.Position;
                    hitArea.RegisterCallback<PointerDownEvent>(OnHitAreaDown);
                    hitArea.RegisterCallback<PointerUpEvent>(OnHitAreaUp);
                    hitArea.RegisterCallback<PointerMoveEvent>(OnHitAreaMove);
                }
                else
                {
                    // Fallback to crossbar if no hit area
                    crossbar = fader.Q<VisualElement>(className: "slider-crossbar");
                }
            }

            fader.RegisterCallback<PointerDownEvent>(OnPointerDown);
            fader.RegisterCallback<PointerUpEvent>(OnPointerUp);
            fader.RegisterCallback<PointerMoveEvent>(OnPointerMove);

            // Initialize value from synth if available
            if (kickSynth != null)
            {
                if (controlVolume)
                    value = Mathf.InverseLerp(0.1f, 1.0f, kickSynth.volume);
                else if (controlAmpDurationMs)
                    value = Mathf.InverseLerp(10f, 400f, kickSynth.ampDurationMs);
                else if (controlRiseMs)
                    value = Mathf.InverseLerp(0f, 20f, kickSynth.riseMs);
                else if (controlFallMs)
                    value = Mathf.InverseLerp(1f, 100f, kickSynth.fallMs);
                else if (controlDipLevelDb)
                    value = Mathf.InverseLerp(-24f, 0f, kickSynth.dipLevelDb);
                else if (controlBounceMs)
                    value = Mathf.InverseLerp(1f, 100f, kickSynth.bounceMs);
                else if (controlFadeOutMs)
                    value = Mathf.InverseLerp(5f, 200f, kickSynth.fadeOutMs);
            }

            // Schedule update after layout is calculated
            fader.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            UpdateVisual();
        });
    }

    void OnPointerDown(PointerDownEvent evt)
    {
        // Only handle if not a slider (sliders use hit area)
        if (fader.ClassListContains("slider"))
            return;
            
        dragging = true;
        startDragY = evt.position.y;
        
        // Calculate value from click position (always vertical)
        if (fader != null)
        {
            var faderWorldPos = fader.worldBound;
            float localY = evt.position.y - faderWorldPos.y;
            float faderHeight = faderWorldPos.height;
            if (faderHeight > 0)
            {
                // Invert: top = 1, bottom = 0
                value = Mathf.Clamp01(1f - (localY / faderHeight));
            }
            UpdateVisual();
            ApplyToSynth();
        }
        
        startValue = value;
        fader.CapturePointer(evt.pointerId);
    }
    
    void OnHitAreaDown(PointerDownEvent evt)
    {
        dragging = true;
        startDragY = evt.position.y;
        
        // Calculate value from click position on the fader track
        if (fader != null)
        {
            var faderWorldPos = fader.worldBound;
            float localY = evt.position.y - faderWorldPos.y;
            float faderHeight = faderWorldPos.height;
            if (faderHeight > 0)
            {
                // Invert: top = 1, bottom = 0
                value = Mathf.Clamp01(1f - (localY / faderHeight));
            }
            UpdateVisual();
            ApplyToSynth();
        }
        
        startValue = value;
        if (hitArea != null)
            hitArea.CapturePointer(evt.pointerId);
    }
    
    void OnHitAreaUp(PointerUpEvent evt)
    {
        dragging = false;
        if (hitArea != null)
            hitArea.ReleasePointer(evt.pointerId);
    }
    
    void OnHitAreaMove(PointerMoveEvent evt)
    {
        if (!dragging)
            return;

        // Always vertical movement
        float deltaY = startDragY - evt.position.y; // drag up = increase
        float sensitivity = 0.005f;
        var faderWorldPos = fader.worldBound;
        if (faderWorldPos.height > 0)
        {
            sensitivity = 1f / faderWorldPos.height;
        }
        value = Mathf.Clamp01(startValue + deltaY * sensitivity);

        UpdateVisual();
        ApplyToSynth();
    }

    void OnPointerUp(PointerUpEvent evt)
    {
        dragging = false;
        fader.ReleasePointer(evt.pointerId);
    }
    
    void OnGeometryChanged(GeometryChangedEvent evt)
    {
        // Update visual when layout is calculated
        UpdateVisual();
    }

    void OnPointerMove(PointerMoveEvent evt)
    {
        if (!dragging)
            return;

        // Always vertical movement
        float deltaY = startDragY - evt.position.y; // drag up = increase
        float sensitivity = 0.005f;
        var faderWorldPos = fader.worldBound;
        if (faderWorldPos.height > 0)
        {
            sensitivity = 1f / faderWorldPos.height;
        }
        value = Mathf.Clamp01(startValue + deltaY * sensitivity);

        UpdateVisual();
        ApplyToSynth();
    }

    private void UpdateVisual()
    {
        if (fader == null) return;
        
        if (hitArea != null)
        {
            // Position hit area (which contains crossbar) vertically: value 0 = bottom, value 1 = top
            // Try resolved style first, then fallback to layout or default values
            float faderHeight = fader.resolvedStyle.height;
            float faderWidth = fader.resolvedStyle.width;
            float hitAreaHeight = hitArea.resolvedStyle.height;
            float hitAreaWidth = hitArea.resolvedStyle.width;
            
            // If resolved styles aren't available, try layout dimensions
            if (faderHeight <= 0) faderHeight = fader.layout.height > 0 ? fader.layout.height : 120f;
            if (faderWidth <= 0) faderWidth = fader.layout.width > 0 ? fader.layout.width : 4f;
            if (hitAreaHeight <= 0) hitAreaHeight = hitArea.layout.height > 0 ? hitArea.layout.height : 20f;
            if (hitAreaWidth <= 0) hitAreaWidth = hitArea.layout.width > 0 ? hitArea.layout.width : 40f;
            
            // Center hit area horizontally on the fader track
            float leftPos = (faderWidth * 0.5f) - (hitAreaWidth * 0.5f);
            hitArea.style.left = new StyleLength(new Length(leftPos, LengthUnit.Pixel));
            
            // Calculate position on fader track (0 = bottom, 1 = top)
            float positionOnTrack = (1f - value) * faderHeight;
            
            // Position hit area so its center aligns with the position on track
            // This centers the crossbar (which is centered in hit area) on the fader track
            float topPos = positionOnTrack - (hitAreaHeight * 0.5f);
            hitArea.style.top = new StyleLength(new Length(topPos, LengthUnit.Pixel));
        }
        else if (crossbar != null)
        {
            // Fallback: position crossbar directly if no hit area
            float faderHeight = fader.resolvedStyle.height;
            float crossbarHeight = crossbar.resolvedStyle.height;
            if (faderHeight <= 0) faderHeight = 120f;
            if (crossbarHeight <= 0) crossbarHeight = 4f;
            
            float topPos = (1f - value) * (faderHeight - crossbarHeight);
            crossbar.style.top = new StyleLength(new Length(topPos, LengthUnit.Pixel));
        }
        else
        {
            // Vertical fader without crossbar: represent as vertical fill from bottom
            float pct = Mathf.Lerp(0f, 1f, value);
            fader.style.backgroundColor = new Color(0.2f, 0.7f, 1f, 0.6f);
            fader.style.height = new Length(100, LengthUnit.Percent);
            fader.style.opacity = 1f;
            fader.style.borderBottomWidth = pct * 4f;
        }
    }

    private void ApplyToSynth()
    {
        if (kickSynth == null)
            return;

        if (controlAmpDurationMs)
            kickSynth.ampDurationMs = Mathf.Lerp(10f, 400f, value);
        else if (controlRiseMs)
            kickSynth.riseMs = Mathf.Lerp(0f, 20f, value);
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

