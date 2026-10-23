using System.Text.Json;
using Microsoft.Win32;

namespace Pitanga.Core;

public sealed class AppSettings
{
    /// <summary>PDF → JPG/PNG: false = só a primeira página; true = uma imagem por página.</summary>
    public bool PdfAllPages { get; set; }

    /// <summary>Resolução de renderização de PDF para imagem.</summary>
    public int PdfRenderDpi { get; set; } = 200;

    public static AppSettings Current { get; } = Load();

    static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pitanga", "settings.json");

    static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch (Exception ex) { Log.Error(ex, "settings load"); }
        return new AppSettings();
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { Log.Error(ex, "settings save"); }
    }
}

/// <summary>
/// Iniciar com o Windows.
/// - Instalação normal (instalador / .exe): entrada em HKCU\...\Run, sem privilégios de administrador.
/// - Pacote MSIX (Microsoft Store): a Store não permite a chave Run; usa a StartupTask declarada no
///   AppxManifest (TaskId "PitangaStartup"), que o usuário também controla em Configurações → Apps → Inicialização.
/// </summary>
public static class StartWithWindows
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "Pitanga";
    const string StartupTaskId = "PitangaStartup";

    public static bool IsEnabled
    {
        get
        {
            if (IsPackaged) return RunOffUiThread(async () => (await GetStartupTask()).State is Windows.ApplicationModel.StartupTaskState.Enabled or Windows.ApplicationModel.StartupTaskState.EnabledByPolicy);
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void Set(bool enabled)
    {
        if (IsPackaged)
        {
            RunOffUiThread(async () =>
            {
                var task = await GetStartupTask();
                if (enabled) await task.RequestEnableAsync();
                else task.Disable();
                return true;
            });
            return;
        }

        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled && Environment.ProcessPath is { } exe) key.SetValue(ValueName, $"\"{exe}\"");
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    static async Task<Windows.ApplicationModel.StartupTask> GetStartupTask() =>
        await Windows.ApplicationModel.StartupTask.GetAsync(StartupTaskId);

    // Bloquear a thread de UI esperando uma operação WinRT pode travar; espera numa thread do pool.
    static T RunOffUiThread<T>(Func<Task<T>> work) => Task.Run(work).GetAwaiter().GetResult();

    /// <summary>true quando roda como pacote MSIX (tem identidade de pacote).</summary>
    public static bool IsPackaged { get; } = DetectPackage();

    static bool DetectPackage()
    {
        int length = 0;
        // APPMODEL_ERROR_NO_PACKAGE (15700) = processo comum, sem pacote
        return GetCurrentPackageFullName(ref length, null) != 15700;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);
}
