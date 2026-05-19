using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;
using ReadTray.Core;

namespace ReadTray.Infrastructure;

public sealed class DpapiSecretStore : ISecretStore
{
    private readonly string _path;

    public DpapiSecretStore()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ReadTray");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "secrets.json");
    }

    public string? GetSecret(string name)
    {
        var values = Load();
        if (!values.TryGetValue(name, out var protectedValue) || string.IsNullOrWhiteSpace(protectedValue))
        {
            return null;
        }

        var bytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedValue), null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }

    public void SetSecret(string name, string? value)
    {
        var values = Load();
        if (string.IsNullOrWhiteSpace(value))
        {
            values.Remove(name);
        }
        else
        {
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser);
            values[name] = Convert.ToBase64String(bytes);
        }

        File.WriteAllText(_path, JsonSerializer.Serialize(values, new JsonSerializerOptions { WriteIndented = true }));
    }

    private Dictionary<string, string> Load()
    {
        if (!File.Exists(_path))
        {
            return new Dictionary<string, string>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path)) ?? new Dictionary<string, string>();
    }
}
