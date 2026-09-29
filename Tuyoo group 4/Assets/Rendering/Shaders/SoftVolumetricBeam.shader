// Analytic ray-marched spotlight cone for URP.
//
// The mesh is only a bounding volume. Each pixel finds the exact segment of
// its camera ray that lies inside the capped spotlight cone, clips it against
// the scene depth buffer, and integrates scattering along that segment using
// the same angular / distance falloff a real spotlight has. This keeps the
// beam consistent from every viewing angle and removes the flat-plane
// "fan" artifacts of the previous crossed-quad approach.
Shader "ShadowBridge/Soft Volumetric Beam"
{
    Properties
    {
        [HDR] _BeamColor("Beam Color", Color) = (1, 0.8, 0.4, 1)
        _Intensity("Intensity", Float) = 0.25
        _MaxBrightness("Max Brightness", Range(0.05, 2)) = 0.5

        _ConeOrigin("Cone Origin (World)", Vector) = (0, 0, 0, 0)
        _ConeAxis("Cone Axis (World)", Vector) = (0, 0, 1, 0)
        _ConeLength("Cone Length", Float) = 10
        _CosOuter("Cos Outer Half Angle", Float) = 0.9659
        _CosInner("Cos Inner Half Angle", Float) = 0.9816

        _Falloff("Distance Falloff", Float) = 8
        _ApexFade("Apex Fade Distance", Float) = 0.35
        _NoiseScale("Noise Scale", Float) = 1.4
        _NoiseSpeed("Noise Speed", Float) = 0.25
        _NoiseStrength("Noise Strength", Range(0, 1)) = 0.35
        _Steps("Ray March Steps", Float) = 16
        _UseSceneDepth("Use Scene Depth", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "VolumetricBeam"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            // Additive light. Only the far side of the bounding cone is drawn
            // (Cull Front) so each pixel is integrated exactly once, whether
            // the camera is inside or outside the beam. Scene occlusion is
            // handled per pixel with the depth texture instead of ZTest.
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Front

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BeamColor;
                float _Intensity;
                float _MaxBrightness;
                float4 _ConeOrigin;
                float4 _ConeAxis;
                float _ConeLength;
                float _CosOuter;
                float _CosInner;
                float _Falloff;
                float _ApexFade;
                float _NoiseScale;
                float _NoiseSpeed;
                float _NoiseStrength;
                float _Steps;
                float _UseSceneDepth;
            CBUFFER_END

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise(float3 p)
            {
                float3 cell = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = Hash31(cell + float3(0, 0, 0));
                float n100 = Hash31(cell + float3(1, 0, 0));
                float n010 = Hash31(cell + float3(0, 1, 0));
                float n110 = Hash31(cell + float3(1, 1, 0));
                float n001 = Hash31(cell + float3(0, 0, 1));
                float n101 = Hash31(cell + float3(1, 0, 1));
                float n011 = Hash31(cell + float3(0, 1, 1));
                float n111 = Hash31(cell + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);
                return lerp(lerp(nx00, nx10, f.y), lerp(nx01, nx11, f.y), f.z);
            }

            // Stable per-pixel jitter (interleaved gradient noise) to hide step banding.
            float PixelJitter(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            // Segment [tEnter, tExit] of the ray ro + t*rd that lies inside the
            // capped cone (apex, axis, half angle, length). Returns false if none.
            bool IntersectCappedCone(float3 ro, float3 rd, float3 apex, float3 axis,
                                     float cosHalf, float coneLength,
                                     out float tEnter, out float tExit)
            {
                tEnter = 0.0;
                tExit = 0.0;

                float3 co = ro - apex;
                float rdA = dot(rd, axis);
                float coA = dot(co, axis);
                float cos2 = cosHalf * cosHalf;

                // Slab between the apex plane and the far cap plane.
                float tSlab0;
                float tSlab1;
                if (abs(rdA) < 1e-5)
                {
                    if (coA < 0.0 || coA > coneLength)
                        return false;
                    tSlab0 = -1e9;
                    tSlab1 = 1e9;
                }
                else
                {
                    float ta = (0.0 - coA) / rdA;
                    float tb = (coneLength - coA) / rdA;
                    tSlab0 = min(ta, tb);
                    tSlab1 = max(ta, tb);
                }

                // Quadratic for the infinite double cone: f(t) >= 0 is inside.
                float a = rdA * rdA - cos2;
                float b = 2.0 * (rdA * coA - dot(rd, co) * cos2);
                float c = coA * coA - dot(co, co) * cos2;

                float in0 = -1e9, in1 = 1e9;   // first inside interval
                float in2 = 1e9,  in3 = -1e9;  // second inside interval (a > 0)

                if (abs(a) < 1e-6)
                {
                    if (abs(b) < 1e-6)
                    {
                        if (c < 0.0)
                            return false;
                    }
                    else
                    {
                        float t = -c / b;
                        if (b > 0.0) in0 = t; else in1 = t;
                    }
                }
                else
                {
                    float disc = b * b - 4.0 * a * c;
                    if (disc < 0.0)
                    {
                        if (a < 0.0)
                            return false;
                    }
                    else
                    {
                        float s = sqrt(disc);
                        float t0 = (-b - s) / (2.0 * a);
                        float t1 = (-b + s) / (2.0 * a);
                        float lo = min(t0, t1);
                        float hi = max(t0, t1);
                        if (a < 0.0)
                        {
                            in0 = lo;
                            in1 = hi;
                        }
                        else
                        {
                            in1 = lo;
                            in2 = hi;
                            in3 = 1e9;
                        }
                    }
                }

                // Clip both candidates to the slab and to t >= 0; the back nappe
                // always ends up with non-positive length after slab clipping.
                float e0 = max(max(in0, tSlab0), 0.0);
                float x0 = min(in1, tSlab1);
                float e1 = max(max(in2, tSlab0), 0.0);
                float x1 = min(in3, tSlab1);
                float len0 = x0 - e0;
                float len1 = x1 - e1;

                if (len0 <= 0.0 && len1 <= 0.0)
                    return false;

                if (len1 > len0)
                {
                    tEnter = e1;
                    tExit = x1;
                }
                else
                {
                    tEnter = e0;
                    tExit = x0;
                }
                return true;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 apex = _ConeOrigin.xyz;
                float3 axis = normalize(_ConeAxis.xyz);
                float coneLength = max(_ConeLength, 0.01);

                float3 ro = _WorldSpaceCameraPos;
                float3 rd = normalize(input.positionWS - ro);

                float tEnter;
                float tExit;
                if (!IntersectCappedCone(ro, rd, apex, axis, _CosOuter, coneLength, tEnter, tExit))
                    return half4(0, 0, 0, 0);

                // Stop the beam where it meets opaque geometry.
                if (_UseSceneDepth > 0.5)
                {
                    float2 screenUV = input.positionCS.xy / _ScaledScreenParams.xy;
                    #if UNITY_REVERSED_Z
                        float rawDepth = SampleSceneDepth(screenUV);
                    #else
                        float rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, SampleSceneDepth(screenUV));
                    #endif
                    float3 scenePos = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
                    float tScene = dot(scenePos - ro, rd);
                    tExit = min(tExit, tScene);
                }

                if (tExit <= tEnter)
                    return half4(0, 0, 0, 0);

                int steps = max((int)_Steps, 2);
                float stepLength = (tExit - tEnter) / steps;
                float t = tEnter + stepLength * PixelJitter(input.positionCS.xy);

                float invLength2 = 1.0 / (coneLength * coneLength);
                float3 noiseDrift = axis * (_Time.y * _NoiseSpeed);
                float energy = 0.0;

                [loop]
                for (int i = 0; i < steps; i++)
                {
                    float3 p = ro + rd * t;
                    float3 toP = p - apex;
                    float dAlong = dot(toP, axis);
                    float dist = length(toP) + 1e-4;

                    // Spotlight angular falloff (soft between inner and outer cone).
                    float cosA = dAlong / dist;
                    float angular = smoothstep(_CosOuter, _CosInner, cosA);

                    // Inverse-square style falloff normalised to the cone length,
                    // plus URP-like smooth range window so the beam ends cleanly.
                    float distAtt = 1.0 / (1.0 + _Falloff * dist * dist * invLength2);
                    float r2 = dAlong * dAlong * invLength2;
                    float window = saturate(1.0 - r2 * r2);
                    window *= window;

                    float apexFade = smoothstep(0.0, max(_ApexFade, 1e-3), dAlong);

                    // Drifting dust so the beam feels like real scattering media.
                    float n = ValueNoise(p * _NoiseScale - noiseDrift);
                    float density = 1.0 - _NoiseStrength + _NoiseStrength * 2.0 * n;

                    energy += angular * distAtt * window * apexFade * density * stepLength;
                    t += stepLength;
                }

                // Beer-Lambert style soft ceiling: linear for thin glancing views,
                // saturating toward _MaxBrightness when looking down the beam axis,
                // so brightness stays consistent from every camera angle.
                float peak = max(_MaxBrightness, 1e-3);
                float scatter = peak * (1.0 - exp(-(energy * _Intensity) / peak));
                half3 color = _BeamColor.rgb * scatter;
                return half4(color, 0);
            }
            ENDHLSL
        }
    }
}
