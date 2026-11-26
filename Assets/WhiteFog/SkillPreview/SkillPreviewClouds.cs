using UnityEngine;

[RequireComponent(typeof(FogOfWar))]
public sealed class SkillPreviewClouds : MonoBehaviour
{
    public Texture2D cloudMatcap;
    private Mesh grid;

    private void Start()
    {
        var fog = GetComponent<FogOfWar>();
        var center = fog.focus ? fog.focus.position : transform.position;
        fog.SetVisibility(new Vector2(center.x, center.z), 1, 4f);
        var surface = fog.GetComponentInChildren<MeshRenderer>();
        if (!surface) return;
        // FogOfWar owns the material instance and supplies its textures, lighting and mask.
        var clouds = surface.sharedMaterial;
        clouds.EnableKeyword("FOG_PORTRAIT_BATTLE");
        clouds.SetFloat("_FogCull", (float)UnityEngine.Rendering.CullMode.Off);
        clouds.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Background;
        clouds.SetTexture("_CloudMatcap", cloudMatcap);
        grid = MakeGrid();
        surface.GetComponent<MeshFilter>().sharedMesh = grid;
    }

    private static Mesh MakeGrid()
    {
        const int columns = 32, rows = 48;
        var vertices = new Vector3[(columns + 1) * (rows + 1)];
        var triangles = new int[columns * rows * 6];
        int cursor = 0;
        for (int y = 0; y <= rows; ++y)
        for (int x = 0; x <= columns; ++x)
        {
            int i = y * (columns + 1) + x;
            vertices[i] = new Vector3((2f * x / columns - 1f) * 1.23f, (2f * y / rows - 1f) * 1.35f);
            if (x == columns || y == rows) continue;
            triangles[cursor++] = i;
            triangles[cursor++] = i + 1;
            triangles[cursor++] = i + columns + 1;
            triangles[cursor++] = i + 1;
            triangles[cursor++] = i + columns + 2;
            triangles[cursor++] = i + columns + 1;
        }
        return new Mesh { name = "战斗云海投影", vertices = vertices, triangles = triangles,
            bounds = new Bounds(Vector3.zero, Vector3.one * 4096f) };
    }

    private void OnDestroy()
    {
        if (grid) Destroy(grid);
    }
}
