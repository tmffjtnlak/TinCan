using UnityEngine;

namespace TinCan
{
    /// <summary>이징. 도트 게임의 부드러운 트윈은 그림이 아니라 이 곡선에서 나온다 (Click the Button 식). t는 0..1</summary>
    public static class Ease
    {
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);
        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float OutExpo(float t) => t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
        /// <summary>살짝 넘쳤다 돌아온다 (버튼·노드 팝)</summary>
        public static float OutBack(float t, float s = 1.70158f) { t -= 1f; return t * t * ((s + 1f) * t + s) + 1f; }
        /// <summary>출렁이며 멈춘다 (큰 보상)</summary>
        public static float OutElastic(float t)
        {
            if (t <= 0f) return 0f; if (t >= 1f) return 1f;
            const float p = 0.3f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - p / 4f) * (2f * Mathf.PI) / p) + 1f;
        }
        public static float InQuad(float t) => t * t;
        /// <summary>갔다 돌아오는 산 모양 (펀치)</summary>
        public static float Punch(float t) => Mathf.Sin(t * Mathf.PI);
    }
}
