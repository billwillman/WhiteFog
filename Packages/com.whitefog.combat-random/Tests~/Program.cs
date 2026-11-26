using System;
using SoftKitty;

internal static class Program
{
    private static CombatRoll SecondHit(CombatRandomSession battle, string target, ulong command)
        => battle.ForEvent("cast", "hero/1", "飞剑", target, command).With("node/4").With(1).With("hit");

    private static CombatRoll Next(CombatRandomSession battle, string target)
        => battle.NextEvent("cast", "hero/1", "飞剑", target);

    private static void Check(bool ok, string reason)
    {
        if (!ok) throw new InvalidOperationException(reason);
    }

    private static void Main()
    {
        var first = new CombatRandomSession(42);
        var replay = new CombatRandomSession(42);
        var hitA = SecondHit(first, "goblin/1", 17);
        var hitB = SecondHit(first, "goblin/2", 17);
        var replayB = SecondHit(replay, "goblin/2", 17);
        replay.ForEvent("cast", "hero/1", "飞剑", "goblin/2", 17).With("node/4").With(1).With("critical");
        Next(replay, "goblin/1");
        SecondHit(replay, "goblin/1", 18);
        Check(replayB.Key == hitB.Key && SecondHit(replay, "goblin/1", 17).Key == hitA.Key, "Order changed the hit");
        Check(hitB.Key == 0x9838361b7f57e800UL && hitB.Index(17) == 10 && hitB.Unit == 0.5946077108383179f, "Replay format changed");
        Check(hitA.Key != hitB.Key && SecondHit(first, "goblin/2", 18).Key != hitB.Key, "Target or command ignored");
        Check(SecondHit(new CombatRandomSession(43), "goblin/2", 17).Key != hitB.Key, "Seed ignored");

        Check(Next(first, "goblin/2").Key == 0x52d609ffbcdb9c3cUL, "First legacy draw changed");
        Next(first, "goblin/1");
        Check(Next(first, "goblin/2").Key == 0xd887b86fdf99f850UL, "Another target advanced the ordinal");
        Check(Next(replay, "goblin/2").Key == 0x52d609ffbcdb9c3cUL, "Sessions shared state");

        Check(new CombatRoll(0).Unit == 0f && new CombatRoll(ulong.MaxValue).Unit < 1f, "Probability endpoints");
        Check(new CombatRoll(0).Range(2, -1) == -1f && new CombatRoll(ulong.MaxValue).Range(-1, 2) == 2f, "Float range endpoints");
        Check(hitB.Index(1) == 0, "Singleton index");
        Console.WriteLine("Combat Random regression passed.");
    }
}
