using AutoDataTable.Abstractions;

namespace AutoDataTable.Tests;

[DataTable("some_table")]
public partial class Customized
{
    [DataColumn("some_string")]
    public string SomeString { get; init; }
}