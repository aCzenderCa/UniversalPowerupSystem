using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using flanne;
using flanne.Core;
using HarmonyLib;
using Sirenix.Utilities;
using UnityEngine;
using UniversalPowerupSystem.ObjectPoolOpt;
using UniversalPowerupSystem.Util;
using Object = UnityEngine.Object;

namespace UniversalPowerupSystem
{
    [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
    public class CustomPowerup : IWeight
    {
        public delegate void PreActionDelegate(CustomPowerup powerup);

        public delegate void BackupPreActionDelegate(CustomPowerup powerup);

        public delegate bool Cond(CustomPowerup powerup);

        public delegate void ReLoad(CustomPowerup powerup);

        public Powerup Powerup;
        public readonly float RawWeight;

        public Cond Condition;
        public ReLoad Reload;
        public float Weight { get; set; }
        public PreActionDelegate PreAction;
        public BackupPreActionDelegate BackupPreAction;
        public bool IsCustom;

        public CustomPowerup(Powerup powerup, float? weight = null, Cond condition = null,
            ReLoad reLoad = null, PreActionDelegate preAction = null, BackupPreActionDelegate backupPreAction = null,
            bool isCustom = false)
        {
            Powerup = powerup;
            Weight = weight ?? UniversalSys.StdWeight;
            RawWeight = weight ?? UniversalSys.StdWeight;
            Condition = condition ?? UniversalSys.StdCondition;
            Reload = reLoad ?? UniversalSys.StdReload;
            PreAction = preAction;
            BackupPreAction = backupPreAction;
            IsCustom = isCustom;
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
                return obj.Powerup != null ? obj.Powerup.GetHashCode() : 0;
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
            return obj.GetType() == GetType() && Equals((CustomPowerup) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Powerup != null ? Powerup.GetHashCode() : 0) * 397) ^ RawWeight.GetHashCode();
            }
        }

