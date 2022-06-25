namespace UniversalPowerupSystem.ObjectPoolOpt
{
    public static class PoolOptInit
    {
        static PoolOptInit()
        {
            ObjectPoolOptSys.RegOptTarget("BrainMonster", 3000);
            ObjectPoolOptSys.RegOptTarget("Boomer", 500);
            ObjectPoolOptSys.RegOptTarget("EyeMonster", 750);
            ObjectPoolOptSys.RegOptTarget("Lamprey", 2400);
            ObjectPoolOptSys.RegOptTarget("SmallXP");
            ObjectPoolOptSys.RegOptTarget("LargeXP");
            ObjectPoolOptSys.RegOptTarget("BulletImpact");
            ObjectPoolOptSys.RegOptTarget("EnemyDeathFX");
            ObjectPoolOptSys.RegOptTarget("Burn");
            ObjectPoolOptSys.RegOptTarget("DamagePopup");
            ObjectPoolOptSys.RegOptTarget("EyeMonsterProjectile");
        }
    }
}