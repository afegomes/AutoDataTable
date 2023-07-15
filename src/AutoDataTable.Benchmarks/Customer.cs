﻿using AutoDataTable.Abstractions;

namespace AutoDataTable.Benchmarks;

[DataTable]
public partial class Customer
{
    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string Email { get; set; }

    public string Address { get; set; }

    public string Country { get; set; }

    public string PhoneNumber { get; set; }

    public int Age { get; set; }
}