using System.Collections;
using UnityEngine;

namespace TinCan
{
    /// <summary>
    /// 빈 탄피 (사용자 10-12). 세트 점수판 합계에서 쏟아져 모래밭에 떨어지고, 눕는 것부터 돈 판자로 날아가 돈이 된다.
    /// 움직임 (사용자 10-12, 용수철 문법): 스폰 = 흰 실루엣으로 작게 떴다가 튕기며 커지고 제 색으로 ·
    /// 떨어짐 = 바닥에 닿을 때 납작해졌다 튕김 · 흡수 = 살짝 부풀었다가 빨려 들어가며 돈다.
    /// 물리 없이 손으로 떨어뜨린다.
    /// </summary>
    public class Casing : MonoBehaviour
    {
        public long Value;
        public bool Gold;
        /// <summary>바닥에 눕는 순간 (판자로 날아갈 차례)</summary>
        public System.Action OnRest;
        SpriteRenderer sr, white;
        Vector2 vel; float spin, floorY; bool resting, flying;
        float baseScale = 1f, sx = 1f, sy = 1f, vx, vy, whiteA;
        static readonly Color Brass = new Color(0.72f, 0.56f, 0.28f), GoldC = new Color(1f, 0.85f, 0.35f);
        static Sprite whiteSprite;

        /// <summary>그림의 흰 실루엣 (한 번 만들어 둔다). 읽을 수 없으면 없음</summary>
        static Sprite WhiteOf(Sprite s)
        {
            if (whiteSprite != null || s == null) return whiteSprite;
            try
            {
                var r = s.textureRect;
                var tex = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = s.texture.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
                for (int i = 0; i < px.Length; i++) px[i] = new Color(1f, 1f, 1f, px[i].a);
                tex.SetPixels(px); tex.Apply();
                whiteSprite = Sprite.Create(tex, new Rect(0, 0, r.width, r.height), new Vector2(0.5f, 0.5f), s.pixelsPerUnit);
            }
            catch { }
            return whiteSprite;
        }

        public static Casing Drop(Vector2 at, long value, bool gold, Vector2? velocity = null)
        {
            var go = new GameObject("Casing");
            var c = go.AddComponent<Casing>();
            c.Value = value; c.Gold = gold;
            var art = GunView.LoadArt("casing");
            if (art != null)
            {
                c.sr = go.AddComponent<SpriteRenderer>();
                c.sr.sprite = art; c.sr.sortingOrder = 1;
                c.sr.color = gold ? new Color(1f, 1f, 0.8f) : Color.white;
                c.baseScale = 2f / 3f;   // 도트 규칙: 27×14 원본은 2픽셀 칸으로 (반 크기로 다시 그리면 1)
                var w = WhiteOf(art);
                if (w != null)
                {
                    var wg = new GameObject("White"); wg.transform.SetParent(go.transform, false);
                    c.white = wg.AddComponent<SpriteRenderer>(); c.white.sprite = w; c.white.sortingOrder = 2; c.white.color = Color.white;
                }
            }
            else c.sr = Look.Quad("Brass", Vector2.zero, new Vector2(gold ? 0.3f : 0.24f, 0.1f), gold ? GoldC : Brass, 1, go.transform);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            c.vel = velocity ?? new Vector2(Random.Range(-2.5f, 2.5f), Random.Range(2f, 5f));
            c.spin = Random.Range(-900f, 900f);
            c.floorY = Random.Range(Look.DropY - 0.55f, Look.DropY + 0.25f);
            // 스폰: 작게(0.25) 흰색으로 시작, 용수철이 튕기며 1로. 흰색은 3프레임 뒤 사라진다
            c.sx = c.sy = 0.25f; c.vx = c.vy = 14f; c.whiteA = 1f;
            c.Apply();
            return c;
        }

        void Apply()
        {
            transform.localScale = new Vector3(sx, sy, 1f) * baseScale;
            if (white != null) white.color = new Color(1f, 1f, 1f, whiteA);
        }

        void Update()
        {
            float dt = Time.deltaTime, udt = Time.unscaledDeltaTime;
            if (!flying)
            {
                Ease.Spring(ref sx, ref vx, 1f, 420f, 14f, udt);
                Ease.Spring(ref sy, ref vy, 1f, 420f, 14f, udt);
            }
            whiteA = Mathf.Max(0f, whiteA - udt / 0.1f);
            Apply();
            if (resting || flying) return;
            vel.y -= 22f * dt;
            transform.position += (Vector3)(vel * dt);
            transform.Rotate(0f, 0f, spin * dt);
            if (transform.position.y <= floorY)
            {
                transform.position = new Vector3(transform.position.x, floorY, 0f);
                // 닿는 순간 납작 (가로 1.4, 세로 0.6) → 용수철이 튕겨 돌려놓는다
                sx = 1.4f; sy = 0.6f; vx = vy = 0f;
                if (Mathf.Abs(vel.y) < 2f)
                {
                    resting = true; vel = Vector2.zero;
                    transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-12f, 12f) + (Random.value < 0.5f ? 0f : 180f));
                    OnRest?.Invoke();
                }
                else
                {
                    vel.y = -vel.y * 0.35f; vel.x *= 0.6f; spin *= 0.5f;
                    Sfx.Play(Sfx.Click(), 0.08f, Random.Range(2.2f, 3f));
                }
            }
        }

        /// <summary>돈 판자로 날아간다: 살짝 부풀며 흰빛이 돌았다가(기대) 빨려 들어가듯 작아지며 돈다. 닿으면 onArrive(값)</summary>
        public IEnumerator FlyTo(Vector2 target, float delay, System.Action<long> onArrive)
        {
            flying = true;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            // 기대: 0.08초 동안 1.35배로 부풀고 흰빛
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.08f) { sx = sy = Mathf.Lerp(1f, 1.35f, Ease.OutQuad(t)); whiteA = t * 0.8f; Apply(); yield return null; }
            Vector2 from = transform.position;
            Vector2 lift = from + Vector2.up * Random.Range(0.6f, 1.4f) + Vector2.right * Random.Range(-0.4f, 0.4f);
            float life = 0.4f;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / life)
            {
                float k = Ease.InQuad(t);                 // 천천히 떴다가 빨라진다
                Vector2 a = Vector2.Lerp(from, lift, t), b = Vector2.Lerp(lift, target, k);
                transform.position = Vector2.Lerp(a, b, t);
                transform.Rotate(0f, 0f, (360f + 720f * t) * Time.unscaledDeltaTime);
                sx = Mathf.Lerp(1.35f, 0.3f, k); sy = Mathf.Lerp(1.35f, 0.9f, k);   // 빨려 들어가며 길쭉해진다
                whiteA = Mathf.Max(0f, 0.8f - t);
                Apply();
                yield return null;
            }
            onArrive?.Invoke(Value);
            Destroy(gameObject);
        }
    }
}
