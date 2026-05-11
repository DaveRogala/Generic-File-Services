using GenericFileServices.Models;
using MagellanFileServices.Models;

namespace GenericFileServices.Tests.Models;

public class FileResultsTests
{
    [Fact]
    public void DefaultConstructor_SetsEmptyFileName()
    {
        var result = new FileResults<string>();

        Assert.Equal("", result.FileName);
    }

    [Fact]
    public void NamedConstructor_SetsAllProperties()
    {
        var data = new List<string> { "row1", "row2" };
        var errors = new List<string> { "error1" };

        var result = new FileResults<string>("myfile.csv", data, errors);

        Assert.Equal("myfile.csv", result.FileName);
        Assert.Equal(data, result.ObjectResults);
        Assert.Equal(errors, result.Errors);
    }

    [Fact]
    public void ObjectResultConstructor_WrapsResultAndSetsFileName()
    {
        var data = new List<string> { "a", "b" };
        var errors = new List<string>();
        var objectResult = new ObjectResult<string>(data, errors);

        var result = new FileResults<string>(objectResult, "source.csv");

        Assert.Equal("source.csv", result.FileName);
        Assert.Equal(data, result.ObjectResults);
        Assert.Equal(errors, result.Errors);
    }

    [Fact]
    public void FileName_CanBeReassigned()
    {
        var result = new FileResults<string> { FileName = "original.csv" };

        result.FileName = "renamed.csv";

        Assert.Equal("renamed.csv", result.FileName);
    }
}
