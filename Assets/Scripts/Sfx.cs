using UnityEngine;

namespace TinCan
{
    /// <summary>소리 파일 없이 코드로 만든 효과음. 탱(깡통), 빵(총), 딸깍(빈 총), 철컥(장전), 툭(떨어짐)</summary>
    public static class Sfx
    {
        static AudioClip tang, bang, click, reload, drop;
        static AudioSource src;

        static AudioSource Src()
        {
            if (src != null) return src;
            var go = new GameObject("Sfx");
            src = go.AddComponent<AudioSource>();
            src.spatialBlend = 0f;
            return src;
        }

        static AudioClip Make(string name, float seconds, System.Func<float, float, float> f)
        {
            int rate = 44100, n = (int)(rate * seconds);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < n; i++) data[i] = f(i / (float)rate, (float)rng.NextDouble() * 2f - 1f);
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>탱: 금속 울림 (비조화 배음 셋이 빠르게 죽는다) + 아주 짧은 타격 잡음</summary>
        public static AudioClip Tang() => tang != null ? tang : (tang = Make("tang", 0.35f, (t, noise) =>
        {
            float env = Mathf.Exp(-t * 14f);
            float a = Mathf.Sin(2f * Mathf.PI * 1850f * t) * 0.5f;
            float b = Mathf.Sin(2f * Mathf.PI * 2730f * t) * 0.3f * Mathf.Exp(-t * 6f);
            float c = Mathf.Sin(2f * Mathf.PI * 4100f * t) * 0.2f * Mathf.Exp(-t * 10f);
            float hit = noise * Mathf.Exp(-t * 300f) * 0.5f;
            return Mathf.Clamp((a + b + c) * env + hit, -1f, 1f);
        }));

        /// <summary>빵: 잡음 폭발 + 낮은 쿵</summary>
        public static AudioClip Bang() => bang != null ? bang : (bang = Make("bang", 0.3f, (t, noise) =>
        {
            float crack = noise * Mathf.Exp(-t * 45f) * 0.9f;
            float low = Mathf.Sin(2f * Mathf.PI * (110f - t * 200f) * t) * Mathf.Exp(-t * 18f) * 0.7f;
            return Mathf.Clamp(crack + low, -1f, 1f);
        }));

        /// <summary>딸깍: 빈 총</summary>
        public static AudioClip Click() => click != null ? click : (click = Make("click", 0.06f, (t, noise) =>
            noise * Mathf.Exp(-t * 120f) * 0.5f));

        /// <summary>철컥: 실린더 돌아가는 소리 (딸깍 여섯 번)</summary>
        public static AudioClip Reload() => reload != null ? reload : (reload = Make("reload", 0.5f, (t, noise) =>
        {
            float phase = (t * 14f) % 1f;
            return noise * Mathf.Exp(-phase * 40f) * 0.35f * (t < 0.45f ? 1f : 0f);
        }));

        /// <summary>툭: 깡통이 땅에 떨어짐</summary>
        public static AudioClip Drop() => drop != null ? drop : (drop = Make("drop", 0.15f, (t, noise) =>
        {
            float env = Mathf.Exp(-t * 30f);
            return (Mathf.Sin(2f * Mathf.PI * 420f * t) * 0.4f + noise * 0.3f) * env;
        }));

        public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            var s = Src();
            s.pitch = pitch;
            s.PlayOneShot(clip, volume);
        }
    }
}
