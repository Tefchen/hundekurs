namespace Hundekurs.Planung;

/// <summary>
/// Gewählte Ausbildung eines Gespanns. Grundlage ist die Brauchbarkeitsprüfung (Hessen):
/// Grundgehorsam ist Pflichtfach (freie Suche unter dem Schuss, Leinenführigkeit, Standruhe),
/// dazu kommen die Wahlfächer Nachsuche auf Schalenwild und Nachsuche auf Niederwild.
/// </summary>
/// <param name="Gehorsam">Teil 1 (Grundgehorsam an den Sonntagen ab März).</param>
/// <param name="Schalenwild">Nachsuche auf Schalenwild (Schweißarbeit, Teil 2 mit Schweißgruppe).</param>
/// <param name="Niederwild">Nachsuche auf Niederwild (Apportierarbeit, mittwochs).</param>
/// <param name="BrauchbarkeitBestanden">Der Hund hat die Brauchbarkeit bereits bestanden.</param>
public sealed record Ausbildung(bool Gehorsam, bool Schalenwild, bool Niederwild, bool BrauchbarkeitBestanden = false)
{
    /// <summary>Jagdlich ist, wer eine der Nachsuchen macht; sonst zivil (nur Grundgehorsam).</summary>
    public bool Jagdlich => Schalenwild || Niederwild;

    /// <summary>
    /// Teil 1 und die übrigen Fächer des Grundgehorsams sind Pflicht für alle jagdlichen Gespanne.
    /// Ausnahme: Wer nur Niederwild macht und die Brauchbarkeit schon bestanden hat. Bei Schalenwild gilt das nicht –
    /// wer Schweiß macht, macht alles.
    /// </summary>
    public bool Teil1Pflicht => Schalenwild || (Niederwild && !BrauchbarkeitBestanden);

    /// <summary>Die 60-%-Regel gilt genau dann, wenn Teil 1 Pflicht ist. Für zivile Gespanne hat Fehlen keine Folgen.</summary>
    public bool ZulassungErforderlich => Teil1Pflicht;

    /// <summary>Gehorsam ist bei Pflicht immer dabei.</summary>
    public Ausbildung Bereinigt() => Teil1Pflicht && !Gehorsam ? this with { Gehorsam = true } : this;

    public bool Leer => !Gehorsam && !Schalenwild && !Niederwild;

    public string Text => Leer ? "–" : string.Join(" · ", new[]
    {
        Gehorsam ? "Grundgehorsam" : null,
        Schalenwild ? "Schalenwild" : null,
        Niederwild ? "Niederwild" : null,
    }.Where(x => x is not null));
}
