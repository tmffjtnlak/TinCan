// 복셀용: 정점 색 그대로, 빛 없음, 깊이 씀, 뒷면 안 그림. 2D 렌더러 안에서 3D 덩어리를 그린다.
Shader "TinCan/VoxelUnlit"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Unlit"
            Cull Back
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 pos : POSITION; float4 col : COLOR; };
            struct V { float4 pos : SV_POSITION; float4 col : COLOR; };
            V vert(A a) { V o; o.pos = TransformObjectToHClip(a.pos.xyz); o.col = a.col; return o; }
            half4 frag(V i) : SV_Target { return i.col; }
            ENDHLSL
        }
    }
}
