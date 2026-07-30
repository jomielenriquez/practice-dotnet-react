using RiskRegister.Core.Enums;

namespace RiskRegister.Tests;

public class RiskStatusParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Absent_or_blank_means_no_filter(string? raw)
    {
        Assert.True(RiskStatusParser.TryParse(raw, out var status));
        Assert.Null(status);
    }

    [Theory]
    [InlineData("Open", RiskStatus.Open)]
    [InlineData("Mitigating", RiskStatus.Mitigating)]
    [InlineData("Accepted", RiskStatus.Accepted)]
    [InlineData("Closed", RiskStatus.Closed)]
    public void Parses_each_canonical_name(string raw, RiskStatus expected)
    {
        Assert.True(RiskStatusParser.TryParse(raw, out var status));
        Assert.Equal(expected, status);
    }

    [Theory]
    [InlineData("open")]
    [InlineData("OPEN")]
    [InlineData("oPeN")]
    public void Casing_does_not_matter(string raw)
    {
        Assert.True(RiskStatusParser.TryParse(raw, out var status));
        Assert.Equal(RiskStatus.Open, status);
    }

    [Fact]
    public void Surrounding_whitespace_is_trimmed()
    {
        Assert.True(RiskStatusParser.TryParse("  Mitigating  ", out var status));
        Assert.Equal(RiskStatus.Mitigating, status);
    }

    [Theory]
    [InlineData("Nonsense")]
    [InlineData("Opened")]
    [InlineData("Open,Closed")]
    [InlineData("Open Closed")]
    [InlineData("!")]
    public void Rejects_anything_that_is_not_a_status_name(string raw)
    {
        Assert.False(RiskStatusParser.TryParse(raw, out var status));
        Assert.Null(status);
    }

    /// <summary>
    /// The trap this parser exists for. <c>Enum.TryParse</c> returns true for all of these:
    /// "2" parses to <see cref="RiskStatus.Accepted"/>, and "99" parses to an undefined
    /// <c>RiskStatus(99)</c> that would otherwise reach a SQL query.
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("2")]
    [InlineData("99")]
    [InlineData("-1")]
    [InlineData("2147483648")]
    public void Rejects_numeric_input(string raw)
    {
        Assert.False(RiskStatusParser.TryParse(raw, out var status));
        Assert.Null(status);
    }

    [Fact]
    public void Enum_TryParse_alone_would_have_accepted_those_numbers()
    {
        // Pins the reason the numeric guard above exists, so nobody "simplifies" it away.
        Assert.True(Enum.TryParse<RiskStatus>("2", ignoreCase: true, out var fromNumber));
        Assert.Equal(RiskStatus.Accepted, fromNumber);

        Assert.True(Enum.TryParse<RiskStatus>("99", ignoreCase: true, out var undefined));
        Assert.False(Enum.IsDefined(undefined));
    }

    [Fact]
    public void ValidValues_lists_every_status_for_the_error_message()
    {
        Assert.Equal("Open, Mitigating, Accepted, Closed", RiskStatusParser.ValidValues);
    }
}
