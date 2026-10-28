using DartsApp.Core.Engine;
using DartsApp.Core.Models;

namespace DartsApp.Core.Bots;

/// <summary>Computergegner: wählt per Spielmodus ein Ziel und simuliert den Wurf.</summary>
public static class Bot
{
    public static Dart NextDart(GameEngine engine, BotLevel level, Random random) =>
        DartBoard.Throw(engine.BotAim(), level.Sigma(), random);

    /// <summary>Spielt so lange für Computergegner, bis ein Mensch dran oder das Spiel vorbei ist.</summary>
    public static void PlayUntilHuman(GameEngine engine, Random random, int maxDarts = 10_000)
    {
        for (var i = 0; i < maxDarts && !engine.IsFinished && engine.Players[engine.CurrentPlayer].Bot is { } level; i++)
            engine.Throw(NextDart(engine, level, random));
    }
}
