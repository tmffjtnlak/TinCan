using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinCan
{
    /// <summary>
    /// 저주 = 득과 실이 붙은 물건 (사용자 10-10). 사면 켜지고, 다시 누르면 끈다(값은 안 돌려준다).
    /// 2주 안엔 하나. 구조만 증명한다.
    /// </summary>
    public class Curse
    {
        public string Id, Name, Gain, Loss;
        public long Price;
        public bool Owned, Active;
        public Action<Game, bool> Apply;   // 켜고 끌 때

        public static readonly Curse Greed = new Curse
        {
            Id = "greed", Name = "탐욕의 부적", Price = 120,
            Gain = "모든 수입 ×1.5",
            Loss = "소환할 때 다섯에 하나꼴로 저주 깡통이 끼어 나온다. 떨어지면 돈 절반. 터뜨리면 무사. 가만두면 혼자 뛰어내린다.",
            Apply = (g, on) => { g.IncomeMult = on ? 1.5f : 1f; g.CursedChance = on ? 0.2f : 0f; },
        };

        public static readonly List<Curse> All = new List<Curse> { Greed };
    }
}
