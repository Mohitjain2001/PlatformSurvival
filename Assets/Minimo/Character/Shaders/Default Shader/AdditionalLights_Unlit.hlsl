// AdditionalLights_Unlit.hlsl

#if defined(SHADERPASS) && ((defined(SHADERPASS_FORWARD) && (SHADERPASS == SHADERPASS_FORWARD)) || (defined(SHADERPASS_FORWARD_UNLIT) && (SHADERPASS == SHADERPASS_FORWARD_UNLIT)))
    #define MINIMO_HDRP_SHADOW_PASS 1
#else
    #define MINIMO_HDRP_SHADOW_PASS 0
#endif

#if !defined(SHADERGRAPH_PREVIEW) && defined(SHADER_STAGE_FRAGMENT) && MINIMO_HDRP_SHADOW_PASS && defined(LIGHTLOOP_HD_SHADOW_HLSL) && defined(UNITY_LIGHT_LOOP_DEF_INCLUDED)
    #define MINIMO_USE_HDRP_SHADOWS 1
#else
    #define MINIMO_USE_HDRP_SHADOWS 0
#endif

#if !defined(SHADERGRAPH_PREVIEW) && defined(SHADER_STAGE_FRAGMENT) && !MINIMO_USE_HDRP_SHADOWS && defined(UNIVERSAL_LIGHTING_INCLUDED)
    #define MINIMO_USE_URP_SHADOWS 1
#else
    #define MINIMO_USE_URP_SHADOWS 0
#endif

#if !defined(SHADERGRAPH_PREVIEW) && defined(SHADER_STAGE_FRAGMENT) && !MINIMO_USE_HDRP_SHADOWS && defined(BUILTIN_LIGHTING_INCLUDED)
    #define MINIMO_USE_BUILTIN_SHADOWS 1
#else
    #define MINIMO_USE_BUILTIN_SHADOWS 0
#endif

#ifndef MINIMO_MAX_PUNCTUAL_SHADOW_LIGHTS
    #define MINIMO_MAX_PUNCTUAL_SHADOW_LIGHTS 32
#endif

#ifndef MINIMO_SHADOW_SOFTNESS_WORLD
    #define MINIMO_SHADOW_SOFTNESS_WORLD 0.12
#endif

float Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float3 SafeNormalize(float3 v, float3 fallback)
{
    float len2 = dot(v, v);

    if (!(len2 > 1e-8))
        return fallback;

    return v * rsqrt(len2);
}

// URP-like normal unpack + strength.
float3 UnpackNormalScaleTS(float4 packed, float strength)
{
    float3 n;

    n.xy = packed.xy * 2.0 - 1.0;
    n.xy *= strength;
    n.z = sqrt(saturate(1.0 - dot(n.xy, n.xy)));

    return SafeNormalize(n, float3(0.0, 0.0, 1.0));
}

// Normal map yalnızca detay varsa uygulanır.
// Düz alanlarda mesh normal korunur.
float3 ApplyNormalMapMaskedApprox(
    float3 worldN,
    UnityTexture2D normalMapTex,
    float2 uv,
    float normalStrength)
{
    float3 Nw = SafeNormalize(worldN, float3(0.0, 1.0, 0.0));

    float s = max(0.0, normalStrength);

    if (s <= 0.0001)
        return Nw;

    float4 nm = SAMPLE_TEXTURE2D(normalMapTex.tex, normalMapTex.samplerstate, uv);
    float3 nTS = UnpackNormalScaleTS(nm, s);

    float detail = length(nTS.xy);

    // Normal map detay maskesi.
    float mask = smoothstep(0.005, 0.05, detail);

    float3 up = abs(Nw.y) < 0.999
        ? float3(0.0, 1.0, 0.0)
        : float3(1.0, 0.0, 0.0);

    float3 T = SafeNormalize(cross(up, Nw), float3(1.0, 0.0, 0.0));
    float3 B = cross(Nw, T);

    float3 perturbed = SafeNormalize(
        Nw + T * nTS.x + B * nTS.y,
        Nw
    );

    return SafeNormalize(lerp(Nw, perturbed, mask), Nw);
}

