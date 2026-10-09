using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>상점 화면. Tab으로 열고 닫는다. 열려 있는 동안 사격은 멈춘다. 숫자키로 산다.</summary>
    public class Shop : MonoBehaviour
    {
        Game game;
        readonly List<Text> rows = new List<Text>();
        Font font;
        Transform canvas;
        Image veil, panel;
        Text money;

        public void Build(Game g, Transform canvasRoot)
        {
            game = g; canvas = canvasRoot;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            veil = MakePanel("ShopVeil", canvas, Vector2.zero, new Vector2(4000f, 3000f), new Color(0.05f, 0.03f, 0.02f, 0.75f));
            panel = MakePanel("ShopPanel", veil.transform, Vector2.zero, new Vector2(900f, 140f + game.Upgrades.Count * 70f), new Color(0.25f, 0.16f, 0.1f, 0.95f));
            float top = panel.rectTransform.sizeDelta.y * 0.5f;
            var title = MakeText("Title", panel.transform, new Vector2(0f, top - 45f), new Vector2(840f, 50f), 36, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.5f));
            title.text = "잡화점";
            money = MakeText("Money", panel.transform, new Vector2(0f, top - 45f), new Vector2(840f, 50f), 32, TextAnchor.MiddleRight, new Color(1f, 0.9f, 0.4f));
            for (int i = 0; i < game.Upgrades.Count; i++)
                rows.Add(MakeText($"Row{i}", panel.transform, new Vector2(0f, top - 110f - i * 70f), new Vector2(840f, 60f), 28, TextAnchor.MiddleLeft, Color.white));
            var hint = MakeText("Hint", panel.transform, new Vector2(0f, -top + 30f), new Vector2(840f, 40f), 22, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.6f));
            hint.text = "숫자키 = 산다   ·   Tab = 닫는다";
        }

        public void SetOpen(bool open) => veil.gameObject.SetActive(open);

        Image MakePanel(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var img = go.AddComponent<Image>(); img.color = color; img.raycastTarget = false;
            return img;
        }

        Text MakeText(string name, Transform parent, Vector2 pos, Vector2 size, int fontSize, TextAnchor anchor, Color color)
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

        void Update()
        {
            if (game == null || !veil.gameObject.activeSelf) return;
            money.text = $"${game.Money:N0}";
            var kb = Keyboard.current;
            for (int i = 0; i < game.Upgrades.Count && i < 9; i++)
            {
                var u = game.Upgrades[i];
                bool can = !u.Maxed && game.Money >= u.NextCost;
                rows[i].text = u.Maxed
                    ? $"{i + 1}. {u.Name}  <color=#88ff88>완료</color>   <size=22><color=#bbbbbb>{u.Desc}</color></size>"
                    : $"{i + 1}. {u.Name}  Lv{u.Level}  <color=#ffd966>${u.NextCost:N0}</color>   <size=22><color=#bbbbbb>{u.Desc}</color></size>";
                rows[i].color = u.Maxed ? new Color(1f, 1f, 1f, 0.5f) : (can ? Color.white : new Color(1f, 1f, 1f, 0.45f));
                if (kb != null && Digit(kb, i + 1)) game.Buy(u);
            }
        }

        static bool Digit(Keyboard kb, int n) => n switch
        {
            1 => kb.digit1Key.wasPressedThisFrame, 2 => kb.digit2Key.wasPressedThisFrame, 3 => kb.digit3Key.wasPressedThisFrame,
            4 => kb.digit4Key.wasPressedThisFrame, 5 => kb.digit5Key.wasPressedThisFrame, 6 => kb.digit6Key.wasPressedThisFrame,
            7 => kb.digit7Key.wasPressedThisFrame, 8 => kb.digit8Key.wasPressedThisFrame, 9 => kb.digit9Key.wasPressedThisFrame,
            _ => false,
        };
    }
}
