using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using learn_Assist.Models;

namespace learn_Assist.Services;

public static class ConfigEncryption
{
    private static readonly byte[] _salt = [0x2A, 0xBC, 0x91, 0x4D, 0xE7, 0x38, 0x5F, 0xC3];

    private static byte[] DeriveKey()
    {
        var machine = Environment.MachineName;
        var user = Environment.UserName;
        return Rfc2898DeriveBytes.Pbkdf2(
            machine + "::learn-assist::" + user,
            _salt,
            200000,
            HashAlgorithmName.SHA256,
            32);
    }

    public static string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return string.Empty;

        var key = DeriveKey();
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        var iv = aes.IV;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var result = new byte[iv.Length + cipherBytes.Length];
        Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, iv.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    public static string Decrypt(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
            return string.Empty;

        var key = DeriveKey();
        var fullBytes = Convert.FromBase64String(ciphertext);
        using var aes = Aes.Create();
        aes.Key = key;
        var iv = new byte[16];
        Buffer.BlockCopy(fullBytes, 0, iv, 0, iv.Length);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var cipherBytes = new byte[fullBytes.Length - iv.Length];
        Buffer.BlockCopy(fullBytes, iv.Length, cipherBytes, 0, cipherBytes.Length);
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }

    private static string GetConfigDir()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "learn-assist");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string GetConfigPath() => Path.Combine(GetConfigDir(), "config.enc");

    public static void SaveConfig(ApiConfig config)
    {
        var json = JsonSerializer.Serialize(config);
        var encrypted = Encrypt(json);
        File.WriteAllText(GetConfigPath(), encrypted);
    }

    public static ApiConfig? LoadConfig()
    {
        var path = GetConfigPath();
        if (!File.Exists(path))
            return null;

        try
        {
            var encrypted = File.ReadAllText(path);
            var json = Decrypt(encrypted);
            return JsonSerializer.Deserialize<ApiConfig>(json);
        }
        catch
        {
            return null;
        }
    }

    public static bool ConfigExists() => File.Exists(GetConfigPath());
}
