using LinkedIn.Data.Import.Features.SchemaInference;

namespace LinkedIn.Data.Import.Tests;

/// <summary>
/// Unit tests for <see cref="TypeDetector"/>'s six type-inference lanes.
/// </summary>
public sealed class TypeDetectorTests
{
    private readonly TypeDetector _sut = new();

    [Fact]
    public void Infer_AllIntegers_ReturnsInt()
    {
        _sut.Infer(["1", "2", "-3", "42"], out var sql, out var clr, out var nullable);
        Assert.Equal("INT", sql);
        Assert.Equal(typeof(int), clr);
        Assert.True(nullable); // Always nullable to prevent NOT NULL constraint violations
    }

    [Fact]
    public void Infer_LargeIntegers_ReturnsBigint()
    {
        _sut.Infer(["3000000000", "4000000000"], out var sql, out var clr, out var nullable);
        Assert.Equal("BIGINT", sql);
        Assert.Equal(typeof(long), clr);
        Assert.True(nullable); // Always nullable to prevent NOT NULL constraint violations
    }

    [Fact]
    public void Infer_Decimals_ReturnsDecimal()
    {
        _sut.Infer(["1.5", "2.75", "-0.001"], out var sql, out var clr, out var nullable);
        Assert.Equal("DECIMAL(18,6)", sql);
        Assert.Equal(typeof(decimal), clr);
        Assert.True(nullable); // Always nullable to prevent NOT NULL constraint violations
    }

    [Fact]
    public void Infer_Dates_ReturnsDatetimeoffset()
    {
        _sut.Infer(["2024-01-15", "2023-12-31T00:00:00+00:00"], out var sql, out var clr, out var nullable);
        Assert.Equal("DATETIMEOFFSET", sql);
        Assert.Equal(typeof(DateTimeOffset), clr);
        Assert.True(nullable); // Always nullable to prevent NOT NULL constraint violations
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("True")]
    public void Infer_BooleanTrueOrFalse_ReturnsBit(string value)
    {
        // Note: "1" and "0" are valid INT values and are caught by the INT
        // check first (higher priority in the type-inference chain).
        _sut.Infer([value, value], out var sql, out var clr, out _);
        Assert.Equal("BIT", sql);
        Assert.Equal(typeof(bool), clr);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("0")]
    public void Infer_OneAndZero_ReturnsIntNotBit(string value)
    {
        // "1" and "0" parse as int32, so INT wins over BIT per the priority order.
        _sut.Infer([value, value], out var sql, out _, out _);
        Assert.Equal("INT", sql);
    }

    [Fact]
    public void Infer_MixedTypes_ReturnsNvarcharMax()
    {
        _sut.Infer(["hello", "42", "world"], out var sql, out var clr, out var nullable);
        Assert.Equal("NVARCHAR(MAX)", sql);
        Assert.Equal(typeof(string), clr);
        Assert.True(nullable); // Always nullable to prevent NOT NULL constraint violations
    }

    [Fact]
    public void Infer_AllEmpty_ReturnsNvarcharMaxAndNullable()
    {
        _sut.Infer(["", " ", "  "], out var sql, out var clr, out var nullable);
        Assert.Equal("NVARCHAR(MAX)", sql);
        Assert.Equal(typeof(string), clr);
        Assert.True(nullable);
    }

    [Fact]
    public void Infer_EmptyEnumerable_ReturnsNvarcharMaxAndNullable()
    {
        _sut.Infer([], out var sql, out var clr, out var nullable);
        Assert.Equal("NVARCHAR(MAX)", sql);
        Assert.Equal(typeof(string), clr);
        Assert.True(nullable);
    }
}
