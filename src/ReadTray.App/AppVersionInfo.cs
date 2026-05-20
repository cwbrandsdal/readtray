using System.Reflection;

namespace ReadTray.App;

public static class AppVersionInfo
{
    public static string Current
    {
        get
        {
            var assembly = Assembly.GetEntryAssembly() ?? typeof(AppVersionInfo).Assembly;
            var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informational))
            {
                return informational.Split('+')[0];
            }

            return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }
    }
}
