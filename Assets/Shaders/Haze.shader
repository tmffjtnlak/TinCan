// 아지랑이 (사용자 10-13): 지평선 띠에서 행마다 좌우로 한두 도트씩 흔들린다. 도트 칸 단위로만 움직여 격자가 안 깨진다.
// 스프라이트 그대로 쓰되 uv.x를 행 단위로 민다. _Band = (아래 uv.y, 위 uv.y): 아래쪽이 가장 세고 위로 갈수록 잦아든다.
Shader "TinCan/Haze"
{
    Properties
    {
        _MainTex ("Tex", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _TexSize ("Texture size (px)", Vector) = (480, 270, 0, 0)
        _Band ("Band (lo, hi)", Vector) = (0.72, 0.88, 0, 0)
        _Amp ("Amplitude (px)", Float) = 1.6
        _Rate ("Steps per second", Float) = 10
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _Color, _TexSize, _Band; float _Amp, _Rate;
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; float4 col : COLOR; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 col : COLOR; };
            V vert(A a) { V o; o.pos = TransformObjectToHClip(a.pos.xyz); o.uv = a.uv; o.col = a.col * _Color; return o; }
            half4 frag(V i) : SV_Target
            {
                float row = floor(i.uv.y * _TexSize.y);                       // 그림의 행
                float k = i.uv.y < _Band.x ? 0 : saturate((_Band.y - i.uv.y) / max(1e-4, _Band.y - _Band.x));   // 띠 안에서만, 아래로 갈수록 1
                float t = floor(_Time.y * _Rate) / _Rate;                      // 초당 _Rate번만 바뀐다
                float w = sin(row * 0.9 + t * 2.1) * sin(row * 0.37 - t * 1.3 + 0.7);
                float shift = round(w * _Amp * k);                             // 도트 칸 단위
                float2 uv = i.uv + float2(shift / _TexSize.x, 0);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * i.col;
            }
            ENDHLSL
        }
    }
}
