using System.Collections.Generic;
using flanne;
using HarmonyLib;
using UnityEngine;

namespace UniversalPowerupSystem.ProjectileOpt
{
    public class MoveComponent2DOpt : MonoBehaviour
    {
        public LinkedListNode<MoveComponent2D> Item;
        public bool Disable;

        public static readonly LinkedList<MoveComponent2D> Active = new LinkedList<MoveComponent2D>();
        public static readonly LinkedList<MoveComponent2D> DisActive = new LinkedList<MoveComponent2D>();

        private void OnEnable()
        {
            if (Disable) return;
            var moveComponent2D = Item.Value;
            DisActive.Remove(Item);
            Item = Active.AddLast(moveComponent2D);
        }

        private void OnDisable()
        {
            if (Disable) return;
            var moveComponent2D = Item.Value;
            Active.Remove(Item);
            Item = DisActive.AddLast(moveComponent2D);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(MoveComponent2D), "Start")]
        public static void RegOptComponent(MoveComponent2D __instance)
        {
            __instance.gameObject.AddComponent<MoveComponent2DOpt>();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(MoveSystem2D), "Awake")]
        public static void StateInit()
        {
            Active.Clear();
            DisActive.Clear();
        }

        [HarmonyPrefix, HarmonyPatch(typeof(MoveSystem2D), "Register")]
        public static bool PatchRegComponent(MoveComponent2D m)
        {
            if (m.isActiveAndEnabled)
            {
                var linkedListNode = Active.AddLast(m);
                var moveComponent2DOpt = m.GetComponent<MoveComponent2DOpt>();
                moveComponent2DOpt.Item = linkedListNode;
                moveComponent2DOpt.Disable = false;
            }
            else
            {
                var linkedListNode = DisActive.AddLast(m);
                var moveComponent2DOpt = m.GetComponent<MoveComponent2DOpt>();
                moveComponent2DOpt.Item = linkedListNode;
                moveComponent2DOpt.Disable = false;
            }

            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(MoveSystem2D), "UnRegister")]
        public static bool PatchUnReg(MoveComponent2D m)
        {
            if (m.isActiveAndEnabled)
            {
                var moveComponent2DOpt = m.GetComponent<MoveComponent2DOpt>();
                Active.Remove(moveComponent2DOpt.Item);
                moveComponent2DOpt.Item = null;
                moveComponent2DOpt.Disable = true;
            }
            else
            {
                var moveComponent2DOpt = m.GetComponent<MoveComponent2DOpt>();
                DisActive.Remove(moveComponent2DOpt.Item);
                moveComponent2DOpt.Item = null;
                moveComponent2DOpt.Disable = true;
            }

            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(MoveSystem2D), "FixedUpdate")]
        public static bool OptMove()
        {
            foreach (var moveComponent2D in Active)
            {
                var num = moveComponent2D.vector.magnitude;
                num *= Mathf.Exp(-moveComponent2D.drag * Time.fixedDeltaTime);
                moveComponent2D.vector = Mathf.Max(0f, num) * moveComponent2D.vector.normalized;
                moveComponent2D.Rb.MovePosition(moveComponent2D.Rb.position +
                                                moveComponent2D.vector * Time.fixedDeltaTime);
                if (!moveComponent2D.rotateTowardsMove) continue;
                var rotation =
                    Quaternion.AngleAxis(Mathf.Atan2(moveComponent2D.vector.y, moveComponent2D.vector.x) * 57.29578f,
                        Vector3.forward);
                moveComponent2D.transform.rotation = rotation;
                moveComponent2D.transform.localScale = moveComponent2D.vector.x < 0f
                    ? new Vector3(1f, -1f, 1f)
                    : new Vector3(1f, 1f, 1f);
            }

            return false;
        }
    }
}