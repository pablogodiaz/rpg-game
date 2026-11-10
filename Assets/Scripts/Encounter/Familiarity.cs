using UnityEngine;

namespace ReadyCheck
{
    /// <summary>
    /// Saved boss progress: attempts, best attempt, kills, and per-member familiarity.
    /// Stored in PlayerPrefs for now. This moves behind a save service (ISaveService) before the Switch port.
    /// </summary>
    public static class Familiarity
    {
        const string Prefix = "ReadyCheck.";

        public static int Get(string bossId, string member) => PlayerPrefs.GetInt($"{Prefix}fam.{bossId}.{member}", 0);

        public static int Increment(string bossId, string member)
        {
            int v = Get(bossId, member) + 1;
            PlayerPrefs.SetInt($"{Prefix}fam.{bossId}.{member}", v);
            return v;
        }

        public static int Attempts(string bossId) => PlayerPrefs.GetInt($"{Prefix}attempts.{bossId}", 0);

        public static int IncrementAttempts(string bossId)
        {
            int v = Attempts(bossId) + 1;
            PlayerPrefs.SetInt($"{Prefix}attempts.{bossId}", v);
            return v;
        }

        /// <summary>Lowest boss health percentage ever reached (100 = never pulled).</summary>
        public static float BestPercent(string bossId) => PlayerPrefs.GetFloat($"{Prefix}best.{bossId}", 100f);

        public static bool TrySetBest(string bossId, float percent)
        {
            if (percent >= BestPercent(bossId)) return false;
            PlayerPrefs.SetFloat($"{Prefix}best.{bossId}", percent);
            return true;
        }

        public static int Kills(string bossId) => PlayerPrefs.GetInt($"{Prefix}kills.{bossId}", 0);

        public static void AddKill(string bossId)
        {
            PlayerPrefs.SetInt($"{Prefix}kills.{bossId}", Kills(bossId) + 1);
        }

        public static void Save() => PlayerPrefs.Save();

        public static void ResetBoss(string bossId, System.Collections.Generic.IEnumerable<string> members)
        {
            PlayerPrefs.DeleteKey($"{Prefix}attempts.{bossId}");
            PlayerPrefs.DeleteKey($"{Prefix}best.{bossId}");
            PlayerPrefs.DeleteKey($"{Prefix}kills.{bossId}");
            foreach (var m in members) PlayerPrefs.DeleteKey($"{Prefix}fam.{bossId}.{m}");
            PlayerPrefs.Save();
        }
    }
}
