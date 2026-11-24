using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public sealed class FogOfWar : MonoBehaviour
{
    public Material fogMaterial;
    public ComputeShader maskShader;
    public Mesh highDensity, lowDensity;
    public FogPreset preset;
    public Transform focus;
    [Range(0, 1)] public float timeOfDay = 0.5f;
    public bool animateTime = true;

    const int Resolution = 128, Size = 200, MaxCutouts = 1024;
    const float PlaneHeight = 5.8f, ColorLerpSpeed = 5f;
    Vector4[] packed;
    readonly List<Cutout> cutouts = new List<Cutout>();
    RenderTexture mask;
    ComputeBuffer buffer;
    MobileFogMask mobileMask;
    MobileFogSurface mobileSurface;
    float nextMaskUpdate;
    Transform planes;
    Material material;
    Color directionColor = Color.black;
    Vector2 maskOrigin;
    bool dirty = true;

    sealed class Cutout
    {
        public Vector2 position;
        public float maxRadius, radius, visibility, fromRadius, fromVisibility, elapsed;
        public int state, target;
        public FogPreset.Transition transition;
        public void Advance(float dt)
        {
            if (transition == null) return;
            elapsed += dt;
            float progress = Mathf.Min(1, elapsed / transition.duration);
            bool reverse = transition.twoWay && target <= state;
            float phase = reverse ? 1 - progress : progress;
            float r = transition.radius.Evaluate(phase), v = transition.visibility.Evaluate(phase);
            radius = Mathf.Lerp(fromRadius, target * 0.5f * maxRadius, reverse ? 1 - r : r);
            visibility = Mathf.Lerp(fromVisibility, target == 2 ? 3 : target * 2, reverse ? 1 - v : v);
            if (progress >= 1) { state = target; transition = null; }
        }
    }

    void OnEnable()
    {
        material = new Material(fogMaterial);
        planes = new GameObject("FogPlanes").transform;
        planes.SetParent(transform, false);
        if (material.IsKeywordEnabled("FOG_MOBILE"))
        {
            mobileMask = new MobileFogMask(Resolution, Size);
            mobileSurface = new MobileFogSurface(planes, material);
            Shader.SetGlobalTexture("g_FogOfWar_Mask", mobileMask.texture);
            nextMaskUpdate = 0;
        }
        else CreateDesktopFog();
        Shader.SetGlobalFloat("g_FogOfWar_Size", Size);
        dirty = true;
        Tick(0);
    }

    void CreateDesktopFog()
    {
        mask = new RenderTexture(Resolution, Resolution, 0, GraphicsFormat.R16_SFloat)
        {
            name = "Fog Of War Mask", enableRandomWrite = true, filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        mask.Create();
        packed = new Vector4[MaxCutouts];
        buffer = new ComputeBuffer(MaxCutouts, 16);
        float width = highDensity.bounds.size.x;
        for (int z = -1; z <= 1; ++z)
        for (int x = -1; x <= 1; ++x)
        {
            var plane = new GameObject("FogPlane");
            plane.transform.SetParent(planes, false);
            plane.transform.localPosition = new Vector3(x * width, 0, z * width);
            var mesh = x == 0 && z == 0 ? highDensity : lowDensity;
            plane.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = plane.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            var bounds = mesh.bounds;
            bounds.Expand(new Vector3(0, 20, 0));
            renderer.localBounds = bounds;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
        Shader.SetGlobalTexture("g_FogOfWar_Mask", mask);
    }

    // World-space XZ point and maximum reveal radius supplied by the calling game.
    public void SetVisibility(Vector2 position, int target, float maxRadius)
    {
        target = Mathf.Clamp(target, 0, 2);
        var cut = cutouts.Find(item => item.position == position);
        if (cut == null)
        {
            if (target == 0 || cutouts.Count >= MaxCutouts) return;
            cut = new Cutout { position = position };
            cutouts.Add(cut);
        }
        if (cut.target == target) return;
        cut.maxRadius = Mathf.Max(0, maxRadius);
        cut.fromRadius = cut.radius;
        cut.fromVisibility = cut.visibility;
        cut.transition = preset.GetTransition(cut.state, target);
        cut.target = target;
        cut.elapsed = 0;
        dirty = true;
    }

    public void HideAll()
    {
        foreach (var cut in cutouts) SetVisibility(cut.position, 0, cut.maxRadius);
    }

    void Update() => Tick(Time.deltaTime);

    void Tick(float dt)
    {
        if (animateTime) timeOfDay = Mathf.Repeat(timeOfDay + dt / 800f, 1);
        directionColor = Color.Lerp(directionColor, preset.directionalColor.Evaluate(timeOfDay), ColorLerpSpeed * dt);
        material.SetColor("_Color", preset.tint.Evaluate(timeOfDay));
        material.SetColor("_DirectionColor", directionColor);
        material.SetFloat("_DirectionIntensity", preset.directionalIntensity.Evaluate(timeOfDay));
        material.SetFloat("_DirectionFalloff", 1);
        bool day = timeOfDay >= 0.2f;
        float angle = day ? Mathf.Lerp(115, 255, (timeOfDay - 0.2f) / 0.8f) : Mathf.Lerp(255, 115, timeOfDay / 0.2f);
        Quaternion orbit = day ? Quaternion.Euler(37.5f, 60, 0) : Quaternion.Euler(40, 0, 0);
        Vector3 direction = orbit * Quaternion.Euler(0, angle, 0) * Vector3.forward;
        material.SetVector("_Direction", direction);

        Vector3 center = focus ? focus.position : transform.position;
        planes.position = new Vector3(Mathf.Floor(center.x), PlaneHeight, Mathf.Floor(center.z));
        float texel = (float)Size / Resolution;
        var origin = new Vector2(Mathf.Floor(center.x / texel), Mathf.Floor(center.z / texel)) * texel;
        dirty |= origin != maskOrigin;
        for (int i = cutouts.Count - 1; i >= 0; i--)
        {
            var cut = cutouts[i];
            if (cut.transition == null) continue;
            cut.Advance(dt);
            if (cut.transition == null && cut.state == 0) cutouts.RemoveAt(i);
            dirty = true;
        }
        nextMaskUpdate = Mathf.Max(0, nextMaskUpdate - dt);
        if (!dirty || nextMaskUpdate > 0) return;
        if (mobileMask != null)
        {
            mobileMask.Clear();
            foreach (var c in cutouts) mobileMask.Stamp(origin, c.position, c.radius, c.visibility);
            mobileMask.Upload();
            nextMaskUpdate = 1f / 15f;
        }
        else UpdateDesktopMask(origin);
        // The origin must travel with the texture, including between throttled updates.
        maskOrigin = origin;
        Shader.SetGlobalVector("g_FogOfWar_Origin", origin);
        dirty = false;
    }

    void UpdateDesktopMask(Vector2 origin)
    {
        for (int i = 0; i < cutouts.Count; ++i)
        {
            var c = cutouts[i];
            packed[i] = new Vector4(c.position.x, c.position.y, c.radius, c.visibility);
        }
        if (cutouts.Count > 0) buffer.SetData(packed, 0, 0, cutouts.Count);
        maskShader.SetInt("_Size", Size);
        maskShader.SetInt("_Resolution", Resolution);
        maskShader.SetInt("_Count", cutouts.Count);
        maskShader.SetVector("_Origin", origin);
        maskShader.SetFloat("_FullRadius", 0.75f);
        maskShader.SetFloat("_HalfRadius", 0.75f);
        maskShader.SetFloat("_OuterRadius", 15);
        maskShader.SetFloat("_FullBlend", 20);
        maskShader.SetFloat("_HalfBlend", 20);
        maskShader.SetFloat("_OuterBlend", 60);
        maskShader.SetBuffer(0, "_Buffer", buffer);
        maskShader.SetTexture(0, "_Mask", mask);
        maskShader.Dispatch(0, Resolution / 8, Resolution / 8, 1);
    }

    void OnDisable()
    {
        Texture activeMask = mobileMask != null ? (Texture)mobileMask.texture : mask;
        if (Shader.GetGlobalTexture("g_FogOfWar_Mask") == activeMask) Shader.SetGlobalTexture("g_FogOfWar_Mask", null);
        mobileSurface?.Dispose();
        mobileSurface = null;
        if (mobileMask != null) Destroy(mobileMask.texture);
        mobileMask = null;
        buffer?.Release();
        buffer = null;
        nextMaskUpdate = 0;
        if (mask) { mask.Release(); Destroy(mask); }
        if (material) Destroy(material);
        if (planes) Destroy(planes.gameObject);
        cutouts.Clear();
    }
}
