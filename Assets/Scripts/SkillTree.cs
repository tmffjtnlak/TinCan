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

        public static List<SkillNode> Make() => new List<SkillNode>
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
            N("pair", "짝 소환", "짝", new Vector2(0.0f, 3.3f), Cyan, 250, "Space 한 번에 2개 (비용도 2개)", g => g.SpawnCount = 2),

            // 돈 (노랑) — 아래
            N("value1", "비싼 깡통", "값", new Vector2(0f, -1.5f), Yellow, 40, "맞출 때 값 +30%", g => g.ValueMult += 0.3f, "value2", "combo1", "payout1"),
            N("value2", "더 비싼 깡통", "값", new Vector2(0f, -2.8f), Yellow, 200, "맞출 때 값 +40% 더", g => g.ValueMult += 0.4f),
            N("combo1", "연속 배수", "배", new Vector2(-1.5f, -2.5f), Yellow, 180, "연속마다 ×1.35", g => g.ComboMult = 1.35f),
            N("payout1", "두둑한 정산", "정", new Vector2(1.2f, -2.9f), Yellow, 220, "떨어질 때 정산 +25%", g => g.PayoutMult += 0.25f),

            // 자동 (초록) — 왼쪽 아래
            N("auto1", "자동 사격기", "자", new Vector2(-1.6f, -0.5f), Green, 250, "선반 옆 기계가 공중의 깡통을 2초마다 한 번 맞춘다 (값 절반)", g => g.AutoGunSeconds = 2f, "auto2"),
            N("auto2", "빠른 사격기", "속", new Vector2(-3.0f, -1.0f), Green, 500, "사격기 간격 1초", g => g.AutoGunSeconds = 1f),

            // 시간 (보라) — 오른쪽 아래. 우클릭 슬로우
            N("slow1", "긴 숨", "숨", new Vector2(1.7f, -0.5f), Purple, 60, "슬로우 게이지 2초 → 3초", g => g.SlowGaugeMax += 0.5f, "slow2", "slow3", "floor"),
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
