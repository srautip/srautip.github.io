/* =====================================================================
   Konfiguration je Jahr des Ehezeitendes.
   Die Werte sind vorbelegt, aber in der Oberfläche frei überschreibbar –
   die Kostenrechtsprechung und die Sozialversicherungsgrößen ändern sich
   laufend und dürfen nicht hart kodiert bleiben.
   ===================================================================== */
(function (global) {
  'use strict';

  /* Monatliche Bezugsgröße (West) nach § 18 Abs. 1 SGB IV und
     Beitragsbemessungsgrenze der allgemeinen Rentenversicherung (jährlich). */
  var JAHRESWERTE = {
    2015: { bezugsgroesse_monat: 2835, bbg_grv_jahr: 72600 },
    2016: { bezugsgroesse_monat: 2905, bbg_grv_jahr: 74400 },
    2017: { bezugsgroesse_monat: 2975, bbg_grv_jahr: 76200 },
    2018: { bezugsgroesse_monat: 3045, bbg_grv_jahr: 78000 },
    2019: { bezugsgroesse_monat: 3115, bbg_grv_jahr: 80400 },
    2020: { bezugsgroesse_monat: 3185, bbg_grv_jahr: 82800 },
    2021: { bezugsgroesse_monat: 3290, bbg_grv_jahr: 85200 },
    2022: { bezugsgroesse_monat: 3290, bbg_grv_jahr: 84600 },
    2023: { bezugsgroesse_monat: 3395, bbg_grv_jahr: 87600 },
    2024: { bezugsgroesse_monat: 3535, bbg_grv_jahr: 90600 },
    2025: { bezugsgroesse_monat: 3745, bbg_grv_jahr: 96600 },
    2026: { bezugsgroesse_monat: 3885, bbg_grv_jahr: 101400, vorlaeufig: true }
  };

  /* Prüfgrenzen und Toleranzen, jahresunabhängig vorbelegt */
  var GRUNDWERTE = {
    extern_rente_faktor: 0.02,      // § 14 Abs. 2 Nr. 2 VersAusglG
    extern_kapital_faktor: 2.40,    // § 14 Abs. 2 Nr. 2 VersAusglG
    bagatell_rente_faktor: 0.01,    // § 18 Abs. 3 VersAusglG
    bagatell_kapital_faktor: 1.20,  // § 18 Abs. 3 VersAusglG
    zins_min: 0.5,                  // % p. a.
    zins_max: 6.0,                  // % p. a.
    zins_max_direktzusage: 3.5,     // % p. a. – Vergleichsmaßstab für KW06
    kosten_quote_max: 0.03,         // 3 % des Ehezeitanteils/Kapitalwerts
    kosten_abs_min: 100,            // €
    kosten_abs_max: 500,            // €
    transferverlust_max: 0.10,      // BVerfG 26.05.2020 – 1 BvL 5/18
    toleranz_relativ: 0.005,        // 0,5 % Rechentoleranz
    barwert_faktor_min: 8,          // Jahresrenten
    barwert_faktor_max: 25          // Jahresrenten
  };

  /** Vollständige Konfiguration für ein Jahr (Fallback: nächstliegendes Jahr). */
  function configFuerJahr(jahr) {
    jahr = Number(jahr);
    var jahre = Object.keys(JAHRESWERTE).map(Number).sort(function (a, b) { return a - b; });
    var quelle = JAHRESWERTE[jahr];
    var hinweis = '';
    if (!quelle) {
      var naechstes = jahre.reduce(function (best, j) {
        return Math.abs(j - jahr) < Math.abs(best - jahr) ? j : best;
      }, jahre[0]);
      quelle = JAHRESWERTE[naechstes];
      hinweis = 'Für ' + jahr + ' sind keine Werte hinterlegt; übernommen wurden die Werte von ' + naechstes + '. Bitte prüfen und überschreiben.';
    } else if (quelle.vorlaeufig) {
      hinweis = 'Die Werte für ' + jahr + ' sind vorläufig und vor der Verwendung zu prüfen.';
    }

    var cfg = { jahr: jahr, hinweis: hinweis };
    Object.keys(GRUNDWERTE).forEach(function (k) { cfg[k] = GRUNDWERTE[k]; });
    cfg.bezugsgroesse_monat = quelle.bezugsgroesse_monat;
    cfg.bbg_grv_jahr = quelle.bbg_grv_jahr;
    return cfg;
  }

  global.BAV = global.BAV || {};
  global.BAV.config = {
    JAHRESWERTE: JAHRESWERTE,
    GRUNDWERTE: GRUNDWERTE,
    configFuerJahr: configFuerJahr
  };
})(typeof window !== 'undefined' ? window : this);
