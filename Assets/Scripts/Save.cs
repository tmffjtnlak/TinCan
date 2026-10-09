using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinCan
{
    /// <summary>저장: PlayerPrefs에 JSON 한 덩어리. 인크리멘탈이니까 자동 저장(5초마다, 가게 닫을 때, 끌 때).</summary>
    public static class Save
    {
        const string Key = "tincan_save_v1";

        [Serializable]
        public class Data
        {
            public long money;
            public int bursts, bestCombo;
            public long bestPayout;
            public List<string> nodeIds = new List<string>();
            public List<int> nodeLevels = new List<int>();
            public List<string> ownedGuns = new List<string>();
            public List<string> ownedCans = new List<string>();
            public string gun = "revolver", spawn = "tin";
            public float volume = 1f;
        }

        public static bool Exists => PlayerPrefs.HasKey(Key);
        public static void Delete() { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }

        public static void Write(Game g)
        {
            var d = new Data { money = g.Money, bursts = g.Bursts, bestCombo = g.BestCombo, bestPayout = g.BestPayout, gun = g.Gun.Kind.Id, spawn = g.SpawnKind.Id, volume = AudioListener.volume };
            foreach (var n in g.Nodes) { d.nodeIds.Add(n.Id); d.nodeLevels.Add(n.Level); }
            d.ownedGuns.AddRange(g.OwnedGuns); d.ownedCans.AddRange(g.OwnedCans);
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(d));
            PlayerPrefs.Save();
        }

        public static Data Read()
        {
            if (!Exists) return null;
            try { return JsonUtility.FromJson<Data>(PlayerPrefs.GetString(Key)); } catch { return null; }
        }
    }
}
