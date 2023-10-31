using AutoDataTable.Core;
using Xunit;

namespace AutoDataTable.Tests;

public partial class DataTableTest
{
    [GenerateDataTable(typeof(Default))]
    private static partial DataTableFactory DefaultFactory();

    [GenerateDataTable(typeof(Customized))]
    private static partial DataTableFactory CustomizedFactory();

    [Fact]
    public void ShouldCreateDataTableUsingDefaultNames()
    {
        // Arrange
        var data = new Default
        {
            SomeBoolean = true,
            SomeByte = 1,
            SomeChar = 'a',
            SomeDateTime = new DateTime(2023, 07, 29, 15, 43, 20, DateTimeKind.Utc),
            SomeDecimal = 10.99m,
            SomeDouble = 1.52,
            SomeInt = 1001,
            SomeString = "all your base are belong yo us"
        };

        // Act
        var table = DefaultFactory().Create();
        table.AddRow(data);

        // Assert
        Assert.Equal(1, table.Rows.Count);

        var row = table.Rows[0];

        Assert.Equal(data.SomeBoolean, row["SomeBoolean"]);
        Assert.Equal(data.SomeByte, row["SomeByte"]);
        Assert.Equal(data.SomeChar, row["SomeChar"]);
        Assert.Equal(data.SomeDateTime, row["SomeDateTime"]);
        Assert.Equal(data.SomeDecimal, row["SomeDecimal"]);
        Assert.Equal(data.SomeDouble, row["SomeDouble"]);
        Assert.Equal(data.SomeInt, row["SomeInt"]);
        Assert.Equal(data.SomeString, row["SomeString"]);
    }

    [Fact]
    public void ShouldCreateDataTableUsingCustomNames()
    {
        // Arrange
        var data = new Customized
        {
            SomeString = "you can't hold no groove if you ain't got no pocket"
        };

        // Act
        var table = CustomizedFactory().Create();
        table.AddRow(data);

        // Assert
        Assert.Equal(1, table.Rows.Count);
        Assert.Equal(data.SomeString, table.Rows[0]["some_string"]);
    }
}

[DataTable]
public class Default
{
    public bool SomeBoolean { get; init; }

    public byte SomeByte { get; init; }

    public char SomeChar { get; init; }

    public DateTime SomeDateTime { get; init; }

    public decimal SomeDecimal { get; init; }

    public double SomeDouble { get; init; }

    public int SomeInt { get; init; }

    public string SomeString { get; init; }
}

[DataTable("some_table")]
public struct Customized
{
    [DataColumn("some_string")]
    public string SomeString { get; init; }
}