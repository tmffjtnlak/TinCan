using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinCan
{
    /// <summary>업그레이드 하나: 이름, 단계별 값, 적용 함수. 데이터로만 늘린다.</summary>
    public class Upgrade
    {
        public string Id, Name, Desc;
        public int Level;
        public int MaxLevel;
        public Func<int, long> Cost;        // 다음 단계 값 (현재 레벨 기준)
        public Action<Game, int> Apply;     // 레벨이 바뀔 때 게임에 반영
        public bool Maxed => Level >= MaxLevel;
        public long NextCost => Cost(Level);
    }

    /// <summary>2주 안에는 여기 적힌 것만. 11번째는 없다.</summary>
    public static class Upgrades
    {
        public static List<Upgrade> Make() => new List<Upgrade>
        {
            new Upgrade
            {
                Id = "reload", Name = "빠른 장전", Desc = "실린더를 더 빨리 돌린다",
                MaxLevel = 5, Cost = l => 10 + l * l * 15,
                Apply = (g, l) => g.Gun.ReloadSeconds = 0.9f - l * 0.13f,
            },
            new Upgrade
            {
                Id = "cans", Name = "깡통 추가", Desc = "울타리에 깡통이 하나 더",
                MaxLevel = 3, Cost = l => 25 + l * 60,
                Apply = (g, l) => g.SetCanCount(3 + l),
            },
            new Upgrade
            {
                Id = "bounce", Name = "가벼운 깡통", Desc = "맞으면 더 높이 튄다",
                MaxLevel = 4, Cost = l => 20 + l * l * 25,
                Apply = (g, l) => Can.BounceBonus = l * 0.9f,
            },
        };
    }
}
