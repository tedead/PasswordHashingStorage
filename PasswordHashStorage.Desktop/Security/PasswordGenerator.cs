using System.Security.Cryptography;

namespace PasswordHashStorage.Desktop.Security;

public static class PasswordGenerator
{
    private const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    private const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Digits    = "0123456789";
    private const string Symbols   = "!@#$%^&*-_=+";

    public static string GenerateRandom(int length, bool useLower, bool useUpper, bool useDigits, bool useSymbols)
    {
        if (length < 1)
            throw new ArgumentOutOfRangeException(nameof(length));

        string charset = BuildCharset(useLower, useUpper, useDigits, useSymbols);
        if (charset.Length == 0)
            charset = Lowercase + Digits;

        char[] result = new char[length];
        for (int i = 0; i < result.Length; i++)
            result[i] = charset[RandomNumberGenerator.GetInt32(charset.Length)];

        return new string(result);
    }

    public static IEnumerable<string> GenerateMutations(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            yield break;

        string t = word.Trim();
        string cap = Capitalize(t);
        string lower = t.ToLowerInvariant();
        string upper = t.ToUpperInvariant();

        yield return t;
        yield return lower;
        yield return upper;
        yield return cap;

        string[] suffixes = { "!", "@", "#", "1", "12", "123", "1234", "2023", "2024", "2025", "2026" };
        foreach (string s in suffixes)
        {
            yield return lower + s;
            yield return cap + s;
            yield return upper + s;
        }

        yield return ApplyLeet(lower);
        yield return ApplyLeet(cap);
    }

    public static IEnumerable<string> GenerateExhaustive(string charset, int length)
    {
        if (length <= 0 || string.IsNullOrEmpty(charset))
            yield break;

        char[] buffer = new char[length];
        foreach (string s in FillPosition(0))
            yield return s;

        IEnumerable<string> FillPosition(int pos)
        {
            if (pos == length)
            {
                yield return new string(buffer);
                yield break;
            }
            foreach (char c in charset)
            {
                buffer[pos] = c;
                foreach (string s in FillPosition(pos + 1))
                    yield return s;
            }
        }
    }

    private static string BuildCharset(bool lower, bool upper, bool digits, bool symbols)
    {
        string cs = string.Empty;
        if (lower)   cs += Lowercase;
        if (upper)   cs += Uppercase;
        if (digits)  cs += Digits;
        if (symbols) cs += Symbols;
        return cs;
    }

    private static string Capitalize(string value) =>
        value.Length == 0
            ? value
            : char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();

    private static string ApplyLeet(string value) =>
        value
            .Replace('a', '@').Replace('A', '@')
            .Replace('e', '3').Replace('E', '3')
            .Replace('i', '1').Replace('I', '1')
            .Replace('o', '0').Replace('O', '0')
            .Replace('s', '$').Replace('S', '$');
}
