using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinCan
{
    /// <summary>스킬 노드 하나. 자리(트리 좌표), 이웃, 값, 효과. 데이터로만 늘린다.</summary>
    public class SkillNode
    {
        public string Id, Name, Glyph;
        public Func<int, string> Desc;          // 설명
        public Vector2 Pos;                     // 트리 좌표 (1 = 노드 간격 하나)
        public string[] Links = new string[0];  // 이웃 (양방향)
        public int Level, MaxLevel = 1;         // 한 번 사면 끝 (사용자 10-10: 강화가 아니라 하나에 하나씩)
        public Func<int, long> Cost;
        public Action<Game, int> Apply;
        public Color Color;                     // 계열 색
        public bool Root;
        /// <summary>핵심 노드 (사용자 10-12): 클릭이 아니라 꾹 눌러 게이지를 채워 해방한다. 해방되면 이웃을 총으로 쏴서 연다</summary>
        public bool Key;
        /// <summary>핵심 노드 (10-14): 플레이가 달라지는 것. 크게 그린다. 아니면 수치 노드</summary>
        public bool Core;
        /// <summary>첫 현상금이 끝나야 살 수 있다 (10-15: 깡통 종류 갈래). 그전엔 반투명 미리보기로만</summary>
        public bool AfterBounty;
        public bool Locked => AfterBounty && Game.I != null && !Game.I.FirstBountyDone;
        public bool Maxed => Level >= MaxLevel;
        public long NextCost => Cost(Level);
    }

    /// <summary>
    /// 스킬트리 = 숫자 (얼마나 잘). 총 종류는 총기류, 깡통 종류는 깡통류에서 산다.
    /// 노드 하나 = 구매 한 번. 깊이가 필요하면 노드를 이어 붙인다 (빠른 장전 → 더 빠른 장전).
    /// 계열 다섯: 장전(빨강), 선반(하늘), 돈(노랑), 자동(초록), 시간(보라). 열림 규칙: 이웃 중 하나라도 샀으면 살 수 있다.
    /// </summary>
    public static class SkillTree
    {
        public static readonly Color Red = new Color(1f, 0.25f, 0.35f);
        public static readonly Color Cyan = new Color(0.3f, 0.9f, 1f);
        public static readonly Color Yellow = new Color(1f, 0.85f, 0.3f);
        public static readonly Color Green = new Color(0.4f, 1f, 0.5f);
        public static readonly Color Purple = new Color(0.75f, 0.55f, 1f);
        public static readonly Color White = new Color(0.9f, 0.9f, 0.95f);
        public static readonly Color Orange = new Color(1f, 0.58f, 0.25f);   // 깡통 종류 (10-15)

        static SkillNode N(string id, string name, string glyph, Vector2 pos, Color color, long cost, string desc, Action<Game> apply, params string[] links)
            => new SkillNode { Id = id, Name = name, Glyph = glyph, Pos = pos, Color = color, Cost = l => cost, Desc = l => desc, Apply = (g, l) => apply(g), Links = links };

        public static List<SkillNode> Make() => Tiered(Raw());

        /// <summary>
        /// 단계 구조 (사용자 10-12: "스킬트리처럼. 무조건 연결이 아니라. 초반 스킬은 첫 노드에서 하나씩, 상위 노드에 상위 스펙,
        /// 상위 스킬 종류는 상위 노드에 붙어 나온다"). 모양(리볼버)은 나중에 이 구조 위에 입힌다.
        ///   0단: 시작(핵심)
        ///   1단: 세 갈래 사슬 — 장전(-20% → -20% → -25%), 선반(+1 → +1 → 할인), 돈(+30% → 연속 배수 → +40%). 하나 사야 다음이 보인다
        ///   관문(핵심): 사슬 끝. 꾹 눌러 해방하면 2단이 열린다
        ///   2단: 장전 응용(첫 발·마지막 발 / 연속 장전·허리춤), 선반 응용(가벼운 깡통·다시 튐 / 붙여 놓기·짝 소환),
        ///        돈 관문 뒤엔 **새 종류**가 붙는다: 자동 사격기(초록), 슬로우(보라), 정산(노랑)
        /// 선은 가로·세로·45°만, 정수 격자, 고리 없음(나무).
        /// </summary>
        static List<SkillNode> Tiered(List<SkillNode> raw)
        {
            var byId = new Dictionary<string, SkillNode>();
            foreach (var n in raw) { byId[n.Id] = n; n.Links = new string[0]; }
            var list = new List<SkillNode>();
            var at = new Dictionary<(int, int), SkillNode>();
            void Link(SkillNode a, SkillNode b) { var l = new List<string>(a.Links); if (!l.Contains(b.Id)) l.Add(b.Id); a.Links = l.ToArray(); }
            SkillNode Put(string id, int x, int y, params (int, int)[] parents)
            {
                var n = byId[id];
                n.Pos = new Vector2(x, y);
                if (at.ContainsKey((x, y))) Debug.LogWarning($"트리 자리가 겹친다: {id} ({x},{y})");
                list.Add(n); at[(x, y)] = n;
                foreach (var pp in parents)
                {
                    int dx = Mathf.Abs(pp.Item1 - x), dy = Mathf.Abs(pp.Item2 - y);
                    if (!(dx == 0 || dy == 0 || dx == dy)) Debug.LogWarning($"트리 선이 격자 각도가 아니다: {pp} → ({x},{y})");
                    Link(at[pp], n);
                }
                return n;
            }

            // 10-14 3판 (사용자: "수치 스킬과 핵심 스킬을 구분"): 갈래마다 수치(작은 노드) 몇 개 → 핵심(큰 노드, 플레이가 달라짐).
            // 꾹 눌러 해방하는 핵심 노드(Key)는 미사용 (사용자 10-14). 시작 노드는 클릭 한 번으로 연다.
            Put("root", 0, 0);
            // 왼쪽 위 대각선 — 깡통 종류 (주황, 10-15: 첫 현상금 뒤에 열린다)
            Put("can_bottle", -1, 1, (0, 0));           // 핵심
            Put("can_bottle2", -2, 2, (-1, 1));
            Put("can_bundle", -3, 3, (-2, 2));          // 핵심
            Put("can_blue", -4, 4, (-3, 3));            // 핵심
            Put("can_magnet", -5, 5, (-4, 4));          // 핵심
            // 위 — 저글링 (하늘)
            // 10-15 사용자: 저글링은 처음부터 주어지는 게 아니라 얻는 것. 선반 1칸으로 시작 → "넓은 선반"(2칸)이 첫 저글링
            // 10-15 2차 (사용자, Click the Button식): 초반은 "손 풀기" — 쏘는 간격·장전 줄이기를 번갈아 → 빨라졌으니 둘이 된다 = 넓은 선반
            Put("quick1", 0, 1, (0, 0));
            Put("reload1", 0, 2, (0, 1));
            Put("quick2", 0, 3, (0, 2));
            Put("reload2", 0, 4, (0, 3));
            Put("shelf1", 0, 5, (0, 4));                // 핵심: 처음으로 둘
            Put("airslow1", 0, 6, (0, 5));
            Put("burst1", 0, 7, (0, 6));
            Put("air2x", 0, 8, (0, 7));                 // 핵심
            Put("airslow2", -1, 9, (0, 8));
            Put("lowbounce", -1, 10, (-1, 9));          // 핵심
            Put("bounce", -1, 11, (-1, 10));
            Put("rebound", -1, 12, (-1, 11));           // 핵심
            Put("burst2", 1, 9, (0, 8));
            Put("popchain", 1, 10, (1, 9));             // 핵심: 파편 튀기기
            Put("juggle", 1, 11, (1, 10));              // 핵심
            Put("burst3", 1, 12, (1, 11));
            // 왼쪽 — 장전 응용 (빨강)
            Put("just1", -1, 0, (0, 0));
            Put("firstshot", -2, 0, (-1, 0));           // 핵심
            Put("quick3", -3, 1, (-2, 0));
            Put("lastshot", -4, 1, (-3, 1));            // 핵심
            Put("reload3", -5, 1, (-4, 1));
            Put("just2", -3, -1, (-2, 0));
            Put("comboreload", -4, -1, (-3, -1));       // 핵심
            Put("holster", -5, -1, (-4, -1));           // 핵심
            // 오른쪽 — 선반 (하늘)
            Put("perfect1", 1, 0, (0, 0));             // (단골 할인은 10-14 소환이 공짜가 되며 뺐다)
            Put("shelf2", 2, 0, (1, 0));                // 핵심: 셋
            Put("cluster", 3, 1, (2, 0));               // 핵심
            Put("shelf3", 3, 0, (2, 0));                // 핵심: 넷
            // (짝 소환은 10-14 Space 한 번에 선반이 꽉 차게 되며 뺐다)
            // 아래 — 돈 (노랑) → 자동 사격 (초록)
            Put("value1", 0, -1, (0, 0));
            Put("payout1", 0, -2, (0, -1));
            Put("value2", 0, -3, (0, -2));
            Put("dynamite", -1, -3, (0, -3));          // 핵심 (10-14: 기본에서 빼 스킬로)
            Put("auto1", 0, -4, (0, -3));               // 핵심
            Put("auto2", -1, -5, (0, -4));
            Put("auto_lever", -2, -6, (-1, -5));        // 핵심
            Put("auto_shotgun", -3, -7, (-2, -6));      // 핵심
            Put("perfect2", 1, -5, (0, -4));
            Put("value3", 2, -6, (1, -5));
            // 오른쪽 아래 — 숨 (보라, 우클릭 슬로우)
            Put("slow1", 1, -3, (0, -3));
            Put("slow3", 2, -3, (1, -3));
            Put("slow2", 3, -3, (2, -3));
            Put("floor", 4, -3, (3, -3));               // 핵심
            return list;
        }

        /// <summary>
        /// (보관) 리볼버 모양 배치 1판. 구조(Tiered)가 먼저고, 모양은 나중에 이 위에 입힌다 (사용자 10-12).
        /// 뿌리 = 실린더 중심. 부위마다 기존 노드를 순서대로 앉히고 모자라면 빈 노드(시험용)로 채운다. 부위 끝은 핵심 노드.
        /// 단위 1 = 노드 간격. 오른쪽을 향한 옆모습.
        /// </summary>
        /// <summary>
        /// 규칙 (사용자 10-12: "뻗어나가는 규칙성, 선 정렬"):
        ///   모든 노드는 **정수 격자** 위. 선은 **가로·세로·45°** 셋뿐. 부위마다 두 줄이 나란히 가고 두 칸마다 가로대로 잇는다.
        /// </summary>
        static List<SkillNode> Revolver(List<SkillNode> raw)
        {
            var byId = new Dictionary<string, SkillNode>();
            foreach (var n in raw) { byId[n.Id] = n; n.Links = new string[0]; }
            var list = new List<SkillNode>();
            var at = new Dictionary<(int, int), SkillNode>();
            int blanks = 0;
            void Link(SkillNode a, SkillNode b) { var l = new List<string>(a.Links); if (!l.Contains(b.Id)) l.Add(b.Id); a.Links = l.ToArray(); }
            // 자리에 노드를 놓는다: id = 기존 노드 / null = 빈 노드 / "key:id:이름" = 핵심 노드. parents = 이어질 자리들 (격자 좌표)
            SkillNode Put(string id, Color c, int x, int y, params (int, int)[] parents)
            {
                SkillNode n;
                if (id == null) { blanks++; n = new SkillNode { Id = $"blank{blanks}", Name = "빈 노드", Glyph = "·", Color = c, Cost = l => 10, Desc = l => "시험용 빈 노드.", Apply = (g, l) => { } }; }
                else if (id.StartsWith("key:")) { var p = id.Split(':'); n = new SkillNode { Id = p[1], Name = p[2], Glyph = "★", Key = true, Color = c, Cost = l => 0, Desc = l => "핵심 노드. 꾹 눌러 해방한다.", Apply = (g, l) => { } }; }
                else n = byId[id];
                n.Pos = new Vector2(x, y);
                list.Add(n); at[(x, y)] = n;
                foreach (var pp in parents)
                {
                    int dx = Mathf.Abs(pp.Item1 - x), dy = Mathf.Abs(pp.Item2 - y);
                    if (!(dx == 0 || dy == 0 || dx == dy)) Debug.LogWarning($"트리 선이 격자 각도가 아니다: {pp} → ({x},{y})");
                    Link(at[pp], n);
                }
                return n;
            }

            // 뿌리 = 실린더 중심
            Put("root", White, 0, 0);
            // 10-14 사용자: 초반 저글링 유도 — 뿌리 바로 위에 셋 (두 배 → 왼쪽 가벼운 낙하 / 오른쪽 낮은 튐)
            Put("air2x", Cyan, 0, 1, (0, 0));
            Put("airslow", Cyan, -1, 1, (0, 1));
            Put("lowbounce", Cyan, 1, 1, (0, 1));
            // 실린더: 뿌리 둘레 여덟 칸 네모 고리 (장전 빨강). 뿌리는 상하좌우 넷에 닿는다
            Put("reload1", Red, 1, 0, (0, 0));
            Put("reload2", Red, 1, 1, (1, 0));
            Put("holster", Red, 0, 1, (1, 1), (0, 0));
            Put("reload3", Red, -1, 1, (0, 1));
            Put("firstshot", Red, -1, 0, (-1, 1), (0, 0));
            Put("lastshot", Red, -1, -1, (-1, 0));
            Put("comboreload", Red, 0, -1, (-1, -1), (0, 0));
            Put(null, Red, 1, -1, (0, -1), (1, 0));
            // 총열 (선반 하늘): 윗줄 y=1, 아랫줄 y=0, 두 칸마다 세로 가로대. 끝은 총구(핵심)
            string[] cy = { "shelf1", "shelf2", "cheap", "bounce", "rebound", "cluster", "pair", null, null, null };
            int ci = 0;
            for (int x = 2; x <= 6; x++) { Put(cy[ci++], Cyan, x, 0, (x - 1, 0)); }
            for (int x = 2; x <= 6; x++) { Put(cy[ci++], Cyan, x, 1, x % 2 == 0 ? new[] { (x - 1, 1), (x, 0) } : new[] { (x - 1, 1) }); }
            Put("key:muzzle:총구", Cyan, 7, 0, (6, 0), (6, 1));
            // 방아쇠울 (자동 초록): 실린더 아래에서 고리로 총열 밑에 닿는다. 아래로 방아쇠(핵심)
            Put("auto1", Green, 2, -2, (1, -1));
            Put("auto2", Green, 3, -2, (2, -2));
            Put(null, Green, 4, -1, (3, -2), (4, 0));
            Put("key:trigger:방아쇠", Green, 2, -3, (2, -2));
            // 손잡이 (돈 노랑): 두 줄이 45°로 내려가고 가로대. 끝은 개머리(핵심)
            Put("value1", Yellow, -2, -2, (-1, -1));
            Put("combo1", Yellow, -1, -2, (0, -1), (-2, -2));
            Put("value2", Yellow, -3, -3, (-2, -2));
            Put("payout1", Yellow, -2, -3, (-1, -2), (-3, -3));
            Put(null, Yellow, -4, -4, (-3, -3));
            Put(null, Yellow, -3, -4, (-2, -3), (-4, -4));
            Put("key:butt:개머리", Yellow, -4, -5, (-4, -4), (-3, -4));
            // 공이 (시간 보라): 뒤쪽 위로 45°. 끝은 공이(핵심)
            Put("slow1", Purple, -2, 1, (-1, 1));
            Put("slow2", Purple, -2, 2, (-2, 1));
            Put("slow3", Purple, -3, 2, (-2, 1), (-2, 2));
            Put("floor", Purple, -3, 3, (-2, 2), (-3, 2));
            Put("key:hammer:공이", Purple, -4, 4, (-3, 3));
            return list;
        }

        /// <summary>핵심 노드 (10-14 사용자: 플레이가 달라지는 것). 크게 그린다. 수치 노드(N)는 숫자만 오른다</summary>
        static SkillNode Gate(SkillNode n) { n.AfterBounty = true; var d = n.Desc; n.Desc = l => (n.Locked ? "[첫 현상금을 끝내면 열린다]  " : "") + d(l); return n; }
        static SkillNode C(string id, string name, string glyph, Color color, long cost, string desc, Action<Game> apply)
        { var n = N(id, name, glyph, Vector2.zero, color, cost, desc, apply); n.Core = true; return n; }

        // 가격 (10-14 2차, 사용자 플레이 기록: 숙련자 분당 ≈ $180, 7개를 25초에 몰아 삼 → 약 3배). 처음 보는 사람은 분당 ≈ $90~120으로 잡고
        // 첫 수치 노드 ≈ 20~30초 벌이, 첫 핵심(둘이면 두 배) ≈ 2분 벌이. 바깥으로 갈수록 오른다
        static List<SkillNode> Raw() => new List<SkillNode>
        {
            // 시작 노드: 눌러서 연다 (10-14 사용자: 꾹 누르는 연출 말고 그냥 클릭, 값 0)
            new SkillNode { Id = "root", Name = "깡통 소환", Glyph = "깡", Root = true, Core = true, Level = 0, Pos = Vector2.zero, Color = White,
                Cost = l => 0, Desc = l => "Space를 누르면 선반에 깡통이 나온다. 모든 것의 시작.", Apply = (g, l) => { } },

            // 저글링 (하늘) — 위
            N("airslow1", "가벼운 낙하", "낙", Vector2.zero, Cyan, 180, "공중 깡통이 10% 느리게 떨어진다", g => Can.AirFall *= 0.81f),
            N("burst1", "터뜨림 배율", "배", Vector2.zero, Cyan, 220, "한 세트에 둘째부터 터뜨릴 때마다 배율 +0.2 → +0.3", g => g.BurstMultStep += 0.1f),
            C("air2x", "둘이면 두 배", "둘", Cyan, 350, "공중에 깡통이 둘 이상일 때 떨어지거나 터진 돈 ×2", g => g.JuggleScoreMult = Mathf.Max(g.JuggleScoreMult, 2f)),
            N("airslow2", "더 가벼운 낙하", "낙", Vector2.zero, Cyan, 350, "공중 깡통이 10% 더 느리게 떨어진다", g => Can.AirFall *= 0.81f),
            C("lowbounce", "낮은 튐", "통", Cyan, 450, "떨어진 깡통이 한 번 낮게 튀어오른다 (연속 유지)", g => { if (g.LandBounces == 0) { g.LandBounces = 1; g.LandBouncePower = 6.5f; } }),   // "다시 튐"을 이미 샀으면 그대로 (그쪽이 높다)
            N("bounce", "가벼운 깡통", "경", Vector2.zero, Cyan, 600, "맞으면 더 높이 튄다", g => Can.BounceBonus += 1.0f),
            C("rebound", "다시 튐", "튐", Cyan, 1200, "떨어진 깡통이 높게 다시 튀어오른다 (낮은 튐 → 높은 튐)", g => { g.LandBounces = 1; g.LandBouncePower = 11f; }),
            N("burst2", "터뜨림 배율 2", "배", Vector2.zero, Cyan, 400, "터뜨림 배율 +0.3 → +0.4", g => g.BurstMultStep += 0.1f),
            C("juggle", "공중 곡예", "곡", Cyan, 800, "공중에 많을수록 천천히 떨어진다 (2개 -30%, 3개 -45%, 4개+ -55%)", g => Can.JuggleFall = new[] { 1f, 0.7f, 0.55f, 0.45f }),
            N("burst3", "터뜨림 배율 3", "배", Vector2.zero, Cyan, 1000, "터뜨림 배율 +0.4 → +0.5", g => g.BurstMultStep += 0.1f),

            // 장전 (빨강) — 왼쪽. 탄창 수는 총의 성격이라 트리에 없다 (사용자 10-10)
            // 손 풀기 (10-15): 간격과 장전을 번갈아 줄인다. 녹슨 리볼버 0.6초/3초 → 둘 다 사면 0.43초/1.9초
            N("quick1", "빠른 손", "손", Vector2.zero, Red, 25, "쏘는 간격 -15%", g => g.Gun.FireIntervalScale *= 0.85f),
            N("reload1", "빠른 장전", "장", Vector2.zero, Red, 40, "장전 시간 -20%", g => g.Gun.ReloadScale *= 0.8f),
            N("quick2", "더 빠른 손", "손", Vector2.zero, Red, 60, "쏘는 간격 -15% 더", g => g.Gun.FireIntervalScale *= 0.85f),
            N("quick3", "번개 손", "손", Vector2.zero, Red, 260, "쏘는 간격 -20% 더", g => g.Gun.FireIntervalScale *= 0.8f),
            N("just1", "저스트 배율", "저", Vector2.zero, Red, 70, "저스트 장전 연속마다 +0.1 → +0.15", g => g.JustPerStreak += 0.05f),
            C("firstshot", "첫 발", "첫", Red, 220, "장전 직후 첫 발은 값 2배 (어느 총이든)", g => g.Gun.FirstShotMult = 2f),
            N("reload2", "더 빠른 장전", "장", Vector2.zero, Red, 90, "장전 시간 -20% 더", g => g.Gun.ReloadScale *= 0.8f),
            C("lastshot", "마지막 발", "끝", Red, 600, "탄창의 마지막 한 발은 더 높이 띄운다", g => g.Gun.LastShotBounce = 1.5f),
            N("reload3", "번개 장전", "번", Vector2.zero, Red, 900, "장전 시간 -25% 더", g => g.Gun.ReloadScale *= 0.75f),
            N("just2", "저스트 배율 2", "저", Vector2.zero, Red, 330, "저스트 연속마다 +0.15 → +0.2, 최대 ×1.5 → ×2", g => { g.JustPerStreak += 0.05f; g.JustMax = 2f; }),
            C("comboreload", "연속 장전", "연", Red, 700, "공중 연속 5번마다 즉시 장전", g => g.Gun.ComboReloadEvery = 5),
            C("holster", "허리춤 장전", "허", Red, 1200, "꺼내지 않은 보조무기가 1.5초마다 한 발씩 찬다", g => g.HolsterReloadSeconds = 1.5f),

            // 선반 (하늘) — 오른쪽
            N("perfect1", "퍼펙트 배율", "퍼", Vector2.zero, Cyan, 90, "한 발도 안 놓친 세트 ×1.1 → ×1.2", g => g.PerfectMult += 0.1f),
            C("shelf1", "넓은 선반", "선", Cyan, 150, "선반 자리 +1 (1칸 → 2칸). 이제 둘을 띄울 수 있다", g => g.MaxCans += 1),
            C("cluster", "붙여 놓기", "붙", Cyan, 450, "소환하면 마지막 깡통 바로 옆에 붙어 나온다", g => g.ClusterSpawn = true),
            C("shelf2", "더 넓은 선반", "선", Cyan, 500, "선반 자리 +1 더 (→ 3칸)", g => g.MaxCans += 1),
            C("shelf3", "선반 확장", "선", Cyan, 1500, "선반 자리 +1 더 (→ 4칸)", g => g.MaxCans += 1),
            C("popchain", "파편 튀기기", "파", Cyan, 600, "깡통이 터지면 선반의 깡통이 전부 파편에 튀어 오른다 (연쇄 저글링)", g => g.ShrapnelPop = true),

            // 돈 (노랑) — 아래
            N("value1", "비싼 깡통", "값", Vector2.zero, Yellow, 50, "깡통 값 +20%", g => g.ValueMult += 0.2f),
            N("payout1", "두둑한 정산", "정", Vector2.zero, Yellow, 120, "떨어질 때 정산 +25%", g => g.PayoutMult += 0.25f),
            N("value2", "더 비싼 깡통", "값", Vector2.zero, Yellow, 210, "깡통 값 +30% 더", g => g.ValueMult += 0.3f),
            N("perfect2", "퍼펙트 배율 2", "퍼", Vector2.zero, Yellow, 540, "퍼펙트 ×1.2 → ×1.3", g => g.PerfectMult += 0.1f),
            N("value3", "아주 비싼 깡통", "값", Vector2.zero, Yellow, 900, "깡통 값 +50% 더", g => g.ValueMult += 0.5f),

            // 깡통 종류 (주황) — 10-15 사용자: 첫 현상금 뒤 새 깡통을 여는 타이밍. 그전엔 잠긴 미리보기
            Gate(C("can_bottle", "병", "병", Orange, 200, "깡통 소환에 병이 섞인다 (15%). 작고 무겁고, 값 3배", g => g.BottleChance += 0.15f)),
            Gate(N("can_bottle2", "병 더", "병", Vector2.zero, Orange, 300, "병이 더 자주 (+10%)", g => g.BottleChance += 0.1f)),
            Gate(C("can_bundle", "묶음 깡통", "묶", Orange, 500, "깡통 셋 묶음이 섞인다 (8%). 터지면 셋으로 풀린다", g => g.BundleChance = 0.08f)),
            Gate(C("can_blue", "파란 깡통", "파", Orange, 700, "파란 깡통이 섞인다 (8%). 터지면 반투명 깡통이 날아오른다", g => g.BlueChance = 0.08f)),
            Gate(C("can_magnet", "자석 깡통", "자", Orange, 900, "자석 깡통이 섞인다 (8%). 맞을 때마다 옆 깡통을 끌어당긴다", g => g.MagnetChance = 0.08f)),

            // 다이너마이트 (10-14 사용자: 공격 수단. 기본 0% → 이 스킬로 쏠 때 1% 투척)
            C("dynamite", "다이너마이트", "폭", Red, 360, "쏠 때 1% 확률로 화면 아래에서 다이너마이트가 날아온다. 3발 맞히면 크게 터진다", g => Can.DynamiteChance = 0.01f),

            // 자동 사격 (초록) — 돈 아래. 총 종류를 뚫을수록 화면 아래에 총이 늘어난다 (값 절반)
            C("auto1", "자동 리볼버", "자", Green, 450, "화면 아래에 리볼버가 서서 쏜다. 빠르고 넓게 쫓는다 (값 절반)", g => g.AddAutoGun(GunKind.Revolver)),
            N("auto2", "빠른 사격기", "속", Vector2.zero, Green, 750, "자동 총들의 간격 -40%", g => g.AutoRate = 0.6f),
            C("auto_lever", "자동 레버액션", "레", Green, 1200, "아래에 레버액션 소총이 합류. 중간 속도로 꾸준히", g => g.AddAutoGun(GunKind.LeverAction)),
            C("auto_shotgun", "자동 샷건", "샷", Green, 1800, "아래에 샷건이 합류. 느리게 쫓지만 원 안을 전부 띄운다", g => g.AddAutoGun(GunKind.Shotgun)),

            // 숨 (보라) — 우클릭 슬로우
            N("slow1", "긴 숨", "숨", Vector2.zero, Purple, 180, "슬로우 3초 → 4.5초 (꽉 채워야 쓸 수 있다)", g => g.SlowGaugeMax += 0.5f),
            N("slow3", "빠른 숨", "빠", Vector2.zero, Purple, 270, "맞출 때 게이지 +0.1 → +0.2", g => g.SlowFillPerHit = 0.2f),
            N("slow2", "깊은 숨", "깊", Vector2.zero, Purple, 420, "슬로우 중 깡통이 더 느리게 (×0.3 → ×0.18)", g => g.SlowStrength = 0.18f),
            C("floor", "바닥 바람", "바", Purple, 750, "바닥 가까이 온 깡통은 낙하 속도 -40%", g => g.FloorSlow = 0.4f),
        };

        /// <summary>이웃 관계를 양방향으로 맞춘다</summary>
        public static Dictionary<string, HashSet<string>> Neighbors(List<SkillNode> nodes)
        {
            var map = new Dictionary<string, HashSet<string>>();
            foreach (var n in nodes) map[n.Id] = new HashSet<string>();
            foreach (var n in nodes) foreach (var l in n.Links) { map[n.Id].Add(l); if (map.ContainsKey(l)) map[l].Add(n.Id); }
            return map;
        }
    }
}
