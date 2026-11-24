using System;
using UnityEngine;

// R8 stores the sum of the three independently combined visibility bands, divided by three.
sealed class MobileFogMask
{
    public readonly Texture2D texture;
    readonly int resolution;
    readonly float texelSize;
    readonly Vector3[] bands;
    readonly byte[] pixels;
    readonly float[] edge;

    public MobileFogMask(int resolution, int worldSize)
    {
        this.resolution = resolution;
        texelSize = (float)worldSize / resolution;
        bands = new Vector3[resolution * resolution];
        pixels = new byte[bands.Length];
        edge = new float[resolution];
        for (int i = 0; i < resolution; ++i)
        {
            float uv = (i + 0.5f) / resolution;
            edge[i] = Mathf.Clamp01(uv * 20) * Mathf.Clamp01((1 - uv) * 20);
        }
        texture = new Texture2D(resolution, resolution, TextureFormat.R8, false, true)
        {
            name = "Mobile Fog Mask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
        };
    }

    public void Clear() => Array.Clear(bands, 0, bands.Length);

    static float Falloff(float distance, float radius, float blend)
    {
        float t = Mathf.Clamp01((radius + blend - distance) / blend);
        return t * t * (3 - 2 * t);
    }

    public void Stamp(Vector2 origin, Vector2 position, float radius, float visibility)
    {
        float inner = 0.75f * radius, outer = 15 * radius;
        float reach = outer + 60;
        // Coordinates of the cutout relative to the centre of texel (0,0).
        Vector2 centre = (position - origin) / texelSize + Vector2.one * ((resolution - 1) * 0.5f);
        float extent = reach / texelSize;
        int x0 = Mathf.Max(0, Mathf.CeilToInt(centre.x - extent));
        int x1 = Mathf.Min(resolution - 1, Mathf.FloorToInt(centre.x + extent));
        int y0 = Mathf.Max(0, Mathf.CeilToInt(centre.y - extent));
        int y1 = Mathf.Min(resolution - 1, Mathf.FloorToInt(centre.y + extent));
        float full = Mathf.Clamp01(visibility - 2), half = Mathf.Clamp01(visibility - 1);
        float rim = Mathf.Clamp01(visibility);
        for (int y = y0; y <= y1; ++y)
        {
            float dy = (y - centre.y) * texelSize;
            for (int x = x0; x <= x1; ++x)
            {
                float dx = (x - centre.x) * texelSize;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float core = Falloff(distance, inner, 20);
                ref Vector3 pixel = ref bands[y * resolution + x];
                pixel.x = Mathf.Max(pixel.x, full * core);
                pixel.y = Mathf.Max(pixel.y, half * core);
                pixel.z = Mathf.Max(pixel.z, rim * Falloff(distance, outer, 60));
            }
        }
    }

    public void Upload()
    {
        for (int y = 0; y < resolution; ++y)
        for (int x = 0; x < resolution; ++x)
        {
            int i = y * resolution + x;
            Vector3 b = bands[i];
            pixels[i] = (byte)Mathf.RoundToInt((b.x + b.y + b.z) * edge[x] * edge[y] * 85);
        }
        texture.SetPixelData(pixels, 0);
        texture.Apply(false, false);
    }
}
