using RiskRegister.Core.Enums;
using RiskRegister.Core.Scoring;

namespace RiskRegister.Tests;

public class RiskScoringTests
{
    /// <summary>
    /// Every boundary of the SPEC.md table, from both sides: the last score in each band and the
    /// first score in the next. An off-by-one in any comparison fails at least two of these.
    /// </summary>
    [Theory]
    [InlineData(1, Severity.Low)]
    [InlineData(4, Severity.Low)]
    [InlineData(5, Severity.Medium)]
    [InlineData(9, Severity.Medium)]
    [InlineData(10, Severity.High)]
    [InlineData(15, Severity.High)]
    [InlineData(16, Severity.Critical)]
    [InlineData(25, Severity.Critical)]
    public void SeverityFor_maps_each_band_boundary(int score, Severity expected)
    {
        Assert.Equal(expected, RiskScoring.SeverityFor(score));
    }

    /// <summary>The three worked examples given in the acceptance criteria.</summary>
    [Theory]
    [InlineData(3, 3, 9, Severity.Medium)]
    [InlineData(5, 5, 25, Severity.Critical)]
    [InlineData(1, 1, 1, Severity.Low)]
    public void Worked_examples_from_the_spec(int likelihood, int impact, int expectedScore, Severity expectedSeverity)
    {
        var score = likelihood * impact;

        Assert.Equal(expectedScore, score);
        Assert.Equal(expectedSeverity, RiskScoring.SeverityFor(score));
    }

    /// <summary>Every score the 1–5 axes can actually produce lands in some band.</summary>
    [Fact]
    public void Every_reachable_score_maps_to_a_defined_band()
    {
        for (var likelihood = RiskScoring.MinAxis; likelihood <= RiskScoring.MaxAxis; likelihood++)
        {
            for (var impact = RiskScoring.MinAxis; impact <= RiskScoring.MaxAxis; impact++)
            {
                var severity = RiskScoring.SeverityFor(likelihood * impact);

                Assert.True(Enum.IsDefined(severity), $"{likelihood} x {impact} produced {severity}");
            }
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void IsValidAxis_accepts_only_one_through_five(int value, bool expected)
    {
        Assert.Equal(expected, RiskScoring.IsValidAxis(value));
    }
}
