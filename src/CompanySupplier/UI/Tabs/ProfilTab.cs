using System.Collections.Generic;
using Mafi;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using CompanySupplier.UI;
using CompanySupplier.Localization;

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

        private Toggle _autoRestoreToggle;
        private bool _suppress;

        public ProfilTab()
        {
            _content = BuildContent();
            CheatUiSync.Register(nameof(ProfilTab), SyncFromState);
        }

        /// <summary>Zieht den Auto-Restore-Toggle aus der Config nach (via CheatUiSync).</summary>
        private void SyncFromState()
        {
            _suppress = true;
            try { _autoRestoreToggle?.Value(Svc?.Config?.AutoRestore ?? true); }
            finally { _suppress = false; }
        }

        public string Name => L.Tab_Profil;

        // Save-Icon (General) — passt zum Profil-Tab (Cheat-Setup/Presets speichern & laden).
        public string IconPath => "Assets/Unity/UserInterface/General/Save.svg";

        public UiComponent Content => _content;

        private static CheatService Svc => CheatService.Instance;

        private UiComponent BuildContent()
        {
            var column = new Column((Px)CheatWidgets.Gap).AlignItemsStretch().Padding((Px)15);

            var children = new List<UiComponent>
            {
                CheatWidgets.SectionTitle(L.Prf_TitleSafety),
                CheatWidgets.DangerButton(
                    L.Prf_PanicOff,
                    () =>
                    {
                        Svc?.DisableAllContinuousCheats();
                        CheatMenuStatus.Show(L.Prf_StatusPanic);
                    },
                    L.Prf_PanicOffTip),

                CheatWidgets.SectionTitle(L.Prf_TitleProfile),
                BuildAutoRestoreToggle(),
                BuildSaveRestoreButtons(),

                CheatWidgets.SectionTitle(L.Prf_TitlePresets),
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
                L.Prf_SlotSave(slot),
                () =>
                {
                    Svc?.SavePreset(slot);
                    CheatMenuStatus.Show(L.Prf_StatusSlotSaved(slot));
                },
                L.Prf_SlotSaveTip);

            var load = CheatWidgets.GeneralButton(
                L.Prf_SlotLoad(slot),
                () =>
                {
                    bool ok = Svc?.LoadPreset(slot) ?? false;
                    CheatMenuStatus.Show(ok ? L.Prf_StatusSlotLoaded(slot) : L.Prf_StatusSlotEmpty(slot));
                },
                L.Prf_SlotLoadTip);

            var row = new Row((Px)CheatWidgets.Gap);
            row.SetChildren(save, load);
            return row;
        }

        // Auto-Restore: beim Spielstart den gespeicherten Zustand automatisch anwenden.
        private UiComponent BuildAutoRestoreToggle()
        {
            bool initial = Svc?.Config?.AutoRestore ?? true;
            _autoRestoreToggle = CheatWidgets.NewToggleRow(
                L.Prf_AutoRestore,
                initial,
                v =>
                {
                    if (_suppress) return;
                    if (Svc?.Config != null)
                    {
                        Svc.Config.AutoRestore = v;
                        Svc.SaveConfig();
                    }
                    CheatMenuStatus.Show(v ? L.Prf_StatusAutoRestoreOn : L.Prf_StatusAutoRestoreOff);
                },
                L.Prf_AutoRestoreTip);
            return _autoRestoreToggle;
        }

        // Zustand speichern / manuell wiederherstellen.
        private UiComponent BuildSaveRestoreButtons()
        {
            var save = CheatWidgets.PrimaryButton(
                L.Prf_SaveState,
                () =>
                {
                    Svc?.SaveCurrentStateToConfig();
                    CheatMenuStatus.Show(L.Prf_StatusStateSaved);
                },
                L.Prf_SaveStateTip);

            var restore = CheatWidgets.GeneralButton(
                L.Prf_RestoreState,
                () =>
                {
                    if (Svc?.Config != null) Svc.ApplyState(Svc.Config.Toggles);
                    CheatMenuStatus.Show(L.Prf_StatusStateApplied);
                },
                L.Prf_RestoreStateTip);

            var row = new Row((Px)CheatWidgets.Gap);
            row.SetChildren(save, restore);
            return row;
        }
    }
}
