// 원형 페이드 (사용자 10-13: 게임 도트에 맞춰서). 화면을 320×180 도트 칸으로 나누고, 칸의 중심이 원 밖이면 검정.
// 카메라 줌과 상관없이 계단 = 항상 게임 도트 한 칸. 화면을 덮는 사각형의 uv로 화면 좌표를 구한다 (렌더 타깃 뒤집힘과 무관).
Shader "TinCan/Iris"
{
    Properties
    {
        _Center ("Center (0..1 of screen)", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Radius (dots)", Float) = 1000
        _Rows ("Dot rows on screen", Float) = 180
        _Color ("Color", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _Center, _Color; float _Radius, _Rows;
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A a) { V o; o.pos = TransformObjectToHClip(a.pos.xyz); o.uv = a.uv; return o; }
            half4 frag(V i) : SV_Target
            {
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 grid = float2(_Rows * aspect, _Rows);          // 화면의 도트 칸 수 (가로, 세로)
                float2 cell = floor(i.uv * grid) + 0.5;                // 이 픽셀이 속한 도트 칸의 중심 (도트 단위)
                float2 c = floor(_Center.xy * grid) + 0.5;             // 원 중심도 도트 칸에 붙인다 → 좌우 대칭 계단
                float d = length(cell - c);
                return d > _Radius ? _Color : half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
