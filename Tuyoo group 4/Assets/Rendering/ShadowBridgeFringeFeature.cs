using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Draws a faint screen-space fringe around the shadow bridge.
/// The bridge mesh and its collider are not modified. The body pass has
/// already been drawn and has marked its pixels in the stencil.
/// </summary>
public class ShadowBridgeFringeFeature : ScriptableRendererFeature
{
    class FringePass : ScriptableRenderPass
    {
        static readonly ShaderTagId FringeTag = new ShaderTagId("ShadowBridgeFringe");

        class PassData
        {
            public RendererListHandle list;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Shadow Bridge Fringe", out var passData))
            {
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                DrawingSettings draw = RenderingUtils.CreateDrawingSettings(
                    FringeTag, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
                FilteringSettings filter = new FilteringSettings(RenderQueueRange.transparent);
                passData.list = renderGraph.CreateRendererList(
                    new RendererListParams(renderingData.cullResults, draw, filter));

                if (!passData.list.IsValid())
                    return;

                builder.UseRendererList(passData.list);
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawRendererList(data.list);
                });
            }
        }
    }

    FringePass pass;

    public override void Create()
    {
        pass = new FringePass
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(pass);
    }
}