float2 MinimoShadowPositionSS(float3 positionWS)
{
#if MINIMO_USE_HDRP_SHADOWS
    float3 positionNDC = ComputeNormalizedDeviceCoordinatesWithZ(positionWS, GetWorldToHClipMatrix());
    return saturate(positionNDC.xy) * _ScreenSize.xy;
#else
    return 0.0;
#endif
}

#if MINIMO_USE_HDRP_SHADOWS
float MinimoDirectionalShadow(float3 positionWS, float3 normalWS, float2 positionSS, inout HDShadowContext shadowContext)
{
#if MINIMO_USE_HDRP_SHADOWS
    if (_DirectionalShadowIndex < 0)
        return 1.0;

    DirectionalLightData light = _DirectionalLightDatas[_DirectionalShadowIndex];

    if ((light.lightLayers & GetMeshRenderingLayerMask()) == 0 ||
        light.lightDimmer <= 0.0 ||
        light.shadowDimmer <= 0.0 ||
        light.shadowIndex < 0)
    {
        return 1.0;
    }

    float3 L = SafeNormalize(-light.forward, float3(0.0, 1.0, 0.0));
    float3 biasNormal = SafeNormalize(normalWS, L);
    float shadow = GetDirectionalShadowAttenuation(shadowContext, positionSS, positionWS, biasNormal, light.shadowIndex, L);

    return lerp(1.0, shadow, saturate(light.shadowDimmer));
#else
    return 1.0;
#endif
}

float MinimoPunctualShadow(float3 positionWS, float3 normalWS, float2 positionSS, HDShadowContext shadowContext)
{
#if MINIMO_USE_HDRP_SHADOWS
    float shadow = 1.0;
    uint renderingLayers = GetMeshRenderingLayerMask();
    uint lightCount = min(_PunctualLightCount, (uint)MINIMO_MAX_PUNCTUAL_SHADOW_LIGHTS);

    for (uint i = 0; i < lightCount; i++)
    {
        LightData light = _LightDatas[i];

        if ((light.lightLayers & renderingLayers) == 0 ||
            light.lightDimmer <= 0.0 ||
            light.shadowDimmer <= 0.0 ||
            light.shadowIndex < 0)
        {
            continue;
        }

        float3 lightVector = light.positionRWS - positionWS;
        float distanceSqr = max(dot(lightVector, lightVector), 1e-6);
        float inverseDistance = rsqrt(distanceSqr);
        float distanceToLight = distanceSqr * inverseDistance;
        float3 L = lightVector * inverseDistance;
        float projectedDistance = dot(-lightVector, light.forward);
        float cosForward = projectedDistance * inverseDistance;
        float rangeAttenuation = saturate(1.0 - distanceSqr * light.rangeAttenuationScale);
        rangeAttenuation *= rangeAttenuation;
        float angleAttenuation = saturate(cosForward * light.angleScale + light.angleOffset);
        angleAttenuation *= angleAttenuation;
        float lightAttenuation = rangeAttenuation * angleAttenuation;

        if (distanceToLight >= light.range || lightAttenuation <= 0.0)
            continue;

        float3 biasNormal = SafeNormalize(normalWS, L);
        float lightShadow = GetPunctualShadowAttenuation(
            shadowContext,
            positionSS,
            positionWS,
            biasNormal,
            light.shadowIndex,
            L,
            distanceToLight,
            light.lightType == GPULIGHTTYPE_POINT,
            light.lightType != GPULIGHTTYPE_PROJECTOR_BOX
        );

        float weightedShadow = lerp(1.0, lightShadow, saturate(light.shadowDimmer * lightAttenuation));
        shadow = min(shadow, weightedShadow);
    }

    return shadow;
#else
    return 1.0;
#endif
}
#endif

#if MINIMO_USE_HDRP_SHADOWS
float MinimoReceivedShadowSample(float3 positionWS, float3 normalWS, inout HDShadowContext shadowContext)
{
#if MINIMO_USE_HDRP_SHADOWS
    float2 positionSS = MinimoShadowPositionSS(positionWS);

    float shadow = MinimoDirectionalShadow(positionWS, normalWS, positionSS, shadowContext);
    shadow = min(shadow, MinimoPunctualShadow(positionWS, normalWS, positionSS, shadowContext));

    return saturate(shadow);
#else
    return 1.0;
#endif
}
#endif

