using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using flanne;
using HarmonyLib;
using Sirenix.Utilities;
using UnityEngine;

namespace UniversalPowerupSystem.ObjectPoolOpt
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public class ObjectPoolOptSys
    {
        public const int UnLimitCount = 10000000;
        public static readonly List<OptTargetData> OptTargets = new List<OptTargetData>();
        public static readonly Dictionary<string, int> TargetsRequire = new Dictionary<string, int>();

        public static readonly Dictionary<string, (LinkedList<GameObject> disActive, LinkedList<GameObject> active)>
            Data = new Dictionary<string, (LinkedList<GameObject> disActive, LinkedList<GameObject> active)>();

        public static void RegOptTarget(string lab,int targetCount = UnLimitCount)
        {
            OptTargets.Add(new OptTargetData(lab, targetCount));
            Data.Add(lab, (new LinkedList<GameObject>(), new LinkedList<GameObject>()));
            TargetsRequire.Add(lab, 0);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ObjectPooler), "Awake")]
        public static void GetPrefabs(ObjectPooler __instance)
        {
            __instance.itemsToPool.Where(item => OptTargets.ContainLabel(item.tag))
                .Where(item => item.objectToPool.GetComponent<PoolItem>() == null).ForEach(item =>
                {
                    item.objectToPool.AddComponent<PoolItem>();
                });
            Data.Values.ForEach((tuple, _) =>
            {
                tuple.active.Clear();
                tuple.disActive.Clear();
            });
            __instance.StartCoroutine(DealRequire(__instance));
        }

        private static IEnumerator DealRequire(ObjectPooler __instance)
        {
            while (__instance != null && __instance.isActiveAndEnabled)
            {
                foreach (var (tag, i) in TargetsRequire)
                {
                    var (disActive, active) = Data[tag];
                    for (var j = 0; j < i; j++)
                    {
                        if (disActive.Count + active.Count >= OptTargets.FindLabel(tag).TargetCount)
                        {
                            TargetsRequire[tag] = 0;
                            break;
                        }

                        RegObject(__instance, tag);
                    }

                    yield return new WaitForFixedUpdate();
                }

                yield return new WaitForSeconds(0.5f);
            }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ObjectPooler), "ObjectPoolItemToPooledObject")]
        public static bool InitPool(ObjectPooler __instance, int index)
        {
            if (__instance.itemsToPool[index] is var item && OptTargets.ContainLabel(item.tag))
            {
                for (var i = 0; i < item.amountToPool; i++)
                {
                    RegObject(__instance, item.tag);
                }

                return false;
            }

            return true;
        }

        private static LinkedListNode<GameObject> RegObject(ObjectPooler pool, string tag)
        {
            var gameObject = Object.Instantiate(pool.itemsToPool.First(item => item.tag == tag).objectToPool,
                pool.transform, true);
            gameObject.SetActive(false);
            var poolItem = gameObject.GetComponent<PoolItem>();
            var linkedListNode = Data[tag].disActive.AddLast(gameObject);
            poolItem.Item = linkedListNode;
            poolItem.DisableLinkedList = Data[tag].disActive;
            poolItem.EnableLinkedList = Data[tag].active;
            return linkedListNode;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ObjectPooler), "GetPooledObject")]
        public static bool RentObject(ObjectPooler __instance, ref GameObject __result, string tag)
        {
            if (OptTargets.ContainLabel(tag))
            {
                var valueTuple = Data[tag];
                if (valueTuple.disActive.Count * 5 < valueTuple.active.Count)
                {
                    TargetsRequire[tag] += 10;
                }

                if (valueTuple.disActive.Count * 10 < valueTuple.active.Count)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        if (valueTuple.disActive.Count + valueTuple.active.Count >=
                            OptTargets.FindLabel(tag).TargetCount)
                        {
                            break;
                        }

                        RegObject(__instance, tag);
                    }
                }

                if (valueTuple.disActive.Count > 0)
                {
                    var gameObject = valueTuple.disActive.First;
                    __result = gameObject.Value;
                    return false;
                }

                if (valueTuple.active.Count < OptTargets.FindLabel(tag).TargetCount)
                {
                    __result = RegObject(__instance, tag).Value;
                    return false;
                }

                __result = null;

                return false;
            }

            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(HordeSpawner), "CountActiveObjects")]
        public static bool Recount(string objectPoolTag, ref int __result)
        {
            if (OptTargets.ContainLabel(objectPoolTag))
            {
                __result = Data[objectPoolTag].active.Count;
                return false;
            }

            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(HordeSpawner), "Spawn")]
        public static bool OptSpawn(string objectPoolTag)
        {
            if (OptTargets.ContainLabel(objectPoolTag) && Data[objectPoolTag].active is var activeList &&
                activeList.Count >= OptTargets.FindLabel(objectPoolTag).TargetCount)
            {
                return false;
            }

            return true;
        }

        public class OptTargetData
        {
            public readonly string Label;
            public readonly int TargetCount;

            public OptTargetData(string label, int targetCount)
            {
                Label = label;
                TargetCount = targetCount;
            }
        }
    }

    public class PoolItem : MonoBehaviour
    {
        public LinkedList<GameObject> DisableLinkedList;
        public LinkedList<GameObject> EnableLinkedList;
        public LinkedListNode<GameObject> Item;

        private void OnEnable()
        {
            if (Item == null) return;
            DisableLinkedList.Remove(Item);
            Item = EnableLinkedList.AddLast(gameObject);
        }

        private void OnDisable()
        {
            if (Item == null) return;
            EnableLinkedList.Remove(Item);
            Item = DisableLinkedList.AddLast(gameObject);
        }
    }

    public static class OptPoolHelper
    {
        public static bool ContainLabel(this List<ObjectPoolOptSys.OptTargetData> list, string label) =>
            list.Select(data => data.Label).Contains(label);

        public static ObjectPoolOptSys.OptTargetData FindLabel(this List<ObjectPoolOptSys.OptTargetData> list,
            string label) =>
            list.FirstOrDefault(data => data.Label == label);
    }
}