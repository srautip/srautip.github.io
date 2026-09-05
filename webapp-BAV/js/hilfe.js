/* =====================================================================
   Erläuterungen zu den Eingabefeldern – Wissensbasis für den Einsatz
   in der Mitarbeiterschulung.

   Je Feld: fachliche Bedeutung, sämtliche Ausprägungen, Auswirkung auf
   die Berechnung, ausgelöste Befunde und ein Praxishinweis.
   Die Schlüssel der Ausprägungen entsprechen exakt den Aufzählungstypen
   in engine.js; die Testfälle prüfen das ab.
   ===================================================================== */
(function (global) {
  'use strict';

  var STAMMDATEN = {

    /* ------------------------------------------------------------------ */
    id: {
      titel: 'Kennung des Anrechts',
      recht: 'kein Rechtsbegriff',
      woher: 'frei vergeben – keine Angabe der Trägerauskunft',
      bedeutung: 'Interne Bezeichnung, unter der dieses Anrecht in der Reiterleiste, in der ' +
        'Übersicht aller Anrechte und im Tenorvorschlag wieder auftaucht. Sie hat keinerlei ' +
        'rechtliche Bedeutung und geht nicht in die Berechnung ein.',
      auspraegungen: [
        { name: 'freier Text', text: 'Üblich sind die Versicherungsschein- oder Personalnummer, ' +
          'ein Kürzel des Trägers oder eine laufende Nummer.' }
      ],
      wirkung: ['Keine. Der Wert wird nur zur Wiedererkennung angezeigt.'],
      befunde: [],
      praxis: 'Hat der Inhaber mehrere Anrechte, sollte die Kennung eindeutig sein – in der ' +
        'Übersicht lassen sich die Anrechte sonst nicht auseinanderhalten. Im späteren Beschluss ' +
        'wird das Anrecht ohnehin über Versorgungsträger und Versicherungs- bzw. Personalnummer ' +
        'bezeichnet, nicht über diese Kennung.'
    },

    /* ------------------------------------------------------------------ */
    traeger: {
      titel: 'Versorgungsträger',
      recht: '§ 219 Nr. 2, 3 FamFG, § 4 VersAusglG',
      woher: 'Briefkopf der Trägerauskunft',
      bedeutung: 'Wer die Versorgung schuldet beziehungsweise durchführt. Der Versorgungsträger ' +
        'ist am Verfahren zu beteiligen, muss die Auskunft erteilen und vollzieht später die ' +
        'angeordnete Teilung.',
      auspraegungen: [
        { name: 'Arbeitgeber', text: 'Bei der Direktzusage sagt der Arbeitgeber die Leistung ' +
          'unmittelbar zu und ist selbst Versorgungsträger.' },
        { name: 'Versorgungseinrichtung', text: 'Bei Pensionskasse, Pensionsfonds, ' +
          'Direktversicherung und Unterstützungskasse führt eine eigenständige Einrichtung die ' +
          'Versorgung durch; sie erteilt die Auskunft und vollzieht die Teilung.' }
      ],
      wirkung: ['Keine rechnerische Auswirkung.'],
      befunde: [],
      praxis: 'Der Name gehört genau so erfasst, wie er auf der Auskunft steht – bei einer ' +
        'externen Teilung ist er zusammen mit der Zielversorgung Bestandteil des Tenors. ' +
        'Achtung bei Konzernen: auskunftgebende Stelle und Versorgungsträger sind nicht immer ' +
        'dieselbe juristische Person.'
    },

    /* ------------------------------------------------------------------ */
    durchfuehrungsweg: {
      titel: 'Durchführungsweg',
      recht: '§ 1b BetrAVG, §§ 45, 17 VersAusglG',
      woher: 'Trägerauskunft, Beschreibung der Zusage',
      bedeutung: 'Auf welchem organisatorischen Weg der Arbeitgeber die Versorgung zusagt. ' +
        'Das Feld ist die wichtigste Weichenstellung der ganzen Prüfung: Es bestimmt die ' +
        'erwartete Bewertungsmethode und den Grenzwert, bis zu dem extern geteilt werden darf.',
      auspraegungen: [
        { wert: 'DIREKTZUSAGE', name: 'Direktzusage (unmittelbare Versorgungszusage)',
          text: 'Der Arbeitgeber sagt die Leistung selbst zu und bildet dafür Rückstellungen. ' +
            'Es gibt keinen externen Kapitalstock, den man ablesen könnte – bewertet wird ' +
            'deshalb zeitratierlich nach § 2 Abs. 1 BetrAVG. Besonderheit: Nach § 17 VersAusglG ' +
            'darf der Träger die externe Teilung einseitig bis zur Beitragsbemessungsgrenze der ' +
            'gesetzlichen Rentenversicherung verlangen – ein sehr viel höherer Grenzwert als ' +
            'der allgemeine des § 14.' },
        { wert: 'UKASSE', name: 'Unterstützungskasse',
          text: 'Rechtlich selbstständige Versorgungseinrichtung, die formal keinen Rechtsanspruch ' +
            'gewährt; einstandspflichtig bleibt der Arbeitgeber (§ 1 Abs. 1 S. 3 BetrAVG). ' +
            'Bewertung und Grenzwert wie bei der Direktzusage: zeitratierlich, § 17 VersAusglG.' },
        { wert: 'PENSIONSKASSE', name: 'Pensionskasse',
          text: 'Rechtlich selbstständige Versorgungseinrichtung mit Rechtsanspruch, ' +
            'versicherungsförmig und der Versicherungsaufsicht unterstellt. Es existiert ein ' +
            'Deckungskapital, das unmittelbar bewertet werden kann. Für die externe Teilung ' +
            'gelten nur die allgemeinen Grenzwerte des § 14 Abs. 2 Nr. 2 VersAusglG.' },
        { wert: 'PENSIONSFONDS', name: 'Pensionsfonds',
          text: 'Versicherungsförmiger Weg mit freierer Kapitalanlage als die Pensionskasse. ' +
            'Bewertung und Grenzwerte wie bei der Pensionskasse. Wegen der Kapitalanlage schwankt ' +
            'das Deckungskapital stärker – ein Rückgang in der Ehezeit ist hier eher erklärbar ' +
            'als bei einer klassischen Direktversicherung.' },
        { wert: 'DIREKTVERSICHERUNG', name: 'Direktversicherung',
          text: 'Der Arbeitgeber schließt eine Lebens- oder Rentenversicherung auf das Leben des ' +
            'Arbeitnehmers ab. Maßgeblich ist der Übertragungswert nach § 4 Abs. 5 BetrAVG, also ' +
            'das gebildete Kapital. Unmittelbare Bewertung, allgemeine Grenzwerte des § 14.' }
      ],
      wirkung: [
        'Schritt 1: Bestimmt, welche Bewertungsmethode erwartet wird (Befund GD07).',
        'Schritt 3: Nur bei Direktzusage und Unterstützungskasse wird der Rechnungszins gegen ' +
          'die gesonderte Zinsgrenze geprüft (Befund KW06).',
        'Schritt 6: Direktzusage und Unterstützungskasse werden gegen die Beitragsbemessungsgrenze ' +
          'geprüft (§ 17), alle übrigen Wege gegen 2 % beziehungsweise 240 % der monatlichen ' +
          'Bezugsgröße (§ 14 Abs. 2 Nr. 2).',
        'Schritt 6: Bestimmt die Auffangzielversorgung, wenn der Berechtigte keine wählt – ' +
          'Versorgungsausgleichskasse bei Direktzusage und Unterstützungskasse, sonst die ' +
          'gesetzliche Rentenversicherung (§ 15 Abs. 5 VersAusglG).',
        'Schritt 6: Nur bei der Direktzusage wird das Vorliegen einer Teilungsordnung geprüft ' +
          '(Befund TA06).'
      ],
      befunde: ['GD07 – Bewertungsmethode untypisch für den Durchführungsweg',
                'GD08 – versicherungsförmiger Weg ohne Deckungskapital',
                'KW06 – hoher Rechnungszins bei Direktzusage oder Unterstützungskasse',
                'TA06 – interne Teilung einer Direktzusage ohne Teilungsordnung'],
      praxis: 'Der Unterschied zwischen § 14 und § 17 ist in der Praxis gewaltig: Bei einer ' +
        'Pensionskasse endet die einseitige externe Teilung schon bei rund 8.500 € Kapital, bei ' +
        'einer Direktzusage erst bei der vollen Beitragsbemessungsgrenze. Wer den ' +
        'Durchführungsweg falsch erfasst, bekommt in Schritt 6 regelmäßig die falsche Teilungsart.'
    },

    /* ------------------------------------------------------------------ */
    zusageart: {
      titel: 'Zusageart',
      recht: '§ 1 Abs. 1, Abs. 2 BetrAVG',
      woher: 'Versorgungszusage beziehungsweise Trägerauskunft',
      bedeutung: 'Was der Arbeitgeber inhaltlich zugesagt hat – eine bestimmte Leistung, die ' +
        'Umwandlung von Beiträgen in Anwartschaften oder Beiträge mit einer garantierten ' +
        'Mindestleistung.',
      auspraegungen: [
        { wert: 'LEISTUNGSZUSAGE', name: 'Leistungszusage',
          text: 'Zugesagt ist eine bestimmte Leistung, etwa 500 € Monatsrente oder ein Prozentsatz ' +
            'des letzten Gehalts. Der klassische Fall der zeitratierlichen Bewertung: Die volle ' +
            'Leistung wird erst mit vollständiger Betriebszugehörigkeit verdient, der Ehezeitanteil ' +
            'entsteht anteilig über die Zeit.' },
        { wert: 'BEITRAGSORIENTIERT', name: 'Beitragsorientierte Leistungszusage',
          text: 'Der Arbeitgeber wandelt festgelegte Beiträge nach einem Umrechnungsschlüssel in ' +
            'Anwartschaften um (Bausteinsystem, § 1 Abs. 2 Nr. 1 BetrAVG). Jeder Baustein ist dem ' +
            'Jahr zuzuordnen, in dem er entstanden ist – deshalb darf hier auch eine unmittelbare ' +
            'Bewertung erfolgen, selbst bei einer Direktzusage. Die App unterdrückt in diesem Fall ' +
            'den Befund GD07.' },
        { wert: 'BEITRAGSZUSAGE_MIT_MINDESTLEISTUNG', name: 'Beitragszusage mit Mindestleistung',
          text: 'Der Arbeitgeber sagt die Beitragszahlung und mindestens die Summe der zugesagten ' +
            'Beiträge zu, vermindert um verbrauchte Risikobeiträge (§ 1 Abs. 2 Nr. 2 BetrAVG). ' +
            'Die Garantie ist kapitalbezogen definiert; eine Darstellung nur als Monatsrente ' +
            'verdeckt sie. Deshalb Befund ER04.' }
      ],
      wirkung: [
        'Schritt 1: Eine beitragsorientierte Zusage rechtfertigt die unmittelbare Bewertung auch ' +
          'dort, wo der Durchführungsweg eigentlich zeitratierlich bewertet wird – Ausnahme zu GD07.',
        'Schritt 8: Bei einer Beitragszusage mit Mindestleistung in Rentenform wird ein Hinweis ' +
          'ausgegeben (ER04).'
      ],
      befunde: ['GD07 – Ausnahmeregel für beitragsorientierte Zusagen',
                'ER04 – Mindestleistung muss auf Kapitalbasis dargestellt werden'],
      praxis: 'Die reine Beitragszusage des Sozialpartnermodells (§ 1 Abs. 2 Nr. 2a BetrAVG, ' +
        '„Nahles-Rente") ist hier bewusst nicht abgebildet – sie kennt keine Garantie und wirft ' +
        'im Versorgungsausgleich eigene Fragen auf. Solche Anrechte gehören in die manuelle ' +
        'Bearbeitung.'
    },

    /* ------------------------------------------------------------------ */
    einheit: {
      titel: 'Einheit des Anrechts',
      recht: '§ 5 Abs. 3, § 18 Abs. 3, § 14 Abs. 2 Nr. 2 VersAusglG',
      woher: 'Trägerauskunft – in welcher Größe Ehezeitanteil und Ausgleichswert genannt sind',
      bedeutung: 'Ob der Träger den Ehezeitanteil als monatliche Rente oder als Kapitalbetrag ' +
        'vorschlägt. Die Einheit steuert sämtliche Grenzwertvergleiche, denn Bagatell- und ' +
        'Externteilungsgrenzen sind für Renten und Kapitalwerte völlig unterschiedlich definiert.',
      auspraegungen: [
        { wert: 'RENTE_MONAT', name: 'Monatsrente',
          text: 'Ehezeitanteil und Ausgleichswert werden in Euro pro Monat angegeben. Grenzwerte: ' +
            'Bagatelle 1 % der monatlichen Bezugsgröße, externe Teilung 2 %. Zusätzlich prüft die ' +
            'App den Barwertfaktor und rechnet Teilungskosten aus Euro in Rente um.' },
        { wert: 'KAPITALWERT', name: 'Kapitalwert',
          text: 'Ehezeitanteil und Ausgleichswert werden als einmaliger Eurobetrag angegeben. ' +
            'Grenzwerte: Bagatelle 120 % der monatlichen Bezugsgröße, externe Teilung 240 %. ' +
            'Ehezeitanteil und korrespondierender Kapitalwert müssen dann übereinstimmen (KW02).' }
      ],
      wirkung: [
        'Schritt 3: Bei Monatsrente werden Barwertfaktor und Näherungsbarwert geprüft (KW03, KW04), ' +
          'bei Kapitalwert der Abgleich mit dem korrespondierenden Kapitalwert (KW02).',
        'Schritt 5: Wählt die Bagatellgrenze – 1 % oder 120 % der monatlichen Bezugsgröße.',
        'Schritt 6: Wählt den allgemeinen Grenzwert der externen Teilung – 2 % oder 240 %.',
        'Schritt 7: Bei Monatsrente werden die in Euro genannten Teilungskosten proportional zum ' +
          'Kapitalwert in Rente umgerechnet.'
      ],
      befunde: ['KW02 – Ehezeitanteil ≠ korrespondierender Kapitalwert (nur Kapitalanrechte)',
                'KW03, KW04 – Barwertprüfungen (nur Rentenanrechte)',
                'BG01 – Bagatellgrenze je nach Einheit'],
      praxis: 'Häufigster Erfassungsfehler: Der Ehezeitanteil wird als Monatsrente eingetragen, die ' +
        'Einheit steht aber auf Kapitalwert. Das Ergebnis ist dann keine Warnung, sondern eine ' +
        'still falsche Bagatellprüfung – 250 € Monatsrente lägen scheinbar unter der Kapitalgrenze ' +
        'von rund 4.200 €. Einheit und Zahlenwert immer gemeinsam kontrollieren.'
    },

    /* ------------------------------------------------------------------ */
    bewertung: {
      titel: 'Bewertungsmethode',
      recht: '§ 45 VersAusglG, § 2 Abs. 1 BetrAVG, § 4 Abs. 5 BetrAVG',
      woher: 'Trägerauskunft – Herleitung des Ehezeitanteils',
      bedeutung: 'Wie der Ehezeitanteil aus dem gesamten Anrecht abgeleitet wird. § 45 Abs. 1 ' +
        'VersAusglG räumt dem Träger ein Wahlrecht ein: Er kann den Übertragungswert nach ' +
        '§ 4 Abs. 5 BetrAVG als Bezugsgröße wählen; andernfalls gilt die zeitratierliche ' +
        'Bewertung nach § 2 Abs. 1 BetrAVG.',
      auspraegungen: [
        { wert: 'UNMITTELBAR', name: 'unmittelbar',
          text: 'Maßgeblich ist der in der Ehezeit tatsächlich erwirtschaftete Zuwachs – bei ' +
            'versicherungsförmigen Wegen der Zuwachs des Deckungskapitals zwischen Ehezeitbeginn ' +
            'und Ehezeitende. Nachrechenbar über Abschnitt 6 des Formulars. Die Prüfung vergleicht ' +
            'den Zuwachs mit dem ausgewiesenen Ehezeitanteil.' },
        { wert: 'ZEITRATIERLICH', name: 'zeitratierlich',
          text: 'Der Ehezeitanteil ergibt sich aus dem Gesamtanrecht im Verhältnis m/n: m sind die ' +
            'Monate, in denen sich Ehezeit und Dienstzeit überschneiden, n die gesamte mögliche ' +
            'Betriebszugehörigkeit vom Diensteintritt bis zur festen Altersgrenze. Nachrechenbar ' +
            'über Abschnitt 5 des Formulars.' }
      ],
      wirkung: [
        'Schritt 1: Wird gegen den Durchführungsweg abgeglichen (GD07).',
        'Schritt 2: Entscheidet, welcher Prüfstrang läuft – die m/n-Nachrechnung (EA03 bis EA07) ' +
          'oder der Abgleich mit dem Deckungskapital (EA08 bis EA10).',
        'Formular: Der jeweils nicht benötigte Abschnitt 5 oder 6 wird ausgegraut.'
      ],
      befunde: ['GD07 – untypische Methode für den Durchführungsweg',
                'EA03 bis EA07 – Prüfungen der zeitratierlichen Bewertung',
                'EA08 bis EA10 – Prüfungen der unmittelbaren Bewertung'],
      praxis: 'Die Methode wird nicht frei gewählt, sondern aus der Auskunft abgelesen: Nennt der ' +
        'Träger Diensteintritt und feste Altersgrenze, rechnet er zeitratierlich. Nennt er ' +
        'Deckungskapital zu zwei Stichtagen, rechnet er unmittelbar. Fehlen beide Angaben, ist die ' +
        'Auskunft nicht nachrechenbar und nachzufordern.'
    },

    /* ------------------------------------------------------------------ */
    unverfallbar: {
      titel: 'Unverfallbarkeit der Anwartschaft',
      recht: '§ 1b BetrAVG, § 19 Abs. 2 Nr. 1 VersAusglG',
      woher: 'Trägerauskunft; bei Zweifeln aus der Zusage und dem Eintrittsdatum herzuleiten',
      bedeutung: 'Ob die Anwartschaft dem Arbeitnehmer erhalten bleibt, wenn er vor dem ' +
        'Versorgungsfall ausscheidet. Nur eine unverfallbare Anwartschaft ist ausgleichsreif. ' +
        'Maßgeblich ist der Stand zum Ehezeitende.',
      auspraegungen: [
        { name: 'gesetzt – unverfallbar',
          text: 'Das Anrecht ist ausgleichsreif und wird im Wertausgleich bei der Scheidung ' +
            'geteilt. Die Prüfung läuft über alle acht Schritte.' },
        { name: 'nicht gesetzt – noch verfallbar',
          text: 'Das Anrecht ist nach § 19 Abs. 2 Nr. 1 VersAusglG nicht ausgleichsreif. Es wird ' +
            'jetzt nicht geteilt; der Ausgleich bleibt dem schuldrechtlichen Verfahren nach ' +
            '§§ 20 ff. VersAusglG vorbehalten. Die App bricht nach Schritt 1 mit dem Status ' +
            'SCHULDRECHTLICH_VORBEHALTEN ab.' }
      ],
      wirkung: ['Schritt 1: Ein verfallbares Anrecht beendet die Prüfung sofort – ohne Berechnung ' +
                'eines Ausgleichswerts.'],
      befunde: ['GD05 – Anwartschaft verfallbar, nicht ausgleichsreif',
                'GD06 – laut Datum inzwischen unverfallbar'],
      praxis: 'Die Fristen des § 1b Abs. 1 BetrAVG hängen vom Zusagedatum ab: Für ab 2018 erteilte ' +
        'Zusagen genügen drei Jahre Zusagedauer und das vollendete 21. Lebensjahr, für ältere ' +
        'Zusagen galten fünf Jahre und ein höheres Mindestalter. Entgeltumwandlung ist nach ' +
        '§ 1b Abs. 5 BetrAVG von Anfang an unverfallbar – dort ist das Feld praktisch immer gesetzt.'
    },

    /* ------------------------------------------------------------------ */
    unverfallbar_ab: {
      titel: 'Unverfallbar ab',
      recht: '§ 1b BetrAVG, § 5 Abs. 2 S. 2 VersAusglG',
      woher: 'Trägerauskunft oder Berechnung aus Zusagedatum und Geburtsdatum',
      bedeutung: 'Das Datum, zu dem die Unverfallbarkeit eintritt oder eingetreten ist. Nur ' +
        'auszufüllen, wenn die Anwartschaft zum Ehezeitende noch verfallbar war.',
      auspraegungen: [
        { name: 'leer', text: 'Keine Prüfung. Sinnvoll, wenn das Anrecht ohnehin unverfallbar ist ' +
            'oder der Eintritt nicht bekannt ist.' },
        { name: 'Datum in der Zukunft', text: 'Die Anwartschaft ist auch heute noch verfallbar. ' +
            'Es bleibt beim Vorbehalt des schuldrechtlichen Ausgleichs.' },
        { name: 'Datum in der Vergangenheit', text: 'Die Unverfallbarkeit ist zwischenzeitlich ' +
            'eingetreten. Befund GD06 fordert eine aktualisierte Auskunft.' }
      ],
      wirkung: ['Schritt 1: Wird gegen das Prüfdatum aus Abschnitt 1 verglichen – nicht gegen den ' +
                'heutigen Tag, damit sich Altfälle reproduzierbar nachvollziehen lassen.'],
      befunde: ['GD06 – laut Datum inzwischen unverfallbar, Auskunft aktualisieren'],
      praxis: 'Tritt die Unverfallbarkeit erst nach dem Ehezeitende ein, ist das nach ' +
        'ständiger Rechtsprechung eine auf den Ehezeitanteil zurückwirkende Veränderung im Sinne ' +
        'des § 5 Abs. 2 S. 2 VersAusglG und deshalb noch zu berücksichtigen. Das Anrecht wird dann ' +
        'doch geteilt – die Auskunft muss aber neu angefordert werden.'
    },

    /* ------------------------------------------------------------------ */
    laufende_leistung: {
      titel: 'Anrecht im Leistungsbezug',
      recht: '§ 30 VersAusglG, § 41 VersAusglG',
      woher: 'Trägerauskunft – Anwartschaft oder laufende Leistung',
      bedeutung: 'Ob der Inhaber bereits eine Betriebsrente bezieht oder das Anrecht noch eine ' +
        'Anwartschaft ist.',
      auspraegungen: [
        { name: 'nicht gesetzt – Anwartschaft',
          text: 'Der Regelfall. Die Teilung wirkt sich erst im späteren Versorgungsfall aus.' },
        { name: 'gesetzt – laufende Leistung',
          text: 'Die Rente wird bereits gezahlt. Die Kürzung beim Verpflichteten greift erst ab ' +
            'Rechtskraft der Entscheidung; bis dahin erbrachte Leistungen sind nach § 30 ' +
            'VersAusglG geschützt. Eine externe Teilung wirft hier zusätzliche Fragen auf, weil ' +
            'die Zielversorgung eine sofort beginnende Leistung abbilden müsste.' }
      ],
      wirkung: [
        'Schritt 6: Bei externer Teilung wird zusätzlich gewarnt (TA05).',
        'Schritt 8: Hinweis auf den Leistungsschutz und den Kürzungszeitpunkt (ER03).'
      ],
      befunde: ['TA05 – externe Teilung einer laufenden Rente',
                'ER03 – Kürzung ab Rechtskraft, § 30 VersAusglG prüfen'],
      praxis: 'Bei laufenden Renten ist die zeitratierliche Bewertung in der Regel nicht mehr ' +
        'einschlägig – maßgeblich ist der Barwert der bereits laufenden Leistung. Weist die ' +
        'Auskunft trotzdem eine m/n-Rechnung aus, lohnt eine Rückfrage.'
    },

    /* ------------------------------------------------------------------ */
    kapitalwahlrecht_ausgeuebt: {
      titel: 'Kapitalwahlrecht ausgeübt',
      recht: '§ 2 Abs. 2 Nr. 3 VersAusglG, §§ 1372 ff. BGB',
      woher: 'Trägerauskunft oder Angabe des Inhabers',
      bedeutung: 'Ob ein vertraglich eingeräumtes Wahlrecht auf Kapitalauszahlung statt Rente ' +
        'bereits wirksam ausgeübt wurde. Entscheidend ist die Ausübung bis zum Ehezeitende.',
      auspraegungen: [
        { name: 'nicht gesetzt',
          text: 'Der Regelfall. Das Anrecht bleibt auf Versorgung gerichtet und wird im ' +
            'Versorgungsausgleich geteilt – auch dann, wenn es als Kapitalwert ausgewiesen ist ' +
            '(§ 2 Abs. 2 Nr. 3 VersAusglG stellt betriebliche Kapitalanrechte ausdrücklich gleich).' },
        { name: 'gesetzt – Wahlrecht ausgeübt',
          text: 'Das Anrecht hat sich in eine reine Kapitalforderung verwandelt. Nach ' +
            'herrschender Auffassung unterliegt es dann dem Zugewinnausgleich und nicht mehr dem ' +
            'Versorgungsausgleich. Die App bricht nach Schritt 1 mit dem Status ' +
            'NICHT_VA_SONDERN_ZUGEWINN ab.' }
      ],
      wirkung: ['Schritt 1: Beendet die Prüfung sofort, ohne Berechnung eines Ausgleichswerts.'],
      befunde: [],
      praxis: 'Nicht verwechseln: Das bloße Bestehen eines Kapitalwahlrechts ändert nichts – das ' +
        'Anrecht bleibt im Versorgungsausgleich. Erst die tatsächliche, wirksame Ausübung bis zum ' +
        'Ehezeitende führt hinaus. Wird das Wahlrecht erst nach dem Ehezeitende ausgeübt, bleibt ' +
        'es beim Versorgungsausgleich.'
    },

    /* ------------------------------------------------------------------ */
    traeger_teilungsordnung_vorhanden: {
      titel: 'Teilungsordnung des Trägers',
      recht: '§ 10 Abs. 1, § 11 VersAusglG',
      woher: 'Anlage zur Trägerauskunft',
      bedeutung: 'Die Regelung, nach welcher der Versorgungsträger die interne Teilung vollzieht: ' +
        'wie das Anrecht für den Berechtigten begründet wird, welchen Risikoschutz es hat und wie ' +
        'es sich weiterentwickelt.',
      auspraegungen: [
        { name: 'gesetzt – liegt vor',
          text: 'Das Gericht kann die interne Teilung anordnen und im Tenor auf die ' +
            'Teilungsordnung Bezug nehmen.' },
        { name: 'nicht gesetzt – fehlt',
          text: 'Bei interner Teilung einer Direktzusage warnt die App (TA06). Ohne die Regelung ' +
            'lässt sich nicht feststellen, ob die gleichwertige Teilhabe nach § 11 VersAusglG ' +
            'gewahrt ist; die Teilungsordnung ist anzufordern.' }
      ],
      wirkung: ['Schritt 6: Wird nur geprüft, wenn intern geteilt wird und es sich um eine ' +
                'Direktzusage handelt.'],
      befunde: ['TA06 – interne Teilung ohne Teilungsordnung'],
      praxis: '§ 11 VersAusglG verlangt gleichwertige Teilhabe: ein eigenständiges Anrecht mit ' +
        'vergleichbarer Wertentwicklung und grundsätzlich gleichem Risikoschutz. Streitpunkte ' +
        'sind regelmäßig der Ausschluss der Hinterbliebenenversorgung und abweichende ' +
        'Rechnungsgrundlagen für das neu begründete Anrecht.'
    }
  };

  global.BAV = global.BAV || {};
  global.BAV.hilfe = { STAMMDATEN: STAMMDATEN };
})(typeof window !== 'undefined' ? window : this);
