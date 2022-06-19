using flanne;
using UnityEngine;

namespace UniversalPowerupSystem
{
    public class NullPowerup : Powerup
    {
        public Powerup Init()
        {
            icon = UniversalSys.NullIcon;
            nameStringID = "Null";
            isRepeatable = false;
            return this;
        }

        public override string description => "空升级，什么用都没有";

        public override void Apply(GameObject target)
        {
        }
    }
}