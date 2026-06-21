<h1 align="center">🏭 Company Supplier</h1>

<p align="center">
  <strong>Das In-Game-Cheat- &amp; Trainer-Menü für <em>Captain of Industry</em></strong><br>
  Gib dir Ressourcen, schalte einen echten Kreativmodus frei, stoppe die Verschmutzung, beherrsche die
  Weltkarte und schraube an Fahrzeugen, Zügen und Wetter – alles aus einem Fenster. Ein Druck auf
  <kbd>F8</kbd>, und du bestimmst die Regeln.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Captain%20of%20Industry-0.8.5.0-E8730C?style=flat-square" alt="Captain of Industry 0.8.5.0">
  <a href="https://github.com/Spacedudeee/Company-Supplier/releases"><img src="https://img.shields.io/github/v/release/Spacedudeee/Company-Supplier?style=flat-square&amp;label=Release&amp;color=blue" alt="Release-Version"></a>
  <a href="https://github.com/Spacedudeee/Company-Supplier/releases"><img src="https://img.shields.io/github/downloads/Spacedudeee/Company-Supplier/total?style=flat-square&amp;color=success&amp;label=Downloads" alt="Downloads"></a>
  <img src="https://img.shields.io/badge/Modus-Singleplayer-blue?style=flat-square" alt="Singleplayer">
  <a href="LICENSE"><img src="https://img.shields.io/badge/Lizenz-Personal--Use%20(nicht--kommerziell)-green?style=flat-square" alt="Lizenz Personal-Use, nicht-kommerziell"></a>
</p>

---

## ✨ Was ist das?

**Company Supplier** ist ein Cheat-/Trainer-Menü für den Singleplayer-Fabrikaufbau *Captain of Industry*
(MaFi Games). Kein Konsolen-Gefummel: Du öffnest mit <kbd>F8</kbd> ein verschiebbares Fenster mit
übersichtlichen Reitern und schaltest per Klick frei, was du brauchst – von „gib mir 10.000 Stahl“ bis
„fülle alle Lager auf einen Schlag“.

> **Neu in v2.0:** 🧪 Kreativmodus · ⏱️ Spielgeschwindigkeit (bis ungebremst) · 🌫️ Verschmutzung aus · 🗺️ Weltkarte · 🪄 God-Werkzeug · 🚚 Fahrzeug- &amp; Zug-Stats pro Typ · 🎚️ Profile &amp; Presets

## 🚀 Schnellstart

