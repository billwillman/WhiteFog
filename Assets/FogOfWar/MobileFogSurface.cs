using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// One transparent draw. Only its index buffer changes when the set of visible patches changes.
sealed class MobileFogSurface
{
    const int HalfWidth = 96, Centre = 32, PatchSize = 16;
    struct Patch
    {
        public Bounds bounds;
        public int first, count;
        public bool visible;
    }

    readonly Transform surface;
    readonly Mesh mesh;
    readonly Plane[] frustum = new Plane[6];
    readonly Patch[] patches;
    readonly int[] allIndices, visibleIndices;
    Matrix4x4 previousMatrix;
    bool initialized;

    public MobileFogSurface(Transform surface, Material material)
    {
        this.surface = surface;
        float displacement = Mathf.Abs(material.GetFloat("_DisplacementHeight"));
        float[] offsets = { material.GetFloat("_OffsetOutside"), material.GetFloat("_OffsetEdge"),
                            material.GetFloat("_OffsetHalf"), material.GetFloat("_OffsetFull") };
        float minY = -Mathf.Max(offsets) - displacement - 0.1f;
        float maxY = -Mathf.Min(offsets) + displacement + 0.1f;
        float midY = (maxY + minY) * 0.5f;
        float height = maxY - minY;
        var vertices = new List<Vector3>();
        var lookup = new Dictionary<Vector2Int, int>();
        var indices = new List<int>();
        var blocks = new List<Patch>();
        int Vertex(int x, int z)
        {
            var key = new Vector2Int(x, z);
            if (lookup.TryGetValue(key, out int id)) return id;
            id = vertices.Count;
            vertices.Add(new Vector3(x, 0, z));
            lookup.Add(key, id);
            return id;
        }
        void Triangle(int a, int b, int c)
        {
            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
        }
        for (int bz = -HalfWidth; bz < HalfWidth; bz += PatchSize)
        for (int bx = -HalfWidth; bx < HalfWidth; bx += PatchSize)
        {
            int first = indices.Count;
            int step = bx >= -Centre && bx < Centre && bz >= -Centre && bz < Centre ? 1 : 2;
            for (int z = bz; z < bz + PatchSize; z += step)
            for (int x = bx; x < bx + PatchSize; x += step)
            {
                int right = x + step, top = z + step;
                int a = Vertex(x, z), b = Vertex(x, top), c = Vertex(right, top), d = Vertex(right, z);
                // Split the coarse triangle touching the fine grid so displaced edges share vertices.
                bool spanX = step == 2 && x >= -Centre && x < Centre;
                bool spanZ = step == 2 && z >= -Centre && z < Centre;
                if (spanZ && x == Centre)
                {
                    int m = Vertex(x, z + 1);
                    Triangle(a, m, c); Triangle(m, b, c);
                }
                else if (spanX && top == -Centre)
                {
                    int m = Vertex(x + 1, top);
                    Triangle(a, b, m); Triangle(a, m, c);
                }
                else Triangle(a, b, c);
                if (spanZ && right == -Centre)
                {
                    int m = Vertex(right, z + 1);
                    Triangle(a, c, m); Triangle(a, m, d);
                }
                else if (spanX && z == Centre)
                {
                    int m = Vertex(x + 1, z);
                    Triangle(a, c, m); Triangle(m, c, d);
                }
                else Triangle(a, c, d);
            }
            blocks.Add(new Patch {
                first = first, count = indices.Count - first,
                bounds = new Bounds(new Vector3(bx + PatchSize / 2, midY, bz + PatchSize / 2),
                                    new Vector3(PatchSize, height, PatchSize))
            });
        }
        patches = blocks.ToArray();
        allIndices = indices.ToArray();
        visibleIndices = new int[allIndices.Length];
        var normals = new Vector3[vertices.Count];
        for (int i = 0; i < normals.Length; ++i) normals[i] = Vector3.up;
        mesh = new Mesh { name = "Mobile fog surface" };
        mesh.MarkDynamic();
        mesh.SetVertices(vertices);
        mesh.normals = normals;
        mesh.SetTriangles(allIndices, 0, false);
        mesh.bounds = new Bounds(new Vector3(0, midY, 0), new Vector3(2 * HalfWidth, height, 2 * HalfWidth));
        surface.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = surface.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        RenderPipelineManager.beginCameraRendering += Cull;
    }

    void Cull(ScriptableRenderContext context, Camera camera)
    {
        Matrix4x4 matrix = camera.cullingMatrix * surface.localToWorldMatrix;
        if (initialized && matrix == previousMatrix) return;
        GeometryUtility.CalculateFrustumPlanes(matrix, frustum);
        bool changed = !initialized;
        for (int i = 0; i < patches.Length; ++i)
        {
            bool visible = GeometryUtility.TestPlanesAABB(frustum, patches[i].bounds);
            changed |= visible != patches[i].visible;
            patches[i].visible = visible;
        }
        previousMatrix = matrix;
        initialized = true;
        if (!changed) return;
        int count = 0;
        foreach (var patch in patches)
        {
            if (!patch.visible) continue;
            Array.Copy(allIndices, patch.first, visibleIndices, count, patch.count);
            count += patch.count;
        }
        mesh.SetTriangles(visibleIndices, 0, count, 0, false);
    }

    public void Dispose()
    {
        RenderPipelineManager.beginCameraRendering -= Cull;
        UnityEngine.Object.Destroy(mesh);
    }
}
