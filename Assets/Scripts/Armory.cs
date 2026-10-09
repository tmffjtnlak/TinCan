using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>
    /// 총기류·깡통류 가게. 목록 하나씩. 숫자키로 사거나 고른다 (산 건 고르기, 안 산 건 사기).
    /// 스킬트리와 같은 화면 안의 다른 탭.
    /// </summary>
    public class Armory : MonoBehaviour
    {
        public enum Page { Guns, Cans }
        Game game;
        Font font;
        Image veil;
        Text title, money, hint;
        readonly List<Text> rows = new List<Text>();
        Page page;

        public void Build(Game g, Transform canvas)
        {
            game = g;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            veil = Panel("Armory", canvas, Vector2.zero, new Vector2(4000f, 3000f), new Color(0.1f, 0.07f, 0.05f, 1f));
            var board = Panel("Board", veil.transform, Vector2.zero, new Vector2(1000f, 620f), new Color(0.22f, 0.15f, 0.1f, 1f));
            Panel("BoardEdge", board.transform, Vector2.zero, new Vector2(1012f, 632f), new Color(0.5f, 0.36f, 0.22f, 1f)).transform.SetAsFirstSibling();
            title = Txt("Title", board.transform, new Vector2(0f, 270f), new Vector2(940f, 50f), 38, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.5f));
            money = Txt("Money", board.transform, new Vector2(0f, 270f), new Vector2(940f, 50f), 32, TextAnchor.MiddleRight, SkillTree.Yellow);
            for (int i = 0; i < 6; i++) rows.Add(Txt($"Row{i}", board.transform, new Vector2(0f, 190f - i * 78f), new Vector2(940f, 70f), 26, TextAnchor.MiddleLeft, Color.white));
            hint = Txt("Hint", veil.transform, new Vector2(0f, -500f), new Vector2(1400f, 40f), 22, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.55f));
            veil.gameObject.SetActive(false);
        }

        public void Show(Page p) { page = p; veil.gameObject.SetActive(true); }
        public void Hide() => veil.gameObject.SetActive(false);
        public bool Visible => veil.gameObject.activeSelf;

        void Update()
        {
            if (game == null || !Visible) return;
            money.text = $"${game.Money:N0}";
            var kb = Keyboard.current;
            title.text = page == Page.Guns ? "총기류  —  쏘는 방식이 다르다" : "깡통류  —  뭘 띄우나";
            hint.text = "숫자키 = 산다 / 고른다   ·   Q 총기류  W 깡통류  E 스킬   ·   Tab = 닫는다";
            if (page == Page.Guns)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    if (i >= GunKind.ForSale.Count) { rows[i].text = ""; continue; }
                    var k = GunKind.ForSale[i];
                    bool owned = game.OwnedGuns.Contains(k.Id);
                    bool current = game.Gun.Kind == k;
                    string state = current ? "<color=#88ff88>장착 중</color>" : owned ? "<color=#bbbbbb>보유</color>" : (game.Money >= k.Price ? $"<color=#ffd966>${k.Price:N0}</color>" : $"<color=#ff6666>${k.Price:N0}</color>");
                    string nm = k.IsCursed ? $"<color=#cc88ff>{k.Name}</color>" : k.Name;
                    rows[i].text = $"{i + 1}. {nm}   {state}\n<size=20><color=#cccccc>{k.Desc}   ·   {k.Capacity}발, 장전 {k.ReloadSeconds:0.0}초</color></size>";
                    rows[i].color = owned || game.Money >= k.Price ? Color.white : new Color(1f, 1f, 1f, 0.5f);
                    if (kb != null && Digit(kb, i + 1)) game.PickGun(k);
                }
            }
            else
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    if (i >= CanKind.ForSale.Count) { rows[i].text = ""; continue; }
                    var k = CanKind.ForSale[i];
                    bool owned = game.OwnedCans.Contains(k.Id);
                    bool current = game.SpawnKind == k;
                    string state = current ? "<color=#88ff88>소환 중</color>" : owned ? "<color=#bbbbbb>보유</color>" : (game.Money >= k.Price ? $"<color=#ffd966>${k.Price:N0}</color>" : $"<color=#ff6666>${k.Price:N0}</color>");
                    rows[i].text = $"{i + 1}. {k.Name}   {state}\n<size=20><color=#cccccc>{k.Desc}   ·   소환 ${game.SpawnCost(k)}</color></size>";
                    rows[i].color = owned || game.Money >= k.Price ? Color.white : new Color(1f, 1f, 1f, 0.5f);
                    if (kb != null && Digit(kb, i + 1)) game.PickCan(k);
                }
            }
        }

        static bool Digit(Keyboard kb, int n) => n switch
        {
            1 => kb.digit1Key.wasPressedThisFrame, 2 => kb.digit2Key.wasPressedThisFrame, 3 => kb.digit3Key.wasPressedThisFrame,
            4 => kb.digit4Key.wasPressedThisFrame, 5 => kb.digit5Key.wasPressedThisFrame, 6 => kb.digit6Key.wasPressedThisFrame,
            _ => false,
        };

        Image Panel(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var img = go.AddComponent<Image>(); img.color = color; img.raycastTarget = false;
            return img;
        }

        Text Txt(string name, Transform parent, Vector2 pos, Vector2 size, int fontSize, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = go.AddComponent<Text>();
            t.font = font; t.fontSize = fontSize; t.alignment = anchor; t.color = color; t.raycastTarget = false; t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
