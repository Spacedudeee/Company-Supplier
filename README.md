<h1 align="center">🏭 Company Supplier</h1>

<p align="center">
  <strong>Das In-Game-Cheat- &amp; Trainer-Menü für <em>Captain of Industry</em></strong><br>
  Gib dir Ressourcen, schraube an Fahrzeugen und Gelände, beherrsche das Wetter und mehr – alles aus
  einem Fenster. Ein Druck auf <kbd>F8</kbd>, und du bestimmst die Regeln.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Captain%20of%20Industry-0.8.5.0-E8730C?style=flat-square" alt="Captain of Industry 0.8.5.0">
  <img src="https://img.shields.io/github/v/release/Spacedudeee/Company-Supplier?style=flat-square&label=Release&color=blue" alt="Release-Version">
  <img src="https://img.shields.io/github/downloads/Spacedudeee/Company-Supplier/total?style=flat-square&color=success&label=Downloads" alt="Downloads">
  <img src="https://img.shields.io/badge/Modus-Singleplayer-blue?style=flat-square" alt="Singleplayer">
  <img src="https://img.shields.io/badge/Lizenz-MIT-green?style=flat-square" alt="Lizenz MIT">
</p>

---

## ✨ Was ist das?

**Company Supplier** ist ein Cheat-/Trainer-Menü für den Singleplayer-Fabrikaufbau *Captain of Industry*
(MaFi Games). Kein Konsolen-Gefummel: Du öffnest mit <kbd>F8</kbd> ein verschiebbares Fenster mit
übersichtlichen Reitern und schaltest per Klick frei, was du brauchst – von „gib mir 10.000 Stahl“ bis
„fülle alle Lager auf einen Schlag“.

> [!NOTE]
> **Lust auf mehr?** Die **v2.0-Beta** bringt einen echten Kreativmodus, Verschmutzungs-Cheats, eine
> Weltkarten-Ebene, ein God-Werkzeug und Profile/Presets – als **separater Test-Mod neben** diesem Stable,
> ohne ihn anzutasten. Hol sie dir als **Pre-Release** auf der
> [Releases-Seite](https://github.com/Spacedudeee/Company-Supplier/releases).

## 🚀 Schnellstart

1. Lade `CompanySupplier-v1.0.0.zip` aus dem **Latest**-Release auf der
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
| 🏗️ **Allgemein** | Bau & Wartung, Bevölkerung & Zufriedenheit, Forschung freischalten, Unity gutschreiben. |
| ⚡ **Erzeugung** | Strom-, Computing- und Unity-Erzeugung direkt setzen. |
| 🚢 **Werft & Flotte** | Steuerung des Welt-Schiffs (Flotte). |
| 🚚 **Fahrzeuge** | Treibstoffverbrauch aus, Fahrzeuglimit anheben, LKW-Ladekapazität vervielfachen. |
| ⛰️ **Gelände** | Sofort abbauen / verfüllen / umwandeln, Grundwasser & Erdöl auffüllen, Bäume pflanzen oder entfernen. |
| ☀️ **Wetter** | Das Wetter dauerhaft auf einen Zustand fixieren. |

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
**Sichere deine Spielstände vor der Nutzung.** Nicht mit MaFi Games verbunden oder von ihnen unterstützt.

## 📜 Lizenz

[MIT](LICENSE) – mach damit, was du willst. Viel Spaß beim Cheaten! 🚀
