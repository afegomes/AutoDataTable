using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoDataTable
{
    [Generator]
    public sealed class ExtensionsGenerator : IIncrementalGenerator
    {
        private const string DataTableAttribute = "AutoDataTable.Abstractions.DataTableAttribute";
        private const string DataColumnAttribute = "AutoDataTable.Abstractions.DataColumnAttribute";

        private static readonly DiagnosticDescriptor Adt001 = new DiagnosticDescriptor(id: "ADT001", title: "Class is not partial", messageFormat: "The class should be declared as partial", category: "Design", DiagnosticSeverity.Error, isEnabledByDefault: true);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var classes = context.SyntaxProvider
                .CreateSyntaxProvider(IsClassWithAttributes, GetClassDeclarationSyntax)
                .Where(cds => cds != null)
                .Collect();

            var combined = context.CompilationProvider.Combine(classes);

            context.RegisterSourceOutput(combined, (ctx, source) => GenerateSource(source.Left, source.Right, ctx));
        }

        private static bool IsClassWithAttributes(SyntaxNode node, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();

            if (node is ClassDeclarationSyntax classDeclarationSyntax)
            {
                return classDeclarationSyntax.AttributeLists.Count > 0;
            }

            return false;
        }

        private static ClassDeclarationSyntax GetClassDeclarationSyntax(GeneratorSyntaxContext ctx, CancellationToken cancellation)
        {
            var classDeclaration = (ClassDeclarationSyntax)ctx.Node;

            foreach (var list in classDeclaration.AttributeLists)
            {
                foreach (var attributeSyntax in list.Attributes)
                {
                    cancellation.ThrowIfCancellationRequested();

                    if (!(ctx.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol is IMethodSymbol attributeSymbol))
                    {
                        continue;
                    }

                    var fullName = attributeSymbol.ContainingType.ToDisplayString();

                    if (fullName == DataTableAttribute)
                    {
                        return classDeclaration;
                    }
                }
            }

            return null;
        }

        private static void GenerateSource(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes, SourceProductionContext ctx)
        {
            if (classes.IsDefaultOrEmpty)
            {
                return;
            }

            for (var i = 0; i < classes.Length; i++)
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();

                var classDeclarationSyntax = classes[i];

                if (!IsPartial(classDeclarationSyntax))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Adt001, classDeclarationSyntax.GetLocation()));

                    continue;
                }

                var model = compilation.GetSemanticModel(classDeclarationSyntax.SyntaxTree);

                if (!(model.GetDeclaredSymbol(classDeclarationSyntax) is INamedTypeSymbol classSymbol))
                {
                    continue;
                }

                var ns = GetNamespace(classDeclarationSyntax);

                ctx.AddSource($"{classSymbol.Name}.g.cs", GenerateFactoryMethod(compilation, classSymbol, ns));
            }

            ctx.AddSource("DataTableExtensions.g.cs", GenerateDataTableExtensions(compilation, classes, ctx.CancellationToken));
        }

        private static string GenerateFactoryMethod(Compilation compilation, INamedTypeSymbol classSymbol, string ns)
        {
            var source = new StringBuilder();

            using (var writer = new IndentedTextWriter(new StringWriter(source), "\t"))
            {
                writer.WriteLine("// Auto-generated code");
                writer.WriteLine("using System.Data;");
                writer.WriteLine();
                writer.WriteLine($"namespace {ns}");
                writer.WriteLine("{");
                writer.Indent++;

                writer.WriteLine($"public partial class {classSymbol.Name}");
                writer.WriteLine("{");
                writer.Indent++;

                GenerateCreateMethod(writer, compilation, classSymbol);

                writer.Indent--;
                writer.WriteLine("}");

                writer.Indent--;
                writer.WriteLine("}");
            }

            return source.ToString();
        }

        private static void GenerateCreateMethod(IndentedTextWriter writer, Compilation compilation, INamedTypeSymbol classSymbol)
        {
            writer.WriteLine("public static DataTable CreateDataTable()");
            writer.WriteLine("{");
            writer.Indent++;

            var tableName = GetTableName(compilation, classSymbol);

            writer.WriteLine($"var table = new DataTable(\"{tableName}\");");

            var attributeSymbol = compilation.GetTypeByMetadataName(DataColumnAttribute);

            foreach (var p in GetProperties(classSymbol))
            {
                var columnName = GetColumnName(attributeSymbol, p);

                writer.WriteLine($"table.Columns.Add(new DataColumn(\"{columnName}\", typeof({p.Type.Name})));");
            }

            writer.WriteLine();
            writer.WriteLine("return table;");

            writer.Indent--;
            writer.WriteLine("}");
        }

        private static string GenerateDataTableExtensions(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes, CancellationToken cancellation)
        {
            var source = new StringBuilder();

            using (var writer = new IndentedTextWriter(new StringWriter(source), "\t"))
            {
                writer.WriteLine("// Auto-generated code");
                writer.WriteLine("using System.Data;");
                writer.WriteLine();
                writer.WriteLine("namespace AutoDataTable");
                writer.WriteLine("{");
                writer.Indent++;

                writer.WriteLine("public static class DataTableExtensions");
                writer.WriteLine("{");
                writer.Indent++;

                for (var i = 0; i < classes.Length; i++)
                {
                    cancellation.ThrowIfCancellationRequested();

                    var classDeclarationSyntax = classes[i];

                    GenerateAddRowMethod(writer, compilation, classDeclarationSyntax);

                    if (i < classes.Length - 1)
                    {
                        writer.WriteLine();
                    }
                }

                writer.Indent--;
                writer.WriteLine("}");

                writer.Indent--;
                writer.WriteLine("}");
            }

            return source.ToString();
        }

        private static void GenerateAddRowMethod(IndentedTextWriter writer, Compilation compilation, SyntaxNode classDeclarationSyntax)
        {
            var model = compilation.GetSemanticModel(classDeclarationSyntax.SyntaxTree);

            if (!(model.GetDeclaredSymbol(classDeclarationSyntax) is INamedTypeSymbol classSymbol))
            {
                return;
            }

            var ns = GetNamespace(classDeclarationSyntax);
            var fullType = string.IsNullOrEmpty(ns) ? classSymbol.Name : $"{ns}.{classSymbol.Name}";

            writer.WriteLine($"public static void AddRow(this DataTable table, {fullType} data)");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine("var row = table.NewRow();");

            var attributeSymbol = compilation.GetTypeByMetadataName(DataColumnAttribute);

            foreach (var property in GetProperties(classSymbol))
            {
                var columnName = GetColumnName(attributeSymbol, property);

                writer.WriteLine($"row[\"{columnName}\"] = data.{property.Name};");
            }

            writer.WriteLine();
            writer.WriteLine("table.Rows.Add(row);");

            writer.Indent--;
            writer.WriteLine("}");
        }

        private static IEnumerable<IPropertySymbol> GetProperties(INamedTypeSymbol symbol)
        {
            foreach (var member in symbol.GetMembers())
            {
                if (member.IsStatic || member.DeclaredAccessibility != Accessibility.Public || !(member is IPropertySymbol property))
                {
                    continue;
                }

                yield return property;
            }
        }

        private static string GetNamespace(SyntaxNode syntax)
        {
            var candidate = syntax.Parent;

            while (candidate != null
                   && !(candidate is NamespaceDeclarationSyntax)
                   && !(candidate is FileScopedNamespaceDeclarationSyntax))
            {
                candidate = candidate.Parent;
            }

            if (!(candidate is BaseNamespaceDeclarationSyntax namespaceParent))
            {
                return null;
            }

            var ns = namespaceParent.Name.ToString();

            while (true)
            {
                if (!(namespaceParent.Parent is NamespaceDeclarationSyntax parent))
                {
                    break;
                }

                namespaceParent = parent;
                ns = $"{namespaceParent.Name}.{ns}";
            }

            return ns;
        }

        private static string GetTableName(Compilation compilation, ISymbol classSymbol)
        {
            var attributeSymbol = compilation.GetTypeByMetadataName(DataTableAttribute);

            if (attributeSymbol is null)
            {
                return classSymbol.Name;
            }

            foreach (var attributeData in classSymbol.GetAttributes())
            {
                if (!attributeSymbol.Equals(attributeData.AttributeClass, SymbolEqualityComparer.Default))
                {
                    continue;
                }

                if (attributeData.ConstructorArguments.Length == 0)
                {
                    break;
                }

                var tableName = (string)attributeData.ConstructorArguments[0].Value;

                if (string.IsNullOrEmpty(tableName))
                {
                    break;
                }

                return tableName;
            }

            return classSymbol.Name;
        }

        private static string GetColumnName(ISymbol attributeSymbol, ISymbol propertySymbol)
        {
            foreach (var attributeData in propertySymbol.GetAttributes())
            {
                if (!attributeSymbol.Equals(attributeData.AttributeClass, SymbolEqualityComparer.Default))
                {
                    continue;
                }

                var propertyName = (string)attributeData.ConstructorArguments[0].Value;

                if (string.IsNullOrEmpty(propertyName))
                {
                    break;
                }

                return propertyName;
            }

            return propertySymbol.Name;
        }

        private static bool IsPartial(ClassDeclarationSyntax classDeclarationSyntax)
        {
            for (var i = 0; i < classDeclarationSyntax.Modifiers.Count; i++)
            {
                var modifier = classDeclarationSyntax.Modifiers[i];

                if (modifier.Text == "partial")
                {
                    return true;
                }
            }

            return false;
        }
    }
}