using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

// Local developer-only admin gate. Deliberately NOT tied to the visible player name:
// admin status only depends on a secret key file that ships with nobody's build but
// the developer's own machine. Only the SHA-256 hash is baked into the game - the
// plaintext key itself never appears in the shipped assembly.
public static class AdminAuth
{
    const string ExpectedHash = "b19a86162ca2403f939aa5c65932e3c1829e2ce5eb2c173b0d4652165c3f1365";

    static bool checkedOnce;
    static bool isAdmin;

    public static bool IsAdmin
    {
        get
        {
            if (!checkedOnce)
                Refresh();
            return isAdmin;
        }
    }

    public static void Refresh()
    {
        checkedOnce = true;
        isAdmin = false;

        string keyPath = Path.Combine(Application.persistentDataPath, "admin.key");
        if (!File.Exists(keyPath))
            return;

        try
        {
            string content = File.ReadAllText(keyPath).Trim();
            if (content.Length == 0)
                return;

            isAdmin = Hash(content) == ExpectedHash;
        }
        catch
        {
            isAdmin = false;
        }
    }

    static string Hash(string input)
    {
        using SHA256 sha = SHA256.Create();
        byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        StringBuilder sb = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
