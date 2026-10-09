using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>오른쪽 위 상점. 줄 하나 = 업그레이드 하나. 숫자키 1~9로 산다 (마우스는 조준에 쓰니까).</summary>
    public class Shop : MonoBehaviour
    {
        Game game;
        readonly List<Text> rows = new List<Text>();
        Font font;
        Transform canvas;
        Image panel;

        public void Build(Game g, Transform canvasRoot)
        {
            game = g; canvas = canvasRoot;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            panel = MakePanel("ShopPanel", new Vector2(640f, 300f), new Vector2(560f, 60f + game.Upgrades.Count * 52f), new Color(0.1f, 0.06f, 0.04f, 0.6f));
            var title = MakeText("ShopTitle", panel.transform, new Vector2(0f, panel.rectTransform.sizeDelta.y * 0.5f - 28f), new Vector2(520f, 40f), 26, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.5f));
            title.text = "잡화점   (숫자키로 산다)";
            for (int i = 0; i < game.Upgrades.Count; i++)
            {
                var t = MakeText($"Row{i}", panel.transform, new Vector2(0f, panel.rectTransform.sizeDelta.y * 0.5f - 72f - i * 52f), new Vector2(520f, 48f), 22, TextAnchor.MiddleLeft, Color.white);
                rows.Add(t);
            }
        }

        Image MakePanel(string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform; rt.SetParent(canvas, false);
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
            t.font = font; t.fontSize = fontSize; t.alignment = anchor; t.color = color; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        void Update()
        {
            if (game == null) return;
            var kb = Keyboard.current;
            for (int i = 0; i < game.Upgrades.Count && i < 9; i++)
            {
                var u = game.Upgrades[i];
                bool can = !u.Maxed && game.Money >= u.NextCost;
                rows[i].text = u.Maxed
                    ? $"{i + 1}. {u.Name}  <color=#88ff88>최대</color>"
                    : $"{i + 1}. {u.Name}  Lv{u.Level}  <color=#ffd966>${u.NextCost:N0}</color>   <size=18><color=#bbbbbb>{u.Desc}</color></size>";
                rows[i].supportRichText = true;
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
