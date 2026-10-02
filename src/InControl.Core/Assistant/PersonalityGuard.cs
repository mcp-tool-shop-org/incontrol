using System.Text.RegularExpressions;

namespace InControl.Core.Assistant;

/// <summary>
/// Validates assistant responses against personality constraints.
/// Ensures the assistant doesn't use forbidden language patterns.
/// </summary>
public static partial class PersonalityGuard
{
    /// <summary>
    /// Forbidden phrases that violate personality constraints.
    /// </summary>
    private static readonly string[] ForbiddenPhrases =
    [
        "i feel",
        "i think emotionally",
        "i'm sorry you feel",
        "as an ai",
        "i'm just an ai",
        "what a great question",
        "great question",
        "excellent question",
        "that's a wonderful",
        "i apologize profusely",
        "i'm deeply sorry",
        "my sincere apologies",
        "i beg your pardon"
    ];

    /// <summary>
    /// Excessive hedging patterns to avoid.
    /// </summary>
    private static readonly string[] ExcessiveHedgingPatterns =
    [
        "i might possibly perhaps",
        "it could potentially maybe",
        "there's a chance that possibly",
        "i would tentatively suggest that maybe"
    ];

    /// <summary>
    /// Flattery patterns to avoid.
    /// </summary>
    private static readonly string[] FlatteryPatterns =
    [
        "you're absolutely right",
        "that's brilliant",
        "what a clever",
        "you're so smart",
        "that's amazing insight"
    ];

    /// <summary>
    /// Validates a response against personality constraints.
    /// </summary>
    /// <param name="response">The response text to validate.</param>
    /// <returns>Validation result with any violations found.</returns>
    public static PersonalityValidationResult Validate(string response)
    {
        if (string.IsNullOrEmpty(response))
        {
            return PersonalityValidationResult.Valid();
        }

        var violations = new List<PersonalityViolation>();
        var lowerResponse = response.ToLowerInvariant();

        // Check forbidden phrases
        foreach (var phrase in ForbiddenPhrases)
        {
            if (lowerResponse.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(new PersonalityViolation(
                    ViolationType.ForbiddenPhrase,
                    phrase,
                    $"Response contains forbidden phrase: '{phrase}'"
                ));
            }
        }

        // Check excessive hedging
        foreach (var pattern in ExcessiveHedgingPatterns)
        {
            if (lowerResponse.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(new PersonalityViolation(
                    ViolationType.ExcessiveHedging,
                    pattern,
                    $"Response contains excessive hedging: '{pattern}'"
                ));
            }
        }

        // Check flattery
        foreach (var pattern in FlatteryPatterns)
        {
            if (lowerResponse.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(new PersonalityViolation(
                    ViolationType.Flattery,
                    pattern,
                    $"Response contains flattery: '{pattern}'"
                ));
            }
        }

        // Check for blame patterns
        if (ContainsBlamePattern(lowerResponse))
        {
            violations.Add(new PersonalityViolation(
                ViolationType.Blame,
                "blame pattern",
                "Response appears to blame the user"
            ));
        }

        return violations.Count == 0
            ? PersonalityValidationResult.Valid()
            : PersonalityValidationResult.Invalid(violations);
    }

    /// <summary>
    /// Checks if the response contains blame patterns.
    /// </summary>
    private static bool ContainsBlamePattern(string lowerResponse)
    {
        // Patterns that blame the user
        var blamePatterns = new[]
        {
            "you should have",
            "you failed to",
            "you didn't follow",
            "that's your fault",
            "you made a mistake",
            "if you had only"
        };

        return blamePatterns.Any(pattern =>
            lowerResponse.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if a response is appropriately concise for the given verbosity setting.
    /// </summary>
    public static bool IsConcise(string response, Verbosity verbosity)
    {
        if (string.IsNullOrEmpty(response))
            return true;

        var wordCount = response.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        return verbosity switch
        {
            Verbosity.Brief => wordCount <= 50,
            Verbosity.Concise => wordCount <= 200,
            Verbosity.Detailed => true, // No limit for detailed
            _ => true
        };
    }
}

/// <summary>
/// Result of personality validation.
/// </summary>
public sealed record PersonalityValidationResult
{
    /// <summary>
    /// Whether the response passes validation.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// List of violations found.
    /// </summary>
    public IReadOnlyList<PersonalityViolation> Violations { get; init; } = [];

    /// <summary>
    /// Creates a valid result.
    /// </summary>
    public static PersonalityValidationResult Valid() => new() { IsValid = true };

    /// <summary>
    /// Creates an invalid result with violations.
    /// </summary>
    public static PersonalityValidationResult Invalid(IReadOnlyList<PersonalityViolation> violations) =>
        new() { IsValid = false, Violations = violations };
}

/// <summary>
/// A single personality constraint violation.
/// </summary>
public sealed record PersonalityViolation(
    ViolationType Type,
    string Pattern,
    string Description
);

/// <summary>
/// Types of personality violations.
/// </summary>
public enum ViolationType
{
    /// <summary>
    /// Contains a forbidden phrase.
    /// </summary>
    ForbiddenPhrase,

    /// <summary>
    /// Contains excessive hedging language.
    /// </summary>
    ExcessiveHedging,

    /// <summary>
    /// Contains flattery.
    /// </summary>
    Flattery,

    /// <summary>
    /// Contains blame language.
    /// </summary>
    Blame,

    /// <summary>
    /// Response is too verbose.
    /// </summary>
    TooVerbose
}
