using Cerebro.Agent.Realtime;
using NFluent;

namespace Cerebro.Tests.Unit;

[Trait("Category", "Unit")]
public class CertificateThumbprintValidatorTests
{
    [Fact]
    public void IsMatch_ShouldReturnTrue_WhenThumbprintsAreIdentical()
    {
        var result = CertificateThumbprintValidator.IsMatch("AABBCCDD", "AABBCCDD");

        Check.That(result).IsTrue();
    }

    [Fact]
    public void IsMatch_ShouldBeCaseInsensitive()
    {
        var result = CertificateThumbprintValidator.IsMatch("aabbccdd", "AABBCCDD");

        Check.That(result).IsTrue();
    }

    [Theory]
    [InlineData("AA:BB:CC:DD", "AABBCCDD")]
    [InlineData("AA BB CC DD", "AABBCCDD")]
    [InlineData("AA-BB-CC-DD", "AABBCCDD")]
    public void IsMatch_ShouldIgnoreCommonSeparators(string actual, string expected)
    {
        var result = CertificateThumbprintValidator.IsMatch(actual, expected);

        Check.That(result).IsTrue();
    }

    [Fact]
    public void IsMatch_ShouldReturnFalse_WhenThumbprintsDiffer()
    {
        // Cas critique : un certificat différent (ex: MITM) doit être rejeté.
        var result = CertificateThumbprintValidator.IsMatch("AABBCCDD", "11223344");

        Check.That(result).IsFalse();
    }
}
