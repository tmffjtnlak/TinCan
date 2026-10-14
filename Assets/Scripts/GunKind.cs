using System.Collections.Generic;
using UnityEngine;

namespace TinCan
{
    /// <summary>
    /// 총 종류 = 쏘는 방식. 스킬트리(얼마나 잘)와 다르다.
    /// 한 발의 판정이 다르다: 리볼버는 점 하나, 샷건은 퍼진 원 안 전부, 머신건은 연사.
    /// </summary>
    /// <summary>무기 슬롯 종류 (FPS 도구 느낌, 사용자 10-11): 1 주무장, 2 보조무기, 3 투척류(소모품). 근접무기 등은 나중에</summary>
    public enum WeaponSlot { Primary = 0, Secondary = 1, Throwable = 2 }

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
        public int Price;              // 구매 값 = 현상금 증표 개수 (10-14, 0 = 처음부터)
        public float BounceScale = 1f; // 맞았을 때 튀는 힘 배수
        /// <summary>저주 총 (사용자 10-10): 맞춘 깡통이 이 확률로 저주통이 된다. 저주통은 떨어지면 쌓인 돈을 뺏고, 터뜨리면 평소대로</summary>
        public float CurseChance = 0f;
        public bool IsCursed => CurseChance > 0f;
        /// <summary>쌍권총: 두 자루가 번갈아 쏜다 (화면에 둘 다 보인다)</summary>
        public bool Dual = false;

        /// <summary>가족 (사용자 10-11, 서부 네 가족): 리볼버 · 레버액션 소총 · 단발 라이플 · 샷건. 변종(쌍·저주)은 가족 안에</summary>
        public enum Family { Revolver, Lever, Single, Shotgun }
        public Family Kin = Family.Revolver;
        /// <summary>발사 뒤 손잡이 동작(레버 철컥)이 보이나</summary>
        public bool LeverCycle = false;

        // ---- 리볼버 가족: 빠르게 6발, 멈춤 ----
        // 10-15 사용자: 처음 총이 너무 좋다 → 시작은 녹슨 리볼버 (쏘는 간격·장전이 느리다). 리볼버는 첫 증표로 사는 첫 업그레이드
        public static readonly GunKind Rusty = new GunKind { Id = "rusty", Kin = Family.Revolver, Name = "녹슨 리볼버", Desc = "6발. 녹이 슬어 한 발 쏘고 나면 뻑뻑하고, 장전도 느리다.", Capacity = 6, ReloadSeconds = 1.6f, AimRadius = 0.22f, HitAll = false, ValueScale = 1f, FireInterval = 0.42f, Auto = false, Kick = 1.1f, Pitch = 0.9f, Price = 0 };
        public static readonly GunKind Revolver = new GunKind { Id = "revolver", Kin = Family.Revolver, Name = "리볼버", Desc = "6발. 손질이 잘 된 리볼버. 빠르고 정확하다.", Capacity = 6, ReloadSeconds = 0.9f, AimRadius = 0.22f, HitAll = false, ValueScale = 1f, FireInterval = 0f, Auto = false, Kick = 1f, Pitch = 1f, Price = 1 };
        public static readonly GunKind DualRevolver = new GunKind { Id = "dual", Kin = Family.Revolver, Name = "쌍권총", Desc = "리볼버 두 자루. 12발, 번갈아 쏜다. 장전은 둘 다.", Capacity = 12, ReloadSeconds = 1.4f, AimRadius = 0.22f, HitAll = false, ValueScale = 1f, FireInterval = 0.06f, Auto = false, Kick = 0.9f, Pitch = 1.05f, Price = 1, Dual = true };
        public static readonly GunKind CursedRevolver = new GunKind { Id = "cursed_revolver", Kin = Family.Revolver, Name = "저주받은 권총", Desc = "값 2배. 대신 맞춘 깡통이 넷에 하나꼴로 저주통이 된다. 저주통은 떨어지면 쌓인 돈을 뺏고, 터뜨리면 평소대로.", Capacity = 6, ReloadSeconds = 0.9f, AimRadius = 0.22f, HitAll = false, ValueScale = 2f, FireInterval = 0f, Auto = false, Kick = 1.2f, Pitch = 0.85f, Price = 2, CurseChance = 0.25f };
        // ---- 레버액션 소총: 한 발마다 철컥, 꾸준히 ----
        public static readonly GunKind LeverAction = new GunKind { Id = "lever", Kin = Family.Lever, Name = "레버액션 소총", Desc = "8발. 한 발마다 레버를 당긴다. 값 1.3배, 더 높이 띄운다.", Capacity = 8, ReloadSeconds = 1.6f, AimRadius = 0.2f, HitAll = false, ValueScale = 1.3f, FireInterval = 0.35f, Auto = false, Kick = 1.3f, Pitch = 0.75f, Price = 1, BounceScale = 1.25f, LeverCycle = true };
        // ---- 단발 라이플: 한 발, 세게, 바로 장전 ----
        public static readonly GunKind SingleShot = new GunKind { Id = "single", Kin = Family.Single, Name = "단발 라이플", Desc = "1발. 쏠 때마다 장전. 값 3배, 아주 높이 띄운다.", Capacity = 1, ReloadSeconds = 0.7f, AimRadius = 0.18f, HitAll = false, ValueScale = 3f, FireInterval = 0f, Auto = false, Kick = 2.2f, Pitch = 0.55f, Price = 1, BounceScale = 1.6f };
        // ---- 샷건: 두 발 묵직, 여럿 ----
        public static readonly GunKind Shotgun = new GunKind { Id = "shotgun", Kin = Family.Shotgun, Name = "샷건", Desc = "2발. 퍼진다. 원 안의 깡통을 전부 띄운다. 값은 70%.", Capacity = 2, ReloadSeconds = 1.3f, AimRadius = 0.9f, HitAll = true, ValueScale = 0.7f, FireInterval = 0f, Auto = false, Kick = 1.8f, Pitch = 0.7f, Price = 1, BounceScale = 1.15f };

        /// <summary>가게 목록: 가족 넷 + 리볼버 변종 둘</summary>
        public static readonly List<GunKind> ForSale = new List<GunKind> { Rusty, Revolver, LeverAction, SingleShot, Shotgun, DualRevolver, CursedRevolver };
    }
}
