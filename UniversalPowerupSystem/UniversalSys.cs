using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using flanne;
using flanne.Core;
using HarmonyLib;
using UnityEngine;
using Random = UnityEngine.Random;

namespace UniversalPowerupSystem
{
    public class CustomPowerup
    {
        public delegate void PreActionDelegate(CustomPowerup powerup);

        public delegate void BackupPreActionDelegate(CustomPowerup powerup);

        public delegate bool Cond(CustomPowerup powerup);

        public delegate void ReLoad(CustomPowerup powerup);

        public Powerup Powerup;
        public float Weight;
        public float RawWeight;

        public Cond Condition;
        public ReLoad Reload;
        public PreActionDelegate PreAction;
        public BackupPreActionDelegate BackupPreAction;

        public CustomPowerup(Powerup powerup, float? weight = null, Cond condition = null,
            ReLoad reLoad = null, PreActionDelegate preAction = null, BackupPreActionDelegate backupPreAction = null)
        {
            Powerup = powerup;
            Weight = weight ?? UniversalSys.StdWeight;
            RawWeight = weight ?? UniversalSys.StdWeight;
            Condition = condition ?? UniversalSys.StdCondition;
            Reload = reLoad ?? UniversalSys.StdReload;
            PreAction = preAction;
            BackupPreAction = backupPreAction;
        }

        #region Equal

        private sealed class PowerupEqualityComparer : IEqualityComparer<CustomPowerup>
        {
            public bool Equals(CustomPowerup x, CustomPowerup y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                if (x.GetType() != y.GetType()) return false;
                return Equals(x.Powerup, y.Powerup);
            }

            public int GetHashCode(CustomPowerup obj)
            {
                return (obj.Powerup != null ? obj.Powerup.GetHashCode() : 0);
            }
        }

        public static IEqualityComparer<CustomPowerup> PowerupComparer { get; } = new PowerupEqualityComparer();

        private bool Equals(CustomPowerup other)
        {
            return Equals(Powerup, other.Powerup);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((CustomPowerup) obj);
        }

        public override int GetHashCode()
        {
            return (Powerup != null ? Powerup.GetHashCode() : 0);
        }

        #endregion
    }

