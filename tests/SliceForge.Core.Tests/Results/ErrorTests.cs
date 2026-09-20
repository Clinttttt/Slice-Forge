using SliceForge.Results;

namespace SliceForge.Core.Tests.Results;

public sealed class ErrorTests
{
    [Fact]
    public void Constructor_should_preserve_error_details()
    {
        IReadOnlyDictionary<string, IReadOnlyList<string>> validationErrors =
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["Name"] = new[] { "Name is required." }
            };

        Error error = new("Sample.Required", "A required value is missing.", ErrorType.Validation, validationErrors);

        Assert.Equal("Sample.Required", error.Code);
        Assert.Equal("A required value is missing.", error.Description);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("Name is required.", error.ValidationErrors["Name"][0]);
    }

    [Fact]
    public void Constructor_should_reject_blank_code()
    {
        Assert.Throws<ArgumentException>(() => new Error(" ", "Description", ErrorType.Failure));
    }

    [Fact]
    public void Constructor_should_reject_blank_description()
    {
        Assert.Throws<ArgumentException>(() => new Error("Code", " ", ErrorType.Failure));
    }

    [Fact]
    public void Constructor_should_reject_undefined_error_type()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Error("Code", "Description", (ErrorType)999));
    }

    [Fact]
    public void Constructor_should_normalize_null_validation_errors_to_empty()
    {
        Error error = new("Code", "Description", ErrorType.Failure);

        Assert.Empty(error.ValidationErrors);
    }

    [Fact]
    public void Constructor_should_reject_validation_details_for_non_validation_errors()
    {
        IReadOnlyDictionary<string, IReadOnlyList<string>> validationErrors =
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["Name"] = new[] { "Name is required." }
            };

        Assert.Throws<ArgumentException>(() => new Error("Code", "Description", ErrorType.Failure, validationErrors));
    }

    [Fact]
    public void Constructor_should_defensively_copy_validation_details()
    {
        List<string> messages = new() { "Name is required." };
        Dictionary<string, IReadOnlyList<string>> source = new(StringComparer.Ordinal)
        {
            ["Name"] = messages
        };

        Error error = new("Code", "Description", ErrorType.Validation, source);
        source["Name"] = new[] { "A different message." };
        messages[0] = "Another different message.";

        Assert.Equal("Name is required.", error.ValidationErrors["Name"][0]);
    }

    [Fact]
    public void Validation_details_should_be_read_only()
    {
        Error error = new(
            "Code",
            "Description",
            ErrorType.Validation,
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["Name"] = new[] { "Name is required." }
            });

        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)error.ValidationErrors["Name"]).Add("Another message."));
    }
}
