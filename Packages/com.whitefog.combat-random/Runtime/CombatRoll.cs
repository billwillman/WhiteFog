using System;

namespace SoftKitty
{
    /// <summary>An immutable random key. Derive a child key for each named purpose or draw slot.</summary>
    public readonly struct CombatRoll
    {
        public ulong Key { get; }
        public CombatRoll(ulong seed) { Key = seed; }

        // SplitMix64 finalizer. All operations have explicitly defined wraparound.
        public CombatRoll With(ulong id)
        {
            unchecked
            {
                ulong z = (Key ^ id) + 0x9e3779b97f4a7c15UL;
                z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9UL;
                z = (z ^ (z >> 27)) * 0x94d049bb133111ebUL;
                return new CombatRoll(z ^ (z >> 31));
            }
        }

        // FNV-1a over UTF-16 code units; null and the empty string represent an absent ID.
        public CombatRoll With(string id)
        {
            ulong hash = 14695981039346656037UL;
            if (id != null)
                foreach (char c in id) hash = unchecked((hash ^ c) * 1099511628211UL);
            return With(hash);
        }

        /// <summary>A sample in [0, 1), suitable for comparing against a probability.</summary>
        public float Unit => (Key >> 40) * (1f / 16777216f);

        /// <summary>A uniformly distributed integer in [0, count).</summary>
        public int Index(int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            uint n = (uint)count;
            uint threshold = unchecked(0u - n) % n;
            var draw = this;
            // Rejection uses child keys of this draw, so a retry cannot advance another event.
            for (ulong retry = 1; ; retry++)
            {
                ulong product = (draw.Key >> 32) * n;
                if (unchecked((uint)product) >= threshold) return (int)(product >> 32);
                draw = With(retry);
            }
        }

        /// <summary>A floating-point sample between the supplied endpoints, both inclusive.</summary>
        public float Range(float a, float b)
        {
            if (a == b) return a;
            if (a > b) (a, b) = (b, a);
            return (float)(a + ((double)b - a) * ((Key >> 40) / 16777215.0));
        }
    }
}
