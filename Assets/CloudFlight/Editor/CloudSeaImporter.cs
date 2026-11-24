using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

[ScriptedImporter(1, "cloudmesh")]
public sealed class CloudSeaImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext context)
    {
        using var reader = new BinaryReader(File.OpenRead(context.assetPath));
        if (new string(reader.ReadChars(4)) != "CSM1") throw new InvalidDataException("Invalid cloud sea mesh.");
        int chunks = reader.ReadInt32();
        int period = reader.ReadInt32();
        if (chunks != 8 || period != 192) throw new InvalidDataException("Unexpected cloud sea dimensions.");
        for (int i = 0; i < chunks; ++i)
        {
            AddMesh(context, reader, "Core" + i, i == 0);
            AddMesh(context, reader, "Mist" + i, false);
        }
    }

    static void AddMesh(AssetImportContext context, BinaryReader reader, string name, bool main)
    {
        int count = reader.ReadInt32(), indices = reader.ReadInt32();
        var points = new Vector3[count];
        var uv = new Vector2[count];
        var color = new Color32[count];
        var triangles = new int[indices];
        for (int i = 0; i < count; ++i) points[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        for (int i = 0; i < count; ++i) uv[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        for (int i = 0; i < count; ++i) color[i] = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
        for (int i = 0; i < indices; ++i) triangles[i] = reader.ReadUInt16();
        var mesh = new Mesh { name = name, vertices = points, uv = uv, colors32 = color, triangles = triangles };
        mesh.UploadMeshData(true);
        context.AddObjectToAsset(name, mesh);
        if (main) context.SetMainObject(mesh);
    }
}
