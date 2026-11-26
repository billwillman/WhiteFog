using System.Collections.Generic;

namespace SoftKitty
{
    /// <summary>One battle's seed and the per-stream ordinals used by callers without a command ID.</summary>
    public sealed class CombatRandomSession
    {
        private readonly Dictionary<ulong, ulong> occurrences = new Dictionary<ulong, ulong>();
        public ulong Seed { get; }

        public CombatRandomSession(ulong seed) { Seed = seed; }

        /// <summary>Same IDs and seed give the same key, regardless of call order or thread.</summary>
        public CombatRoll ForEvent(string purpose, string source, string action, string target, ulong eventId)
            => Stream(purpose, source, action, target).With("command").With(eventId);

        /// <summary>Allocate the next ordinal for this combination of purpose, source, action and target.</summary>
        public CombatRoll NextEvent(string purpose, string source, string action, string target)
        {
            var stream = Stream(purpose, source, action, target);
            lock (occurrences)
            {
                occurrences.TryGetValue(stream.Key, out ulong ordinal);
                occurrences[stream.Key] = checked(ordinal + 1);
                return stream.With(ordinal);
            }
        }

        private CombatRoll Stream(string purpose, string source, string action, string target)
            => new CombatRoll(Seed).With(purpose).With(source).With(action).With(target);
    }
}
