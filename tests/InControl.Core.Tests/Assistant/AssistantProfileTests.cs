using FluentAssertions;
using InControl.Core.Assistant;
using Xunit;

namespace InControl.Core.Tests.Assistant;

public class AssistantProfileTests
{
    [Fact]
    public void Default_HasProfessionalTone()
    {
        var profile = AssistantProfile.Default;

        profile.Tone.Should().Be(Tone.Professional);
    }

    [Fact]
    public void Default_IsConcise()
    {
        var profile = AssistantProfile.Default;

        profile.Verbosity.Should().Be(Verbosity.Concise);
    }

    [Fact]
    public void Default_ExplainsOnRequest()
    {
        var profile = AssistantProfile.Default;

        profile.ExplanationLevel.Should().Be(ExplanationLevel.OnRequest);
    }

    [Fact]
    public void Default_HasModerateRiskTolerance()
    {
        var profile = AssistantProfile.Default;

        profile.RiskTolerance.Should().Be(RiskTolerance.Moderate);
    }

    [Fact]
    public void Minimal_IsBrief()
    {
        var profile = AssistantProfile.Minimal;

        profile.Verbosity.Should().Be(Verbosity.Brief);
        profile.ExplanationLevel.Should().Be(ExplanationLevel.Minimal);
    }

    [Fact]
    public void Detailed_IsDetailed()
    {
        var profile = AssistantProfile.Detailed;

        profile.Verbosity.Should().Be(Verbosity.Detailed);
        profile.ExplanationLevel.Should().Be(ExplanationLevel.Proactive);
    }

    [Theory]
    [InlineData(Tone.Professional)]
    [InlineData(Tone.Friendly)]
    [InlineData(Tone.Direct)]
    public void Tone_AllValuesAreDefined(Tone tone)
    {
        Enum.IsDefined(tone).Should().BeTrue();
    }

    [Theory]
    [InlineData(Verbosity.Brief)]
    [InlineData(Verbosity.Concise)]
    [InlineData(Verbosity.Detailed)]
    public void Verbosity_AllValuesAreDefined(Verbosity verbosity)
    {
        Enum.IsDefined(verbosity).Should().BeTrue();
    }
}

public class PersonalityGuardTests
{
    [Fact]
    public void Validate_AcceptsNormalResponse()
    {
        var response = "The model is ready. You can start a conversation now.";

        var result = PersonalityGuard.Validate(response);

        result.IsValid.Should().BeTrue();
        result.Violations.Should().BeEmpty();
    }

    [Fact]
    public void Validate_RejectsForbiddenPhrase_IFeel()
    {
        var response = "I feel that this approach would work better.";

        var result = PersonalityGuard.Validate(response);

        result.IsValid.Should().BeFalse();
        result.Violations.Should().Contain(v => v.Type == ViolationType.ForbiddenPhrase);
    }

    [Fact]
    public void Validate_RejectsForbiddenPhrase_AsAnAI()
    {
        var response = "As an AI, I cannot access the internet.";

        var result = PersonalityGuard.Validate(response);

        result.IsValid.Should().BeFalse();
        result.Violations.Should().Contain(v => v.Pattern == "as an ai");
    }

    [Fact]
    public void Validate_RejectsForbiddenPhrase_JustAnAI()
    {
        var response = "I'm just an AI, so I have limitations.";

        var result = PersonalityGuard.Validate(response);

        result.IsValid.Should().BeFalse();
        result.Violations.Should().Contain(v => v.Pattern == "i'm just an ai");
    }

    [Fact]
    public void Validate_RejectsFlattery_GreatQuestion()
    {
        var response = "What a great question! Let me explain...";

        var result = PersonalityGuard.Validate(response);

        result.IsValid.Should().BeFalse();
        // "great question" is in ForbiddenPhrases list
        result.Violations.Should().Contain(v => v.Type == ViolationType.ForbiddenPhrase);
    }

