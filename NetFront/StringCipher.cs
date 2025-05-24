using System.Security.Cryptography;
using System.Text;

namespace NetFront;

public static class StringCipher
{
    public static string Encrypt(string text, string key, string iv)
    {
        var sourceBytes = Encoding.UTF8.GetBytes(text);
        var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = Encoding.UTF8.GetBytes(key);
        aes.IV = Encoding.UTF8.GetBytes(iv);
        var transform = aes.CreateEncryptor();
        return Convert.ToBase64String(transform.TransformFinalBlock(sourceBytes, 0, sourceBytes.Length));
    }

    public static string Decrypt(string text, string key, string iv)
    {
        var encryptBytes = Convert.FromBase64String(text);
        var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = Encoding.UTF8.GetBytes(key);
        aes.IV = Encoding.UTF8.GetBytes(iv);
        var transform = aes.CreateDecryptor();
        return Encoding.UTF8.GetString(transform.TransformFinalBlock(encryptBytes, 0, encryptBytes.Length));
    }
}
