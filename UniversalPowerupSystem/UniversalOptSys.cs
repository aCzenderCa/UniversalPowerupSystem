using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using BepInEx;
using flanne;
using flanne.Pickups;
using flanne.PowerupSystem;
using HarmonyLib;
using UnityEngine;
using Random = UnityEngine.Random;

namespace UniversalPowerupSystem
{
    [BepInPlugin("UniversalPowerupSystem.UniversalOptSys", "性能调优", "0.0.1")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class UniversalOptSys : BaseUnityPlugin
    {
        private static GameObject XpMergerSmallPrefab;
        private static GameObject XpMergerBigPrefab;
        public static readonly List<CircleCollider2D> CircleCollider2Ds = new List<CircleCollider2D>();
        public static BoolSign ShouldRecalculation = new BoolSign();

        private void Start()
        {
            XpMergerSmallPrefab = UniversalSys.IconAsset.LoadAsset<GameObject>("XpMergerSmall");
            XpMergerSmallPrefab.AddComponent<XpMergerCode>().rawXp = 1;
            XpMergerBigPrefab = UniversalSys.IconAsset.LoadAsset<GameObject>("XpMergerBig");
            XpMergerBigPrefab.AddComponent<XpMergerCode>().rawXp = 10;
            UniversalSys.GameReloadAct += () => { };
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ObjectPooler), "Awake")]
        public static void RegPrefab(ObjectPooler __instance)
        {
            var objectToPool1 = __instance.itemsToPool.FirstOrDefault(item => item.tag == "SmallXP")?.objectToPool;
            if (objectToPool1 != null && objectToPool1.GetComponentInChildren<XpMergerCode>() == null)
            {
                XpMergerSmallPrefab.transform.SetParent(objectToPool1.transform);
            }

            var objectToPool2 = __instance.itemsToPool.FirstOrDefault(item => item.tag == "LargeXP")
                ?.objectToPool;
            if (objectToPool2 != null && objectToPool2.GetComponentInChildren<XpMergerCode>() == null)
            {
                XpMergerBigPrefab.transform.SetParent(objectToPool2.transform);
            }

            CircleCollider2Ds.Clear();
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Pickup), "OnTriggerEnter2D")]
        public static bool DoNotPhy2D(Pickup __instance)
        {
            return !(__instance is XPPickup);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(BuffSummonAttackSpeedOverTime), "Update")]
        public static bool DragonCDLimit(BuffSummonAttackSpeedOverTime __instance)
        {
            if (__instance._timer > __instance.secondsPerBuff)
            {
                foreach (var shootingSummon in __instance._summons.Where(shootingSummon => shootingSummon != null))
                {
                    if (shootingSummon.attackCooldown > 0.4)
                    {
                        shootingSummon.attackCooldown /= 1f + __instance.attackSpeedBuff;
                    }
                    else
                    {
                        shootingSummon.baseDamage += 6;
                    }
                }

                __instance._timer -= __instance.secondsPerBuff;
            }

            __instance._timer += Time.deltaTime;

            return false;
        }

        public class BoolSign
        {
            public bool sign;
            public float time;
        }
    }

    public class XpMergerCode : MonoBehaviour
    {
        private bool _merger;
        private CircleCollider2D _circleCollider2D;
        private CircleCollider2D _xpCollider2D;
        private XPPickup _xpPickup;
        public float rawXp;
        private bool _running = true;

        private void OnEnable()
        {
            _running = true;
            _merger = false;
            _circleCollider2D = gameObject.GetComponent<CircleCollider2D>();
            _xpPickup = gameObject.GetComponentInParent<XPPickup>();
            _xpCollider2D = _xpPickup.GetComponent<CircleCollider2D>();
            _xpPickup.amount = rawXp;
            if (Random.value < 0.4)
            {
                if (UniversalOptSys.CircleCollider2Ds.Count == 0 || UniversalOptSys.CircleCollider2Ds.FirstOrDefault(
                        collider2D => collider2D.Distance(_circleCollider2D).isOverlapped) == null)
                {
                    _merger = true;
                    UniversalOptSys.CircleCollider2Ds.Add(_circleCollider2D);
                    UniversalOptSys.ShouldRecalculation.sign = true;
                    UniversalOptSys.ShouldRecalculation.time = Time.time;
                }
            }

            if (_running && !_merger)
            {
                Merger();
            }
        }

        private void OnDisable()
        {
            if (_merger)
            {
                UniversalOptSys.CircleCollider2Ds.Remove(_circleCollider2D);
            }
        }

        private void FixedUpdate()
        {
            if (UniversalSys.PickupRange != null && _xpCollider2D.Distance(UniversalSys.PickupRange).isOverlapped)
            {
                _running = false;
                UniversalOptSys.CircleCollider2Ds.Remove(_circleCollider2D);
                _xpPickup.StartCoroutine(_xpPickup.PickupCR(UniversalSys.PickupRange.gameObject));
            }

            switch (_running)
            {
                case true when !_merger:
                {
                    if (UniversalOptSys.ShouldRecalculation.sign)
                    {
                        if (Time.time - UniversalOptSys.ShouldRecalculation.time < 0.5)
                        {
                            Merger();
                        }
                        else
                        {
                            UniversalOptSys.ShouldRecalculation.sign = false;
                        }
                    }

                    break;
                }
            }
        }

        private void Merger()
        {
            var circleCollider2D = UniversalOptSys.CircleCollider2Ds.FirstOrDefault(collider2D =>
                _circleCollider2D.Distance(collider2D).isOverlapped);
            if (circleCollider2D == null) return;
            var xp = circleCollider2D.GetComponentInParent<XPPickup>();
            xp.amount += _xpPickup.amount;
            _xpPickup.gameObject.SetActive(false);
        }
    }
}