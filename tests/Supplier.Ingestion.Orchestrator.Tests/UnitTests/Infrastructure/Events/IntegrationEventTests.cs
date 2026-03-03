using FluentAssertions;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;

namespace Supplier.Ingestion.Orchestrator.Tests.UnitTests.Infrastructure.Events;

// Minimal concrete implementation used only in these tests
file record TestIntegrationEvent(string Key) : IntegrationEvent(Key);

public class IntegrationEventTests
{
    [Fact]
    public void CorrelationId_SameBusinessKey_ReturnsSameGuid()
    {
        var a = new TestIntegrationEvent("EXT-001");
        var b = new TestIntegrationEvent("EXT-001");

        a.CorrelationId.Should().Be(b.CorrelationId);
    }

    [Fact]
    public void CorrelationId_DifferentBusinessKeys_ReturnsDifferentGuids()
    {
        var a = new TestIntegrationEvent("EXT-001");
        var b = new TestIntegrationEvent("EXT-002");

        a.CorrelationId.Should().NotBe(b.CorrelationId);
    }

    [Fact]
    public void CorrelationId_IsVersion5Uuid()
    {
        var evt = new TestIntegrationEvent("EXT-001");

        // Version is encoded in the 13th hex digit of the standard string representation
        // e.g. "xxxxxxxx-xxxx-5xxx-xxxx-xxxxxxxxxxxx"
        evt.CorrelationId.ToString()[14].Should().Be('5');
    }

    [Fact]
    public void CorrelationId_HasRfc4122Variant()
    {
        var evt = new TestIntegrationEvent("EXT-001");

        // The variant bits occupy the two most-significant bits of byte 8.
        // RFC 4122 variant = 10xxxxxx → value in [0x80, 0xBF]
        var bytes = evt.CorrelationId.ToByteArray();
        // In .NET's little-endian Guid layout, byte 8 is at index 8
        (bytes[8] & 0xC0).Should().Be(0x80);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhiteSpaceBusinessKey_Throws(string? key)
    {
        var act = () => new TestIntegrationEvent(key!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CorrelationId_KnownVector_MatchesExpected()
    {
        // RFC 4122 §B test vector:
        //   namespace = DNS (6ba7b810-9dad-11d1-80b4-00c04fd430c8)
        //   name      = "www.example.com"
        //   expected  = 2ed6657d-e927-568b-95e3-af9fe9239a39
        var evt = new TestIntegrationEvent("www.example.com");

        evt.CorrelationId.Should().Be(new Guid("2ed6657d-e927-568b-95e3-af9fe9239a39"));
    }
}
