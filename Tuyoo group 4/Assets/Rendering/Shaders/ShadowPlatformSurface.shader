// Visual-only shading for the shadow bridge.
// Mesh, collider, and the fade-in (material colour alpha) stay as they are.
// Vertex colours are ignored, matching the previous URP Lit material.
Shader "ShadowBridge/Shadow Platform"
{
    Properties
    {
        [MainColor] _BaseColor("Color", Color) = (0.20, 0.22, 0.26, 0.6392157)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Body"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            Stencil { Ref 1 Comp Always Pass Replace }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #define SHADOW_PLATFORM_BODY 1
            #include "ShadowPlatformSurface.hlsl"
            ENDHLSL
        }

        // Drawn by ShadowBridgeFringeFeature. The geometry shader adds a
        // short gradient outside the silhouette; stencil keeps it off the body.
        Pass
        {
            Name "Fringe"
            Tags { "LightMode" = "ShadowBridgeFringe" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            Stencil { Ref 1 Comp NotEqual Pass Keep }

            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex Vert
            #pragma geometry Geom
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "ShadowPlatformSurface.hlsl"
            ENDHLSL
        }
    }
}
