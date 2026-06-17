using System.Collections.Generic;
using Mafi;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CompanySupplier.UI;

namespace CompanySupplier.UI.Tabs
{
    /// <summary>
    /// Reiter "Profil": Komfort rund um die Dauer-Cheats — Panik-Aus (alles abschalten), Auto-Restore
    /// (gespeicherten Zustand beim Spielstart anwenden) sowie Zustand manuell speichern/wiederherstellen.
    ///
    /// Stuetzt sich auf die zentrale Zustands-Verwaltung des <see cref="CheatService"/> und die
    /// persistente <see cref="CompanySupplier.Config.ModConfig"/>.
    /// </summary>
    [GlobalDependency(RegistrationMode.AsEverything)]
    public sealed class ProfilTab : ICheatTab
    {
        private readonly UiComponent _content;

        public ProfilTab()
        {
            _content = BuildContent();
        }

        public string Name => "Profil";

        // Save-Icon (General) — passt zum Profil-Tab (Cheat-Setup/Presets speichern & laden).
        public string IconPath => "Assets/Unity/UserInterface/General/Save.svg";

        public UiComponent Content => _content;

        private static CheatService Svc => CheatService.Instance;

        private UiComponent BuildContent()
        {
            var column = new Column((Px)CheatWidgets.Gap).AlignItemsStretch().Padding((Px)15);

            var children = new List<UiComponent>
            {
                CheatWidgets.SectionTitle("Sicherheit"),
                CheatWidgets.DangerButton(
                    "Panik-Aus (alle Dauer-Cheats aus)",
                    () =>
                    {
                        Svc?.DisableAllContinuousCheats();
                        CheatMenuStatus.Show("Alle Dauer-Cheats abgeschaltet");
                    },
                    "Schaltet auf einen Schlag ALLE laufenden Dauer-Cheats ab und setzt die Geschwindigkeit auf 1x."),

                CheatWidgets.SectionTitle("Cheat-Profil"),
                BuildAutoRestoreToggle(),
                BuildSaveRestoreButtons(),

                CheatWidgets.SectionTitle("Preset-Slots"),
                BuildPresetRow(1),
                BuildPresetRow(2),
                BuildPresetRow(3)
            };

            column.SetChildren(children.ToArray());
            return column;
        }

        // Ein Preset-Slot: Speichern (aktuellen Zustand) + Laden (Slot anwenden).
        private UiComponent BuildPresetRow(int slot)
        {
            var save = CheatWidgets.PrimaryButton(
                $"Slot {slot} speichern",
                () =>
                {
                    Svc?.SavePreset(slot);
                    CheatMenuStatus.Show($"Preset-Slot {slot} gespeichert");
                },
                "Speichert den aktuellen Dauer-Cheat-Zustand in diesen Slot.");

            var load = CheatWidgets.GeneralButton(
                $"Slot {slot} laden",
                () =>
                {
                    bool ok = Svc?.LoadPreset(slot) ?? false;
                    CheatMenuStatus.Show(ok ? $"Preset-Slot {slot} geladen" : $"Preset-Slot {slot} ist leer");
                },
                "Wendet den in diesem Slot gespeicherten Zustand an.");

            var row = new Row((Px)CheatWidgets.Gap);
            row.SetChildren(save, load);
            return row;
        }

        // Auto-Restore: beim Spielstart den gespeicherten Zustand automatisch anwenden.
        private UiComponent BuildAutoRestoreToggle()
        {
            bool initial = Svc?.Config?.AutoRestore ?? true;
            return CheatWidgets.NewToggleRow(
                "Auto-Restore beim Laden",
                initial,
                v =>
                {
                    if (Svc?.Config != null)
                    {
                        Svc.Config.AutoRestore = v;
                        Svc.SaveConfig();
                    }
                    CheatMenuStatus.Show(v ? "Auto-Restore AN" : "Auto-Restore AUS");
                },
                "Wendet den zuletzt gespeicherten Cheat-Zustand beim Öffnen des Menüs nach einem Spielstand-Laden automatisch an.");
        }

        // Zustand speichern / manuell wiederherstellen.
        private UiComponent BuildSaveRestoreButtons()
        {
            var save = CheatWidgets.PrimaryButton(
                "Zustand speichern",
                () =>
                {
                    Svc?.SaveCurrentStateToConfig();
                    CheatMenuStatus.Show("Aktueller Cheat-Zustand gespeichert");
                },
                "Merkt sich alle aktuell aktiven Dauer-Cheats (für Auto-Restore und manuelles Wiederherstellen).");

            var restore = CheatWidgets.GeneralButton(
                "Zustand wiederherstellen",
                () =>
                {
                    if (Svc?.Config != null) Svc.ApplyState(Svc.Config.Toggles);
                    CheatMenuStatus.Show("Gespeicherter Cheat-Zustand angewendet");
                },
                "Wendet den zuletzt gespeicherten Cheat-Zustand sofort an.");

            var row = new Row((Px)CheatWidgets.Gap);
            row.SetChildren(save, restore);
            return row;
        }
    }
}