float MinimoShaderGraphMainLightShadow(float3 positionWS)
{
#if MINIMO_USE_URP_SHADOWS
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    return saturate(MainLightRealtimeShadow(shadowCoord));
#elif MINIMO_USE_BUILTIN_SHADOWS
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
        float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(positionWS));
        return saturate(MainLightRealtimeShadow(shadowCoord));
    #elif defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
        float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
        return saturate(MainLightRealtimeShadow(shadowCoord));
    #elif defined(SHADOWS_SCREEN) || defined(SHADOWS_DEPTH) || defined(SHADOWS_CUBE)
        float4 screenPos = ComputeScreenPos(TransformWorldToHClip(positionWS));
        return saturate(UnityComputeForwardShadows(float2(0.0, 0.0), positionWS, screenPos));
    #else
        return 1.0;
    #endif
#else
    return 1.0;
#endif
}

float MinimoReceivedShadow(float3 positionWS, float3 normalWS)
{
#if MINIMO_USE_HDRP_SHADOWS
    HDShadowContext shadowContext = InitShadowContext();

    float3 N = SafeNormalize(normalWS, float3(0.0, 1.0, 0.0));
    float3 filterAxis = SafeNormalize(-positionWS, float3(0.0, 0.0, 1.0));

    if (_DirectionalShadowIndex >= 0)
    {
        DirectionalLightData mainShadowLight = _DirectionalLightDatas[_DirectionalShadowIndex];
        filterAxis = SafeNormalize(-mainShadowLight.forward, filterAxis);
    }

    float3 up = abs(filterAxis.y) < 0.999 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
    float3 T = SafeNormalize(cross(up, filterAxis), float3(1.0, 0.0, 0.0));
    float3 B = SafeNormalize(cross(filterAxis, T), float3(0.0, 1.0, 0.0));

    float radius = MINIMO_SHADOW_SOFTNESS_WORLD;
    float2 diagonal = normalize(float2(1.0, 1.0)) * radius;
    float shadow = MinimoReceivedShadowSample(positionWS, N, shadowContext) * 0.28;
    shadow += MinimoReceivedShadowSample(positionWS + T * radius, N, shadowContext) * 0.12;
    shadow += MinimoReceivedShadowSample(positionWS - T * radius, N, shadowContext) * 0.12;
    shadow += MinimoReceivedShadowSample(positionWS + B * radius, N, shadowContext) * 0.12;
    shadow += MinimoReceivedShadowSample(positionWS - B * radius, N, shadowContext) * 0.12;
    shadow += MinimoReceivedShadowSample(positionWS + T * diagonal.x + B * diagonal.y, N, shadowContext) * 0.06;
    shadow += MinimoReceivedShadowSample(positionWS - T * diagonal.x + B * diagonal.y, N, shadowContext) * 0.06;
    shadow += MinimoReceivedShadowSample(positionWS + T * diagonal.x - B * diagonal.y, N, shadowContext) * 0.06;
    shadow += MinimoReceivedShadowSample(positionWS - T * diagonal.x - B * diagonal.y, N, shadowContext) * 0.06;

    return saturate(smoothstep(0.0, 1.0, shadow));
#elif MINIMO_USE_URP_SHADOWS || MINIMO_USE_BUILTIN_SHADOWS
    return MinimoShaderGraphMainLightShadow(positionWS);
#else
    return 1.0;
#endif
}

