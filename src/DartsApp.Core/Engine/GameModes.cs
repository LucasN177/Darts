using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

public sealed record GameModeInfo(GameMode Mode, string Name, string Short, string Description, string Rules, int MinPlayers = 1);

public static class GameModes
{
    public static readonly IReadOnlyList<GameModeInfo> All =
    [
        new(GameMode.X01, "X01", "301 / 501 / 701",
            "Der Klassiker: Punkte von 501 (oder 301, 701, ...) auf genau 0 herunterspielen.",
            "Jede Aufnahme wird vom Rest abgezogen. Je nach Einstellung muss mit einem Doppel (Double Out) " +
            "oder Doppel/Triple (Master Out) beendet werden. Wer unter 0, auf 1 (bei Double/Master Out) oder ohne " +
            "passendes Feld auf 0 kommt, überwirft sich (Bust) – die Aufnahme zählt nicht. Optional mit Double/Master In, " +
            "mehreren Legs und Sets."),
        new(GameMode.Cricket, "Cricket", "15-20 & Bull",
            "Felder 15 bis 20 und Bull schließen und dabei Punkte sammeln.",
            "Ein Feld ist mit drei Treffern geschlossen (Single = 1, Double = 2, Triple = 3 Marks). Weitere Treffer auf ein " +
            "eigenes geschlossenes Feld bringen Punkte, solange ein Gegner es noch offen hat. Gewonnen hat, wer alle Felder " +
            "geschlossen hat und mindestens so viele Punkte wie alle anderen hat. Bei Cut Throat gehen die Punkte an die " +
            "Gegner – hier gewinnt die niedrigste Punktzahl."),
        new(GameMode.AroundTheClock, "Around the Clock", "1 → 20 → Bull",
            "Alle Zahlen der Reihe nach treffen – ideal zum Aufwärmen.",
            "Jeder Spieler beginnt bei der 1 und muss die Zahlen der Reihe nach treffen, optional zum Schluss das Bull. " +
            "Varianten: nur Doppel oder nur Triple zählen, oder Double/Triple überspringen entsprechend viele Zahlen. " +
            "Wer zuerst durch ist, gewinnt."),
        new(GameMode.Shanghai, "Shanghai", "Runde = Zahl",
            "In jeder Runde zählt nur eine Zahl – mit Shanghai gewinnt man sofort.",
            "In Runde 1 zählt nur die 1, in Runde 2 nur die 2, usw. Getroffene Felder zählen ihren Wert (Double doppelt, " +
            "Triple dreifach). Wer in einer Aufnahme Single, Double und Triple der Rundenzahl trifft, hat einen Shanghai " +
            "und gewinnt sofort. Sonst gewinnt nach der letzten Runde die höchste Punktzahl."),
        new(GameMode.Bobs27, "Bob's 27", "Doppeltraining",
            "Das bekannte Doppeltraining von Bob Anderson.",
            "Start mit 27 Punkten. In Runde 1 wird auf D1 geworfen, dann D2 bis D20 und zum Schluss Bullseye. Jeder Treffer " +
            "bringt den Wert des Doppels. Wird in einer Aufnahme kein Doppel getroffen, wird der Doppelwert abgezogen. " +
            "Optional scheidet aus, wer auf 0 oder darunter fällt."),
        new(GameMode.Checkout121, "121 Checkout", "Finish-Training",
            "Checkouts trainieren: ab 121 aufwärts in begrenzten Aufnahmen auschecken.",
            "Pro Versuch stehen (standardmäßig) drei Aufnahmen bzw. neun Darts zur Verfügung, um das Ziel mit Double Out " +
            "auszuchecken. Gelingt der Checkout, steigt das Ziel um eins. Misslingt er, bleibt das Ziel gleich, sinkt um " +
            "eins oder springt auf den Startwert zurück – je nach Einstellung. Gezählt werden erfolgreiche Checkouts."),
        new(GameMode.HighScore, "High Score", "Punkte sammeln",
            "So viele Punkte wie möglich in einer festen Anzahl Runden.",
            "Jede Aufnahme zählt voll. Nach der eingestellten Rundenzahl gewinnt die höchste Punktzahl. Gut zum Training " +
            "des Scorings auf T20/T19."),
        new(GameMode.Killer, "Killer", "Partyspiel ab 2",
            "Jeder hat eine eigene Zahl – als Killer jagst du die Doppel der anderen.",
            "Zu Beginn bekommt jeder Spieler zufällig eine eigene Zahl. Wer das Doppel seiner Zahl trifft, wird zum Killer. " +
            "Ab dann kostet jeder Treffer auf das Doppel eines Gegners diesen ein Leben. Vorsicht: Trifft ein Killer sein " +
            "eigenes Doppel, verliert er selbst ein Leben. Wer keine Leben mehr hat, scheidet aus – der Letzte gewinnt.",
            MinPlayers: 2),
        new(GameMode.HalveIt, "Halve-It", "Treffen oder halbieren",
            "Neun Runden mit festen Zielen – wer nichts trifft, verliert die Hälfte.",
            "Alle starten mit 40 Punkten. Die Ziele der Runden sind 15, 16, ein beliebiges Doppel, 17, 18, ein beliebiges " +
            "Triple, 19, 20 und Bull. Jeder Treffer auf das Ziel zählt seinen Wert. Wer in einer Runde das Ziel gar nicht " +
            "trifft, dessen Punkte werden halbiert. Nach neun Runden gewinnt die höchste Punktzahl."),
        new(GameMode.Baseball, "Baseball", "9 Innings",
            "Runs sammeln: im n-ten Inning zählt nur die Zahl n.",
            "Gespielt werden (standardmäßig) neun Innings. Im ersten Inning zählt nur die 1, im zweiten die 2 usw. " +
            "Ein Single bringt einen Run, ein Double zwei und ein Triple drei. Nach dem letzten Inning gewinnt, wer die " +
            "meisten Runs hat."),
        new(GameMode.Gotcha, "Gotcha", "Hochzählen & ärgern",
            "Von 0 auf genau 301 – und Gegner auf 0 zurückschicken.",
            "Alle zählen von 0 nach oben. Wer das Ziel (Standard 301) genau erreicht, gewinnt; wer darüber kommt, überwirft " +
            "sich und die Aufnahme zählt nicht. Erreicht ein Spieler mit einem Dart genau den Punktestand eines Gegners, " +
            "fällt dieser auf 0 zurück.")
    ];

