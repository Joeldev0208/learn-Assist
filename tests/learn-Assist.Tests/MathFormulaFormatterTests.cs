using learn_Assist.Controls;
using Xunit;

namespace learn_assist.Tests;

public class MathFormulaFormatterTests
{
    [Fact]
    public void Format_PreservesFractionsAndGroupedPowers()
    {
        var result = MathFormulaFormatter.Format(@"\frac{x^2 + 1}{\sqrt{y}}");

        Assert.Equal("(x^2 + 1)/(√(y))", result);
    }

    [Fact]
    public void Format_ConvertsCommonLatexSymbolsWithoutRemovingStructure()
    {
        var result = MathFormulaFormatter.Format(@"x_{n+1} \leq \infty");

        Assert.Equal("x_(n+1) ≤ ∞", result);
    }
}
