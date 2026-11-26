using System.Runtime.CompilerServices;
using UnityEngine;

namespace SoftKitty
{
    internal static class SkillDefence
    {
        private sealed class State
        {
            public float shield, shieldUntil, bonusPercent, weakUntil;
        }
        private static ConditionalWeakTable<AttributeData, State> states = new ConditionalWeakTable<AttributeData, State>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => states = new ConditionalWeakTable<AttributeData, State>();

        internal static void Shield(AttributeData hp, float amount, float seconds)
        {
            var s = states.GetValue(hp, _ => new State());
            s.shield = Mathf.Max(0, amount);
            s.shieldUntil = Time.time + Mathf.Max(0, seconds);
        }

        internal static void Weaken(AttributeData hp, float percent, float seconds)
        {
            var s = states.GetValue(hp, _ => new State());
            s.bonusPercent = Mathf.Max(0, percent);
            s.weakUntil = Time.time + Mathf.Max(0, seconds);
        }

        internal static float ApplyVulnerability(AttributeData attribute, float amount)
        {
            if (amount <= 0 || !states.TryGetValue(attribute, out var s)) return amount;
            return Time.time < s.weakUntil ? Mathf.Floor(amount * (1f + s.bonusPercent * .01f) + .5f) : amount;
        }

        internal static float ShieldAbsorption(AttributeData attribute, float amount)
        {
            if (amount <= 0 || !states.TryGetValue(attribute, out var s) || Time.time >= s.shieldUntil) return 0;
            return Mathf.Min(s.shield, amount);
        }

        internal static void SpendShield(AttributeData attribute, float absorbed)
        {
            if (absorbed > 0 && states.TryGetValue(attribute, out var s)) s.shield = Mathf.Max(0, s.shield - absorbed);
        }

        internal static float Incoming(AttributeData attribute, float amount)
        {
            amount = ApplyVulnerability(attribute, amount);
            float absorbed = ShieldAbsorption(attribute, amount);
            SpendShield(attribute, absorbed);
            return amount - absorbed;
        }
    }
}
