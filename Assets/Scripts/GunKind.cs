using System.Collections.Generic;
using UnityEngine;

namespace TinCan
{
    /// <summary>
    /// 총 종류 = 쏘는 방식. 스킬트리(얼마나 잘)와 다르다.
    /// 한 발의 판정이 다르다: 리볼버는 점 하나, 샷건은 퍼진 원 안 전부, 머신건은 연사.
    /// </summary>
    public class GunKind
    {
        public string Id, Name, Desc;
        public int Capacity;           // 한 번에 장전되는 수
        public float ReloadSeconds;
        public float AimRadius;        // 판정 원 (샷건은 크다)
        public bool HitAll;            // 원 안의 깡통 전부 맞추나 (샷건)
        public float ValueScale;       // 한 발 값 배수 (머신건은 낮게)
        public float FireInterval;     // 연사 간격 (0 = 클릭마다 한 발)
        public bool Auto;              // 누르고 있으면 계속 나가나
        public float Kick;             // 화면 반동
        public float Pitch;            // 총소리 높이
        public long Price;             // 구매 값 (0 = 처음부터)
        public float BounceScale = 1f; // 맞았을 때 튀는 힘 배수

        public static readonly GunKind Revolver = new GunKind { Id = "revolver", Name = "리볼버", Desc = "6발. 정확하다. 모든 것의 시작.", Capacity = 6, ReloadSeconds = 0.9f, AimRadius = 0.22f, HitAll = false, ValueScale = 1f, FireInterval = 0f, Auto = false, Kick = 1f, Pitch = 1f, Price = 0 };
        public static readonly GunKind Shotgun = new GunKind { Id = "shotgun", Name = "샷건", Desc = "2발. 퍼진다. 원 안의 깡통을 전부 띄운다. 값은 70%.", Capacity = 2, ReloadSeconds = 1.3f, AimRadius = 0.9f, HitAll = true, ValueScale = 0.7f, FireInterval = 0f, Auto = false, Kick = 1.8f, Pitch = 0.7f, Price = 150, BounceScale = 1.15f };
        public static readonly GunKind Magnum = new GunKind { Id = "magnum", Name = "매그넘", Desc = "4발. 세게 튄다(더 오래 공중에). 값 1.5배.", Capacity = 4, ReloadSeconds = 1.1f, AimRadius = 0.22f, HitAll = false, ValueScale = 1.5f, FireInterval = 0f, Auto = false, Kick = 1.6f, Pitch = 0.8f, Price = 400, BounceScale = 1.4f };
        public static readonly GunKind MachineGun = new GunKind { Id = "mg", Name = "머신건", Desc = "30발. 누르고 있으면 연사. 값은 35%.", Capacity = 30, ReloadSeconds = 2.0f, AimRadius = 0.2f, HitAll = false, ValueScale = 0.35f, FireInterval = 0.09f, Auto = true, Kick = 0.4f, Pitch = 1.3f, Price = 900, BounceScale = 0.75f };

        /// <summary>2주 안엔 리볼버 + 샷건만 산다. 나머지는 데이터만 있고 가게에 안 나온다</summary>
        public static readonly List<GunKind> ForSale = new List<GunKind> { Revolver, Shotgun };
    }
}
