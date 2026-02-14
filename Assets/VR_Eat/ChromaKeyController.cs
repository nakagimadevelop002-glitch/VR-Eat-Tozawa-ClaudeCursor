using UnityEngine;

[ExecuteAlways] // çƒê∂ëOÇ≈Ç‡ìÆçÏÇ≥ÇπÇΩÇ¢èÍçáÇÕÇ±ÇÍÇïtÇØÇÈ
public class ChromaKeyController : MonoBehaviour
{
    public enum ChromaKeyColor
    {
        Black,
        Red,
        Green,
        Blue
    }

    [Header("Chroma Key Settings")]
    public ChromaKeyColor chromaKeyColor = ChromaKeyColor.Black;

    public Material targetMaterial;

    private static readonly int ChromaColorID = Shader.PropertyToID("_ChromaColor");

    private void Update()
    {
        if (targetMaterial == null) return;

        targetMaterial.SetColor(ChromaColorID, GetColorFromEnum(chromaKeyColor));
    }

    private Color GetColorFromEnum(ChromaKeyColor colorEnum)
    {
        switch (colorEnum)
        {
            case ChromaKeyColor.Red: return Color.red;
            case ChromaKeyColor.Green: return Color.green;
            case ChromaKeyColor.Blue: return Color.blue;
            case ChromaKeyColor.Black: return Color.black;
            default: return Color.black;
        }
    }
}