1. Lade `CompanySupplier-v2.0.0.zip` aus dem **Latest**-Release auf der
   [Releases-Seite](https://github.com/Spacedudeee/Company-Supplier/releases) – oder baue ihn selbst (siehe unten).
2. Schließe Captain of Industry komplett.
3. Entpacke die ZIP nach `%APPDATA%\Captain of Industry\Mods\`. Es entsteht der Ordner
   `Mods\CompanySupplier\` (mit `manifest.json` + `CompanySupplier.dll`).
4. Starte das Spiel, aktiviere Mods in den Optionen und füge **Company Supplier** deinem Spielstand hinzu
   (das Manifest erlaubt das Hinzufügen zu / Entfernen aus bestehenden Spielständen).
5. <kbd>F8</kbd> drücken – fertig. 🎉

## 🎛️ Funktionen

Alles erreichbar über die Reiter im <kbd>F8</kbd>-Fenster:

| Reiter | Was du damit anstellst |
|--------|------------------------|
| 📦 **Ressourcen** | Schütte dir jedes Produkt – oder *alle* auf einmal – ins Lager. Per Welt-Klick ein einzelnes Lager füllen oder leeren (z. B. Atommüll loswerden), oder mit einem Klick **alle** Lager randvoll. |
| 🏗️ **Allgemein** | **Kreativmodus**: Fabrik läuft ohne Strom, Arbeiter, Computing, Unity oder Lebensmittel; Sofortbau, kein Treibstoff, keine Wartung. **Spielgeschwindigkeit** jenseits von 3× (5× / 10× / 20× + ungebremst). **Unerschöpfliche Quelle/Senke** als Bau-Gebäude. **God-Werkzeug** (Werft/Depot/Fahrzeug anklicken = vollgetankt). Plus Bevölkerung & Zufriedenheit, Forschung freischalten, Unity gutschreiben. |
| 🌫️ **Umwelt** | Verschmutzung abschalten – Luft, Wasser, Deponie, Fahrzeuge, Schiffe und Züge (einzeln oder alles auf einmal). |
| 🗺️ **Weltkarte** | Ganze Karte aufdecken, unerschöpfliche Welt-Minen, Minen ohne Unity, dazu Effizienz- und Handels-Boost. |
| ⚡ **Erzeugung** | Strom-, Computing- und Unity-Erzeugung direkt setzen. |
| 🚢 **Werft & Flotte** | Steuerung des Welt-Schiffs (Flotte). |
| 🚚 **Fahrzeuge** | Treibstoffverbrauch aus, Fahrzeuglimit per Zahlenfeld, LKW-Kapazität – plus **Stats pro Fahrzeugtyp** (Geschwindigkeit + Ladekapazität exakt setzen) und **Zug-Waggon-Kapazität** pro Typ. |
| ⛰️ **Gelände** | Sofort abbauen / verfüllen / umwandeln, Grundwasser & Erdöl auffüllen, Bäume pflanzen oder entfernen. |
| ☀️ **Wetter** | Das Wetter dauerhaft auf einen Zustand fixieren. |
| 🎚️ **Profil** | **Panik-Aus** (alle Dauer-Cheats mit einem Klick abschalten), **Auto-Restore** deines Setups beim Laden und **3 Preset-Slots**. |

> Produkt- und Materialnamen erscheinen in deiner Spielsprache – sie kommen direkt aus der Lokalisierung des Spiels.

<details>
<summary><strong>🛠️ Aus dem Quellcode bauen</strong></summary>

<br>

Du brauchst das **.NET SDK** und eine lokale Captain-of-Industry-Installation (der Mod kompiliert gegen die
spieleigenen Assemblies – kein separates Targeting-Pack nötig).

```powershell
# COI_ROOT auf deine Installation zeigen lassen, falls sie vom Standard in build.ps1 abweicht:
$env:COI_ROOT = "C:\Program Files (x86)\Steam\steamapps\common\Captain of Industry"

.\build.ps1 -Config Release
```

Der Build kompiliert `src/CompanySupplier` und deployt DLL + Manifest automatisch in deinen
`%APPDATA%\Captain of Industry\Mods\`-Ordner.

</details>

<details>
<summary><strong>❓ FAQ</strong></summary>

<br>

**Das Menü öffnet sich nicht bei <kbd>F8</kbd>.**
Der Reihe nach prüfen: Mods sind in den Spieloptionen aktiviert, der Mod ist deinem aktuellen Spielstand
hinzugefügt, du bist auf der **stabilen** Spielversion (0.8.5.0, nicht experimental), und
`manifest.json` + `CompanySupplier.dll` liegen in `%APPDATA%\Captain of Industry\Mods\CompanySupplier\`.

**Läuft es auf dem Experimental-Branch des Spiels?**
Nein – der Mod zielt nur auf stabile Releases.

**Läuft es im Multiplayer?**
Nein – Captain of Industry ist Singleplayer, und dieser Mod ebenso.

**Wo finde ich meine Log-Dateien?** (für Fehlerberichte)
`%USERPROFILE%\Documents\Captain of Industry\Logs` – <kbd>Win</kbd> + <kbd>R</kbd>, Pfad einfügen, Enter,
die neueste Datei nehmen.

**Produktnamen erscheinen in einer anderen Sprache.**
Das ist Absicht – die Namen stammen aus der Lokalisierung des Spiels und passen so zu deiner Spielsprache.

**Ein Cheat hat meine Wirtschaft seltsam verändert.**
Das ist zu erwarten – dieses Werkzeug umgeht die Spielwirtschaft bewusst. Mach vorher ein Backup deiner Spielstände.

</details>

## ⚠️ Hinweis

Dies ist ein Singleplayer-Cheat-Werkzeug – es umgeht die normale Wirtschaft des Spiels absichtlich.
**Sichere deine Spielstände vor der Nutzung.**

## 📜 Lizenz & rechtlicher Hinweis

Der **Original-Quellcode** von Company Supplier steht unter einer eigenen, **nicht-kommerziellen
[Personal-Use-Lizenz](LICENSE)**: Du darfst den Code **nutzen** und für dich **selbst verändern**, aber
**nicht verkaufen, kommerziell nutzen oder weitergeben** – auch keine geänderten Versionen. Verteilt wird
der Mod ausschließlich über die offiziellen Releases dieses Repos. (Das ist bewusst **keine**
Open-Source-Lizenz, sondern *source-available*.)

Diese Lizenz gilt **ausschließlich** für diesen eigenen Quellcode und **nicht** für *Captain of Industry*
oder andere MaFi-Games-Materialien (Spielcode, Assets, `Mafi.*`-Assemblies). Diese bleiben Eigentum von
Mafisoft Limited (MaFi Games) und werden ausschließlich im Rahmen der
[Captain of Industry Modding Policy](https://www.captain-of-industry.com/modding-policy) genutzt.

> This Mod includes short excerpts or references to Captain of Industry Game Code. Any such Game Code is
> © MaFi Games and is used only under the Captain of Industry Modding Policy.

Company Supplier ist ein **kostenloser, nicht-kommerzieller** Fan-Mod und **nicht** mit MaFi Games
verbunden oder von ihnen unterstützt. Der Mod selbst darf gemäß der Modding Policy nicht verkauft, gegen
Gebühr weiterlizenziert oder hinter einer Bezahlschranke angeboten werden. Viel Spaß beim Cheaten! 🚀

## 👤 Autor

Company Supplier wird von **[Spacedudee](https://github.com/Spacedudeee)** entwickelt (GitHub-Handle: [`Spacedudeee`](https://github.com/Spacedudeee)).
