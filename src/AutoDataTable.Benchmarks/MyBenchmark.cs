using System.Data;
using BenchmarkDotNet.Attributes;
using Bogus;

namespace AutoDataTable.Benchmarks;

[MemoryDiagnoser]
public class MyBenchmark
{
    private List<Customer> _customers;

    [GlobalSetup]
    public void Setup()
    {
        Randomizer.Seed = new Random(8675309);

        var faker = new Faker<Customer>()
            .StrictMode(true)
            .RuleFor(x => x.FirstName, f => f.Person.FirstName)
            .RuleFor(x => x.LastName, f => f.Person.LastName)
            .RuleFor(x => x.Email, f => f.Internet.Email())
            .RuleFor(x => x.PhoneNumber, f => f.Person.Phone)
            .RuleFor(x => x.Address, f => f.Person.Address.ToString())
            .RuleFor(x => x.Country, f => f.Address.Country())
            .RuleFor(x => x.Age, f => f.Random.Int(18, 70));

        _customers = faker.Generate(1000);
    }

    [Benchmark]
    public DataTable Reflection()
    {
        var type = typeof(Customer);
        var properties = type.GetProperties();

        var table = new DataTable(type.Name);

        foreach (var p in properties)
        {
            var column = new DataColumn(p.Name, p.PropertyType);
            table.Columns.Add(column);
        }

        foreach (var customer in _customers)
        {
            var row = table.NewRow();

            foreach (var p in properties)
            {
                row[p.Name] = p.GetValue(customer)!;
            }

            table.Rows.Add(row);
        }

        return table;
    }

    [Benchmark]
    public DataTable Generated()
    {
        var table = Customer.CreateDataTable();

        _customers.ForEach(c => table.AddRow(c));

        return table;
    }
}