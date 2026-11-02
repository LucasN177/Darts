using DartsApp.Core.Engine;
using DartsApp.Core.Models;

namespace DartsApp.Core.Bots;

/// <summary>Computergegner: wählt per Spielmodus ein Ziel und simuliert den Wurf.</summary>
public static class Bot
{
    public static Dart NextDart(GameEngine engine, BotLevel level, Random random) =>
        DartBoard.Throw(engine.BotAim(), level.Sigma(), random);

    /// <summary>
    /// Ein Schritt des Computers: entweder ein Wurf oder – wenn der Modus es erlaubt und es sich
    /// lohnt (z.B. Golf) – das vorzeitige Beenden der Aufnahme.
    /// </summary>
    public static void Step(GameEngine engine, BotLevel level, Random random)
    {
        if (engine.BotWantsToEndTurn) engine.FinishTurn();
        else engine.Throw(NextDart(engine, level, random));
    }

    /// <summary>Spielt so lange für Computergegner, bis ein Mensch dran oder das Spiel vorbei ist.</summary>
    public static void PlayUntilHuman(GameEngine engine, Random random, int maxDarts = 10_000)
    {
        for (var i = 0; i < maxDarts && !engine.IsFinished && engine.Players[engine.CurrentPlayer].Bot is { } level; i++)
            Step(engine, level, random);
    }
}
