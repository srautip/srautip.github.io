#!/usr/bin/perl
# Zieht die Designsystem-Kopien in einer Viewer-Vorlage nach.
#
# Die Vorlagen tragen ZEICHENGLEICHE Kopien von design-tokens.css und
# design-basis.css (arc42 8.16; warum Kopie statt Injektion, steht im
# Kopf der CSS-Datei). Aendert sich eine Quelle, meldet
# DesignTokenTests die Abweichung - nachziehen tut sie dieses Skript.
#
# Aufruf (aus timetable-dotnet/):
#   perl tools/design-einsetzen.pl TimetableWorkflow/Templates/stundentafel.html
#
# Optional laesst sich das vorlageneigene CSS aus einer Datei ersetzen:
#   perl tools/design-einsetzen.pl <vorlage.html> <eigen.css>
# Ohne dieses Argument bleibt das eigene CSS der Vorlage unveraendert -
# es wird aus ihr selbst gelesen (alles nach dem Basis-Endmarker).
#
# Danach: `dotnet build TimetableCore.sln`, `render` fuer beide
# Beispielschulen, dann TimetableWorkflow.Tests + TimetableViewer.Tests
# und tools/viewer-smoke.ps1.
#
# ZWEI FALLSTRICKE, beide live erlebt:
#  - Die Vorlagen sind CRLF. Das Skript arbeitet deshalb binaer (:raw)
#    und schreibt ausdruecklich CRLF; `sed -i` wuerde sie still auf LF
#    umschreiben und einen Diff ueber die ganze Datei erzeugen.
#  - Die Reihenfolge ist bedeutsam: Tokens, dann Basis, dann eigenes
#    CSS. `#meta` und `#controls` sind ID-Selektoren - stuende die Basis
#    hinten, wuerde sie jede spezifischere Ueberschreibung still
#    clobbern. DesignTokenTests.DieBasisStehtVorDenEigenenRegeln haelt
#    das fest.
use strict;
use warnings;

my ($vorlage, $eigenDatei) = @ARGV;
die "Aufruf: perl tools/design-einsetzen.pl <vorlage.html> [eigen.css]\n" unless $vorlage;

my $ordner = $vorlage;
$ordner =~ s{[^/\\]+$}{};
$ordner = '.' if $ordner eq '';
my $tokQuelle = $ordner . 'design-tokens.css';
my $basQuelle = $ordner . 'design-basis.css';

sub lies {
    my ($p) = @_;
    local $/;
    open my $fh, '<:raw', $p or die "$p: $!\n";
    my $t = <$fh>;
    close $fh;
    return $t;
}

sub region {
    my ($text, $marke) = @_;
    (my $n = $text) =~ s/\r\n/\n/g;
    my ($r) = $n =~ m{(/\* == \Q$marke\E:.*?/\* == ENDE \Q$marke\E == \*/)}s;
    return $r;
}

my $tok = region(lies($tokQuelle), 'DESIGN-TOKENS')
    or die "$tokQuelle: Marker DESIGN-TOKENS nicht gefunden\n";
my $bas = region(lies($basQuelle), 'DESIGN-BASIS')
    or die "$basQuelle: Marker DESIGN-BASIS nicht gefunden\n";

my $html = lies($vorlage);
my $vorher = length $html;

# Das vorlageneigene CSS: entweder aus der uebergebenen Datei, oder -
# und das ist der Regelfall - aus der Vorlage selbst, naemlich alles
# nach dem Basis-Endmarker bis </style>.
my $eigen;
if (defined $eigenDatei) {
    $eigen = lies($eigenDatei);
} else {
    my ($stil) = $html =~ m{<style>(.*?)</style>}s
        or die "$vorlage: kein <style>-Block\n";
    my $ende = index($stil, '== ENDE DESIGN-BASIS == */');
    die "$vorlage: noch nicht umgestellt (kein Basis-Endmarker) - dann eigen.css angeben\n" if $ende < 0;
    $eigen = substr($stil, $ende + length('== ENDE DESIGN-BASIS == */'));
}
$eigen =~ s/\r\n/\n/g;
$eigen =~ s/^\s+//;
$eigen =~ s/\s+$//;

my $inhalt = "\n$tok\n\n$bas\n\n  $eigen\n";
$inhalt =~ s/\n/\r\n/g;

$html =~ s{(<style>).*?(</style>)}{$1 . $inhalt . $2}se
    or die "$vorlage: kein <style>-Block\n";

# Schutz gegen genau den Unfall, der dieses Skript einmal beinahe eine
# Vorlage haette leeren lassen: nie schreiben, wenn der Inhalt
# unplausibel geschrumpft ist.
die "$vorlage: Inhalt waere von $vorher auf " . length($html) . " B geschrumpft - Abbruch\n"
    if length($html) < $vorher * 0.5;

open my $out, '>:raw', $vorlage or die "$vorlage: $!\n";
print $out $html;
close $out;

printf "%s: Tokens %d B, Basis %d B, eigen %d B (%d -> %d)\n",
    $vorlage, length($tok), length($bas), length($eigen), $vorher, length($html);
