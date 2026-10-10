#ifndef SHADOW_PLATFORM_SURFACE_INCLUDED
#define SHADOW_PLATFORM_SURFACE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float fog : TEXCOORD2;
    float fringeScale : TEXCOORD3;
};

CBUFFER_START(UnityPerMaterial)
    half4 _BaseColor;
CBUFFER_END

Varyings Vert(Attributes input)
{
    Varyings output;
    VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = pos.positionCS;
    output.positionWS = pos.positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.fog = ComputeFogFactor(pos.positionCS.z);
    output.fringeScale = 1.0;
    return output;
}

#ifndef SHADOW_PLATFORM_BODY
// Screen-space fringe outside the top silhouette. Each edge becomes a
// quad whose alpha falls from the body colour to nothing. The mesh
// itself is not moved. Internal edges land back on the body and the
// stencil test drops them.
[maxvertexcount(21)]
void Geom(triangle Varyings input[3], inout TriangleStream<Varyings> stream)
{
    float3 face = cross(input[1].positionWS - input[0].positionWS,
                        input[2].positionWS - input[0].positionWS);
    if (face.y <= 1e-5)
        return;

    float2 ndc[3];
    [unroll]
    for (int i = 0; i < 3; i++)
        ndc[i] = input[i].positionCS.xy / max(input[i].positionCS.w, 1e-5);

    float2 edgeN[3];
    [unroll]
    for (int edge = 0; edge < 3; edge++)
    {
        float2 dir = ndc[(edge + 1) % 3] - ndc[edge];
        float len2 = dot(dir, dir);
        float2 nrm = (len2 > 1e-12) ? float2(-dir.y, dir.x) * rsqrt(len2) : float2(0.0, 0.0);
        float2 mid = (ndc[edge] + ndc[(edge + 1) % 3]) * 0.5;
        float2 cent = (ndc[0] + ndc[1] + ndc[2]) * (1.0 / 3.0);
        if (dot(nrm, mid - cent) < 0.0)
            nrm = -nrm;
        edgeN[edge] = nrm;
    }

    // About 16 pixels. At a normal gameplay distance this is a few centimetres.
    float2 pixelNDC = 16.0 * 2.0 / _ScaledScreenParams.xy;

    [unroll]
    for (int side = 0; side < 3; side++)
    {
        int a = side;
        int b = (side + 1) % 3;
        float2 offset = edgeN[side] * pixelNDC;

        Varyings innerA = input[a];
        innerA.fringeScale = 1.0;
        stream.Append(innerA);

        Varyings outerA = input[a];
        outerA.positionCS.xy += offset * outerA.positionCS.w;
        outerA.fringeScale = 0.0;
        stream.Append(outerA);

        Varyings innerB = input[b];
        innerB.fringeScale = 1.0;
        stream.Append(innerB);

        Varyings outerB = input[b];
        outerB.positionCS.xy += offset * outerB.positionCS.w;
        outerB.fringeScale = 0.0;
        stream.Append(outerB);

        stream.RestartStrip();
    }

    [unroll]
    for (int v = 0; v < 3; v++)
    {
        float2 d1 = edgeN[(v + 2) % 3] * pixelNDC;
        float2 d2 = edgeN[v] * pixelNDC;
        if (d1.x * d2.y - d1.y * d2.x < 0.0)
        {
            float2 swap = d1;
            d1 = d2;
            d2 = swap;
        }

        Varyings center = input[v];
        center.fringeScale = 1.0;
        stream.Append(center);

        Varyings outer1 = input[v];
        outer1.positionCS.xy += d1 * outer1.positionCS.w;
        outer1.fringeScale = 0.0;
        stream.Append(outer1);

        Varyings outer2 = input[v];
        outer2.positionCS.xy += d2 * outer2.positionCS.w;
        outer2.fringeScale = 0.0;
        stream.Append(outer2);

        stream.RestartStrip();
    }
}
#endif

half4 Frag(Varyings input) : SV_Target
{
    // Top keeps the material alpha, which the fade-in script drives.
    // The thin vertical lip is only the extrusion, so it stays faint.
    float up = saturate(normalize(input.normalWS).y);
    float topFace = smoothstep(0.2, 0.65, up);
    half scale = 1.0h;
#ifndef SHADOW_PLATFORM_BODY
    scale = (half)smoothstep(0.0, 1.0, input.fringeScale);
#endif
    half alpha = _BaseColor.a * scale * lerp(0.2h, 1.0h, (half)topFace);
    half3 rgb = MixFog(_BaseColor.rgb, input.fog);
    return half4(rgb, alpha);
}

#endif
