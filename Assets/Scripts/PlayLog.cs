using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace TinCan
{
    /// <summary>
    /// 밸런스 기록 (사용자 10-14: "시간대별 재화 획득 곡선과 트리 방향성을 확인할 수 있어야").
    /// 플레이를 시작하면 PlayLogs/play_날짜.jsonl 한 파일에 5초마다 돈 상태 + 사건(노드·총 구매, 현상금, 세트)을 한 줄씩 쓴다.
    /// 트리 전체(가격·자리·이웃)는 PlayLogs/tree.json. 보는 건 Tools/balance-report.ps1 → PlayLogs/report.html
    /// </summary>
    public static class PlayLog
    {
        const float SampleEvery = 5f;
        static string path;
        static float t0, nextSample;
        static long earned, spent;

        public static bool Active => path != null;
        static float T => Time.realtimeSinceStartup - t0;
        static string Dir => Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "PlayLogs")) : Path.Combine(Application.persistentDataPath, "PlayLogs");

        /// <summary>새 플레이 직전: 이전 파일을 닫는다 (시작 돈·불러온 돈이 수입으로 잡히지 않게)</summary>
        public static void Stop() { path = null; menuPage = null; }

        static string menuPage; static float menuSince;
        /// <summary>
        /// 메뉴에 머문 시간 (사용자 10-14): 페이지가 바뀌거나 닫힐 때 앞 페이지를 "menu" 한 줄로 남긴다.
        /// id = 페이지 (tree / gunshop / cans), amount = 머문 밀리초. page가 null이면 닫힘
        /// </summary>
        public static void Menu(string page)
        {
            if (!Active) return;
            if (menuPage != null) Event("menu", menuPage, (long)Mathf.Round((T - menuSince) * 1000f));
            menuPage = page; menuSince = T;
        }

        public static void Begin(Game g, bool loaded)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                path = Path.Combine(Dir, $"play_{DateTime.Now:yyyyMMdd_HHmmss}.jsonl");
                t0 = Time.realtimeSinceStartup; nextSample = 0f; earned = 0; spent = 0;
                Line("start", loaded ? "load" : "new", g.Money, g);
                WriteTree(g);
            }
            catch (Exception e) { Debug.LogWarning("PlayLog: " + e.Message); path = null; }
        }

        /// <summary>Game.Money가 바뀔 때마다 (들어온 돈 / 나간 돈 누적)</summary>
        public static void MoneyDelta(long d)
        {
            if (!Active) return;
            if (d > 0) earned += d; else spent -= d;
        }

        public static void Tick(Game g)
        {
            if (!Active || T < nextSample) return;
            nextSample = T + SampleEvery;
            Line("sample", "", 0, g);
        }

        public static void Event(string kind, string id, long amount)
        {
            if (!Active || Game.I == null) return;
            Line(kind, id, amount, Game.I);
        }

        static void Line(string e, string id, long amount, Game g)
        {
            var sb = new StringBuilder(160);
            sb.Append("{\"t\":").Append(T.ToString("0.0", CultureInfo.InvariantCulture))
              .Append(",\"e\":\"").Append(Esc(e)).Append("\",\"id\":\"").Append(Esc(id)).Append("\",\"amount\":").Append(amount)
              .Append(",\"money\":").Append(g.Money).Append(",\"earned\":").Append(earned).Append(",\"spent\":").Append(spent)
              .Append(",\"tokens\":").Append(g.Tokens).Append(",\"bursts\":").Append(g.Bursts)
              .Append(",\"debug\":").Append(g.DebugMoney ? "true" : "false").Append("}\n");
            try { File.AppendAllText(path, sb.ToString(), new UTF8Encoding(false)); } catch { }
        }

        static void WriteTree(Game g)
        {
            var sb = new StringBuilder("[\n");
            for (int i = 0; i < g.Nodes.Count; i++)
            {
                var n = g.Nodes[i];
                sb.Append("{\"id\":\"").Append(Esc(n.Id)).Append("\",\"name\":\"").Append(Esc(n.Name))
                  .Append("\",\"cost\":").Append(n.Cost(0)).Append(",\"desc\":\"").Append(Esc(n.Desc(0)))
                  .Append("\",\"x\":").Append(n.Pos.x.ToString("0.###", CultureInfo.InvariantCulture))
                  .Append(",\"y\":").Append(n.Pos.y.ToString("0.###", CultureInfo.InvariantCulture))
                  .Append(",\"color\":\"#").Append(ColorUtility.ToHtmlStringRGB(n.Color))
                  .Append("\",\"key\":").Append(n.Key ? "true" : "false").Append(",\"root\":").Append(n.Root ? "true" : "false")
                  .Append(",\"links\":[");
                for (int j = 0; j < n.Links.Length; j++) sb.Append(j > 0 ? "," : "").Append('"').Append(Esc(n.Links[j])).Append('"');
                sb.Append("]}").Append(i < g.Nodes.Count - 1 ? ",\n" : "\n");
            }
            sb.Append("]\n");
            File.WriteAllText(Path.Combine(Dir, "tree.json"), sb.ToString(), new UTF8Encoding(false));
        }

        static string Esc(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "");
    }
}
