namespace LandaDoc.Shared.Security;

// The rules a doctor's password must follow. Shared by the Identity service (which enforces them
// when a doctor registers or changes their password) and the web app (which shows the same rules
// as a checklist while typing), so the two can never disagree.
public enum PasswordRule
{
    Length,        // 8 to 100 characters
    Lowercase,     // at least one lowercase letter
    Uppercase,     // at least one uppercase letter
    Digit,         // at least one digit
    Special,       // at least one character that's neither a letter nor a digit
    Repetition,    // no character more than 3 times in a row ("aaaa")
    Sequence,      // no run of more than 3 consecutive characters ("1234", "abcd", "dcba")
    PersonalData,  // doesn't contain the person's first or last name, username or company name
}

public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 100;
    public const int MaxRepeat = 3;     // "aaa" is fine, "aaaa" isn't
    public const int MaxSequence = 3;   // "abc" is fine, "abcd" isn't

    // The platform itself counts as the company name
    private static readonly string[] CompanyNames = ["landadoc"];

    // Shorter names are too common to count as personal ("Li", "Jo"). Words taken out of a longer
    // name must be longer still, so "des" in "Clinique des Anges" doesn't ban "Desk".
    private const int MinPersonalPart = 3;
    private const int MinPersonalWord = 4;

    public static IReadOnlyList<PasswordRule> AllRules { get; } = Enum.GetValues<PasswordRule>();

    // The rules this password breaks; empty when it's acceptable. personalData: first name, last
    // name, email (its part before "@" is the username), and any other names to avoid, such as
    // the doctor's clinics.
    public static IReadOnlyList<PasswordRule> Check(string? password, params string?[] personalData)
    {
        password ??= "";
        var failed = new List<PasswordRule>();
        if (password.Length is < MinLength or > MaxLength) failed.Add(PasswordRule.Length);
        if (!password.Any(char.IsLower)) failed.Add(PasswordRule.Lowercase);
        if (!password.Any(char.IsUpper)) failed.Add(PasswordRule.Uppercase);
        if (!password.Any(char.IsDigit)) failed.Add(PasswordRule.Digit);
        if (!password.Any(c => !char.IsLetterOrDigit(c))) failed.Add(PasswordRule.Special);
        if (HasRepetition(password)) failed.Add(PasswordRule.Repetition);
        if (HasSequence(password)) failed.Add(PasswordRule.Sequence);
        if (ContainsPersonalData(password, personalData)) failed.Add(PasswordRule.PersonalData);
        return failed;
    }

    private static bool HasRepetition(string password)
    {
        var run = 1;
        for (var i = 1; i < password.Length; i++)
        {
            run = char.ToLowerInvariant(password[i]) == char.ToLowerInvariant(password[i - 1]) ? run + 1 : 1;
            if (run > MaxRepeat) return true;
        }
        return false;
    }

    // Letters and digits that follow each other up or down ("abcd", "4321"), ignoring case
    private static bool HasSequence(string password)
    {
        int up = 1, down = 1;
        for (var i = 1; i < password.Length; i++)
        {
            char prev = char.ToLowerInvariant(password[i - 1]), cur = char.ToLowerInvariant(password[i]);
            var sameKind = (char.IsDigit(prev) && char.IsDigit(cur)) || (IsAsciiLetter(prev) && IsAsciiLetter(cur));
            up = sameKind && cur == prev + 1 ? up + 1 : 1;
            down = sameKind && cur == prev - 1 ? down + 1 : 1;
            if (up > MaxSequence || down > MaxSequence) return true;
        }
        return false;
    }

    private static bool IsAsciiLetter(char c) => c is >= 'a' and <= 'z';

    private static bool ContainsPersonalData(string password, string?[] personalData)
    {
        var lower = password.ToLowerInvariant();
        return personalData
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .SelectMany(Parts)
            .Concat(CompanyNames)
            .Any(lower.Contains);
    }

    // "jean-paul.mukendi@mail.cd" -> "jean-paul.mukendi", "jean", "paul", "mukendi";
    // "Clinique Ngaliema" -> "clinique ngaliema", "clinique", "ngaliema"
    private static IEnumerable<string> Parts(string? value)
    {
        var text = value!.Trim().ToLowerInvariant();
        var at = text.IndexOf('@');
        if (at > 0) text = text[..at];
        if (text.Length >= MinPersonalPart) yield return text;
        foreach (var word in text.Split([' ', '.', '-', '_', '\''], StringSplitOptions.RemoveEmptyEntries))
            if (word.Length >= MinPersonalWord && word != text) yield return word;
    }
}
