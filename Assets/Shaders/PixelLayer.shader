// 낮은 해상도 렌더텍스처를 화면에 붙인다 (투명 유지, 점 필터). 복셀 총을 도트처럼 보이게.
Shader "TinCan/PixelLayer"
{
    Properties { _MainTex ("Tex", 2D) = "white" {} }
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
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A a) { V o; o.pos = TransformObjectToHClip(a.pos.xyz); o.uv = a.uv; return o; }
            half4 frag(V i) : SV_Target { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv); }
            ENDHLSL
        }
    }
}
