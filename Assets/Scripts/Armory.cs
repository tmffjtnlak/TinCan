using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>
    /// 총기류·깡통류 가게. 카드 한 장 = 물건 하나 (왼쪽 아이콘 자리, 이름, 상태, 설명).
    /// 숫자키로 사거나 고른다. 스킬트리와 같은 화면 안의 다른 탭.
    /// </summary>
    public class Armory : MonoBehaviour
    {
        public enum Page { Guns, Cans, Display }
        class Card { public Image Root, Icon, IconFill; public Text Name, State, Desc, Key; }
        Game game;
        Image veil;
        Text title, money, hint;
        readonly List<Card> cards = new List<Card>();
        Page page;

        public void Build(Game g, Transform canvas)
        {
            game = g;
            veil = Ui.Image("Armory", canvas, Vector2.zero, new Vector2(4000f, 3000f), Ui.BoardDark);
            title = Ui.Text("Title", veil.transform, new Vector2(-300f, 330f), new Vector2(700f, 60f), 40, TextAnchor.MiddleLeft, Ui.Gold);
            money = Ui.Text("Money", veil.transform, new Vector2(300f, 330f), new Vector2(700f, 60f), 34, TextAnchor.MiddleRight, Ui.Gold);
            for (int i = 0; i < 6; i++) cards.Add(MakeCard(i));
            hint = Ui.Text("Hint", veil.transform, new Vector2(0f, -500f), new Vector2(1400f, 40f), 20, TextAnchor.MiddleCenter, Ui.Dim);
            hint.text = "숫자키 = 산다 / 고른다   ·   Tab = 닫는다";
            veil.gameObject.SetActive(false);
        }

        Card MakeCard(int i)
        {
            var c = new Card();
            c.Root = Ui.Panel($"Card{i}", veil.transform, new Vector2(0f, 262f - i * 104f), new Vector2(1300f, 94f), nails: false);
            c.Key = Ui.Text("Key", c.Root.transform, new Vector2(-610f, 0f), new Vector2(50f, 60f), 34, TextAnchor.MiddleCenter, Ui.Dim);
            c.Icon = Ui.Diamond("Icon", c.Root.transform, new Vector2(-540f, 0f), 64f, Ui.BoardEdge, Ui.BoardDark);
            c.IconFill = c.Icon.transform.GetChild(0).GetComponent<Image>();
            c.Name = Ui.Text("Name", c.Root.transform, new Vector2(-170f, 22f), new Vector2(620f, 44f), 30, TextAnchor.MiddleLeft, Ui.Ink);
            c.State = Ui.Text("State", c.Root.transform, new Vector2(480f, 22f), new Vector2(300f, 44f), 26, TextAnchor.MiddleRight, Ui.Gold);
            c.Desc = Ui.Text("Desc", c.Root.transform, new Vector2(50f, -26f), new Vector2(1060f, 40f), 20, TextAnchor.MiddleLeft, Ui.Dim);
            return c;
        }

        public void Show(Page p) { page = p; veil.gameObject.SetActive(true); }
        public void Hide() => veil.gameObject.SetActive(false);
        public bool Visible => veil.gameObject.activeSelf;

        void Update()
        {
            if (game == null || !Visible) return;
            money.text = $"${game.Money:N0}";
            var kb = Keyboard.current;
            title.text = page == Page.Guns ? "총기류  —  1 주무장 · 2 보조무기 · 3 투척류(추후)" : page == Page.Display ? "진열장" : "깡통류  —  뭘 띄우나";
            hint.text = page == Page.Display ? "Tab = 닫는다" : "숫자키 = 산다 / 고른다   ·   Tab = 닫는다";
            if (page == Page.Display) { ShowDisplay(); return; }
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                if (page == Page.Guns)
                {
                    if (i >= GunKind.ForSale.Count) { c.Root.gameObject.SetActive(false); continue; }
                    var k = GunKind.ForSale[i];
                    c.Root.gameObject.SetActive(true);
                    bool owned = game.OwnedGuns.Contains(k.Id), current = game.Primary == k, second = game.Secondary == k;
                    string fam = k.Kin switch { GunKind.Family.Revolver => "리볼버", GunKind.Family.Lever => "레버액션", GunKind.Family.Single => "단발", _ => "샷건" };
                    Fill(c, i, k.Name, k.IsCursed ? Ui.Curse : Ui.Ink, k.IsCursed ? new Color(0.35f, 0.15f, 0.45f) : new Color(0.3f, 0.3f, 0.36f),
                        owned, current || second, k.Price, $"<color=#bbaa88>[{fam}]</color>  {k.Desc}   ·   {k.Capacity}발, 장전 {k.ReloadSeconds:0.0}초", current ? "1 주무장" : second ? "2 보조무기 (누르면 자리 교대)" : "보유 (누르면 보조로)");
                    if (kb != null && Digit(kb, i + 1)) game.PickGun(k);
                }
                else
                {
                    if (i >= CanKind.ForSale.Count) { c.Root.gameObject.SetActive(false); continue; }
                    var k = CanKind.ForSale[i];
                    c.Root.gameObject.SetActive(true);
                    bool owned = game.OwnedCans.Contains(k.Id), current = game.SpawnKind == k;
                    long burst = game.BurstPay(k);
                    Fill(c, i, k.Name, Ui.Ink, k.Body, owned, current, k.Price, $"{k.Desc}   ·   소환 ${game.SpawnCost(k)}   ·   <color=#ffd966>터뜨리면 ${burst:N0}</color>", current ? "소환 중" : "보유");
                    if (kb != null && Digit(kb, i + 1)) game.PickCan(k);
                }
            }
        }

        void Fill(Card c, int i, string name, Color nameColor, Color iconColor, bool owned, bool current, long price, string desc, string ownedText)
        {
            c.Key.text = (i + 1).ToString();
            c.Name.text = name; c.Name.color = nameColor;
            c.IconFill.color = iconColor;
            c.Icon.color = current ? Ui.Good : Ui.BoardEdge;
            c.Desc.text = desc;
            bool afford = game.Money >= price;
            c.State.text = current ? $"<color=#88ff99>{ownedText}</color>" : owned ? $"<color=#bbbbbb>{ownedText}</color>" : (afford ? $"${price:N0}" : $"<color=#ff6666>${price:N0}</color>");
            var alpha = owned || afford ? 1f : 0.55f;
            c.Name.color = new Color(nameColor.r, nameColor.g, nameColor.b, alpha);
            c.Desc.color = new Color(Ui.Dim.r, Ui.Dim.g, Ui.Dim.b, Ui.Dim.a * alpha);
        }

        // ---------- 진열장 (사용자 10-15): 모은 깡통·현상금의 세부 정보 — 떨어짐·터짐 값, 체력, 특수 효과 ----------
        static readonly (CanKind kind, string effect)[] Shelf =
        {
            (CanKind.Tin, "기본 깡통. 띄우고, 맞히고, 터뜨린다."),
            (CanKind.Bottle, "작고 무겁다. 깨질 때 값이 크다."),
            (CanKind.Bundle, "터지면 깡통 셋으로 풀린다. 풀린 깡통은 처음 맞기 전까지 느리다."),
            (CanKind.Blue, "터지면 반투명 깡통이 날아오른다."),
            (CanKind.Magnet, "맞을 때마다 옆 깡통을 끌어당긴다."),
            (CanKind.Bounty, "총알이 안 먹힌다. 부하를 터뜨린 파편으로 깎고, 체력 1에서 막타. 잡으면 증표."),
        };
        bool Unlocked(CanKind k) =>
            k == CanKind.Tin ? true :
            k == CanKind.Bottle ? game.BottleChance > 0f :
            k == CanKind.Bundle ? game.BundleChance > 0f :
            k == CanKind.Blue ? game.BlueChance > 0f :
            k == CanKind.Magnet ? game.MagnetChance > 0f :
            k.IsBounty ? game.FirstBountyDone : false;

        void ShowDisplay()
        {
            title.text = $"진열장   <size=24><color=#bbaa88>잡은 현상금 {game.BountiesCaught}</color></size>";
            title.supportRichText = true;
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                if (i >= Shelf.Length) { c.Root.gameObject.SetActive(false); continue; }
                c.Root.gameObject.SetActive(true);
                var (k, effect) = Shelf[i];
                bool open = Unlocked(k);
                c.Key.text = "";
                c.Icon.color = Ui.BoardEdge;
                c.IconFill.color = open ? k.Body : new Color(0.15f, 0.13f, 0.12f);
                c.Name.supportRichText = c.Desc.supportRichText = c.State.supportRichText = true;
                if (!open)
                {
                    c.Name.text = "???"; c.Name.color = new Color(Ui.Ink.r, Ui.Ink.g, Ui.Ink.b, 0.5f);
                    c.State.text = "<color=#888888>아직</color>";
                    c.Desc.text = k.IsBounty ? "첫 현상금을 끝내면 여기에 놓인다." : "스킬트리 주황 갈래(깡통)에서 연다.";
                    c.Desc.color = new Color(Ui.Dim.r, Ui.Dim.g, Ui.Dim.b, 0.6f);
                    continue;
                }
                c.Name.text = k.IsBounty ? "현상수배 깡통" : k.Name; c.Name.color = Ui.Ink;
                long land = game.LandPay(k), burst = game.BurstPay(k);
                c.State.text = k.IsBounty ? $"<color=#ffd966>현상금 ${burst:N0} + 증표</color>" : $"<color=#ffd966>터짐 ${burst:N0}</color>";
                string hp = k.IsBounty ? "체력 8~15" : $"체력 {k.Hits}";
                c.Desc.text = (k.IsBounty ? "" : $"떨어짐 ${land:N0}   ·   ") + $"{hp}   ·   {effect}";
                c.Desc.color = Ui.Dim;
            }
        }

        static bool Digit(Keyboard kb, int n) => n switch
        {
            1 => kb.digit1Key.wasPressedThisFrame, 2 => kb.digit2Key.wasPressedThisFrame, 3 => kb.digit3Key.wasPressedThisFrame,
            4 => kb.digit4Key.wasPressedThisFrame, 5 => kb.digit5Key.wasPressedThisFrame, 6 => kb.digit6Key.wasPressedThisFrame,
            _ => false,
        };
    }
}
