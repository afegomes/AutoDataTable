AutoDataTable
=======

![Nuget](https://img.shields.io/nuget/v/AutoDataTable)

AutoDataTable is a .NET library that simplifies the object to row conversion when dealing with `System.Data.DataTable`.

It uses the Source Generators from Roslyn to generate the conversion code, instead of using reflection during runtime.

Using the generated code not only results in a faster object conversion, but also allows the app to be published as
Native AOT.


### Prerequisites

Since this library uses Source Generators, the app needs to target at least .NET Standard 2.0.


### Installation

Use the .NET CLI to install the package and its dependencies:
   ```sh
   dotnet add package AutoDataTable
   ```


### Usage

The classes or structs targeted for conversion should have the `DataTable` attribute:

   ```csharp
   using AutoDataTable.Core;
   
   namespace MyProject;
   
   [DataTable]
   public class Customer
   {
   }
   ```

By default, the `DataTable` instance will be created using the class name as the table name, but this
can be overridden using the attribute's properties:

   ```csharp
   [DataTable("Customers")]
   public class Customer
   {
   }
   ```

The columns will use the property name by default. While it's not required to add the `DataColumn` attribute to the
properties, it can be used to override the column name:

   ```csharp
   [DataTable]
   public class Customer
   {
       [DataColumn("phone_number")]
       public string PhoneNumber { get; set; }
   }
   ```

To create a `DataTable` you need to first declare a factory method with the `static` and `partial` modifiers and no body.
Then you should annotate the factory method with the `GenerateDataTable` attribute and provide as a parameter the type
you wish to convert:

   ```csharp
   public partial class CustomerService
   {
       [GenerateDataTable(typeof(Customer))]
       private static partial DataTableFactory CustomerFactory();
       
       public DataTable CreateCustomerTable()
       {
           return CustomerFactory().Create();
       }
   }
   ```

---
**NOTE**

The factory method implementation returns a singleton, so you don't need to cache it.

---

To convert an object to a row, just call the `AddRow` method:

   ```csharp
   var customer = new Customer();
   
   dataTable.AddRow(customer);
   ```


## Contributing

Pull requests are welcome, but please open an issue first to discuss your idea.


## License

Distributed under the GPL-3.0 License. See `LICENSE` for more information.


## Changelog

### 1.1 (2023-10-30)

* Added support for structs
* Factory method can now be placed anywhere

### 1.0 (2023-07-30)

First release