namespace SoftKitty
{
    /// <summary>The active MCC battle. Standalone simulations can each own a CombatRandomSession.</summary>
    public static class CombatRandom
    {
        private static CombatRandomSession session = new CombatRandomSession(0);
        public static ulong Seed => session.Seed;

        public static void BeginBattle(ulong seed)
        {
            session = new CombatRandomSession(seed);
        }

        internal static CombatRoll Event(string purpose, string source, string action, string target, ulong? eventId = null)
        {
            return eventId.HasValue
                ? session.ForEvent(purpose, source, action, target, eventId.Value)
                : session.NextEvent(purpose, source, action, target);
        }
    }
}
