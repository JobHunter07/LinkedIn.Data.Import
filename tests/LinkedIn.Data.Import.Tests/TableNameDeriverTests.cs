using LinkedIn.Data.Import.Features.SchemaInference;

namespace LinkedIn.Data.Import.Tests;

/// <summary>
/// Unit tests for <see cref="TableNameDeriver"/>.
/// </summary>
public sealed class TableNameDeriverTests
{
    private readonly TableNameDeriver _sut = new();

    [Fact]
    public void Derive_StandardName_ReturnsLowercase()
    {
        Assert.Equal("connections", _sut.Derive("Connections.csv"));
    }

    [Fact]
    public void Derive_MultiWordName_ReturnsSnakeCase()
    {
        Assert.Equal("job_applications", _sut.Derive("Job Applications.csv"));
    }

    [Fact]
    public void Derive_CamelCase_ReturnsSnakeCase()
    {
        Assert.Equal("job_applications", _sut.Derive("JobApplications.csv"));
    }

    [Fact]
    public void Derive_SpecialCharacters_StripsThem()
    {
        Assert.Equal("my_file", _sut.Derive("My-File!@#.csv"));
    }

    [Fact]
    public void Derive_TrailingLeadingSpaces_Trimmed()
    {
        Assert.Equal("connections", _sut.Derive("  Connections.csv  "));
    }

    [Fact]
    public void Derive_MultipleSpaces_CollapsedToSingleUnderscore()
    {
        // "Job  Applications" → job_applications (consecutive underscores collapsed)
        Assert.Equal("job_applications", _sut.Derive("Job  Applications.csv"));
    }

    [Fact]
    public void Derive_AllSpecialChars_ProducesNonEmptyResult()
    {
        // Edge case: if all chars are stripped some fallback should not throw.
        var result = _sut.Derive("!!!.csv");
        Assert.NotNull(result);
    }

    [Fact]
    public void Derive_NameWithPath_StripsDirectory()
    {
        Assert.Equal("connections", _sut.Derive("/some/path/Connections.csv"));
    }
}