    [Fact]
    public void Validate_RejectsFlattery_ExcellentQuestion()
    {
        var response = "Excellent question! Here's the answer...";

        var result = PersonalityGuard.Validate(response);

        result.IsValid.Should().BeFalse();
        // "excellent question" is in ForbiddenPhrases list
        result.Violations.Should().Contain(v => v.Type == ViolationType.ForbiddenPhrase);
    }

    [Fact]
    public void Validate_RejectsFlattery_AbsolutelyRight()
    {
        var response = "You're absolutely right about that!";

        var result = PersonalityGuard.Validate(response);

        result.IsValid.Should().BeFalse();
        result.Violations.Should().Contain(v => v.Type == ViolationType.Flattery);
    }

    [Fact]
    public void Validate_RejectsExcessiveApology()
    {
        var response = "I apologize profusely for this error.";

        var result = PersonalityGuard.Validate(response);

        result.IsValid.Should().BeFalse();
        result.Violations.Should().Contain(v => v.Type == ViolationType.ForbiddenPhrase);
    }

    [Fact]
    public void Validate_RejectsBlame()
    {
        var response = "You should have configured the settings correctly.";

        var result = PersonalityGuard.Validate(response);

        result.IsValid.Should().BeFalse();
        result.Violations.Should().Contain(v => v.Type == ViolationType.Blame);
    }

    [Fact]
    public void Validate_RejectsBlame_Failed()
    {
        var response = "You failed to provide the required parameters.";

        var result = PersonalityGuard.Validate(response);

        result.IsValid.Should().BeFalse();
        result.Violations.Should().Contain(v => v.Type == ViolationType.Blame);
    }

    [Fact]
    public void Validate_AcceptsSimpleApology()
    {
        var response = "An error occurred. Please try again.";

        var result = PersonalityGuard.Validate(response);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_AcceptsProfessionalCorrection()
    {
        var response = "The parameter format is incorrect. Expected: 'name=value'.";

        var result = PersonalityGuard.Validate(response);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_AcceptsEmptyResponse()
    {
        var result = PersonalityGuard.Validate(string.Empty);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_AcceptsNullResponse()
    {
        var result = PersonalityGuard.Validate(null!);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void IsConcise_ReturnsTrueForBriefWithin50Words()
    {
        var response = string.Join(" ", Enumerable.Repeat("word", 45));

        var result = PersonalityGuard.IsConcise(response, Verbosity.Brief);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsConcise_ReturnsFalseForBriefOver50Words()
    {
        var response = string.Join(" ", Enumerable.Repeat("word", 60));

        var result = PersonalityGuard.IsConcise(response, Verbosity.Brief);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsConcise_ReturnsTrueForConciseWithin200Words()
    {
        var response = string.Join(" ", Enumerable.Repeat("word", 150));

        var result = PersonalityGuard.IsConcise(response, Verbosity.Concise);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsConcise_AlwaysTrueForDetailed()
    {
        var response = string.Join(" ", Enumerable.Repeat("word", 500));

        var result = PersonalityGuard.IsConcise(response, Verbosity.Detailed);

        result.Should().BeTrue();
    }
}

public class PersonalityViolationTests
{
    [Theory]
    [InlineData(ViolationType.ForbiddenPhrase)]
    [InlineData(ViolationType.ExcessiveHedging)]
    [InlineData(ViolationType.Flattery)]
    [InlineData(ViolationType.Blame)]
    [InlineData(ViolationType.TooVerbose)]
    public void ViolationType_AllValuesAreDefined(ViolationType type)
    {
        Enum.IsDefined(type).Should().BeTrue();
    }

    [Fact]
    public void PersonalityViolation_RecordsAllProperties()
    {
        var violation = new PersonalityViolation(
            ViolationType.ForbiddenPhrase,
            "i feel",
            "Response contains forbidden phrase"
        );

        violation.Type.Should().Be(ViolationType.ForbiddenPhrase);
        violation.Pattern.Should().Be("i feel");
        violation.Description.Should().Contain("forbidden phrase");
    }
}