    [BepInPlugin("UniversalPowerupSystem.UniversalSys", "通用升级系统", "1.0.0")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "RedundantAssignment")]
    public class UniversalSys : BaseUnityPlugin
    {
        private static readonly ManualLogSource UniversalSysModLogger =
            BepInEx.Logging.Logger.CreateLogSource("UniversalSys");

        public static bool RegCustomPowerup(CustomPowerup customPowerup)
        {
            if (RawCustomPowerupPool.Contains(customPowerup)) return false;
            RawCustomPowerupPool.Add(customPowerup);
            return true;
        }
        
        public static bool RegCustomPowerup(Powerup powerup)
        {
            if (RawCustomPowerupPool.Select(customPowerup => customPowerup.Powerup).Contains(powerup)) return false;
            RawCustomPowerupPool.Add(new CustomPowerup(powerup));
            return true;
        }

        public static readonly List<CustomPowerup> RawCustomPowerupPool = new List<CustomPowerup>();
        public static int OncePowerupCount;
        public static readonly List<CustomPowerup> RunnerPowerupPool = new List<CustomPowerup>();
        public static readonly List<CustomPowerup> TakenPool = new List<CustomPowerup>();
        public static int OnceCount;
        public static GameController ThisGame;
        public const int StdWeight = 10;

        private static void VerifyPowerups()
        {
            RawCustomPowerupPool.AddRange(from p in PowerupGenerator.Instance.powerupPool
                where !RawCustomPowerupPool.Select(customPowerup => customPowerup.Powerup).Contains(p)
                select new CustomPowerup(p));
            foreach (var powerup in RawCustomPowerupPool.Select(powerup => powerup.Powerup)
                         .Where(powerup => !PowerupGenerator.Instance.powerupPool.Contains(powerup)))
            {
                RawCustomPowerupPool.RemoveAll(customPowerup => customPowerup.Powerup == powerup);
            }
        }

        public static readonly CustomPowerup.Cond StdCondition = powerup =>
            powerup.Powerup.prereqs.Count == 0 && !powerup.Powerup.isRepeatable
            ||
            powerup.Powerup.prereqs.Count != 0
            &&
            (!powerup.Powerup.anyPrereqFulfill && powerup.Powerup.prereqs.All(powerup1 =>
                 TakenPool.Select(customPowerup => customPowerup.Powerup).Contains(powerup1)) ||
             powerup.Powerup.anyPrereqFulfill && powerup.Powerup.prereqs.Any(powerup1 =>
                 TakenPool.Select(customPowerup => customPowerup.Powerup).Contains(powerup1)))
            &&
            !TakenPool.Contains(powerup)
            || (powerup.Powerup.isRepeatable && OncePowerupCount < OnceCount + GameState.numPowerupChoices &&
                !RunnerPowerupPool.Contains(powerup));

        public static readonly CustomPowerup.ReLoad StdReload = powerup =>
        {
            TakenPool.Clear();
            RunnerPowerupPool.Clear();
            powerup.Weight = powerup.RawWeight;
            OnceCount = 0;
        };

        public static readonly CustomPowerup.PreActionDelegate StdPreAction = null;

        public static readonly CustomPowerup.BackupPreActionDelegate StdBackupPreAction = null;

        public static AssetBundle IconAsset;
        public static Sprite NullIcon;
        public static GameState GameState;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameState), "Awake")]
        public static void GameStateAwake(GameState __instance)
        {
            GameState = __instance;
        }

        private void Awake()
        {
            if (File.Exists($"{Paths.PluginPath}/Addition/powerupicon.icons"))
            {
                IconAsset = AssetBundle.LoadFromFile($"{Paths.PluginPath}/Addition/powerupicon.icons");
                NullIcon = IconAsset.LoadAsset<Sprite>("NullIcon");
            }
        }

        private static bool RawPoolBuild;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameController), "Start")]
        public static void InitOnGameStart(GameController __instance)
        {
            VerifyPowerups();
            ThisGame = __instance;
            foreach (var customPowerup in RawCustomPowerupPool)
            {
                customPowerup.Reload(customPowerup);
            }

            if (RawPoolBuild) return;
            RawCustomPowerupPool.AddRange(from p in PowerupGenerator.Instance.profile.powerupPool
                select new CustomPowerup(p));
            foreach (var _ in PowerupGenerator.Instance.profile.powerupPool.Where(powerup => !powerup.isRepeatable))
            {
                OncePowerupCount += 1;
            }

            RawPoolBuild = true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PowerupGenerator), "GetRandom", typeof(int))]
        public static bool RollPowerup(PowerupGenerator __instance, ref List<Powerup> __result, int num)
        {
            __result = new List<Powerup>();
            foreach (var customPowerup in RawCustomPowerupPool.Where(customPowerup =>
                         customPowerup.Condition(customPowerup)))
            {
                RunnerPowerupPool.Add(customPowerup);
                customPowerup.PreAction?.Invoke(customPowerup);
            }

            var rmList = RunnerPowerupPool.Where(customPowerup => !customPowerup.Condition(customPowerup)).ToList();

            foreach (var customPowerup in rmList)
            {
                customPowerup.BackupPreAction?.Invoke(customPowerup);
                RunnerPowerupPool.Remove(customPowerup);
            }

            for (var i = 0; i < Math.Min(num, RunnerPowerupPool.Count); i++)
            {
                var SumWeight = RunnerPowerupPool.Sum(powerup => powerup.Weight);
                while (true)
                {
                    var randomV = Random.Range(0, SumWeight);
                    var randomPowerup = RunnerPowerupPool.FirstOrDefault(powerup =>
                    {
                        randomV -= powerup.Weight;
                        return randomV <= 0;
                    });
                    if (randomPowerup == null || __result.Contains(randomPowerup.Powerup)) continue;
                    __result.Add(randomPowerup.Powerup);

                    break;
                }
            }

            for (var i = __result.Count; i < num; i++)
            {
                var none = ScriptableObject.CreateInstance<NullPowerup>();
                __result.Add(none.Init());
            }

            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PowerupGenerator), "RemoveFromPool")]
        public static void MonitorRemove(Powerup powerup)
        {
            var customPowerups = RawCustomPowerupPool.FindAll(customPowerup => customPowerup.Powerup == powerup);
            TakenPool.AddRange(customPowerups.Where(customPowerup => !customPowerup.Powerup.isRepeatable));
            if (!powerup.isRepeatable)
            {
                OnceCount += 1;
            }
        }
    }
}