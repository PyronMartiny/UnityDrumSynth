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
    public float refreshInterval = 0.1f;

    private Texture2D texture;
    private Image image;
    private float[] sampleBuffer = new float[2048];
    private Color[] clearPixels;
    private bool pendingClear = true;
    private float refreshTimer = 0f;
    private bool bound = false;
    private bool subscribed = false;

    void OnEnable()
    {
        TryBind();
        if (uiDocument != null && uiDocument.rootVisualElement != null)
            uiDocument.rootVisualElement.RegisterCallback<AttachToPanelEvent>(_ => TryBind());
    }

    void OnDisable()
    {
        if (kickSynth != null && subscribed)
        {
            kickSynth.onKickStart -= HandleKickStart;
            kickSynth.onKickComplete -= HandleKickComplete;
            subscribed = false;
        }
        if (texture != null)
        {
            Destroy(texture);
            texture = null;
        }
        bound = false;
    }

    private void TryBind()
    {
        if (bound)
            return;

        var root = uiDocument != null ? uiDocument.rootVisualElement : null;
        if (root == null)
            return;

        image = root.Q<Image>(imageName);
        if (image == null)
            return;

        CreateTexture();
        image.style.width = textureWidth;
        image.style.height = textureHeight;
        image.scaleMode = ScaleMode.StretchToFill;

        if (kickSynth != null && !subscribed)
        {
            kickSynth.onKickStart += HandleKickStart;
            kickSynth.onKickComplete += HandleKickComplete;
            subscribed = true;
        }

        bound = true;
        Debug.Log($"{name}: Oscilloscope bound to '{imageName}'", this);
    }

    void HandleKickStart()
    {
        pendingClear = true;
    }

    void HandleKickComplete(float[] data)
    {
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

        if (!bound)
            TryBind();

        refreshTimer += Time.deltaTime;
        if (refreshTimer < refreshInterval)
            return;
        refreshTimer = 0f;

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

        texture.SetPixels(clearPixels);

        int w = textureWidth;
        int h = textureHeight;
        float midY = (h - 1) * 0.5f;
        float amp = h * 0.45f;

        int midRow = Mathf.RoundToInt(midY);
        for (int x = 0; x < w; x++)
            texture.SetPixel(x, midRow, midLineColor);

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
