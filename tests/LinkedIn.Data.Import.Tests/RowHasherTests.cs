using LinkedIn.Data.Import.Features.ImportTracking;

namespace LinkedIn.Data.Import.Tests;

/// <summary>
/// Unit tests for <see cref="RowHasher"/>.
/// </summary>
public sealed class RowHasherTests
{
    private readonly RowHasher _sut = new();

    [Fact]
    public void Hash_SameInputsTwice_ReturnsSameHash()
    {
        var values = new[] { "Alice", "Smith", "alice@example.com" };
        var h1 = _sut.Hash(values);
        var h2 = _sut.Hash(values);
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void Hash_DifferentValues_ReturnsDifferentHash()
    {
        var h1 = _sut.Hash(["Alice", "Smith"]);
        var h2 = _sut.Hash(["Bob", "Jones"]);
        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void Hash_ReturnsLowercaseHex()
    {
        var hash = _sut.Hash(["value"]);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void Hash_TrimsWhitespace_ProducesConsistentOutput()
    {
        var h1 = _sut.Hash(["  Alice  "]);
        var h2 = _sut.Hash(["Alice"]);
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void Hash_NullValues_TreatedAsEmpty()
    {
        var h1 = _sut.Hash([null, "Alice"]);
        var h2 = _sut.Hash(["", "Alice"]);
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void Hash_DifferentColumnOrder_ReturnsDifferentHash()
    {
        var h1 = _sut.Hash(["Alice", "Smith"]);
        var h2 = _sut.Hash(["Smith", "Alice"]);
        Assert.NotEqual(h1, h2);
    }
}
