using AutoDataTable.Abstractions;

namespace AutoDataTable.Tests;

[DataTable]
public partial class Default
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