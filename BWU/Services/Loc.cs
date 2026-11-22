namespace BWU.Services;

/// <summary>
/// Minimal localisation for navigation and page headers (English / Brazilian Portuguese).
/// Body text of the pages is English-only in version 1.0.
/// </summary>
public static class Loc
{
    private static readonly Dictionary<string, (string En, string Pt)> Strings = new()
    {
        ["dashboard"] = ("Dashboard", "Painel"),
        ["dashboard.sub"] = ("Your system at a glance", "Seu sistema em resumo"),
        ["sysinfo"] = ("System Information", "Informações do Sistema"),
        ["sysinfo.sub"] = ("Detailed information read from this computer", "Informações detalhadas lidas deste computador"),
        ["optimization"] = ("Optimization", "Otimização"),
        ["optimization.sub"] = ("Reversible performance settings, startup apps and services", "Ajustes reversíveis, aplicativos de inicialização e serviços"),
        ["debloater"] = ("Debloater", "Debloater"),
        ["debloater.sub"] = ("Review and remove optional preinstalled apps", "Revise e remova aplicativos pré-instalados opcionais"),
        ["customization"] = ("Customization", "Personalização"),
        ["customization.sub"] = ("Winver owner, OEM information and personalization", "Proprietário no winver, informações OEM e personalização"),
        ["packages"] = ("Package Manager", "Gerenciador de Pacotes"),
        ["packages.sub"] = ("Install and manage software with Windows Package Manager", "Instale e gerencie programas com o Windows Package Manager"),
        ["maintenance"] = ("Maintenance", "Manutenção"),
        ["maintenance.sub"] = ("Integrity checks, repairs and restore points", "Verificações de integridade, reparos e pontos de restauração"),
        ["morespaces"] = ("More Spaces", "More Spaces"),
        ["morespaces.sub"] = ("Recover disk space without touching personal files", "Recupere espaço em disco sem tocar nos seus arquivos"),
        ["privacy"] = ("Privacy", "Privacidade"),
        ["privacy.sub"] = ("Understandable controls for Windows privacy settings", "Controles claros para as configurações de privacidade"),
        ["settings"] = ("Settings", "Configurações"),
        ["settings.sub"] = ("Theme, language, startup, updates and about", "Tema, idioma, inicialização, atualizações e sobre"),
    };

    public static string Language { get; private set; } = "en";
    public static event Action? Changed;

    public static void SetLanguage(string language)
    {
        Language = language == "pt-BR" ? "pt-BR" : "en";
        Changed?.Invoke();
    }

    public static string T(string key)
        => Strings.TryGetValue(key, out var v) ? (Language == "pt-BR" ? v.Pt : v.En) : key;
}
