using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Camera))]
public sealed class OriginalSkillPreviewLight : MonoBehaviour
{
    public Shader compositeShader;
    private Material composite;
    private PreviewPass pass;
    private Camera sceneCamera;
    public Camera monitorCamera { private get; set; }

    private void OnEnable()
    {
        sceneCamera = GetComponent<Camera>();
        if (!compositeShader) return;
        composite = CoreUtils.CreateEngineMaterial(compositeShader);
        pass = new PreviewPass(composite);
        RenderPipelineManager.beginCameraRendering += Enqueue;
    }

    private void Enqueue(ScriptableRenderContext context, Camera camera)
    {
        if (camera != sceneCamera && camera != monitorCamera) return;
        camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(pass);
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= Enqueue;
        CoreUtils.Destroy(composite);
    }

    private sealed class PreviewPass : ScriptableRenderPass
    {
        private const int Levels = 5;
        private readonly int[] down = new int[Levels], up = new int[Levels];
        private readonly int output = Shader.PropertyToID("_SkillPreviewComposite");
        private readonly int bloom = Shader.PropertyToID("_SkillPreviewBloom");
        private readonly Material material;
        public PreviewPass(Material source)
        {
            material = source;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            ConfigureInput(ScriptableRenderPassInput.Color);
            for (int i = 0; i < Levels; i++)
            {
                down[i] = Shader.PropertyToID("_SkillBloomDown" + i);
                up[i] = Shader.PropertyToID("_SkillBloomUp" + i);
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData data)
        {
            var cmd = CommandBufferPool.Get("宗师之上 · 技能预览曝光与泛光");
            var desc = data.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0; desc.msaaSamples = 1;
            desc.colorFormat = RenderTextureFormat.DefaultHDR;
            var target = data.cameraData.renderer.cameraColorTargetHandle.nameID;
            cmd.GetTemporaryRT(output, desc, FilterMode.Bilinear);
            for (int i = 0; i < Levels; i++)
            {
                desc.width = Mathf.Max(1, desc.width / 2);
                desc.height = Mathf.Max(1, desc.height / 2);
                cmd.GetTemporaryRT(down[i], desc, FilterMode.Bilinear);
                if (i + 1 < Levels) cmd.GetTemporaryRT(up[i], desc, FilterMode.Bilinear);
                cmd.Blit(i == 0 ? target : new RenderTargetIdentifier(down[i - 1]), down[i], material, i == 0 ? 0 : 1);
            }
            int previous = down[Levels - 1];
            for (int i = Levels - 2; i >= 0; i--)
            {
                cmd.SetGlobalTexture(bloom, previous);
                cmd.Blit(down[i], up[i], material, 2);
                previous = up[i];
            }
            cmd.SetGlobalTexture(bloom, previous);
            cmd.Blit(target, output, material, 3);
            cmd.Blit(output, target);
            cmd.ReleaseTemporaryRT(output);
            for (int i = 0; i < Levels; i++)
            {
                cmd.ReleaseTemporaryRT(down[i]);
                if (i + 1 < Levels) cmd.ReleaseTemporaryRT(up[i]);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
