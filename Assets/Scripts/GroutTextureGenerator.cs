using UnityEngine;

public class GroutTextureGenerator : MonoBehaviour
{
    public Material targetMaterial;
    public int size = 256;
    public int border = 12; // thickness of grout lines

    void Start()
    {
        Texture2D tex = new Texture2D(size, size);

        // Tile color (inside)
        Color tileColor = Color.white;

        // Grout color (#807E78)
        Color groutColor;
        ColorUtility.TryParseHtmlString("#807E78", out groutColor);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isBorder =
                    x < border || x >= size - border ||
                    y < border || y >= size - border;

                tex.SetPixel(x, y, isBorder ? groutColor : tileColor);
            }
        }

        tex.Apply();
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point;

        if (targetMaterial != null)
            targetMaterial.mainTexture = tex;
    }
}