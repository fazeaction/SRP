using UnityEngine;
using UnityEngine.Rendering;
using Conditional = System.Diagnostics.ConditionalAttribute;

public class MyPipeline : RenderPipeline
{
    DrawingSettings drawSettings = new DrawingSettings(new ShaderTagId("SRPDefaultUnlit"), new SortingSettings());
    CommandBuffer cameraBuffer = new CommandBuffer { name="Render camera"};
    FilteringSettings filterSettings = FilteringSettings.defaultValue;
    ScriptableCullingParameters cullingParameters;
    
    Material errorMaterial;
    DrawingSettings drawSettingsError = new DrawingSettings(new ShaderTagId("ForwardBase"), new SortingSettings());
    
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach (var camera in cameras) {
            Render(context, camera);
        }
    }
    
    void Render (ScriptableRenderContext context, Camera camera) {
        
        
        context.SetupCameraProperties(camera);
        CameraClearFlags clearFlags = camera.clearFlags;
        
        cameraBuffer.ClearRenderTarget((clearFlags & CameraClearFlags.Depth) != 0,
            (clearFlags & CameraClearFlags.Color) != 0,
            camera.backgroundColor);
        cameraBuffer.BeginSample("Render Camera");
        context.ExecuteCommandBuffer(cameraBuffer);
        cameraBuffer.Clear();
        
        if (!camera.TryGetCullingParameters(out cullingParameters)) {
            return;
        }
        
        #if UNITY_EDITOR
                if (camera.cameraType == CameraType.SceneView) {
                    ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
                }
        #endif
        
        CullingResults cull = context.Cull(ref cullingParameters);
        drawSettings.sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque }; // front to back.
        filterSettings.renderQueueRange = RenderQueueRange.opaque;
 
        context.DrawRenderers(cull, ref drawSettings, ref filterSettings);
        
        context.DrawSkybox(camera);
        
        drawSettings.sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonTransparent }; // back to front
        filterSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cull, ref drawSettings, ref filterSettings);
        
        DrawDefaultPipeline(context, camera, cull);
        
        cameraBuffer.EndSample("Render Camera");
        context.ExecuteCommandBuffer(cameraBuffer);
        cameraBuffer.Clear();
        
        context.Submit();
    }
    
    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    void DrawDefaultPipeline(ScriptableRenderContext context, Camera camera, CullingResults cull) {
        if (errorMaterial == null) {
            Shader errorShader = Shader.Find("Hidden/InternalErrorShader");
            errorMaterial = new Material(errorShader) {
                hideFlags = HideFlags.HideAndDontSave
            };
            drawSettingsError.SetShaderPassName(1, new ShaderTagId("PrepassBase"));
            drawSettingsError.SetShaderPassName(2, new ShaderTagId("Always"));
            drawSettingsError.SetShaderPassName(3, new ShaderTagId("Vertex"));
            drawSettingsError.SetShaderPassName(4, new ShaderTagId("VertexLMRGBM"));
            drawSettingsError.SetShaderPassName(5, new ShaderTagId("VertexLM"));
            drawSettingsError.overrideMaterial=errorMaterial;
        } 
        
        filterSettings.renderQueueRange = RenderQueueRange.all;
        context.DrawRenderers(
            cull, ref drawSettingsError, ref filterSettings
        );
    }
}
