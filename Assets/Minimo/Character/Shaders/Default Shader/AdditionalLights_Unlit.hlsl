// AdditionalLights_Unlit.hlsl
// Matches ShaderGraph Custom Function input order EXACTLY.
// Adds NormalStrength and applies normal map only where it has detail (flat areas keep mesh normal).
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile_fragment _ _SHADOWS_SOFT
#pragma multi_compile_fragment _ _LIGHT_LAYERS
#pragma multi_compile_fragment _ _FORWARD_PLUS
#pragma multi_compile_fog

#ifndef SHADERGRAPH_PREVIEW
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#endif

float Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

#ifndef SHADERGRAPH_PREVIEW

float3 SafeNormalize(float3 v, float3 fallback)
{
    float len2 = dot(v, v);
    if (!(len2 > 1e-8)) return fallback;
    return v * rsqrt(len2);
}

// URP-like unpack + strength: scale XY, recompute Z
float3 UnpackNormalScaleTS(float4 packed, float strength)
{
    float3 n;
    n.xy = packed.xy * 2.0 - 1.0;
    n.xy *= strength;
    n.z  = sqrt(saturate(1.0 - dot(n.xy, n.xy)));
    return SafeNormalize(n, float3(0, 0, 1));
}

// Build world perturbation from TS normal using a constructed basis around world normal.
float3 ApplyNormalMapMaskedApprox(float3 worldN, UnityTexture2D normalMapTex, float2 uv, float normalStrength)
{
    float3 Nw = SafeNormalize(worldN, float3(0, 1, 0));
    float s = max(0.0, normalStrength);
    if (s <= 0.0001) return Nw;

    float4 nm = SAMPLE_TEXTURE2D(normalMapTex.tex, normalMapTex.samplerstate, uv);
    float3 nTS = UnpackNormalScaleTS(nm, s);
    
    float detail = length(nTS.xy);
    
    // GÜNCELLEME: Normal map ve pürüzlerin tekrar net okunması için maske eşiğini daralttık
    float mask = smoothstep(0.005, 0.05, detail);

    float3 up = (abs(Nw.y) < 0.999) ? float3(0, 1, 0) : float3(1, 0, 0);
    float3 T = SafeNormalize(cross(up, Nw), float3(1, 0, 0));
    float3 B = cross(Nw, T);
    
    // GÜNCELLEME: Doku derinliğini geri getirdik (0.25 yerine 1.0)
    float approxScale = 1.0; 
    float3 perturbed = SafeNormalize(Nw + (T * nTS.x + B * nTS.y) * approxScale, Nw);
    
    return SafeNormalize(lerp(Nw, perturbed, mask), Nw);
}

