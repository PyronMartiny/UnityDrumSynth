using UnityEngine;
using UnityEngine.UIElements;

public class Oscilloscope : MonoBehaviour
{
    public UIDocument uiDocument;
    public string imageName = "ScopeImage";
    public KickSynth kickSynth;

    [Header("Display")]
    public int textureWidth = 2048;
    public int textureHeight = 256;
    public Color background = new Color(0.08f, 0.08f, 0.08f, 1f);
    public Color lineColor = new Color(0f, 0.9f, 0.4f, 1f);
    public Color midLineColor = new Color(0f, 0f, 0f, 0.4f);
    public float refreshInterval = 0.1f; // seconds between preview redraws

    private Texture2D texture;
    private Image image;
    private float[] sampleBuffer = new float[2048];
    private Color[] clearPixels;
    private bool pendingClear = true;
    private float refreshTimer = 0f;

    void OnEnable()
    {
        uiDocument.rootVisualElement.RegisterCallback<AttachToPanelEvent>(evt =>
        {
            image = uiDocument.rootVisualElement.Q<Image>(imageName);
            if (image == null)
            {
                Debug.LogError($"{name}: Could not find Image named '{imageName}' in UI document.", this);
                return;
            }

            CreateTexture();
        });

        if (kickSynth != null)
        {
            kickSynth.onKickStart += HandleKickStart;
            kickSynth.onKickComplete += HandleKickComplete;
        }
    }

    void OnDisable()
    {
        if (kickSynth != null)
        {
            kickSynth.onKickStart -= HandleKickStart;
            kickSynth.onKickComplete -= HandleKickComplete;
        }
        if (texture != null)
        {
            Destroy(texture);
            texture = null;
        }
    }

    void HandleKickStart()
    {
        pendingClear = true;
    }

    void HandleKickComplete(float[] data)
    {
        // Not used for preview, but kept for compatibility if needed later
    }

    void CreateTexture()
    {
        texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        clearPixels = new Color[textureWidth * textureHeight];
        for (int i = 0; i < clearPixels.Length; i++)
            clearPixels[i] = background;

        texture.SetPixels(clearPixels);
        texture.Apply(false);
        if (image != null)
            image.image = texture;
    }

    void Update()
    {
        if (kickSynth == null || texture == null || image == null)
            return;

        refreshTimer += Time.deltaTime;
        if (refreshTimer < refreshInterval)
            return;
        refreshTimer = 0f;

        // Request fixed-length preview so width maps 1:1 start-to-end
        var preview = kickSynth.GeneratePreviewWaveform(textureWidth);
        if (preview == null || preview.Length == 0)
            return;

        if (pendingClear)
        {
            texture.SetPixels(clearPixels);
            pendingClear = false;
        }

        DrawWaveform(preview, preview.Length);
    }

    private void DrawWaveform(float[] samples, int count)
    {
        if (count < 2)
            return;

        // Clear to background
        texture.SetPixels(clearPixels);

        int w = textureWidth;
        int h = textureHeight;
        float midY = (h - 1) * 0.5f;
        float amp = h * 0.45f;

        // Draw mid line
        int midRow = Mathf.RoundToInt(midY);
        for (int x = 0; x < w; x++)
        {
            texture.SetPixel(x, midRow, midLineColor);
        }

        // Resample the entire waveform to exactly fit the width
        float prevY = midY - Mathf.Clamp(samples[0], -1f, 1f) * amp;
        for (int x = 1; x < w; x++)
        {
            float t = x / (float)(w - 1);
            int idx = Mathf.Clamp(Mathf.RoundToInt(t * (count - 1)), 0, count - 1);
            float y = midY - Mathf.Clamp(samples[idx], -1f, 1f) * amp;
            DrawLine(x - 1, (int)prevY, x, (int)y, lineColor);
            prevY = y;
        }

        texture.Apply(false);
    }

    // Simple Bresenham line
    private void DrawLine(int x0, int y0, int x1, int y1, Color color)
    {
        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy, e2;

        while (true)
        {
            if (x0 >= 0 && x0 < textureWidth && y0 >= 0 && y0 < textureHeight)
                texture.SetPixel(x0, y0, color);
            if (x0 == x1 && y0 == y1) break;
            e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }
}

