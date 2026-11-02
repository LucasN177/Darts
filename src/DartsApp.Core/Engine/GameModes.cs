using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

public sealed record GameModeInfo(GameMode Mode, string Name, string Short, string Description, string Rules, int MinPlayers = 1, int MaxPlayers = GameEngine.MaxPlayers, bool Training = false);

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
            "Gegner – hier gewinnt die niedrigste Punktzahl. Varianten: Tactics (10 bis 20 und Bull), Random (sechs zufällige " +
            "Zahlen und Bull) und Mickey Mouse (12 bis 20, beliebiges Doppel, beliebiges Triple und Bull)."),
        new(GameMode.AroundTheClock, "Around the Clock", "1 bis 20 und Bull",
            "Alle Zahlen der Reihe nach treffen – ideal zum Aufwärmen.",
            "Jeder Spieler beginnt bei der 1 und muss die Zahlen der Reihe nach treffen, optional zum Schluss das Bull. " +
            "Varianten: nur Doppel oder nur Triple zählen, oder Double/Triple überspringen entsprechend viele Zahlen. " +
            "Wer zuerst durch ist, gewinnt.",
            Training: true),
        new(GameMode.Shanghai, "Shanghai", "Runde = Zahl",
            "In jeder Runde zählt nur eine Zahl – mit Shanghai gewinnt man sofort.",
            "In Runde 1 zählt nur die 1, in Runde 2 nur die 2, usw. Getroffene Felder zählen ihren Wert (Double doppelt, " +
            "Triple dreifach). Wer in einer Aufnahme Single, Double und Triple der Rundenzahl trifft, hat einen Shanghai " +
            "und gewinnt sofort. Sonst gewinnt nach der letzten Runde die höchste Punktzahl."),
        new(GameMode.Bobs27, "Bob's 27", "Doppeltraining",
            "Das bekannte Doppeltraining von Bob Anderson.",
            "Start mit 27 Punkten. In Runde 1 wird auf D1 geworfen, dann D2 bis D20 und zum Schluss Bullseye. Jeder Treffer " +
            "bringt den Wert des Doppels. Wird in einer Aufnahme kein Doppel getroffen, wird der Doppelwert abgezogen. " +
            "Optional scheidet aus, wer auf 0 oder darunter fällt.",
            Training: true),
        new(GameMode.Checkout121, "121 Checkout", "Finish-Training",
            "Checkouts trainieren: ab 121 aufwärts in begrenzten Aufnahmen auschecken.",
            "Pro Versuch stehen (standardmäßig) drei Aufnahmen bzw. neun Darts zur Verfügung, um das Ziel mit Double Out " +
            "auszuchecken. Gelingt der Checkout, steigt das Ziel um eins. Misslingt er, bleibt das Ziel gleich, sinkt um " +
            "eins oder springt auf den Startwert zurück – je nach Einstellung. Gezählt werden erfolgreiche Checkouts.",
            Training: true),
        new(GameMode.HighScore, "High Score", "Punkte sammeln",
            "So viele Punkte wie möglich in einer festen Anzahl Runden.",
            "Jede Aufnahme zählt voll. Nach der eingestellten Rundenzahl gewinnt die höchste Punktzahl. Gut zum Training " +
            "des Scorings auf T20/T19.",
            Training: true),
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
            "trifft, dessen Punkte werden halbiert. Nach neun Runden gewinnt die höchste Punktzahl. Variante Bermuda Triangle: " +
            "13 Runden auf 12, 13, 14, Doppel, 15, 16, 17, Triple, 18, 19, 20, 25 und Bullseye, Start mit 0 Punkten."),
        new(GameMode.Baseball, "Baseball", "9 Innings",
            "Runs sammeln: im n-ten Inning zählt nur die Zahl n.",
            "Gespielt werden (standardmäßig) neun Innings. Im ersten Inning zählt nur die 1, im zweiten die 2 usw. " +
            "Ein Single bringt einen Run, ein Double zwei und ein Triple drei. Nach dem letzten Inning gewinnt, wer die " +
            "meisten Runs hat."),
        new(GameMode.Gotcha, "Gotcha", "Hochzählen & ärgern",
            "Von 0 auf genau 301 – und Gegner auf 0 zurückschicken.",
            "Alle zählen von 0 nach oben. Wer das Ziel (Standard 301) genau erreicht, gewinnt; wer darüber kommt, überwirft " +
            "sich und die Aufnahme zählt nicht. Erreicht ein Spieler mit einem Dart genau den Punktestand eines Gegners, " +
            "fällt dieser auf 0 zurück."),
        new(GameMode.Golf, "Golf", "9 oder 18 Löcher",
            "Wenige Schläge gewinnen – in Loch n zählt die Zahl n.",
            "Gespielt werden 9 oder 18 Löcher; im n-ten Loch zählt nur die Zahl n. Ein Triple ist 1 Schlag, ein Double 2, " +
            "ein Single 3, ohne Treffer gibt es 5 Schläge. Es zählt der letzte Treffer der Aufnahme: Wer mit seinem Ergebnis " +
            "zufrieden ist, beendet die Aufnahme und bleibt stehen. Nach dem letzten Loch gewinnen die wenigsten Schläge."),
        new(GameMode.Scram, "Scram", "Stopper gegen Scorer",
            "Einer schließt die Felder, der andere punktet – danach wird getauscht.",
            "Für genau zwei Spieler. In der ersten Hälfte ist Spieler 1 Stopper: Jeder Treffer schließt das getroffene Feld " +
            "(1 bis 20 und Bull). Spieler 2 ist Scorer und bekommt für jeden Treffer auf ein noch offenes Feld dessen Wert. " +
            "Sind alle Felder geschlossen, werden die Rollen getauscht. Nach beiden Hälften gewinnt die höhere Punktzahl.",
            MinPlayers: 2, MaxPlayers: 2),
        new(GameMode.TicTacToe, "Tic-Tac-Toe", "Drei in einer Reihe",
            "Felder auf einem 3×3-Raster belegen – drei in einer Reihe gewinnen.",
            "Für genau zwei Spieler. Das Raster besteht aus acht zufälligen Zahlen und dem Bull in der Mitte. Wer zuerst drei " +
            "Marks auf ein Feld bringt (Single = 1, Double = 2, Triple = 3), belegt es. Drei belegte Felder in einer Reihe, " +
            "Spalte oder Diagonale gewinnen. Sind alle Felder vergeben, endet die Partie unentschieden.",
            MinPlayers: 2, MaxPlayers: 2),
        new(GameMode.Football, "Football", "Bull = Ballbesitz",
            "Mit dem Bull den Ball erobern, mit Doppeln Tore schießen.",
            "Wer das Bull trifft (25 oder Bullseye), bekommt den Ball. Solange man den Ball hat, zählt jedes Doppel als Tor. " +
            "Ein Gegner erobert den Ball mit einem eigenen Bull-Treffer. Wer zuerst die eingestellte Zahl an Toren erzielt " +
            "(Standard 10), gewinnt."),
        new(GameMode.CheckoutRoulette, "Checkout Roulette", "Zufällige Finishes",
            "Zufällige Checkouts trainieren – jedes Mal ein anderes Ziel.",
            "Pro Versuch wird ein zufälliges, mit drei Darts auscheckbares Ziel im eingestellten Bereich (Standard 2 bis 170) " +
            "gezogen; alle Spieler bekommen dieselben Ziele. Für jeden Versuch steht die eingestellte Anzahl Aufnahmen zur " +
            "Verfügung. Gezählt werden erfolgreiche Checkouts.",
            Training: true),
        new(GameMode.Catch40, "Catch 40", "61 bis 100",
            "Die 40 Checkouts von 61 bis 100 mit je sechs Darts.",
            "Der Reihe nach werden die Ziele 61 bis 100 gespielt, für jedes stehen sechs Darts (zwei Aufnahmen) zur Verfügung. " +
            "Ein Checkout mit zwei Darts bringt 3 Punkte, mit drei Darts 2 Punkte und mit vier bis sechs Darts 1 Punkt. " +
            "Maximal sind 120 Punkte möglich.",
            Training: true),
        new(GameMode.JdcChallenge, "JDC Challenge", "Jugendtraining",
            "Das Trainingsprogramm der Junior Darts Corporation.",
            "Drei Abschnitte: Shanghai auf 10 bis 15 (Treffer zählen ihren Wert, Single + Double + Triple in einer Aufnahme " +
            "bringen 100 Bonuspunkte), danach je ein Dart auf D1 bis D20 und Bull (50 Punkte pro Treffer) und zum Schluss " +
            "Shanghai auf 15 bis 20. Die Gesamtpunktzahl lässt sich gut über die Zeit vergleichen.",
            Training: true),
        new(GameMode.ScoringDrill, "Scoring-Drill", "Trefferquote",
            "Viele Darts auf ein Feld – zum Beispiel 99 Darts auf T20.",
            "Alle Darts gehen auf das gewählte Feld (Standard T20). Gezählt werden die exakten Treffer; zusätzlich zeigt die " +
            "Statistik, wie oft das richtige Segment getroffen wurde und welcher Average dabei herauskam.",
            Training: true),
        new(GameMode.BullTraining, "Bull-Training", "25 & Bullseye",
            "Das Bull gezielt trainieren.",
            "Alle Darts gehen auf das Bull. Ein Treffer auf die 25 bringt 1 Punkt, ein Bullseye 2 Punkte. Nach der eingestellten " +
            "Rundenzahl gewinnt die höchste Punktzahl.",
            Training: true)
    ];

    public static GameModeInfo Get(GameMode mode) => All.First(m => m.Mode == mode);

    public static GameEngine Create(GameSettings settings, IReadOnlyList<PlayerRef> players) => settings.Mode switch
    {
        GameMode.X01 => new X01Engine(settings, players),
        GameMode.Cricket => new CricketEngine(settings, players),
        GameMode.AroundTheClock => new AroundTheClockEngine(settings, players),
        GameMode.Shanghai => new ShanghaiEngine(settings, players),
        GameMode.Bobs27 => new Bobs27Engine(settings, players),
        GameMode.Checkout121 or GameMode.CheckoutRoulette or GameMode.Catch40 => new CheckoutTrainingEngine(settings, players),
        GameMode.HighScore => new HighScoreEngine(settings, players),
        GameMode.Killer => new KillerEngine(settings, players),
        GameMode.HalveIt => new HalveItEngine(settings, players),
        GameMode.Baseball => new BaseballEngine(settings, players),
        GameMode.Gotcha => new GotchaEngine(settings, players),
        GameMode.Golf => new GolfEngine(settings, players),
        GameMode.Scram => new ScramEngine(settings, players),
        GameMode.TicTacToe => new TicTacToeEngine(settings, players),
        GameMode.Football => new FootballEngine(settings, players),
        GameMode.JdcChallenge => new JdcChallengeEngine(settings, players),
        GameMode.ScoringDrill or GameMode.BullTraining => new ScoringDrillEngine(settings, players),
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