    public static GameModeInfo Get(GameMode mode) => All.First(m => m.Mode == mode);

    public static GameEngine Create(GameSettings settings, IReadOnlyList<PlayerRef> players) => settings.Mode switch
    {
        GameMode.X01 => new X01Engine(settings, players),
        GameMode.Cricket => new CricketEngine(settings, players),
        GameMode.AroundTheClock => new AroundTheClockEngine(settings, players),
        GameMode.Shanghai => new ShanghaiEngine(settings, players),
        GameMode.Bobs27 => new Bobs27Engine(settings, players),
        GameMode.Checkout121 => new Checkout121Engine(settings, players),
        GameMode.HighScore => new HighScoreEngine(settings, players),
        GameMode.Killer => new KillerEngine(settings, players),
        GameMode.HalveIt => new HalveItEngine(settings, players),
        GameMode.Baseball => new BaseballEngine(settings, players),
        GameMode.Gotcha => new GotchaEngine(settings, players),
        _ => throw new ArgumentOutOfRangeException(nameof(settings))
    };

    /// <summary>Erzeugt eine Engine und spielt die gespeicherten Darts nach.</summary>
    public static GameEngine Restore(GameSettings settings, IReadOnlyList<PlayerRef> players, IEnumerable<string> darts)
    {
        var engine = Create(settings, players);
        engine.Replay(darts.Select(Dart.Parse));
        return engine;
    }
}
