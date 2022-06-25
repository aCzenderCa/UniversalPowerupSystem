using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using flanne;
using UnityEngine;

namespace UniversalPowerupSystem.Util
{
    public interface IWeight
    {
        float Weight { get; set; }
    }

    public static class CustomPowerupPoolHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // ReSharper disable once MemberCanBePrivate.Global
        public static IEnumerable<Powerup> ToPowerupEnum(this IEnumerable<CustomPowerup> list) =>
            list.Select(powerup => powerup.Powerup);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsPowerup(this IEnumerable<CustomPowerup> list, Powerup p) =>
            list.ToPowerupEnum().Contains(p);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsRepeatable(this CustomPowerup customPowerup) => customPowerup.Powerup.isRepeatable;

        public static T GetRandom<T>(this List<T> list) => list[Random.Range(0, list.Count)];

        public static T GetWeightRandom<T>(this List<T> list)
            where T : IWeight
        {
            var randW = Random.Range(0, list.Sum(weight => weight.Weight));
            return list.FirstOrDefault(weight =>
            {
                randW -= weight.Weight;
                return randW <= 0;
            });
        }
    }
}