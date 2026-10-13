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
            SkillNode Put(string id, Color c, int x, int y, params (int, int)[] parents)
            {
                SkillNode n;
                if (id.StartsWith("key:")) { var p = id.Split(':'); n = new SkillNode { Id = p[1], Name = p[2], Glyph = "★", Key = true, Color = c, Cost = l => long.Parse(p[3]), Desc = l => p[4], Apply = (g, l) => { } }; }
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

            // 배치: 일직선이 아니라 뿌리 둘레에 **뭉쳐서** (사용자 10-12). 사슬은 꺾이고, 갈래는 덩어리로. 전체 -4..4 × -5..2 안
            Put("root", White, 0, 0);
            // 1단 — 장전 (왼쪽): 왼쪽으로 둘, 위로 꺾어 관문
            Put("reload1", Red, -1, 0, (0, 0));
            Put("reload2", Red, -2, 0, (-1, 0));
            Put("reload3", Red, -2, 1, (-2, 0));
            Put("key:gate_reload:사격술:150:관문. 꾹 눌러 해방하면 장전 응용이 열린다.", Red, -3, 1, (-2, 1));
            // 2단 — 장전 응용: 관문 둘레에 덩어리
            Put("firstshot", Red, -4, 2, (-3, 1));
            Put("lastshot", Red, -4, 1, (-4, 2));
            Put("comboreload", Red, -4, 0, (-3, 1));
            Put("holster", Red, -3, -1, (-4, 0));
            // 1단 — 선반 (오른쪽): 거울
            Put("shelf1", Cyan, 1, 0, (0, 0));
            Put("shelf2", Cyan, 2, 0, (1, 0));
            Put("cheap", Cyan, 2, 1, (2, 0));
            Put("key:gate_shelf:곡예:150:관문. 꾹 눌러 해방하면 선반 응용이 열린다.", Cyan, 3, 1, (2, 1));
            // 2단 — 선반 응용
            Put("bounce", Cyan, 4, 2, (3, 1));
            Put("rebound", Cyan, 4, 1, (4, 2));
            Put("cluster", Cyan, 4, 0, (3, 1));
            Put("pair", Cyan, 3, -1, (4, 0));
            Put("juggle", Cyan, 3, 2, (3, 1));   // 10-14: 저글링 낙하 완화는 기본이 아니라 성장 보상
            // 1단 — 돈 (아래): 내려가다 오른쪽으로 꺾어 관문
            Put("value1", Yellow, 0, -1, (0, 0));
            Put("combo1", Yellow, 0, -2, (0, -1));
            Put("value2", Yellow, 1, -2, (0, -2));
            Put("key:gate_money:장사:300:관문. 꾹 눌러 해방하면 새 종류(자동·슬로우·정산)가 열린다.", Yellow, 1, -3, (1, -2));
            // 2단 — 돈 관문 둘레에 새 종류가 붙는다
            Put("payout1", Yellow, 2, -3, (1, -3));
            Put("auto1", Green, 0, -3, (1, -3));
            Put("auto2", Green, -1, -4, (0, -3));
            Put("auto_lever", Green, 0, -4, (0, -3));
            Put("auto_shotgun", Green, -2, -5, (-1, -4));
            Put("slow1", Purple, 2, -4, (1, -3));
            Put("slow2", Purple, 3, -4, (2, -4));
            Put("slow3", Purple, 3, -5, (3, -4));
            Put("floor", Purple, 2, -5, (3, -5));
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

        static List<SkillNode> Raw() => new List<SkillNode>
        {
            // 시작 노드 = 핵심 노드. 처음엔 잠겨 있고, 꾹 눌러 해방하면 주변이 총에 맞아 열린다 (사용자 10-12)
            new SkillNode { Id = "root", Name = "사수", Glyph = "총", Root = true, Key = true, Level = 0, Pos = Vector2.zero, Color = White,
                Cost = l => 0, Desc = l => "모든 것의 시작. 꾹 눌러 해방한다.", Apply = (g, l) => { },
                Links = new[] { "reload1", "shelf1", "value1", "auto1", "slow1" } },

            // 장전 (빨강) — 왼쪽 위
            // 탄창 수는 총의 성격이라 트리에 없다 (사용자 10-10). 트리는 "그 탄창을 어떻게 쓰나"만
            N("reload1", "빠른 장전", "장", new Vector2(-1.4f, 1f), Red, 30, "장전 시간 -20%", g => g.Gun.ReloadScale *= 0.8f, "reload2", "firstshot", "comboreload"),
            N("reload2", "더 빠른 장전", "장", new Vector2(-2.8f, 1.5f), Red, 120, "장전 시간 -20% 더", g => g.Gun.ReloadScale *= 0.8f, "reload3"),
            N("reload3", "번개 장전", "번", new Vector2(-4.1f, 1.9f), Red, 400, "장전 시간 -25% 더", g => g.Gun.ReloadScale *= 0.75f),
            N("firstshot", "첫 발", "첫", new Vector2(-2.2f, 2.7f), Red, 150, "장전 직후 첫 발은 값 2배 (어느 총이든)", g => g.Gun.FirstShotMult = 2f, "lastshot"),
            N("lastshot", "마지막 발", "끝", new Vector2(-3.2f, 3.6f), Red, 220, "탄창의 마지막 한 발은 더 높이 띄운다", g => g.Gun.LastShotBounce = 1.5f),
            N("comboreload", "연속 장전", "연", new Vector2(-0.9f, 2.3f), Red, 200, "공중 연속 5번마다 즉시 장전", g => g.Gun.ComboReloadEvery = 5, "holster"),
            N("holster", "허리춤 장전", "허", new Vector2(-1.4f, 3.5f), Red, 260, "꺼내지 않은 보조무기가 1.5초마다 한 발씩 찬다", g => g.HolsterReloadSeconds = 1.5f),

            // 선반 (하늘) — 오른쪽 위
            N("shelf1", "넓은 선반", "선", new Vector2(1.4f, 1f), Cyan, 40, "선반 자리 +1", g => g.MaxCans += 1, "shelf2", "bounce", "cluster"),
            N("shelf2", "더 넓은 선반", "선", new Vector2(1.9f, 2.1f), Cyan, 160, "선반 자리 +1 더", g => g.MaxCans += 1, "cheap"),
            N("cheap", "단골 할인", "할", new Vector2(2.6f, 3.1f), Cyan, 220, "소환 비용 -30%", g => g.SpawnDiscount = 0.3f),
            N("bounce", "가벼운 깡통", "경", new Vector2(2.8f, 1.3f), Cyan, 60, "맞으면 더 높이 튄다", g => Can.BounceBonus += 1.0f, "rebound"),
            N("rebound", "다시 튐", "튐", new Vector2(4.1f, 1.7f), Cyan, 300, "떨어진 깡통이 혼자 한 번 다시 튀어오른다 (연속 유지)", g => g.LandBounces = 1),
            N("cluster", "붙여 놓기", "붙", new Vector2(0.5f, 2.2f), Cyan, 50, "소환하면 마지막 깡통 바로 옆에 붙어 나온다", g => g.ClusterSpawn = true, "pair"),
            // 10-14 사용자: 저글링 중 낙하 완화를 기본에서 빼 스킬로 (초반은 아슬아슬, 사면 쉬워지는 성장)
            N("juggle", "공중 곡예", "곡", new Vector2(3.4f, 2.4f), Cyan, 180, "공중에 둘 이상이면 천천히 떨어진다 (2개 -30%, 3개 -45%, 4개+ -55%)", g => Can.JuggleFall = new[] { 1f, 0.7f, 0.55f, 0.45f }),
            N("pair", "짝 소환", "짝", new Vector2(0.0f, 3.3f), Cyan, 250, "Space 한 번에 2개 (비용도 2개)", g => g.SpawnCount = 2),

            // 돈 (노랑) — 아래
            N("value1", "비싼 깡통", "값", new Vector2(0f, -1.5f), Yellow, 40, "맞출 때 값 +30%", g => g.ValueMult += 0.3f, "value2", "combo1", "payout1"),
            N("value2", "더 비싼 깡통", "값", new Vector2(0f, -2.8f), Yellow, 200, "맞출 때 값 +40% 더", g => g.ValueMult += 0.4f),
            N("combo1", "연속 배수", "배", new Vector2(-1.5f, -2.5f), Yellow, 180, "연속마다 ×1.35", g => g.ComboMult = 1.35f),
            N("payout1", "두둑한 정산", "정", new Vector2(1.2f, -2.9f), Yellow, 220, "떨어질 때 정산 +25%", g => g.PayoutMult += 0.25f),

            // 자동 (초록) — 왼쪽 아래
            // 자동 사격 (10-12 2판): 총 종류를 뚫을수록 화면 아래에 총이 늘어난다. 값은 절반
            N("auto1", "자동 리볼버", "자", new Vector2(-1.6f, -0.5f), Green, 250, "화면 아래에 리볼버가 서서 쏜다. 빠르고 넓게 쫓는다 (값 절반)", g => g.AddAutoGun(GunKind.Revolver), "auto2"),
            N("auto2", "빠른 사격기", "속", new Vector2(-3.0f, -1.0f), Green, 500, "자동 총들의 간격 -40%", g => g.AutoRate = 0.6f),
            N("auto_lever", "자동 레버액션", "레", new Vector2(-3.0f, -1.0f), Green, 400, "아래에 레버액션 소총이 합류. 중간 속도로 꾸준히", g => g.AddAutoGun(GunKind.LeverAction)),
            N("auto_shotgun", "자동 샷건", "샷", new Vector2(-3.0f, -1.0f), Green, 600, "아래에 샷건이 합류. 느리게 쫓지만 원 안을 전부 띄운다", g => g.AddAutoGun(GunKind.Shotgun)),

            // 시간 (보라) — 오른쪽 아래. 우클릭 슬로우
            N("slow1", "긴 숨", "숨", new Vector2(1.7f, -0.5f), Purple, 60, "슬로우 3초 → 4.5초 (꽉 채워야 쓸 수 있다)", g => g.SlowGaugeMax += 0.5f, "slow2", "slow3", "floor"),
            N("slow2", "깊은 숨", "깊", new Vector2(3.1f, -0.9f), Purple, 200, "슬로우 중 깡통이 더 느리게 (×0.3 → ×0.18)", g => g.SlowStrength = 0.18f),
            N("slow3", "빠른 숨", "빠", new Vector2(2.6f, -1.9f), Purple, 180, "맞출 때 게이지 +0.1 → +0.2", g => g.SlowFillPerHit = 0.2f),
            N("floor", "바닥 바람", "바", new Vector2(3.3f, 0.3f), Purple, 220, "바닥 가까이 온 깡통은 낙하 속도 -40%", g => g.FloorSlow = 0.4f),
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
