using UnityEngine;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>가게 화면 맨 위 탭 셋: 총기류 / 깡통류 / 스킬. 켜진 탭이 밝다. (가게 화면들 위에 그려진다)</summary>
    public class MenuTabs : MonoBehaviour
    {
        Image root;
        readonly Image[] tabs = new Image[3];
        readonly Text[] labels = new Text[3];
        static readonly string[] Names = { "Q  총기류", "W  깡통류", "E  스킬" };

        public void Build(Transform canvas)
        {
            root = Ui.Image("MenuTabs", canvas, new Vector2(0f, 455f), new Vector2(900f, 70f), Color.clear);
            for (int i = 0; i < 3; i++)
            {
                tabs[i] = Ui.Panel($"Tab{i}", root.transform, new Vector2(-300f + i * 300f, 0f), new Vector2(280f, 64f), nails: false);
                labels[i] = Ui.Text("Label", tabs[i].transform, Vector2.zero, new Vector2(260f, 50f), 26, TextAnchor.MiddleCenter, Ui.Ink);
                labels[i].text = Names[i];
            }
            root.gameObject.SetActive(false);
        }

        public void Show(int active)
        {
            root.gameObject.SetActive(true);
            root.transform.SetAsLastSibling();
            for (int i = 0; i < 3; i++)
            {
                bool on = i == active;
                tabs[i].color = on ? Ui.Gold : Ui.BoardEdge;
                tabs[i].transform.GetChild(0).GetComponent<Image>().color = on ? new Color(0.3f, 0.2f, 0.1f, 1f) : Ui.Board;
                labels[i].color = on ? Ui.Gold : Ui.Dim;
                tabs[i].rectTransform.localScale = Vector3.one * (on ? 1.06f : 1f);
            }
        }

        public void Hide() => root.gameObject.SetActive(false);
    }
}