float3 ShadePlastic(
    float3 positionWS,
    float3 normalWS,
    UnityTexture2D normalMapTex,
    float  normalStrength,
    float2 uv0,
    float3 baseColor,
    float  smoothness,
    float3 specularColor,
    float3 shadowColor,
    float  noiseScale,
    float  noiseStrength)
{
    #if defined(SHADERPASS_SCENEPICKING) || defined(SHADERPASS_SCENESELECTION)
        return baseColor;
    #endif

    float3 N = SafeNormalize(normalWS, float3(0, 1, 0));
    N = ApplyNormalMapMaskedApprox(N, normalMapTex, uv0, normalStrength);
    float3 V = GetWorldSpaceNormalizeViewDir(positionWS);

    smoothness = saturate(smoothness);
    
    // Highlight'ı iğne ucu gibi keskin olmaktan çıkarıp daha geniş ve 'soft' bir alana yayıyoruz
    float specPower = exp2(7.0 * smoothness + 1.0);

    float3 shadowTint = saturate(shadowColor);
    float3 specColor  = saturate(specularColor);

    float3 diff = 0.0;
    float3 spec = 0.0;

    // Spec noise - Gürültü etkisinin parlama üzerinde %100 çalışmasını sağlar
    float3 N_spec = N;
    {
        float n = Hash21(uv0 * noiseScale);
        float3 noiseN = (n.xxx * 2.0 - 1.0);
        N_spec = SafeNormalize(N_spec + noiseN * noiseStrength, N);
    }

    float wrap = 0.4; // Işık geçişi yumuşaklığı (Soft Shadow)

    // --- Main light
    {
        float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
        Light mainLight = GetMainLight(shadowCoord);

        float3 L = mainLight.direction;

        float NdotL_raw = dot(N, L);
        float NdotL = saturate((NdotL_raw + wrap) / (1.0 + wrap));
        
        float distAtt = mainLight.distanceAttenuation;
        float shAtt = smoothstep(0.1, 1.0, mainLight.shadowAttenuation);

        float lit = NdotL * distAtt * shAtt;
        diff += baseColor * mainLight.color.rgb * lit;

        float shadowOnly = (1.0 - shAtt) * NdotL * distAtt;
        diff += baseColor * shadowTint * shadowOnly * 0.8; 

        // --- Highlight (Specular) Düzeltmesi ---
        float3 H = normalize(L + V);
        
        // Gürültü (Noise) N_spec üzerinden doğrudan parlamaya etki ediyor
        float NdotH = saturate(dot(N_spec, H));
        float LdotH = saturate(dot(L, H));
        
        float fresnel = lerp(0.5, 1.0, pow(1.0 - LdotH, 5.0));
        spec += specColor * pow(NdotH, specPower) * lit * fresnel * 2.0;
    }

    // --- Additional lights
    #if defined(_ADDITIONAL_LIGHTS)
    {
        uint count = GetAdditionalLightsCount();
        for (uint i = 0u; i < count; i++)
        {
            Light light = GetAdditionalLight(i, positionWS);
            float3 L = light.direction;

            float NdotL_raw = dot(N, L);
            float NdotL = saturate((NdotL_raw + wrap) / (1.0 + wrap));
            
            float distAtt = light.distanceAttenuation;
            float shAtt = smoothstep(0.1, 1.0, light.shadowAttenuation);

            float lit = NdotL * distAtt * shAtt;
            diff += baseColor * light.color.rgb * lit;

            float shadowOnly = (1.0 - shAtt) * NdotL * distAtt;
            diff += baseColor * shadowTint * shadowOnly * 0.8;

            float3 H = normalize(L + V);
            float NdotH = saturate(dot(N_spec, H));
            float LdotH = saturate(dot(L, H));
            
            float fresnel = lerp(0.5, 1.0, pow(1.0 - LdotH, 5.0));
            spec += specColor * pow(NdotH, specPower) * lit * fresnel * 2.0;
        }
    }
    #endif

    float3 ambient = SampleSH(N) * baseColor * 0.9;
    
    return diff + spec + ambient;
}

// ============================================================
// IMPORTANT: Signature must match ShaderGraph inputs EXACTLY
// ============================================================

void AdditionalLights_Unlit_float(
    float3 PositionWS,
    float3 NormalWS,
    UnityTexture2D GeomNormalWS,
    float  NormalStrength,
    float2 UV0,
    float3 BaseColor,
    float  Smoothness,
    float3 SpecularColor,
    float3 ShadowColor,
    float  NoiseScale,
    float  NoiseStrength,
    out float3 OutColor)
{
    OutColor = ShadePlastic(PositionWS, NormalWS, GeomNormalWS, NormalStrength, UV0, BaseColor,
                            Smoothness, SpecularColor, ShadowColor, NoiseScale, NoiseStrength);
}

void AdditionalLights_Unlit_half(
    half3 PositionWS,
    half3 NormalWS,
    UnityTexture2D GeomNormalWS,
    half  NormalStrength,
    half2 UV0,
    half3 BaseColor,
    half  Smoothness,
    half3 SpecularColor,
    half3 ShadowColor,
    half  NoiseScale,
    half  NoiseStrength,
    out half3 OutColor)
{
    OutColor = (half3)ShadePlastic((float3)PositionWS, (float3)NormalWS, GeomNormalWS, (float)NormalStrength,
                                   (float2)UV0, (float3)BaseColor, (float)Smoothness, (float3)SpecularColor,
                                   (float3)ShadowColor, (float)NoiseScale, (float)NoiseStrength);
}

#else // SHADERGRAPH_PREVIEW

void AdditionalLights_Unlit_float(
    float3 PositionWS,
    float3 NormalWS,
    UnityTexture2D GeomNormalWS,
    float  NormalStrength,
    float2 UV0,
    float3 BaseColor,
    float  Smoothness,
    float3 SpecularColor,
    float3 ShadowColor,
    float  NoiseScale,
    float  NoiseStrength,
    out float3 OutColor)
{
    OutColor = BaseColor;
}

#endif