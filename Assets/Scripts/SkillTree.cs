using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinCan
{
    /// <summary>스킬 노드 하나. 자리(트리 좌표), 이웃, 단계, 값, 효과. 데이터로만 늘린다.</summary>
    public class SkillNode
    {
        public string Id, Name, Glyph;
        public Func<int, string> Desc;          // 현재 레벨 → 설명 (다음 단계 효과까지)
        public Vector2 Pos;                     // 트리 좌표 (1 = 노드 간격 하나)
        public string[] Links = new string[0];  // 이웃 (양방향)
        public int Level, MaxLevel;
        public Func<int, long> Cost;
        public Action<Game, int> Apply;
        public Color Color;                     // 계열 색
        public bool Root;
        public bool Maxed => Level >= MaxLevel;
        public long NextCost => Cost(Level);
    }

    /// <summary>
    /// 2주 안에는 여기 적힌 것만. 계열 넷: 장전(빨강), 깡통(하늘), 돈(노랑), 자동(초록).
    /// 열림 규칙: 이웃 중 하나라도 1단계 이상이면 살 수 있다.
    /// </summary>
    public static class SkillTree
    {
        public static readonly Color Red = new Color(1f, 0.25f, 0.35f);
        public static readonly Color Cyan = new Color(0.3f, 0.9f, 1f);
        public static readonly Color Yellow = new Color(1f, 0.85f, 0.3f);
        public static readonly Color Green = new Color(0.4f, 1f, 0.5f);
        public static readonly Color White = new Color(0.9f, 0.9f, 0.95f);

        public static List<SkillNode> Make() => new List<SkillNode>
        {
            new SkillNode { Id = "root", Name = "리볼버", Glyph = "총", Root = true, Level = 1, MaxLevel = 1, Pos = Vector2.zero, Color = White,
                Cost = l => 0, Desc = l => "모든 것의 시작. 6발.", Apply = (g, l) => { },
                Links = new[] { "reload1", "shelf1", "value1", "auto1" } },

            // 장전 (빨강) — 왼쪽 위
            new SkillNode { Id = "reload1", Name = "빠른 장전", Glyph = "장", Pos = new Vector2(-1.4f, 1f), Color = Red, MaxLevel = 5,
                Cost = l => 15 + l * l * 20, Desc = l => $"장전 시간 {0.9f - l * 0.13f:0.00}초 → {0.9f - (l + 1) * 0.13f:0.00}초",
                Apply = (g, l) => g.Gun.ReloadSeconds = 0.9f - l * 0.13f, Links = new[] { "reload2", "dual" } },
            new SkillNode { Id = "reload2", Name = "연속 장전", Glyph = "연", Pos = new Vector2(-2.8f, 1.6f), Color = Red, MaxLevel = 3,
                Cost = l => 80 + l * 120, Desc = l => $"공중 연속 {5 - l}번마다 즉시 장전 → {5 - (l + 1)}번마다",
                Apply = (g, l) => g.Gun.ComboReloadEvery = 5 - l, Links = new string[0] },
            new SkillNode { Id = "dual", Name = "쌍권총", Glyph = "쌍", Pos = new Vector2(-2.2f, 2.8f), Color = Red, MaxLevel = 1,
                Cost = l => 300, Desc = l => "총이 두 자루. 12발.",
                Apply = (g, l) => g.Gun.Capacity = 12, Links = new string[0] },

            // 깡통 (하늘) — 오른쪽 위
            new SkillNode { Id = "shelf1", Name = "넓은 선반", Glyph = "선", Pos = new Vector2(1.4f, 1f), Color = Cyan, MaxLevel = 3,
                Cost = l => 30 + l * 70, Desc = l => $"선반 자리 {3 + l}개 → {3 + l + 1}개",
                Apply = (g, l) => g.MaxCans = 3 + l, Links = new[] { "bounce", "bottle" } },
            new SkillNode { Id = "bounce", Name = "가벼운 깡통", Glyph = "경", Pos = new Vector2(2.8f, 1.6f), Color = Cyan, MaxLevel = 4,
                Cost = l => 25 + l * l * 30, Desc = l => $"맞으면 더 높이 튄다 (+{(l + 1) * 0.9f:0.0})",
                Apply = (g, l) => Can.BounceBonus = l * 0.9f, Links = new string[0] },
            new SkillNode { Id = "bottle", Name = "병 납품", Glyph = "병", Pos = new Vector2(2.2f, 2.8f), Color = Cyan, MaxLevel = 1,
                Cost = l => 60, Desc = l => "소환이 병으로 바뀐다. $8, 값 3배, 작고 가볍다.",
                Apply = (g, l) => g.SpawnKind = CanKind.Bottle, Links = new[] { "hat" } },
            new SkillNode { Id = "hat", Name = "모자 납품", Glyph = "모", Pos = new Vector2(2.6f, 4.0f), Color = Cyan, MaxLevel = 1,
                Cost = l => 400, Desc = l => "소환이 모자로 바뀐다. $30, 값 10배, 더 작고 더 가볍다.",
                Apply = (g, l) => g.SpawnKind = CanKind.Hat, Links = new string[0] },

            // 돈 (노랑) — 아래
            new SkillNode { Id = "value1", Name = "비싼 깡통", Glyph = "값", Pos = new Vector2(0f, -1.5f), Color = Yellow, MaxLevel = 5,
                Cost = l => 20 + l * l * 25, Desc = l => $"맞출 때 값 +{l * 20}% → +{(l + 1) * 20}%",
                Apply = (g, l) => g.ValueMult = 1f + l * 0.2f, Links = new[] { "combo1", "payout1" } },
            new SkillNode { Id = "combo1", Name = "연속 배수", Glyph = "배", Pos = new Vector2(-1.4f, -2.6f), Color = Yellow, MaxLevel = 3,
                Cost = l => 100 + l * 150, Desc = l => $"연속마다 ×{1f + l * 0.25f:0.00} → ×{1f + (l + 1) * 0.25f:0.00}",
                Apply = (g, l) => g.ComboMult = 1f + l * 0.25f, Links = new string[0] },
            new SkillNode { Id = "payout1", Name = "두둑한 정산", Glyph = "정", Pos = new Vector2(1.4f, -2.6f), Color = Yellow, MaxLevel = 3,
                Cost = l => 120 + l * 200, Desc = l => $"떨어질 때 정산 +{l * 15}% → +{(l + 1) * 15}%",
                Apply = (g, l) => g.PayoutMult = 1f + l * 0.15f, Links = new string[0] },

            // 자동 (초록) — 왼쪽 아래
            new SkillNode { Id = "auto1", Name = "자동 사격기", Glyph = "자", Pos = new Vector2(-1.6f, -0.4f), Color = Green, MaxLevel = 1,
                Cost = l => 200, Desc = l => "선반 옆 기계가 공중의 깡통을 2초마다 한 번 맞춘다.",
                Apply = (g, l) => g.AutoGunSeconds = 2f, Links = new[] { "auto2" } },
            new SkillNode { Id = "auto2", Name = "빠른 사격기", Glyph = "속", Pos = new Vector2(-3.0f, -0.9f), Color = Green, MaxLevel = 3,
                Cost = l => 250 + l * 300, Desc = l => $"사격기 간격 {2f - l * 0.4f:0.0}초 → {2f - (l + 1) * 0.4f:0.0}초",
                Apply = (g, l) => g.AutoGunSeconds = 2f - l * 0.4f, Links = new string[0] },
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