        #endregion
    }

    [BepInPlugin("UniversalPowerupSystem.UniversalSys", "通用升级系统", "1.0.0")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "RedundantAssignment")]
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public class UniversalSys : BaseUnityPlugin
    {
        public static readonly ManualLogSource UniversalSysModLogger =
            BepInEx.Logging.Logger.CreateLogSource("UniversalSys");

        public static bool RegCustomPowerup(Powerup powerup, bool isCustom = true)
        {
            if (powerup.isRepeatable || RawCustomPowerupPool.ContainsPowerup(powerup)) return false;
            RawCustomPowerupPool.Add(new CustomPowerup(powerup, isCustom: isCustom));
            return true;
        }

        public static bool RegCustomPowerup(CustomPowerup customPowerup, bool isCustom = true)
        {
            if (customPowerup.IsRepeatable() || RawCustomPowerupPool.Contains(customPowerup)) return false;
            customPowerup.IsCustom = isCustom;
            RawCustomPowerupPool.Add(customPowerup);
            return true;
        }

        public static bool RegRepeatableCustomPowerup(Powerup powerup, bool isCustom = true)
        {
            if (RawRepeatableCustomPowerupPool.ContainsPowerup(powerup)) return false;
            RawRepeatableCustomPowerupPool.Add(new CustomPowerup(powerup, isCustom: isCustom));
            return true;
        }

        public static bool RegRepeatableCustomPowerup(CustomPowerup customPowerup, bool isCustom = true)
        {
            if (RawRepeatableCustomPowerupPool.Contains(customPowerup)) return false;
            customPowerup.IsCustom = isCustom;
            RawRepeatableCustomPowerupPool.Add(customPowerup);
            return true;
        }

        public static readonly List<CustomPowerup> RawCustomPowerupPool = new List<CustomPowerup>();
        public static readonly List<CustomPowerup> RawRepeatableCustomPowerupPool = new List<CustomPowerup>();
        public static readonly List<CustomPowerup> RunnerPowerupPool = new List<CustomPowerup>();
        public static readonly List<CustomPowerup> TakenPool = new List<CustomPowerup>();
        public static GameController ThisGame;
        public const int StdWeight = 10;

        private static void VerifyPowerups()
        {
            RawCustomPowerupPool.AddRange(
                from p in PowerupGenerator.Instance.powerupPool ?? PowerupGenerator.Instance.profile.powerupPool
                where !RawCustomPowerupPool.ContainsPowerup(p) && !p.isRepeatable
                select new CustomPowerup(p));
            RawRepeatableCustomPowerupPool.AddRange(
                from p in PowerupGenerator.Instance.powerupPool ?? PowerupGenerator.Instance.profile.powerupPool
                where !RawRepeatableCustomPowerupPool.ContainsPowerup(p) && p.isRepeatable
                select new CustomPowerup(p, condition: StdRepeatableCondition));

            RawCustomPowerupPool
                .Where(powerup =>
                    !powerup.IsCustom && !PowerupGenerator.Instance.profile.powerupPool.Contains(powerup.Powerup))
                .ToImmutableList().ForEach(powerup => RawCustomPowerupPool.Remove(powerup));
            RawRepeatableCustomPowerupPool
                .Where(powerup =>
                    !powerup.IsCustom && !PowerupGenerator.Instance.profile.powerupPool.Contains(powerup.Powerup))
                .ToImmutableList().ForEach(powerup => RawRepeatableCustomPowerupPool.Remove(powerup));
        }

        private void Start()
        {
            BlockActions.Add(health => false);
        }

        public static readonly CustomPowerup.Cond StdCondition = NotRepeatableCond;
        public static readonly CustomPowerup.Cond StdRepeatableCondition = RepeatableCond;

        public static bool NotRepeatableCond(CustomPowerup powerup)
        {
            if (TakenPool.Contains(powerup) || PowerupGenerator.Instance.takenPowerups.Contains(powerup.Powerup) ||
                powerup.IsRepeatable())
            {
                return false;
            }

            switch (powerup.Powerup.prereqs.Count == 0)
            {
                case true:
                    return true;
                case false:
                    switch (powerup.Powerup.anyPrereqFulfill)
                    {
                        case true:
                            return powerup.Powerup.prereqs.Any(powerup1 =>
                                TakenPool.ContainsPowerup(powerup1) ||
                                PowerupGenerator.Instance.takenPowerups.Contains(powerup1));
                        case false:
                            return powerup.Powerup.prereqs.All(powerup1 =>
                                TakenPool.ContainsPowerup(powerup1) ||
                                PowerupGenerator.Instance.takenPowerups.Contains(powerup1));
                    }
                    break;
            }
            return false;
        }

        public static bool RepeatableCond(CustomPowerup powerup)
        {
            return powerup.IsRepeatable();
        }

        public static readonly CustomPowerup.ReLoad StdReload = powerup => { powerup.Weight = powerup.RawWeight; };

        public static readonly CustomPowerup.PreActionDelegate StdPreAction = null;

        public static readonly CustomPowerup.BackupPreActionDelegate StdBackupPreAction = null;

        public static readonly AssetBundle IconAsset;
        public static readonly Sprite NullIcon;
        public static GameState GameState;

        static UniversalSys()
        {
            Harmony.CreateAndPatchAll(typeof(ObjectPoolOptSys));

            IconAsset = AssetBundle.LoadFromFile($"{Paths.PluginPath}/Addition/powerupicon.icons");
            NullIcon = LoadAsset<Sprite>("NullIcon");
        }

        public static T LoadAsset<T>(string name)
            where T : Object
        {
            return IconAsset.LoadAsset<T>(name);
        }

        public static Collider2D PickupRange;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameState), "Awake")]
        public static void GameStateAwake(GameState __instance)
        {
            GameState = __instance;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(PlayerController), "Awake")]
        public static void PlayerAwake(PlayerController __instance)
        {
            PickupRange = __instance.GetComponentsInChildren<Collider2D>()
                .FirstOrDefault(collider2D => collider2D.gameObject.tag.Contains("Pickupper"));
        }

        public static event Action GameReloadAct = () => { };

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameController), "Start")]
        public static void InitOnGameStart(GameController __instance)
        {
            ThisGame = __instance;
            GameReloadAct();

            RawCustomPowerupPool.ForEach(powerup => powerup.Reload(powerup));
            RawRepeatableCustomPowerupPool.ForEach(powerup => powerup.Reload(powerup));
            TakenPool.Clear();
            RunnerPowerupPool.Clear();
        }

        public static void RollPowerupInPool(ref List<Powerup> __result, List<CustomPowerup> PowerupPool, int num)
        {
            for (var i = 0; i < num; i++)
            {
                while (true)
                {
                    var pool = __result;
                    if (PowerupPool.All(powerup => pool.Contains(powerup.Powerup)))
                    {
                        break;
                    }

                    var randomPowerup = PowerupPool.GetWeightRandom();

                    if (randomPowerup != null && !__result.Contains(randomPowerup.Powerup) &&
                        randomPowerup.Condition(randomPowerup))
                    {
                        __result.Add(randomPowerup.Powerup);
                        break;
                    }
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PowerupGenerator), "GetRandom", typeof(int))]
        public static bool RollPowerup(PowerupGenerator __instance, ref List<Powerup> __result, int num)
        {
            VerifyPowerups();
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

            RollPowerupInPool(ref __result, RunnerPowerupPool, Math.Min(num, RunnerPowerupPool.Count));
            RollPowerupInPool(ref __result, RawRepeatableCustomPowerupPool, num - __result.Count);

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
            if (RawCustomPowerupPool.FirstOrDefault(customPowerup => customPowerup.Powerup == powerup) is var p &&
                p != null)
            {
                TakenPool.Add(p);
            }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PowerupGenerator), "GetRandomDevilProfile")]
        public static bool GenEmptyBook(PowerupGenerator __instance, ref List<Powerup> __result, int num)
        {
            __result = __instance.devilPool.Count < num
                ? __instance.devilPool.Take(__instance.devilPool.Count).ToList()
                : __instance.GetRandom(num, __instance.devilPool);

            while (__result.Count < num)
            {
                __result.Add(ScriptableObject.CreateInstance<NullPowerup>().Init());
            }

            return false;
        }

        #region Block

        public static readonly List<Func<Health, bool>> BlockActions = new List<Func<Health, bool>>();
        public static readonly List<Func<HPChangeData, bool>> EndPassActions = new List<Func<HPChangeData, bool>>();
        public static readonly List<Func<Health, bool>> PlayerBlockActions = new List<Func<Health, bool>>();
        public static readonly List<Func<HPChangeData, int>> ModifierActions = new List<Func<HPChangeData, int>>();
        public static readonly List<Func<Health, float>> MulModifierActions = new List<Func<Health, float>>();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Health), "HPChange")]
        public static bool Health_HPChange(Health __instance, ref int change)
        {
            // if (__instance.tag == "Player")
            // {
            //     return false;
            // }
            // else
            // {
            //     change = Math.Min(-__instance._maxHP / 2, change);
            // }

            if (__instance.isInvincible.value && change < 0)
            {
                __instance.onDamagePrevented.Invoke();
                return false;
            }

            change = change.NotifyModifiers(Health.TweakDamageEvent, __instance);
            if (change < 0 && __instance.tag == "Player" &&
                PlayerBlockActions.Any(blockAction => blockAction(__instance)))
            {
                change = 0;
            }

            if (change < 0 && BlockActions.Any(blockAction => blockAction(__instance)))
            {
                change = 0;
            }

            var hpChangeData = new HPChangeData
            {
                Change = change,
                Health = __instance,
                IsPlayer = __instance.tag == "Player"
            };

            foreach (var modifierAction in ModifierActions)
            {
                change += modifierAction(hpChangeData);
                hpChangeData.Change = change;
            }

            change = (int) (change * MulModifierActions.Aggregate(1f,
                (current, mulModifierAction) => current * mulModifierAction(__instance)));
            if (__instance.tag != "Player" && change < 0)
            {
                change = Math.Min(change, -1);
            }

            if (EndPassActions.Any(endPassAction => endPassAction(hpChangeData)))
            {
                change = 0;
            }


            var hp = __instance.HP != 0;
            __instance.HP = Mathf.Clamp(__instance.HP + change, 0, __instance.maxHP);
            __instance.onHealthChange.Invoke(__instance.HP);
            if (change < 0)
            {
                __instance.onHurt.Invoke(change);
            }
            else if (change > 0)
            {
                __instance.onHeal.Invoke(change);
            }

            if (!hp || __instance.HP != 0) return false;
            __instance.onDeath.Invoke();
            __instance.PostNotification(Health.DeathEvent, null);

            return false;
        }

        public class HPChangeData
        {
            public int Change;
            public Health Health;
            public bool IsPlayer;
        }

        #endregion
    }
}