float3 ShadePlastic_HDRP_TransparentSafe(
    float3 positionWS,
    float3 normalWS,
    UnityTexture2D normalMapTex,
    float normalStrength,
    float2 uv0,
    float3 baseColor,
    float smoothness,
    float3 specularColor,
    float3 shadowColor,
    float noiseScale,
    float noiseStrength)
{
    #if defined(SHADERPASS_SCENEPICKING) || defined(SHADERPASS_SCENESELECTION)
        return baseColor;
    #endif

    float3 N = SafeNormalize(normalWS, float3(0.0, 1.0, 0.0));
    N = ApplyNormalMapMaskedApprox(N, normalMapTex, uv0, normalStrength);

    // HDRP ShaderGraph World Position çoğu durumda camera-relative olur.
    float3 V = SafeNormalize(-positionWS, float3(0.0, 0.0, 1.0));

    smoothness = saturate(smoothness);

    float3 shadowTint = saturate(shadowColor);
    float3 specColor = saturate(specularColor);

    // HDRP LightLoop kullanılmıyor.
    // Böylece g_vBigTileLightList / LightLoop redefinition hatası çıkmaz.
    // Işık hissi için güvenli fake directional ışıklar.
    float3 L0 = SafeNormalize(float3(0.35, 0.85, 0.45), float3(0.0, 1.0, 0.0));
    float3 L1 = SafeNormalize(float3(-0.55, 0.35, -0.25), float3(0.0, 1.0, 0.0));
    float3 L2 = SafeNormalize(float3(0.10, 0.25, -0.85), float3(0.0, 1.0, 0.0));

    float wrap = 0.4;

    float NdotL0 = saturate((dot(N, L0) + wrap) / (1.0 + wrap));
    float NdotL1 = saturate((dot(N, L1) + wrap) / (1.0 + wrap));
    float NdotL2 = saturate((dot(N, L2) + wrap) / (1.0 + wrap));

    float3 diff = 0.0;
    float receivedShadow = MinimoReceivedShadow(positionWS, N);
    float shadowVisibility = lerp(0.28, 1.0, receivedShadow);
    float3 directShadow = lerp(shadowTint * shadowVisibility, float3(1.0, 1.0, 1.0), receivedShadow);

    // Ana ışık.
    diff += baseColor * NdotL0 * 0.95;

    // Dolgu ışığı.
    diff += baseColor * NdotL1 * 0.20;

    // Hafif rim/back ışık.
    diff += baseColor * NdotL2 * 0.08;

    // Gölge rengi hissi.
    float fakeShadow = saturate(1.0 - NdotL0);
    diff += baseColor * shadowTint * fakeShadow * 0.14;
    diff *= directShadow;

    // Specular noise.
    float3 N_spec = N;

    {
        float safeNoiseScale = max(noiseScale, 0.0001);
        float n = Hash21(uv0 * safeNoiseScale);
        float3 noiseN = n.xxx * 2.0 - 1.0;
        N_spec = SafeNormalize(N_spec + noiseN * noiseStrength, N);
    }

    // URP sürümündeki daha geniş/soft highlight mantığı.
    float specPower = exp2(7.0 * smoothness + 1.0);

    float3 H0 = SafeNormalize(L0 + V, N);
    float NdotH0 = saturate(dot(N_spec, H0));
    float LdotH0 = saturate(dot(L0, H0));

    float fresnel0 = lerp(0.5, 1.0, pow(1.0 - LdotH0, 5.0));

    float3 spec = specColor * pow(NdotH0, specPower) * NdotL0 * fresnel0 * 2.0 * receivedShadow;

    // Ambient.
    float3 ambient = baseColor * 0.08;

    float3 finalColor = diff + spec + ambient;

    return finalColor;
}

// ============================================================
// ShaderGraph Custom Function signatures
// Function Name: AdditionalLights_Unlit
// ============================================================

void AdditionalLights_Unlit_float(
    float3 PositionWS,
    float3 NormalWS,
    UnityTexture2D GeomNormalWS,
    float NormalStrength,
    float2 UV0,
    float3 BaseColor,
    float Smoothness,
    float3 SpecularColor,
    float3 ShadowColor,
    float NoiseScale,
    float NoiseStrength,
    out float3 OutColor)
{
    OutColor = ShadePlastic_HDRP_TransparentSafe(
        PositionWS,
        NormalWS,
        GeomNormalWS,
        NormalStrength,
        UV0,
        BaseColor,
        Smoothness,
        SpecularColor,
        ShadowColor,
        NoiseScale,
        NoiseStrength
    );
}

void AdditionalLights_Unlit_half(
    half3 PositionWS,
    half3 NormalWS,
    UnityTexture2D GeomNormalWS,
    half NormalStrength,
    half2 UV0,
    half3 BaseColor,
    half Smoothness,
    half3 SpecularColor,
    half3 ShadowColor,
    half NoiseScale,
    half NoiseStrength,
    out half3 OutColor)
{
    OutColor = (half3)ShadePlastic_HDRP_TransparentSafe(
        (float3)PositionWS,
        (float3)NormalWS,
        GeomNormalWS,
        (float)NormalStrength,
        (float2)UV0,
        (float3)BaseColor,
        (float)Smoothness,
        (float3)SpecularColor,
        (float3)ShadowColor,
        (float)NoiseScale,
        (float)NoiseStrength
    );
}
