using UnityEngine;
using UnityEngine.UIElements;

public class KickSynthGUI : MonoBehaviour
{
    public UIDocument uiDocument;

    private VisualElement knob;
    private VisualElement slider;
    private Label pitchLabel;
    private Label volumeLabel;

    void OnEnable()
    {
        var root = uiDocument.rootVisualElement;

        // Get UI references
        knob = root.Q<VisualElement>("PitchKnob");
        slider = root.Q<VisualElement>("VolumeSlider");
        pitchLabel = root.Q<Label>("PitchLabel");
        volumeLabel = root.Q<Label>("VolumeLabel");

        // (No styling here – only logic later)
    }
}

