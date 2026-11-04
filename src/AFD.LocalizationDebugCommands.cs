#if DEBUG && COI_UPDATE_4_3
using System;
using System.IO;
using System.Text;
using CoI.AutoHelpers.Settings;
using Mafi;
using Mafi.Core;
using Mafi.Core.Console;
using Mafi.Core.SaveGame;
using Mafi.Localization;
using Mafi.Unity;
using UnityEngine;

namespace AutoForestryDesignations;

/// <summary>Development checks for a real same-process language change through world reload.</summary>
[GlobalDependency(RegistrationMode.AsSelf, false, false)]
public sealed class AfdLocalizationDebugCommands
{
    private readonly IMain m_main;
    private readonly ISaveManager m_saves;
    public AfdLocalizationDebugCommands(IMain main, ISaveManager saves)
    {
        m_main = main;
        m_saves = saves;
    }

    [ConsoleCommand(true, false, "Inspect shared settings registrations and built UI text; optionally open the window.", "afd_localization_inspect")]
    private string Inspect(bool open = true) => ModSettings.DebugInspect(open, "auto-forestry-designations");

    [ConsoleCommand(true, false, "DEBUG: select a language and discard unsaved changes by reloading a named save from this world. Supply the filename without extension as UTF-8 base64.", "afd_localization_reload")]
    private string Reload(string culture, string saveNameBase64)
    {
        if (m_saves.IsNonAutosaveInProgress()) return "Cannot reload during a save.";
        if (!LocalizationManager.TryGetLangInfo(culture, out LocalizationManager.LangInfo lang))
            return "Unknown language: " + culture;
        string name = Encoding.UTF8.GetString(Convert.FromBase64String(saveNameBase64));
        if (name != Path.GetFileName(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return "Expected a save filename without a path.";
        var files = m_main.FileSystemHelper.GetAllFiles(FileType.GameSave, "*", m_saves.GameName);
        if (!Array.Exists(files, file => Path.GetFileNameWithoutExtension(file.Name) == name))
            return "Save not found in the current world: " + name;
        // Same preference API as the game's picker. Main.LoadGame starts a scene;
        // ApplyPreferredLanguage will apply it there, not to the outgoing UI.
        PlayerPrefs.SetString("UserInterfaceLanguage", lang.CultureInfoId);
        PlayerPrefs.Save();
        Log.Info("[AFD_LOCALIZATION_RELOAD] requested=" + culture + "; from=" + LocalizationManager.CurrentLangInfo.CultureInfoId);
        m_main.LoadGame(new SaveFileInfo(name, m_saves.GameName), null);
        return "Reload requested; unsaved changes are discarded.";
    }
}
#endif
