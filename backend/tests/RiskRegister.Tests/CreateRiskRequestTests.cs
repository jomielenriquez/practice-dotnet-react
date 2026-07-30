using System.ComponentModel.DataAnnotations;
using RiskRegister.Api.Contracts;

namespace RiskRegister.Tests;

/// <summary>
/// The validation rules from SPEC.md, exercised the way MVC exercises them.
/// </summary>
/// <remarks>
/// <c>[ApiController]</c> rejects an invalid body before the action method runs, so a controller
/// test can never see one — the rules have to be pinned here. <see cref="Validator"/> is the same
/// component MVC's DataAnnotations provider uses, and the keys it produces are the ones that end up
/// in <c>ValidationProblemDetails.Errors</c> (camelCased on the wire).
/// </remarks>
public class CreateRiskRequestTests
{
    private static CreateRiskRequest Valid() => new()
    {
        Title = "Customer database has no tested restore path",
        Description = "Nightly backups report success; a restore has never been attempted.",
        Owner = "Priya Raman",
        Likelihood = 4,
        Impact = 5
    };

    private static IReadOnlyList<ValidationResult> Validate(CreateRiskRequest request)
    {
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);

        return results;
    }

    private static void AssertFails(CreateRiskRequest request, string field)
    {
        var failures = Validate(request);

        Assert.NotEmpty(failures);

        // The frontend maps the error back to a control by this name, so an error that names no
        // field — or the wrong one — is as bad as no validation at all.
        Assert.Contains(failures, failure => failure.MemberNames.Contains(field));
    }

    [Fact]
    public void A_complete_body_is_valid()
    {
        Assert.Empty(Validate(Valid()));
    }

    [Fact]
    public void Description_is_optional()
    {
        Assert.Empty(Validate(Valid() with { Description = null }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]       // trimmed to empty, so it is "missing", not "too short"
    [InlineData("ab")]
    public void Rejects_a_title_that_is_missing_or_too_short(string? title)
    {
        AssertFails(Valid() with { Title = title! }, nameof(CreateRiskRequest.Title));
    }

    [Fact]
    public void Rejects_a_title_over_200_characters()
    {
        AssertFails(Valid() with { Title = new string('x', 201) }, nameof(CreateRiskRequest.Title));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(200)]
    public void Accepts_a_title_at_the_length_boundaries(int length)
    {
        Assert.Empty(Validate(Valid() with { Title = new string('x', length) }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_missing_owner(string? owner)
    {
        AssertFails(Valid() with { Owner = owner! }, nameof(CreateRiskRequest.Owner));
    }

    [Fact]
    public void Rejects_an_owner_over_100_characters()
    {
        AssertFails(Valid() with { Owner = new string('x', 101) }, nameof(CreateRiskRequest.Owner));
    }

    [Fact]
    public void Rejects_a_description_over_2000_characters()
    {
        AssertFails(
            Valid() with { Description = new string('x', 2001) },
            nameof(CreateRiskRequest.Description));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(300)]   // would overflow a byte; int? keeps it a range error, not a JSON error
    public void Rejects_a_likelihood_outside_1_to_5(int? likelihood)
    {
        AssertFails(Valid() with { Likelihood = likelihood }, nameof(CreateRiskRequest.Likelihood));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(6)]
    public void Rejects_an_impact_outside_1_to_5(int? impact)
    {
        AssertFails(Valid() with { Impact = impact }, nameof(CreateRiskRequest.Impact));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Accepts_the_axis_boundaries(int axis)
    {
        Assert.Empty(Validate(Valid() with { Likelihood = axis, Impact = axis }));
    }

    [Fact]
    public void Reports_every_offending_field_at_once()
    {
        // One round trip, not one per mistake: the form marks up all of its fields from a single
        // response.
        var failures = Validate(new CreateRiskRequest { Title = "ab", Owner = "", Likelihood = 9 });

        Assert.Contains(failures, f => f.MemberNames.Contains(nameof(CreateRiskRequest.Title)));
        Assert.Contains(failures, f => f.MemberNames.Contains(nameof(CreateRiskRequest.Owner)));
        Assert.Contains(failures, f => f.MemberNames.Contains(nameof(CreateRiskRequest.Likelihood)));
        Assert.Contains(failures, f => f.MemberNames.Contains(nameof(CreateRiskRequest.Impact)));
    }

    [Fact]
    public void Trims_the_text_fields_on_the_way_in()
    {
        // Trimming before validation, not after: "  ab  " is 6 characters raw and would sneak past
        // a 3-character minimum, then fail the database's CHECK constraint as a 503.
        var request = Valid() with { Title = "  Padded title  ", Owner = "  Priya Raman  " };

        Assert.Equal("Padded title", request.Title);
        Assert.Equal("Priya Raman", request.Owner);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalises_a_blank_description_to_null(string description)
    {
        // Absent and blank mean the same thing; storing "" would give the frontend two cases to
        // handle for one state.
        Assert.Null((Valid() with { Description = description }).Description);
    }
}
