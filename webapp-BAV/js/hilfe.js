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

  var TRAEGERAUSKUNFT = {

    /* ------------------------------------------------------------------ */
    ehezeitanteil: {
      titel: 'Ehezeitanteil',
      recht: '§ 39, § 45 VersAusglG',
      woher: 'Trägerauskunft – die zentrale Wertangabe',
      bedeutung: 'Der Teil des Anrechts, der in der Ehezeit erworben wurde. Er ist die ' +
        'Bezugsgröße der gesamten Berechnung: Aus ihm entsteht durch Halbteilung der ' +
        'Ausgleichswert, und an ihm bemisst sich die Angemessenheit der Teilungskosten.',
      auspraegungen: [
        { name: 'positiver Wert – der Regelfall',
          text: 'Die Prüfung rechnet den Wert in Schritt 2 nach – zeitratierlich über m/n oder ' +
            'über den Zuwachs des Deckungskapitals – und leitet in Schritt 4 die Halbteilung ab.' },
        { name: 'null',
          text: 'In der Ehezeit wurde nichts erworben. Hinweis EA02; ein Ausgleich findet nicht ' +
            'statt. Plausibel etwa, wenn das Dienstverhältnis erst nach dem Ehezeitende begann.' },
        { name: 'negativer Wert',
          text: 'Fachlich unmöglich. Fehler EA01, die Prüfung endet mit Abbruch.' }
      ],
      wirkung: [
        'Schritt 2: Wird gegen die Nachrechnung geprüft (m/n beziehungsweise Kapitalzuwachs).',
        'Schritt 4: Halber Ehezeitanteil ist der Sollwert der Halbteilung.',
        'Schritt 7: Bei Rentenanrechten Basis der Umrechnung der Eurokosten in Rente.',
        'Schritt 8: Obergrenze des Ausgleichsbetrags – mehr als die Hälfte darf nie übertragen ' +
          'werden (ER02).'
      ],
      befunde: ['EA01 – negativer Ehezeitanteil', 'EA02 – kein Erwerb in der Ehezeit',
                'EA04 – Abweichung zur m/n-Nachrechnung', 'EA06 – Ehezeitanteil > Gesamtanrecht',
                'EA09 – Abweichung zum Zuwachs des Deckungskapitals',
                'HT03 – Ausgleichswert nicht als Halbteilung nachvollziehbar'],
      praxis: 'Nicht mit dem Gesamtanrecht verwechseln: Der Ehezeitanteil ist der ehezeitbezogene ' +
        'Ausschnitt, das Gesamtanrecht die volle erreichbare Anwartschaft. Und die Einheit ' +
        'beachten – der Wert muss in derselben Größe stehen, die in den Stammdaten gewählt ist.'
    },

    /* ------------------------------------------------------------------ */
    ausgleichswert: {
      titel: 'Ausgleichswert laut Träger',
      recht: '§ 1 Abs. 1, § 5 Abs. 3 VersAusglG',
      woher: 'Trägerauskunft – Vorschlag des Trägers',
      bedeutung: 'Der Wert, den der Träger auf den Berechtigten übertragen will. Nach dem ' +
        'Halbteilungsgrundsatz ist das die Hälfte des Ehezeitanteils. Die App übernimmt ihn nicht, ' +
        'sondern rechnet ihn in Schritt 4 nach.',
      auspraegungen: [
        { name: 'genau die Hälfte des Ehezeitanteils',
          text: 'Der Regelfall. Teilungskosten sind noch nicht abgezogen und werden in Schritt 7 ' +
            'berücksichtigt.' },
        { name: '(Ehezeitanteil − Teilungskosten) / 2',
          text: 'Der Träger hat die Kosten schon vor der Halbteilung abgezogen. Die App erkennt ' +
            'das (HT01) und zieht sie später kein zweites Mal ab.' },
        { name: 'passend zum halben kostenbereinigten Kapitalwert',
          text: 'Bei Rentenanrechten rechnen Träger die Eurokosten oft über den Kapitalwert um. ' +
            'Erkannt wird das nur, wenn zusätzlich der Ausgleichswert als Kapital erfasst ist (HT02).' },
        { name: 'keine dieser Varianten',
          text: 'Der Wert ist nicht herleitbar. Fehler HT03, die Prüfung endet mit Abbruch – die ' +
            'Auskunft ist zu beanstanden.' }
      ],
      wirkung: [
        'Schritt 4: Gegenstand der Halbteilungsprüfung.',
        'Schritt 5: Wird gegen die Bagatellgrenze gehalten.',
        'Schritt 6: Wird bei den allgemeinen Grenzwerten des § 14 Abs. 2 Nr. 2 verglichen.'
      ],
      befunde: ['HT01 – Kosten bereits im Ausgleichswert enthalten',
                'HT02 – Kostenabzug auf Kapitalwertebene erkannt',
                'HT03 – nicht als Halbteilung nachvollziehbar',
                'BG01 – Geringfügigkeit', 'TA01 – externe Teilung ohne Rechtsgrundlage'],
      praxis: 'Wichtig für das Verständnis der Schritte 5 und 6: Bagatellgrenze und ' +
        '§-14-Grenzwert werden gegen diesen Wert des Trägers geprüft, nicht gegen den von der App ' +
        'nachgerechneten. Der Vorschlag des Trägers bindet das Gericht nicht – er ist Prüfstoff.'
    },

    /* ------------------------------------------------------------------ */
    ausgleichswert_kapital: {
      titel: 'Ausgleichswert als Kapital',
      recht: '§ 13, § 47 VersAusglG',
      woher: 'Trägerauskunft, sofern dort zusätzlich ein Kapitalbetrag genannt ist',
      bedeutung: 'Ein zusätzlicher Kapitalbetrag, den manche Träger bei Rentenanrechten neben der ' +
        'Monatsrente ausweisen. Er dient hier einem einzigen Zweck: zu erkennen, ob der Träger die ' +
        'Teilungskosten auf Kapitalwertebene abgezogen hat.',
      auspraegungen: [
        { name: 'leer – der Regelfall',
          text: 'Keine zusätzliche Prüfung. Bei Kapitalanrechten ist das Feld ohnehin ohne ' +
            'Bedeutung, weil der Ausgleichswert dort bereits ein Eurobetrag ist.' },
        { name: 'Betrag angegeben',
          text: 'Passt er zu (Kapitalwert − Teilungskosten) / 2, erkennt die App den Vorabzug ' +
            'auf Kapitalebene (HT02) und vermeidet den doppelten Abzug.' }
      ],
      wirkung: ['Schritt 4: Wird nur ausgewertet, wenn die Einheit Monatsrente ist und ' +
                'Teilungskosten geltend gemacht werden.'],
      befunde: ['HT02 – Kostenabzug auf Kapitalwertebene erkannt',
                'HT03 – ohne diesen Wert bleibt die Halbteilung unter Umständen unauflösbar'],
      praxis: 'Meldet die App bei einem Rentenanrecht mit Teilungskosten den Fehler HT03, lohnt ' +
        'der Blick in die Auskunft, ob dort ein Kapitalbetrag steht. Oft löst allein dieser Wert ' +
        'den vermeintlichen Fehler auf.'
    },

    /* ------------------------------------------------------------------ */
    korr_kapitalwert: {
      titel: 'Korrespondierender Kapitalwert',
      recht: '§ 47 VersAusglG, § 4 Abs. 5 BetrAVG',
      woher: 'Trägerauskunft – Pflichtangabe',
      bedeutung: 'Der Kapitalbetrag, der dem Ehezeitanteil entspricht: der Übertragungswert nach ' +
        '§ 4 Abs. 5 BetrAVG oder der mit dem Rechnungszins des Trägers ermittelte Barwert. Er ist ' +
        'die gemeinsame Währung, in der sich Renten- und Kapitalanrechte vergleichen lassen.',
      auspraegungen: [
        { name: 'positiver Wert – der Regelfall',
          text: 'Bei Kapitalanrechten muss er mit dem Ehezeitanteil übereinstimmen (KW02), bei ' +
            'Rentenanrechten wird er über den Barwertfaktor und eine Näherungsrechnung geprüft ' +
            '(KW03, KW04).' },
        { name: 'null oder leer bei positivem Ehezeitanteil',
          text: 'Fehler KW01. Ohne Kapitalwert sind weder Bagatellgrenze noch externe Teilung noch ' +
            'die Kostenquote prüfbar – die Auskunft ist unvollständig.' }
      ],
      wirkung: [
        'Schritt 3: Gegenstand aller Kapitalwertprüfungen.',
        'Schritt 6: Die Hälfte dieses Werts wird bei Direktzusage und Unterstützungskasse gegen ' +
          'die Beitragsbemessungsgrenze gehalten (§ 17 VersAusglG).',
        'Schritt 6: Die Hälfte gilt als das Kapital, das bei externer Teilung in die ' +
          'Zielversorgung fließt – Grundlage der Transferverlustrechnung.',
        'Schritt 7: Nenner der Kostenquote und Umrechnungsschlüssel für Kosten bei Rentenanrechten.'
      ],
      befunde: ['KW01 – Kapitalwert 0 bei positivem Ehezeitanteil',
                'KW02 – Ehezeitanteil ≠ Kapitalwert', 'KW03 – Barwertfaktor außerhalb der Spanne',
                'KW04 – Abweichung zur Näherungsrechnung', 'TK03 – Kostenquote über der Obergrenze'],
      praxis: 'Der wirkungsvollste Hebel im ganzen Verfahren. Ein niedrig gerechneter Kapitalwert ' +
        'kann ein Anrecht unter die Grenze des § 17 drücken und damit erst die einseitige externe ' +
        'Teilung eröffnen – zum Nachteil des Berechtigten. Deshalb gehören Rechnungszins und ' +
        'Rechnungsgrundlagen hier immer mitgeprüft.'
    },

    /* ------------------------------------------------------------------ */
    rechnungszins: {
      titel: 'Rechnungszins',
      recht: '§ 47 Abs. 4 VersAusglG',
      woher: 'Trägerauskunft – Angabe zu den Rechnungsgrundlagen',
      bedeutung: 'Der Zinssatz, mit dem der Träger künftige Leistungen auf den Stichtag abzinst. ' +
        'Der Zusammenhang ist gegenläufig: Je höher der Zins, desto niedriger der Kapitalwert und ' +
        'damit der Wert des Anrechts.',
      auspraegungen: [
        { name: 'innerhalb der konfigurierten Spanne',
          text: 'Vorbelegt sind 0,5 % bis 6 % (Abschnitt 8 der Formulare). Keine Beanstandung.' },
        { name: 'außerhalb der Spanne',
          text: 'Warnung KW05. Der Träger muss die Herleitung offenlegen; ein unüblicher Zins ' +
            'verschiebt den Kapitalwert erheblich.' },
        { name: 'über der gesonderten Grenze bei Direktzusage oder Unterstützungskasse',
          text: 'Warnung KW06. Vorbelegt sind 3,5 %. Ein hoher Zins ist hier besonders kritisch, ' +
            'weil er zugleich Grenzwertprüfung und Transferverlust beeinflusst.' }
      ],
      wirkung: [
        'Schritt 3: Zwei Plausibilitätsprüfungen und Eingangsgröße der Barwert-Näherung (KW04).'
      ],
      befunde: ['KW05 – Rechnungszins außerhalb der üblichen Spanne',
                'KW06 – hoher Zins bei Direktzusage oder Unterstützungskasse'],
      praxis: 'Bei Direktzusagen wird oft mit einem handels- oder steuerrechtlich geprägten Zins ' +
        'gerechnet, der mit dem Marktniveau wenig zu tun hat. Der Effekt ist doppelt: Der ' +
        'Kapitalwert sinkt, das Anrecht rutscht eher unter Grenzwerte, und bei externer Teilung ' +
        'wächst der Transferverlust. Der Zins ist deshalb einer der häufigsten Streitpunkte.'
    },

    /* ------------------------------------------------------------------ */
    ehezeit_monate_traeger: {
      titel: 'Ehezeitmonate laut Träger',
      recht: '§ 3 Abs. 1 VersAusglG',
      woher: 'Trägerauskunft – Angabe zur zugrunde gelegten Ehezeit',
      bedeutung: 'Die Monatszahl, mit der der Träger gerechnet hat. Das Feld hat keinen eigenen ' +
        'Rechenwert; es dient allein dem Abgleich mit der Ehezeit des Verfahrens aus Abschnitt 1.',
      auspraegungen: [
        { name: 'gleich der Monatszahl des Verfahrens',
          text: 'Träger und Gericht legen dieselbe Ehezeit zugrunde. Die Prüfung läuft weiter.' },
        { name: 'abweichend',
          text: 'Fehler GD02 mit sofortigem Abbruch. Bei abweichender Ehezeit ist der gesamte ' +
            'Ehezeitanteil falsch abgegrenzt – jede weitere Rechnung wäre wertlos.' }
      ],
      wirkung: ['Schritt 1: Abbruchkriterium. Ohne übereinstimmende Ehezeit wird nicht gerechnet.'],
      befunde: ['GD02 – Ehezeitmonate weichen ab'],
      praxis: 'Weicht die Zahl um genau einen Monat ab, ist fast immer die Zählweise die Ursache ' +
        'und kein Rechenfehler: Die Ehezeit läuft vom ersten Tag des Heiratsmonats bis zum letzten ' +
        'Tag des Monats vor Zustellung, und beide Monate zählen mit. Bei größeren Abweichungen ' +
        'stimmt meist das Zustellungsdatum nicht.'
    },

    /* ------------------------------------------------------------------ */
    teilungskosten: {
      titel: 'Teilungskosten',
      recht: '§ 13 VersAusglG',
      woher: 'Trägerauskunft – stets als Eurobetrag, auch bei Rentenanrechten',
      bedeutung: 'Die Kosten, die der Träger für den Vollzug der internen Teilung geltend macht. ' +
        'Sie werden mit dem Ehezeitanteil verrechnet, so dass beide Ehegatten sie hälftig tragen.',
      auspraegungen: [
        { name: 'null',
          text: 'Der Träger erhebt keine Kosten (Hinweis TK02). Der volle halbe Ehezeitanteil ' +
            'wird übertragen.' },
        { name: 'übliche Pauschale',
          text: 'Angemessen sind nach der Rechtsprechung regelmäßig 2 bis 3 % des Ehezeitanteils, ' +
            'begrenzt der Höhe nach. Die Grenzen sind in Abschnitt 8 einstellbar.' },
        { name: 'über der Kostenquote',
          text: 'Warnung TK03. Bei kleinen Anrechten kann eine Mindestpauschale die Quote ' +
            'zwangsläufig überschreiten – dann zusätzlich Hinweis TK05.' },
        { name: 'über der absoluten Obergrenze',
          text: 'Warnung TK04. Vorbelegt sind 500 €.' },
        { name: 'negativer Wert',
          text: 'Fachlich unmöglich. Fehler TK01.' }
      ],
      wirkung: [
        'Schritt 4: Grundlage der Prüfung, ob der Träger die Kosten bereits vorab abgezogen hat.',
        'Schritt 7: Angemessenheitsprüfung und hälftiger Abzug; bei Rentenanrechten werden die ' +
          'Eurokosten proportional zum Kapitalwert in Rente umgerechnet.',
        'Schritt 8: Mindert den Ausgleichsbetrag.'
      ],
      befunde: ['TK01 bis TK07 – sämtliche Kostenprüfungen',
                'HT01, HT02 – Erkennung eines bereits erfolgten Abzugs'],
      praxis: 'Zwei Fallen. Erstens: Bei externer Teilung dürfen keine Kosten abgezogen werden ' +
        '(Hinweis TK06) – § 13 gilt nur für die interne Teilung. Zweitens der Doppelabzug: Hat der ' +
        'Träger die Kosten schon im Ausgleichswert berücksichtigt, darf man sie nicht erneut ' +
        'abziehen. Genau das prüft Schritt 4, bevor Schritt 7 rechnet.'
    },

    /* ------------------------------------------------------------------ */
    teilungsart_vorschlag: {
      titel: 'Vorschlag des Trägers zur Teilungsart',
      recht: '§§ 10, 14, 17 VersAusglG',
      woher: 'Trägerauskunft – Antrag beziehungsweise Vorschlag',
      bedeutung: 'Welche Art der Teilung der Träger vorschlägt. Der Vorschlag bindet das Gericht ' +
        'nicht: Die App prüft in Schritt 6, ob er von einer Rechtsgrundlage getragen wird.',
      auspraegungen: [
        { wert: 'INTERN', name: 'interne Teilung',
          text: 'Wird ohne weitere Prüfung übernommen. Die interne Teilung ist der gesetzliche ' +
            'Regelfall des § 10 VersAusglG und immer zulässig – beim Berechtigten entsteht ein ' +
            'eigenständiges Anrecht beim selben Träger.' },
        { wert: 'EXTERN', name: 'externe Teilung',
          text: 'Nur zulässig mit Zustimmung des Berechtigten (§ 14 Abs. 2 Nr. 1), innerhalb der ' +
            'allgemeinen Grenzwerte (§ 14 Abs. 2 Nr. 2) oder – bei Direktzusage und ' +
            'Unterstützungskasse – bis zur Beitragsbemessungsgrenze (§ 17). Trägt keine dieser ' +
            'Grundlagen, ordnet die App die interne Teilung an und meldet TA01.' }
      ],
      wirkung: [
        'Schritt 6: Ausgangspunkt der Prüfung der Teilungsart.',
        'Schritt 7: Bei externer Teilung entfällt der Kostenabzug vollständig.'
      ],
      befunde: ['TA01 – externe Teilung ohne Rechtsgrundlage verlangt',
                'TA02 bis TA05 – Folgeprüfungen der externen Teilung'],
      praxis: 'Die Asymmetrie ist beabsichtigt: Ein interner Vorschlag wird übernommen, ein ' +
        'externer muss sich rechtfertigen. Verlangt der Träger extern und ist der Grenzwert ' +
        'überschritten, hat man zwei Wege – interne Teilung anordnen oder die Zustimmung des ' +
        'Berechtigten einholen.'
    },

    /* ------------------------------------------------------------------ */
    auskunftsdatum: {
      titel: 'Datum der Auskunft',
      recht: '§ 5 Abs. 2 VersAusglG',
      woher: 'Trägerauskunft – Erstellungsdatum',
      bedeutung: 'Wann der Träger die Auskunft erstellt hat. Bewertungsstichtag ist und bleibt das ' +
        'Ehezeitende; das Erstellungsdatum sagt nur etwas über die Belastbarkeit der Angaben.',
      auspraegungen: [
        { name: 'nach dem Ehezeitende und aktuell',
          text: 'Der Regelfall, keine Beanstandung.' },
        { name: 'vor dem Ehezeitende',
          text: 'Warnung GD03. Zum Zeitpunkt der Erstellung stand der Ehezeitanteil noch nicht ' +
            'fest – die Auskunft kann ihn nicht endgültig ausweisen.' },
        { name: 'mehr als zwölf Monate vor dem Prüfdatum',
          text: 'Warnung GD04. Rechtlich bleibt der Stichtag maßgeblich, zwischenzeitliche ' +
            'rückwirkende Änderungen sind aber nach § 5 Abs. 2 S. 2 VersAusglG zu berücksichtigen.' }
      ],
      wirkung: ['Schritt 1: Beide Prüfungen laufen gegen das Prüfdatum aus Abschnitt 1, nicht ' +
                'gegen den heutigen Tag – so bleiben Altfälle reproduzierbar nachvollziehbar.'],
      befunde: ['GD03 – Auskunft vor Ehezeitende erstellt', 'GD04 – Auskunft älter als 12 Monate'],
      praxis: 'Die beiden Warnungen bedeuten Unterschiedliches. GD03 heißt: neu anfordern, der ' +
        'Wert kann nicht stimmen. GD04 heißt nur: prüfen, ob sich rückwirkend etwas geändert hat – ' +
        'etwa der Eintritt der Unverfallbarkeit oder eine korrigierte Zusage.'
    }
  };

  /* ====================================================================
     Befunde: typische Ursachen und konkrete Handlungsschritte.
     Titel, Rechtsgrundlage und die fachliche Erläuterung stehen im
     Katalog der Engine; hier steht, was zu tun ist.
     ==================================================================== */
  var BEFUNDE = {
    /* ---- Schritt 1: Grunddaten ---- */
    GD01: { ursachen: ['Die Auskunft enthält die gesetzlichen Pflichtangaben nicht vollständig.',
                       'Angaben liegen vor, sind aber noch nicht in das Formular übertragen.',
                       'Der Träger verwendet ein eigenes Formular ohne die Angaben nach § 5 VersAusglG.'],
            massnahmen: ['Die fehlenden Werte in der Auskunft suchen und nachtragen.',
                         'Ergänzende Auskunft nach § 4 Abs. 1 VersAusglG anfordern.',
                         'Bei Weigerung des Trägers Auskunftsanordnung des Gerichts erwirken.'] },
    GD02: { ursachen: ['Der Träger legt ein anderes Zustellungsdatum des Scheidungsantrags zugrunde.',
                       'Anfangs- oder Endmonat wurde nicht mitgezählt.',
                       'Der Träger rechnet in vollen Kalendermonaten statt nach § 3 Abs. 1 VersAusglG.'],
            massnahmen: ['Zustellungsdatum aus der Akte prüfen.',
                         'Ehezeit neu abgrenzen: erster Tag des Heiratsmonats bis letzter Tag des Monats vor Zustellung, beide Monate zählen mit.',
                         'Liegt der Fehler beim Träger, korrigierte Auskunft anfordern.'] },
    GD03: { ursachen: ['Die Auskunft stammt aus einem früheren Verfahren oder einer Vorabanfrage.',
                       'Der Träger hat auf einen anderen Stichtag gerechnet.'],
            massnahmen: ['Auskunft zum Stichtag Ehezeitende neu anfordern.',
                         'Die vorliegende Auskunft nicht als Berechnungsgrundlage verwenden.'] },
    GD04: { ursachen: ['Langes Verfahren, die Auskunft wurde früh eingeholt.'],
            massnahmen: ['Prüfen, ob rückwirkende Änderungen eingetreten sind – eingetretene Unverfallbarkeit, korrigierte Zusage, Nachverrechnung (§ 5 Abs. 2 S. 2 VersAusglG).',
                         'Bei Zweifeln aktualisierte Auskunft anfordern.'],
            vertiefung: 'Der Bewertungsstichtag bleibt das Ehezeitende. Das Alter der Auskunft macht sie nicht falsch – es erhöht nur die Wahrscheinlichkeit, dass zwischenzeitlich etwas passiert ist, das zurückwirkt.' },
    GD05: { ursachen: ['Die Zusage bestand zum Ehezeitende noch nicht lange genug.',
                       'Das nach § 1b BetrAVG erforderliche Lebensalter war noch nicht erreicht.'],
            massnahmen: ['Das Anrecht im Beschluss dem schuldrechtlichen Ausgleich vorbehalten (§ 19 Abs. 4 VersAusglG).',
                         'Prüfen, ob die Unverfallbarkeit zwischenzeitlich eingetreten ist – dann gilt Befund GD06.'] },
    GD06: { ursachen: ['Das Verfahren dauert an, die Frist des § 1b BetrAVG ist inzwischen erfüllt.'],
            massnahmen: ['Aktualisierte Auskunft anfordern.',
                         'Das Anrecht wird dann doch im Wertausgleich bei der Scheidung geteilt.'],
            vertiefung: 'Der nachträgliche Eintritt der Unverfallbarkeit ist eine auf den Ehezeitanteil zurückwirkende Veränderung im Sinne des § 5 Abs. 2 S. 2 VersAusglG und deshalb zu berücksichtigen.' },
    GD07: { ursachen: ['Es handelt sich um eine beitragsorientierte Zusage – dann ist die unmittelbare Bewertung zulässig, die Zusageart ist aber im Formular nicht so erfasst.',
                       'Der Träger hat vom Wahlrecht des § 45 Abs. 1 VersAusglG Gebrauch gemacht.',
                       'Durchführungsweg oder Bewertungsmethode wurden falsch erfasst.'],
            massnahmen: ['Zusageart in den Stammdaten prüfen und gegebenenfalls korrigieren.',
                         'Begründung des Trägers für die gewählte Methode einholen.'] },
    GD08: { ursachen: ['Die Auskunft nennt nur die Monatsrente.',
                       'Der Übertragungswert ist an anderer Stelle der Auskunft ausgewiesen.'],
            massnahmen: ['Deckungskapital beziehungsweise Übertragungswert zum Ehezeitende nachfordern.',
                         'Ohne diesen Wert lässt sich der Kapitalwert nicht gegenprüfen.'] },

    /* ---- Schritt 2: Ehezeitanteil ---- */
    EA01: { ursachen: ['Vorzeichenfehler bei der Erfassung.',
                       'Der Träger hat eine Verrechnung oder Rückforderung ausgewiesen.'],
            massnahmen: ['Wert gegen die Auskunft prüfen.',
                         'Bei tatsächlich negativem Wert Rückfrage beim Träger – ein negativer Ehezeitanteil ist fachlich ausgeschlossen.'] },
    EA02: { ursachen: ['Das Dienstverhältnis begann erst nach dem Ehezeitende.',
                       'Der Vertrag war während der gesamten Ehezeit beitragsfrei gestellt.',
                       'Das Anrecht ruht.'],
            massnahmen: ['Gegen Diensteintritt beziehungsweise Vertragsbeginn plausibilisieren.',
                         'Bestätigt sich die Null, findet für dieses Anrecht kein Ausgleich statt.'] },
    EA03: { ursachen: ['Die Auskunft nennt nur den Ehezeitanteil ohne Herleitung.',
                       'Die Angaben liegen vor, sind aber in Abschnitt 5 nicht erfasst.'],
            massnahmen: ['Gesamtanrecht, Diensteintritt und feste Altersgrenze nachfordern.',
                         'Ohne diese drei Werte ist die Auskunft nicht nachrechenbar und damit nicht prüfbar.'] },
    EA04: { ursachen: ['Sonderregelungen der Zusage: Festbeträge, Bausteine, anrechnungsfreie Zeiten, Dynamisierung.',
                       'Der Träger legt eine andere Altersgrenze zugrunde als erfasst.',
                       'Vordienstzeiten wurden angerechnet.',
                       'Diensteintritt oder feste Altersgrenze wurden falsch erfasst.'],
            massnahmen: ['Beide Datumsangaben gegen die Auskunft prüfen.',
                         'Vollständige Herleitung des Ehezeitanteils beim Träger anfordern.',
                         'Bei Bausteinsystemen die Abweichung würdigen – dort ist die zeitratierliche Nachrechnung ohnehin nur eine Näherung.'] },
    EA05: { ursachen: ['Erfassungsfehler.',
                       'Verwechslung von Diensteintritt und Datum der Versorgungszusage.'],
            massnahmen: ['Diensteintritt korrigieren.',
                         'Liegt er tatsächlich nach dem Ehezeitende, muss der Ehezeitanteil null sein – dann ist die Auskunft zu beanstanden.'] },
    EA06: { ursachen: ['Die beiden Felder wurden vertauscht.',
                       'Die Werte stehen in unterschiedlichen Einheiten – Monatsrente gegen Kapitalwert.'],
            massnahmen: ['Ehezeitanteil und Gesamtanrecht gegen die Auskunft prüfen.',
                         'Einheit in den Stammdaten kontrollieren.'] },
    EA07: { ursachen: ['Der Träger zieht Zeiten ab, die er nicht für anrechnungsfähig hält.',
                       'Es gilt eine andere Altersgrenze als erfasst.'],
            massnahmen: ['Herleitung des Ehezeitanteils anfordern.',
                         'Feste Altersgrenze der Zusage prüfen.'] },
    EA08: { ursachen: ['Beitragsfreistellung während der Ehezeit.',
                       'Entnahme, Beleihung oder Abtretung des Vertrags.',
                       'Verrechnung von Abschluss- und Verwaltungskosten.',
                       'Kursverluste – vor allem beim Pensionsfonds.'],
            massnahmen: ['Ursache beim Träger klären.',
                         'Bei einseitiger Verfügung des Inhabers zulasten des anderen Ehegatten § 27 VersAusglG prüfen.',
                         'Marktbedingte Kursverluste sind hinzunehmen.'] },
    EA09: { ursachen: ['Überschussanteile, die nicht im Ehezeitanteil enthalten sind.',
                       'Abschluss- und Verwaltungskosten, Zillmerung.',
                       'Die Deckungskapitalien beziehen sich auf andere Stichtage als die Ehezeit.'],
            massnahmen: ['Aufschlüsselung des Ehezeitanteils anfordern.',
                         'Stichtage der beiden Kapitalwerte prüfen.'],
            vertiefung: 'Kleine Abweichungen sind bei versicherungsförmiger bAV normal. Große Abweichungen deuten darauf hin, dass der Träger nicht den Zuwachs, sondern etwa den Endstand als Ehezeitanteil ausweist.' },
    EA10: { ursachen: ['Der Vertrag hat innerhalb der Ehezeit begonnen.'],
            massnahmen: ['Prüfen, ob der Ehezeitanteil dem gesamten Anrecht entspricht.',
                         'Weicht er ab, Herleitung anfordern.'] },

    /* ---- Schritt 3: Kapitalwert ---- */
    KW01: { ursachen: ['Die Angabe fehlt in der Auskunft.', 'Erfassungsfehler.'],
            massnahmen: ['Korrespondierenden Kapitalwert nachfordern – § 5 Abs. 3 VersAusglG verpflichtet den Träger ausdrücklich dazu.',
                         'Ohne ihn sind Bagatellgrenze, externe Teilung und Kostenquote nicht prüfbar.'] },
    KW02: { ursachen: ['Kapitalwert und Ehezeitanteil beziehen sich auf verschiedene Stichtage.',
                       'Der Kapitalwert enthält Überschussanteile, der Ehezeitanteil nicht.',
                       'Erfassungsfehler in einem der beiden Felder.'],
            massnahmen: ['Beide Werte gegen die Auskunft prüfen.',
                         'Herleitung anfordern, wenn die Abweichung bestehen bleibt.'] },
    KW03: { ursachen: ['Unüblicher Rechnungszins.',
                       'Der Inhaber ist zum Ehezeitende noch weit vom Leistungsbeginn entfernt – dann ist der Faktor klein.',
                       'Falsche Einheit erfasst: ein Kapitalbetrag steht im Feld Monatsrente oder umgekehrt.',
                       'Es handelt sich um eine bereits laufende Rente.'],
            massnahmen: ['Einheit und Zahlenwerte prüfen – das ist die häufigste Ursache.',
                         'Rechnungsgrundlagen beim Träger anfordern.',
                         'Passt die Spanne für den Fall nicht, lässt sie sich in Abschnitt 8 anpassen.'] },
    KW04: { ursachen: ['Die Sterbetafel des Trägers weicht von der vereinfachten Näherung ab – Generationentafeln liefern deutlich höhere Barwerte.',
                       'Eine Hinterbliebenenversorgung ist mitbewertet.',
                       'Der Leistungsbeginn weicht von der erfassten Altersgrenze ab.'],
            massnahmen: ['Den Befund allein nie als Beanstandung verwenden.',
                         'Ernst nehmen, wenn zugleich KW03 oder KW05 anschlagen.',
                         'Dann Rechnungsgrundlagen anfordern: Sterbetafel, Zins, Leistungsbeginn.'] },
    KW05: { ursachen: ['Steuerlicher Zins von 6 % statt eines Marktzinses.',
                       'Zins null bei einer reinen Kapitalzusage.',
                       'Erfassungsfehler: 0,0175 statt 1,75.'],
            massnahmen: ['Eingabeformat prüfen – der Zins wird in Prozent erfasst, nicht als Dezimalzahl.',
                         'Herleitung des Zinssatzes beim Träger anfordern.'] },
    KW06: { ursachen: ['Der Träger verwendet einen handels- oder steuerrechtlich geprägten Zins.'],
            massnahmen: ['Auswirkung auf den Kapitalwert quantifizieren.',
                         'Bei externer Teilung zwingend den Transferverlust prüfen.',
                         'Prüfen, ob der Zins das Anrecht künstlich unter einen Grenzwert drückt.'],
            vertiefung: 'Der Effekt ist doppelt: Ein hoher Zins senkt den Kapitalwert, wodurch das Anrecht eher unter die Grenze des § 17 fällt und die einseitige externe Teilung möglich wird – und zugleich wächst der Transferverlust für den Berechtigten.' },

    /* ---- Schritt 4: Halbteilung ---- */
    HT01: { ursachen: ['Der Träger zieht die Teilungskosten vor der Halbteilung vom Ehezeitanteil ab.'],
            massnahmen: ['Kein Handlungsbedarf zur Rechnung – die Anwendung vermeidet den doppelten Abzug.',
                         'Die Angemessenheit der Kosten wird in Schritt 7 trotzdem geprüft.'] },
    HT02: { ursachen: ['Der Träger rechnet die Eurokosten über den Kapitalwert um und zieht sie dort ab.'],
            massnahmen: ['Kein Handlungsbedarf zur Rechnung.',
                         'Der Rechenweg des Trägers sollte in der Auskunft dokumentiert sein.'] },
    HT03: { ursachen: ['Ausgleichswert und Ehezeitanteil passen rechnerisch nicht zusammen.',
                       'Die Kosten wurden auf Kapitalebene abgezogen, das Feld „Ausgleichswert als Kapital" ist aber leer.',
                       'Erfassungsfehler in einem der Felder.'],
            massnahmen: ['Prüfen, ob die Auskunft einen Kapitalbetrag für den Ausgleichswert nennt, und ihn erfassen – das löst den Befund häufig auf.',
                         'Ehezeitanteil, Ausgleichswert und Teilungskosten gegen die Auskunft prüfen.',
                         'Bleibt der Wert unerklärt, die Auskunft beanstanden: Der Halbteilungsgrundsatz verlangt Nachvollziehbarkeit.'] },

    /* ---- Schritt 5: Bagatelle ---- */
    BG01: { ursachen: ['Der Ausgleichswert liegt unter der Grenze des § 18 Abs. 3 VersAusglG.'],
            massnahmen: ['Ermessensentscheidung treffen – § 18 Abs. 2 ist eine Soll-Vorschrift, kein Automatismus.',
                         'Beteiligte anhören.',
                         'Zusätzlich § 18 Abs. 1 prüfen: mehrere Anrechte gleicher Art mit geringer Wertdifferenz.'],
            vertiefung: 'Gegen den Ausschluss kann sprechen, dass der Berechtigte auf jedes Anrecht angewiesen ist oder dass sich mehrere kleine Anrechte summieren. Die Anwendung betrachtet jedes Anrecht einzeln und kann diese Gesamtschau nicht leisten.' },

    /* ---- Schritt 6: Teilungsart ---- */
    TA01: { ursachen: ['Der Grenzwert des § 14 beziehungsweise § 17 VersAusglG ist überschritten.',
                       'Die Zustimmung des Berechtigten liegt nicht vor.'],
            massnahmen: ['Interne Teilung anordnen – sie ist der gesetzliche Regelfall.',
                         'Alternativ Zustimmung des Berechtigten einholen (§ 14 Abs. 2 Nr. 1 VersAusglG).',
                         'Den Träger auf die fehlende Rechtsgrundlage hinweisen.'] },
    TA02: { ursachen: ['Der Berechtigte hat keine Zielversorgung gewählt.'],
            massnahmen: ['Berechtigten zur Wahl auffordern (§ 15 Abs. 1 VersAusglG).',
                         'Ohne Wahl die Auffangversorgung im Tenor benennen: Versorgungsausgleichskasse bei Direktzusage und Unterstützungskasse, sonst gesetzliche Rentenversicherung.'] },
    TA03: { ursachen: ['Die Auskunft nennt nur den externen Ausgleichswert.',
                       'Der Rentenfaktor der Zielversorgung ist nicht bekannt.'],
            massnahmen: ['Beim abgebenden Träger anfordern, welche Leistung der Berechtigte bei interner Teilung erhielte.',
                         'Bei der Zielversorgung die erwartete Leistung aus dem Ausgleichskapital erfragen.'] },
    TA04: { ursachen: ['Hoher Rechnungszins beim abgebenden Träger senkt das übertragene Kapital.',
                       'Niedriger Rentenfaktor oder hohe Kosten der Zielversorgung.',
                       'Zinsniveau zum Stichtag.'],
            massnahmen: ['Ausgleichsbetrag erhöhen – das BVerfG verlangt eine verfassungskonforme Handhabung des § 17 VersAusglG.',
                         'Oder intern teilen.',
                         'Oder mit dem Berechtigten eine günstigere Zielversorgung suchen.'],
            vertiefung: 'BVerfG, Beschluss vom 26.05.2020 – 1 BvL 5/18: Die externe Teilung nach § 17 VersAusglG ist nur verfassungsgemäß, wenn die Gerichte sicherstellen, dass der Berechtigte keine unzumutbaren Transferverluste erleidet. Als Orientierung dient eine Grenze von rund 10 %.' },
    TA05: { ursachen: ['Das Anrecht befindet sich bereits im Leistungsbezug.'],
            massnahmen: ['Prüfen, ob die Zielversorgung eine sofort beginnende Leistung darstellen kann.',
                         'Andernfalls interne Teilung vorziehen.'] },
    TA06: { ursachen: ['Der Arbeitgeber hat keine Teilungsordnung.',
                       'Sie wurde mit der Auskunft nicht übersandt.'],
            massnahmen: ['Teilungsordnung anfordern.',
                         'Gleichwertigkeit nach § 11 VersAusglG prüfen: eigenständiges Anrecht, vergleichbare Wertentwicklung, grundsätzlich gleicher Risikoschutz.',
                         'Ohne Regelung kann die interne Teilung nicht vollzogen werden.'],
            vertiefung: 'Häufige Streitpunkte sind der Ausschluss der Hinterbliebenenversorgung für den Berechtigten und abweichende Rechnungsgrundlagen für das neu begründete Anrecht.' },

    /* ---- Schritt 7: Teilungskosten ---- */
    TK01: { ursachen: ['Erfassungsfehler.'],
            massnahmen: ['Wert gegen die Auskunft prüfen und korrigieren.'] },
    TK02: { ursachen: ['Der Träger macht keine Kosten geltend.'],
            massnahmen: ['Kein Handlungsbedarf. Der volle halbe Ehezeitanteil wird übertragen.'] },
    TK03: { ursachen: ['Mindestpauschale des Trägers bei einem kleinen Anrecht.',
                       'Der Träger setzt tatsächlich entstandene Kosten an.'],
            massnahmen: ['Herleitung der Kosten anfordern.',
                         'Angemessenheit nach § 13 VersAusglG würdigen – das Gericht kann den Abzug auf das angemessene Maß begrenzen.',
                         'Die Obergrenze in Abschnitt 8 an die eigene Rechtsprechungslinie anpassen.'] },
    TK04: { ursachen: ['Der Träger setzt einen hohen Festbetrag an.'],
            massnahmen: ['Herleitung anfordern und Angemessenheit prüfen.',
                         'Absolute Obergrenze in Abschnitt 8 an die eigene Rechtsprechungslinie anpassen.'] },
    TK05: { ursachen: ['Mindestpauschale, die bei einem kleinen Anrecht die Quote zwangsläufig überschreitet.'],
            massnahmen: ['Grundsätzlich zulässig, aber zu würdigen.',
                         'Prüfen, ob nicht ohnehin die Bagatellgrenze des § 18 Abs. 2 VersAusglG greift.'] },
    TK06: { ursachen: ['Der Träger nennt Kosten, es wird aber extern geteilt.'],
            massnahmen: ['Kein Handlungsbedarf – § 13 VersAusglG gilt nur für die interne Teilung.',
                         'Im Tenor keinen Kostenabzug ausweisen.'] },
    TK07: { ursachen: ['Der Träger hat die Kosten bereits vor der Halbteilung abgezogen.'],
            massnahmen: ['Kein Handlungsbedarf.'],
            vertiefung: 'Schritt 8 rechnet vom ungekürzten halben Ehezeitanteil. Damit der Kostenabzug weder verloren geht noch doppelt wirkt, wird der hälftige Kostenanteil hier genau einmal berücksichtigt. Das Ergebnis entspricht dem kostenbereinigten Ausgleichswert der Auskunft.' },

    /* ---- Schritt 8: Ergebnis ---- */
    ER01: { ursachen: ['Die Teilungskosten übersteigen den halben Ehezeitanteil.',
                       'Sehr kleines Anrecht mit Mindestpauschale.'],
            massnahmen: ['Kostenansatz prüfen und auf das angemessene Maß kürzen.',
                         'Ausschluss wegen Geringfügigkeit nach § 18 Abs. 2 VersAusglG erwägen.'] },
    ER02: { ursachen: ['Rechnerische Inkonsistenz in den Eingaben.'],
            massnahmen: ['Ehezeitanteil, Ausgleichswert und Teilungskosten gegen die Auskunft prüfen.'] },
    ER03: { ursachen: ['Das Anrecht befindet sich im Leistungsbezug.'],
            massnahmen: ['Kürzungszeitpunkt im Tenor beachten: Die Kürzung wirkt ab Rechtskraft.',
                         'Leistungsschutz nach § 30 VersAusglG prüfen – bis zur Rechtskraft an den Verpflichteten erbrachte Leistungen sind geschützt.'] },
    ER04: { ursachen: ['Die kapitalbezogene Mindestleistung ist als Monatsrente dargestellt.'],
            massnahmen: ['Darstellung der Teilung auf Kapitalbasis beim Träger anfordern.',
                         'Sicherstellen, dass die Mindestleistung auch beim Berechtigten abgebildet wird.'] },
    ER05: { ursachen: ['Zusammenfassung des Ergebnisses – kein Mangel.'],
            massnahmen: ['Tenor formulieren: Anrecht, Träger, Ausgleichswert, Teilungsart, Stichtag Ehezeitende.',
                         'Bei interner Teilung auf die Teilungsordnung Bezug nehmen.'] }
  };

  /* ====================================================================
     Die acht Prüfschritte
     ==================================================================== */
  var SCHRITTE = {
    1: {
      titel: 'Grunddaten und Ausgleichsreife',
      recht: '§§ 5, 19 VersAusglG, § 1b BetrAVG',
      zweck: 'Der Torwächter der ganzen Prüfung. Bevor irgendetwas gerechnet wird, muss ' +
        'feststehen, dass die Auskunft vollständig ist, dieselbe Ehezeit zugrunde liegt und das ' +
        'Anrecht überhaupt jetzt geteilt werden darf.',
      prueft: [
        'Sind alle Pflichtangaben der Auskunft vorhanden?',
        'Rechnet der Träger mit derselben Ehezeit wie das Verfahren?',
        'Ist die Auskunft nach dem Ehezeitende erstellt und noch aktuell?',
        'Ist die Anwartschaft unverfallbar und damit ausgleichsreif?',
        'Passt die Bewertungsmethode zum Durchführungsweg?'
      ],
      ergebnis: [
        'Verfallbare Anwartschaft: sofortiger Abbruch mit Status SCHULDRECHTLICH_VORBEHALTEN.',
        'Ausgeübtes Kapitalwahlrecht: sofortiger Abbruch mit Status NICHT_VA_SONDERN_ZUGEWINN.',
        'Fehlerbefund: Abbruch – ohne belastbare Grunddaten wird nicht gerechnet.',
        'Sonst: die Prüfung läuft weiter zu Schritt 2.'
      ],
      befunde: ['GD01', 'GD02', 'GD03', 'GD04', 'GD05', 'GD06', 'GD07', 'GD08'],
      praxis: 'Dieser Schritt ist der einzige, der bei einem Fehler sofort abbricht. Das ist ' +
        'Absicht: Eine falsch abgegrenzte Ehezeit oder eine unvollständige Auskunft macht jede ' +
        'weitere Zahl wertlos. Alle späteren Schritte sammeln dagegen weiter, damit man in einem ' +
        'Durchgang das vollständige Bild der Beanstandungen bekommt.'
    },
    2: {
      titel: 'Ehezeitanteil prüfen und nachrechnen',
      recht: '§ 45 VersAusglG, § 2 Abs. 1 BetrAVG',
      zweck: 'Der Ehezeitanteil ist die Basis von allem. Hier wird er nicht übernommen, sondern ' +
        'mit der Methode nachgerechnet, die der Träger selbst gewählt hat.',
      prueft: [
        'Zeitratierlich: Gesamtanrecht × m / n – stimmt der ausgewiesene Wert mit der ' +
          'Nachrechnung überein?',
        'Zeitratierlich: Liegt der Diensteintritt vor dem Ehezeitende, ist der Ehezeitanteil ' +
          'kleiner als das Gesamtanrecht, deckt die Ehezeit die ganze Dienstzeit ab?',
        'Unmittelbar: Entspricht der Ehezeitanteil dem Zuwachs des Deckungskapitals?',
        'Unmittelbar: Ist das Deckungskapital in der Ehezeit gesunken?'
      ],
      ergebnis: [
        'Die Prüfung läuft in jedem Fall weiter – auch Fehlerbefunde führen erst in Schritt 8 ' +
          'zum Abbruch.',
        'Der Ehezeitanteil des Trägers bleibt maßgeblich; die Nachrechnung ist Kontrolle, nicht ' +
          'Ersatz.'
      ],
      befunde: ['EA01', 'EA02', 'EA03', 'EA04', 'EA05', 'EA06', 'EA07', 'EA08', 'EA09', 'EA10'],
      praxis: 'Die Toleranz liegt bei 0,5 %. Kleine Abweichungen sind Rundung, größere haben ' +
        'immer eine Ursache – Sonderregelungen der Zusage, abweichende Altersgrenze, angerechnete ' +
        'Vordienstzeiten. Die App kann die Ursache nicht kennen; sie zeigt nur, dass es eine gibt.'
    },
    3: {
      titel: 'Kapitalwert und Rechnungszins',
      recht: '§ 47 VersAusglG, § 4 Abs. 5 BetrAVG',
      zweck: 'Der korrespondierende Kapitalwert entscheidet über Bagatellgrenze, externe Teilung ' +
        'und Transferverlust. Er ist damit der wirkungsvollste Hebel im Verfahren – und wird hier ' +
        'auf innere Stimmigkeit geprüft.',
      prueft: [
        'Kapitalanrechte: Stimmen Ehezeitanteil und Kapitalwert überein?',
        'Rentenanrechte: Liegt der Barwertfaktor in der plausiblen Spanne von 8 bis 25 Jahresrenten?',
        'Rentenanrechte: Wie weit weicht der Kapitalwert von einer eigenen Näherungsrechnung ab?',
        'Liegt der Rechnungszins in der üblichen Spanne, bei Direktzusage und Unterstützungskasse ' +
          'zusätzlich unter der gesonderten Grenze?'
      ],
      ergebnis: [
        'Alle Befunde dieses Schritts sind Warnungen oder Hinweise – bis auf KW01, wenn der ' +
          'Kapitalwert ganz fehlt.'
      ],
      befunde: ['KW01', 'KW02', 'KW03', 'KW04', 'KW05', 'KW06'],
      praxis: 'Die Näherungsrechnung (KW04) arbeitet mit einer vereinfachten Sterblichkeit und ' +
        'ersetzt keine Sterbetafel. Sie ist bewusst mit 15 % Toleranz ausgestattet und nie ein ' +
        'Fehler. Ernst wird es, wenn KW03, KW04 und KW05 gemeinsam anschlagen – dann stimmt an ' +
        'den Rechnungsgrundlagen etwas nicht.'
    },
    4: {
      titel: 'Halbteilung',
      recht: '§ 1 Abs. 1 VersAusglG',
      zweck: 'Der Halbteilungsgrundsatz ist der Kern des Versorgungsausgleichs. Der Ausgleichswert ' +
        'muss die Hälfte des Ehezeitanteils sein – hier wird geprüft, ob der Wert des Trägers das ' +
        'hergibt und ob er die Teilungskosten bereits enthält.',
      prueft: [
        'Entspricht der Ausgleichswert genau dem halben Ehezeitanteil?',
        'Falls nicht: Passt er zu (Ehezeitanteil − Teilungskosten) / 2?',
        'Bei Rentenanrechten: Passt der als Kapital genannte Ausgleichswert zu ' +
          '(Kapitalwert − Kosten) / 2?'
      ],
      ergebnis: [
        'Trifft eine der Kostenvarianten zu, wird das für Schritt 7 gemerkt – der Kostenanteil ' +
          'wird dann genau einmal abgezogen, nicht zweimal.',
        'Lässt sich der Wert gar nicht herleiten: Fehler HT03, am Ende Abbruch.'
      ],
      befunde: ['HT01', 'HT02', 'HT03'],
      praxis: 'Dieser Schritt entscheidet über den häufigsten Rechenfehler der Praxis, den ' +
        'doppelten Kostenabzug. Die Auskünfte sind uneinheitlich: Manche Träger nennen den ' +
        'ungekürzten halben Ehezeitanteil und die Kosten getrennt, andere haben sie schon ' +
        'verrechnet. Man sieht es dem Wert nicht an – man muss es nachrechnen.'
    },
    5: {
      titel: 'Geringfügigkeit (Bagatellprüfung)',
      recht: '§ 18 Abs. 2, 3 VersAusglG',
      zweck: 'Kleine Anrechte sollen nicht geteilt werden, weil der Verwaltungsaufwand außer ' +
        'Verhältnis zum Nutzen steht.',
      prueft: [
        'Liegt der Ausgleichswert unter 1 % der monatlichen Bezugsgröße (Rente) beziehungsweise ' +
          '120 % (Kapital)?'
      ],
      ergebnis: [
        'Bagatelle ohne vorherige Fehlerbefunde: Abbruch mit Status BAGATELL_VORSCHLAG.',
        'Bagatelle mit Fehlerbefunden: Hinweis, aber kein Abbruch – ein Ausschluss setzt ' +
          'belastbare Zahlen voraus.',
        'Sonst: weiter zu Schritt 6.'
      ],
      befunde: ['BG01'],
      praxis: '§ 18 Abs. 2 ist eine Soll-Vorschrift, keine zwingende. Das Gericht entscheidet ' +
        'nach Ermessen und muss die Beteiligten anhören. Zu prüfen ist außerdem § 18 Abs. 1: ' +
        'Mehrere Anrechte gleicher Art mit geringer Wertdifferenz können ebenfalls ausgeschlossen ' +
        'werden – das erfasst diese Anwendung nicht, weil sie jedes Anrecht einzeln betrachtet.'
    },
    6: {
      titel: 'Teilungsart bestimmen',
      recht: '§§ 10, 14, 17 VersAusglG',
      zweck: 'Interne oder externe Teilung? Die interne ist der gesetzliche Regelfall; die ' +
        'externe braucht eine Rechtsgrundlage. Bei ihr kommt die verfassungsrechtliche Grenze des ' +
        'Transferverlusts hinzu.',
      prueft: [
        'Schlägt der Träger intern vor, wird das übernommen.',
        'Liegt die Zustimmung des Berechtigten vor, ist extern zulässig.',
        'Direktzusage und Unterstützungskasse: Liegt der halbe Kapitalwert unter der ' +
          'Beitragsbemessungsgrenze (§ 17)?',
        'Übrige Wege: Liegt der Ausgleichswert unter 2 % beziehungsweise 240 % der Bezugsgröße ' +
          '(§ 14 Abs. 2 Nr. 2)?',
        'Bei externer Teilung: Zielversorgung gewählt, Transferverlust unter 10 %, kein ' +
          'Leistungsbezug?',
        'Bei interner Teilung einer Direktzusage: Liegt die Teilungsordnung vor?'
      ],
      ergebnis: [
        'Das Ergebnis steuert Schritt 7: Bei externer Teilung entfällt der Kostenabzug vollständig.'
      ],
      befunde: ['TA01', 'TA02', 'TA03', 'TA04', 'TA05', 'TA06'],
      praxis: 'Die Grenzwerte liegen weit auseinander. Bei einer Pensionskasse endet die ' +
        'einseitige externe Teilung 2020 schon bei rund 7.600 € Kapital, bei einer Direktzusage ' +
        'erst bei 82.800 €. Wer den Durchführungsweg falsch erfasst, bekommt hier zuverlässig die ' +
        'falsche Teilungsart.'
    },
    7: {
      titel: 'Teilungskosten',
      recht: '§ 13 VersAusglG',
      zweck: 'Der Träger darf die angemessenen Kosten der internen Teilung mit dem Ehezeitanteil ' +
        'verrechnen. Beide Ehegatten tragen sie hälftig – der Abzug beim Ausgleichswert beträgt ' +
        'also die Hälfte der Kosten.',
      prueft: [
        'Liegt die Kostenquote unter der Obergrenze von üblicherweise 3 %?',
        'Bleiben die Kosten unter der absoluten Obergrenze?',
        'Handelt es sich um eine Mindestpauschale bei einem kleinen Anrecht?',
        'Wird bei externer Teilung fälschlich ein Kostenabzug verlangt?'
      ],
      ergebnis: [
        'Externe Teilung: Abzug null.',
        'Interne Teilung: die Hälfte der Kosten, bei Rentenanrechten zuvor über den Kapitalwert ' +
          'in Rente umgerechnet.'
      ],
      befunde: ['TK01', 'TK02', 'TK03', 'TK04', 'TK05', 'TK06', 'TK07'],
      praxis: 'Die Umrechnung bei Rentenanrechten ist der Punkt, an dem die Nachvollziehbarkeit ' +
        'leidet: 500 € Kosten werden bei einem Kapitalwert von 62.000 € und 560 € Monatsrente zu ' +
        '4,52 € Rente, hälftig also 2,26 €. Die Anwendung zeigt diesen Rechenweg offen an.'
    },
    8: {
      titel: 'Ergebnis und Anordnung',
      recht: '§§ 10, 14, 30 VersAusglG',
      zweck: 'Zusammenführung: halber Ehezeitanteil abzüglich Kostenanteil ergibt den ' +
        'Ausgleichsbetrag. Anschließend wird geprüft, ob das Ergebnis in sich schlüssig ist.',
      prueft: [
        'Ist der Ausgleichsbetrag größer als null?',
        'Übersteigt er den halben Ehezeitanteil?',
        'Steht das Anrecht bereits im Leistungsbezug?',
        'Ist eine kapitalbezogene Mindestleistung als Rente dargestellt?'
      ],
      ergebnis: [
        'Fehlerbefunde irgendwo in den Schritten 2 bis 8: Status ABBRUCH.',
        'Nur Warnungen: Status FREIGABE_ERFORDERLICH – rechnerisch schlüssig, aber jede Warnung ' +
          'braucht eine dokumentierte Freigabe.',
        'Keine Warnungen: Status OK, die Anordnung kann so ergehen.'
      ],
      befunde: ['ER01', 'ER02', 'ER03', 'ER04', 'ER05'],
      praxis: 'Hier laufen alle bis dahin gesammelten Befunde zusammen. Ein Fehler aus Schritt 2 ' +
        'führt erst jetzt zum Abbruch – deshalb sieht man in der Schrittfolge auch bei Fehlern ' +
        'noch, was Teilungsart und Kosten ergeben hätten.'
    }
  };

  /* ====================================================================
     Schweregrade und Ergebnisstatus
     ==================================================================== */
  var SCHWEREGRADE = {
    titel: 'Befundarten und Ergebnisstatus',
    recht: 'Aufbau der Prüfung',
    bedeutung: 'Jede Einzelprüfung erzeugt entweder nichts oder einen Befund. Die Befundart sagt, ' +
      'wie mit dem Ergebnis umzugehen ist; der Status am Kopf der Auswertung fasst alle Befunde ' +
      'des Anrechts zusammen.',
    arten: [
      { name: 'ERROR – Fehler',
        text: 'Die Berechnung ist nicht tragfähig. Ein Fehler in Schritt 1 bricht sofort ab, ' +
          'Fehler in späteren Schritten führen in Schritt 8 zum Abbruch. Es ergeht keine ' +
          'Anordnung, bevor die Ursache geklärt ist – in aller Regel durch Rückfrage beim ' +
          'Versorgungsträger oder Korrektur der Erfassung.' },
      { name: 'WARN – Warnung',
        text: 'Die Rechnung geht auf, aber etwas ist erklärungsbedürftig. Die Anordnung kann ' +
          'ergehen, jede Warnung braucht jedoch eine protokollierte Freigabe mit Begründung. ' +
          'Warnungen sind der eigentliche Arbeitsvorrat der Sachbearbeitung.' },
      { name: 'INFO – Hinweis',
        text: 'Kein Mangel, sondern eine Feststellung, die man kennen sollte – etwa dass der ' +
          'Träger keine Teilungskosten erhebt oder dass ein Kostenabzug bereits enthalten war. ' +
          'Hinweise erfordern keine Freigabe.' }
    ],
    status: [
      { name: 'OK', text: 'Keine Warnung und kein Fehler. Die Anordnung kann so ergehen.' },
      { name: 'FREIGABE_ERFORDERLICH',
        text: 'Rechnerisch schlüssig, aber mindestens eine Warnung. Vor der Anordnung sind alle ' +
          'Warnungen zu würdigen und freizugeben.' },
      { name: 'ABBRUCH',
        text: 'Mindestens ein Fehler. Es wird kein Ausgleichsbetrag vorgeschlagen.' },
      { name: 'BAGATELL_VORSCHLAG',
        text: 'Der Ausgleichswert liegt unter der Geringfügigkeitsgrenze. Vorgeschlagen wird der ' +
          'Ausschluss nach § 18 Abs. 2 VersAusglG – eine Ermessensentscheidung des Gerichts.' },
      { name: 'SCHULDRECHTLICH_VORBEHALTEN',
        text: 'Die Anwartschaft war zum Ehezeitende noch verfallbar und damit nicht ' +
          'ausgleichsreif. Der Ausgleich bleibt dem Verfahren nach der Scheidung vorbehalten ' +
          '(§ 19 Abs. 4, §§ 20 ff. VersAusglG).' },
      { name: 'NICHT_VA_SONDERN_ZUGEWINN',
        text: 'Das Kapitalwahlrecht wurde ausgeübt. Das Anrecht gehört nach herrschender ' +
          'Auffassung in den Zugewinnausgleich.' }
    ],
    praxis: 'Für die Schulung wichtig: Die Anwendung entscheidet nichts. Sie stellt fest, rechnet ' +
      'nach und benennt, was zu klären ist. Ob eine Warnung im konkreten Fall hinnehmbar ist, ' +
      'bleibt eine fachliche Entscheidung, die dokumentiert werden muss.'
  };

  /* Nachschlagewerk über alle dokumentierten Felder */
  var FELDER = {};
  [STAMMDATEN, TRAEGERAUSKUNFT].forEach(function (gruppe) {
    Object.keys(gruppe).forEach(function (k) { FELDER[k] = gruppe[k]; });
  });

  global.BAV = global.BAV || {};
  global.BAV.hilfe = {
    STAMMDATEN: STAMMDATEN,
    TRAEGERAUSKUNFT: TRAEGERAUSKUNFT,
    FELDER: FELDER,
    SCHRITTE: SCHRITTE,
    SCHWEREGRADE: SCHWEREGRADE,
    BEFUNDE: BEFUNDE
  };
})(typeof window !== 'undefined' ? window : this);
