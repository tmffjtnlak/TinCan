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
        public static float InCubic(float t) => t * t * t;
        /// <summary>천천히 출발해 빨라졌다 천천히 닿는다 (카메라 이동)</summary>
        public static float InOutSine(float t) => -(Mathf.Cos(Mathf.PI * Mathf.Clamp01(t)) - 1f) / 2f;   // 카메라 둘러보기 (10-15 오프닝)
        public static float InOutCubic(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        /// <summary>처음에 살짝 뒤로 물러났다(예비 동작) 나간다</summary>
        public static float InBack(float t, float s = 1.70158f) => t * t * ((s + 1f) * t - s);
        /// <summary>줌: 크기는 곱으로 변해야 일정한 속도로 느껴진다 (선형 보간이면 끝에서 확 빨라 보임)</summary>
        public static float LerpLog(float a, float b, float k) => Mathf.Exp(Mathf.Lerp(Mathf.Log(a), Mathf.Log(b), k));
        /// <summary>갔다 돌아오는 산 모양 (펀치)</summary>
        public static float Punch(float t) => Mathf.Sin(t * Mathf.PI);
        /// <summary>
        /// 용수철: 값이 목표로 튕기며 간다 (Click the Button 식 말랑한 움직임). 매 프레임 부른다.
        /// stiffness 크면 빠르고, damping 작으면 더 출렁인다. (300, 18)이면 0.15초쯤에 살짝 넘쳤다 자리 잡는다
        /// </summary>
        public static void Spring(ref float x, ref float v, float target, float stiffness, float damping, float dt)
        {
            dt = Mathf.Min(dt, 0.05f);
            v += (target - x) * stiffness * dt;
            v *= Mathf.Max(0f, 1f - damping * dt);
            x += v * dt;
        }
    }
}
