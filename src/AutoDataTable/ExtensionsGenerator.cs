using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoDataTable
{
    [Generator]
    public sealed class ExtensionsGenerator : IIncrementalGenerator
    {
        private const string GenerateDataTableAttribute = "AutoDataTable.Core.GenerateDataTableAttribute";
        private const string DataTableAttribute = "AutoDataTable.Core.DataTableAttribute";
        private const string DataColumnAttribute = "AutoDataTable.Core.DataColumnAttribute";

        private static readonly DiagnosticDescriptor Adt001 = new DiagnosticDescriptor(id: "ADT001", title: "Method is not partial", messageFormat: "The method should be declared as partial", category: "Design", DiagnosticSeverity.Error, isEnabledByDefault: true);
        private static readonly DiagnosticDescriptor Adt002 = new DiagnosticDescriptor(id: "ADT002", title: "Method is not static", messageFormat: "The method should be declared as static", category: "Design", DiagnosticSeverity.Error, isEnabledByDefault: true);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var methods = context.SyntaxProvider
                .CreateSyntaxProvider(IsCandidateClass, GetClassDeclarationSyntax)
                .Where(syntax => syntax != null)
                .Collect();

            var combined = context.CompilationProvider.Combine(methods);

            context.RegisterSourceOutput(combined, (ctx, source) => GenerateSource(source.Left, source.Right, ctx));
        }

        private static bool IsCandidateClass(SyntaxNode node, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();

            if (!(node is ClassDeclarationSyntax classDeclarationSyntax))
            {
                return false;
            }

            return classDeclarationSyntax.Members
                .Any(member => member.Kind() == SyntaxKind.MethodDeclaration && member.AttributeLists.Count > 0);
        }

        private static ClassDeclarationSyntax GetClassDeclarationSyntax(GeneratorSyntaxContext ctx, CancellationToken cancellation)
        {
            var classDeclarationSyntax = (ClassDeclarationSyntax)ctx.Node;

            foreach (var member in classDeclarationSyntax.Members)
            {
                cancellation.ThrowIfCancellationRequested();

                if (IsFactoryMethod(member, ctx.SemanticModel))
                {
                    return classDeclarationSyntax;
                }
            }

            return null;
        }

        private static bool IsFactoryMethod(MemberDeclarationSyntax memberDeclarationSyntax, SemanticModel model)
        {
            if (memberDeclarationSyntax.Kind() != SyntaxKind.MethodDeclaration)
            {
                return false;
            }

            return memberDeclarationSyntax.AttributeLists
                .SelectMany(list => list.Attributes)
                .Any(attribute => IsGenerateDataTableAttribute(attribute, model));
        }

        private static bool IsGenerateDataTableAttribute(SyntaxNode attributeSyntax, SemanticModel model)
        {
            if (!(model.GetSymbolInfo(attributeSyntax).Symbol is IMethodSymbol attributeSymbol))
            {
                return false;
            }

            var fullName = attributeSymbol.ContainingType.ToDisplayString();

            return fullName == GenerateDataTableAttribute;
        }

        private static void GenerateSource(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes, SourceProductionContext ctx)
        {
            if (classes.IsDefaultOrEmpty)
            {
                return;
            }

            var generateDataTableSymbol = compilation.GetTypeByMetadataName(GenerateDataTableAttribute);
            var dataTableSymbol = compilation.GetTypeByMetadataName(DataTableAttribute);
            var dataColumnSymbol = compilation.GetTypeByMetadataName(DataColumnAttribute);

            var entityTypes = new List<INamedTypeSymbol>();

            foreach (var classDeclarationSyntax in classes)
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();

                var model = compilation.GetSemanticModel(classDeclarationSyntax.SyntaxTree);

                if (!(ModelExtensions.GetDeclaredSymbol(model, classDeclarationSyntax) is INamedTypeSymbol classSymbol))
                {
                    continue;
                }

                var ns = GetNamespace(classDeclarationSyntax);
                var methods = GetFactoryMethods(classDeclarationSyntax, model, ctx);

                ctx.AddSource($"{classSymbol.Name}.g.cs", GenerateFactory(ns, classSymbol, generateDataTableSymbol,
                    dataTableSymbol, dataColumnSymbol, methods, entityTypes));
            }

            ctx.AddSource("DataTableExtensions.g.cs", GenerateDataTableExtensions(dataColumnSymbol, entityTypes, ctx.CancellationToken));
        }

        private static IEnumerable<IMethodSymbol> GetFactoryMethods(TypeDeclarationSyntax typeDeclarationSyntax,
            SemanticModel model, SourceProductionContext ctx)
        {
            var methods = typeDeclarationSyntax.Members
                .Where(member => IsFactoryMethod(member, model));

            foreach (var methodDeclarationSyntax in methods)
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();

                var isPartial = methodDeclarationSyntax.Modifiers.Any(x => x.Text == "partial");
                var isStatic = methodDeclarationSyntax.Modifiers.Any(x => x.Text == "static");

                if (!isPartial)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Adt001, methodDeclarationSyntax.GetLocation()));
                }

                if (!isStatic)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Adt002, methodDeclarationSyntax.GetLocation()));
                }

                if (isPartial && isStatic)
                {
                    yield return ModelExtensions.GetDeclaredSymbol(model, methodDeclarationSyntax) as IMethodSymbol;
                }
            }
        }

        private static string GenerateFactory(string ns, ISymbol enclosingClass, ISymbol generateDataTableSymbol,
            ISymbol dataTableSymbol, ISymbol dataColumnSymbol, IEnumerable<IMethodSymbol> methods,
            List<INamedTypeSymbol> entityTypes)
        {
            var source = new StringBuilder();

            using (var writer = new IndentedTextWriter(new StringWriter(source), "\t"))
            {
                writer.WriteLine("// Auto-generated code");
                writer.WriteLine("using System.Data;");
                writer.WriteLine("using AutoDataTable.Core;");
                writer.WriteLine();
                writer.WriteLine($"namespace {ns}");
                writer.WriteLine("{");
                writer.Indent++;

                writer.WriteLine($"public partial class {enclosingClass.Name}");
                writer.WriteLine("{");
                writer.Indent++;

                var count = 0;

                foreach (var methodSymbol in methods)
                {
                    count++;

                    var entitySymbol = GenerateCreateMethod(count, writer, methodSymbol, generateDataTableSymbol, dataTableSymbol, dataColumnSymbol);

                    entityTypes.Add(entitySymbol);
                }

                writer.Indent--;
                writer.WriteLine("}");

                writer.Indent--;
                writer.WriteLine("}");
            }

            return source.ToString();
        }

        private static INamedTypeSymbol GenerateCreateMethod(int current, IndentedTextWriter writer, ISymbol methodSymbol,
            ISymbol generateDataTableSymbol, ISymbol dataTableSymbol, ISymbol dataColumnSymbol)
        {
            var classSymbol = GetTargetClass(methodSymbol, generateDataTableSymbol);
            var factoryName = $"DataTableFactory_{current}";

            writer.WriteLine();
            writer.WriteLine($"private static partial DataTableFactory {methodSymbol.Name}() => {factoryName}.Instance;");

            writer.WriteLine();
            writer.WriteLine($"private sealed class {factoryName} : DataTableFactory");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine($"public static readonly {factoryName} Instance = new();");

            writer.WriteLine();
            writer.WriteLine("public override DataTable Create()");
            writer.WriteLine("{");
            writer.Indent++;

            var tableName = GetTableName(dataTableSymbol, classSymbol);

            writer.WriteLine($"var table = new DataTable(\"{tableName}\");");

            foreach (var p in GetProperties(classSymbol))
            {
                var columnName = GetColumnName(dataColumnSymbol, p);

                writer.WriteLine($"table.Columns.Add(new DataColumn(\"{columnName}\", typeof({p.Type.Name})));");
            }

            writer.WriteLine();
            writer.WriteLine("return table;");

            writer.Indent--;
            writer.WriteLine("}");

            writer.Indent--;
            writer.WriteLine("}");

            return classSymbol;
        }

        private static INamedTypeSymbol GetTargetClass(ISymbol methodSymbol, ISymbol generateDataTableSymbol)
        {
            var attributeData = methodSymbol.GetAttributes()
                .Single(data => generateDataTableSymbol.Equals(data.AttributeClass, SymbolEqualityComparer.Default));

            return (INamedTypeSymbol)attributeData.ConstructorArguments[0].Value;
        }

        private static string GenerateDataTableExtensions(ISymbol dataColumnSymbol,
            IReadOnlyList<INamedTypeSymbol> entityTypes, CancellationToken cancellation)
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

                for (var i = 0; i < entityTypes.Count; i++)
                {
                    cancellation.ThrowIfCancellationRequested();

                    var classSymbol = entityTypes[i];

                    GenerateAddRowMethod(writer, classSymbol, dataColumnSymbol);

                    if (i < entityTypes.Count - 1)
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

        private static void GenerateAddRowMethod(IndentedTextWriter writer, INamespaceOrTypeSymbol classSymbol, ISymbol dataColumnSymbol)
        {
            var fullType = GetFullType(classSymbol);

            writer.WriteLine($"public static void AddRow(this DataTable table, {fullType} data)");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine("var row = table.NewRow();");

            foreach (var property in GetProperties(classSymbol))
            {
                var columnName = GetColumnName(dataColumnSymbol, property);

                writer.WriteLine($"row[\"{columnName}\"] = data.{property.Name};");
            }

            writer.WriteLine();
            writer.WriteLine("table.Rows.Add(row);");

            writer.Indent--;
            writer.WriteLine("}");
        }

        private static IEnumerable<IPropertySymbol> GetProperties(INamespaceOrTypeSymbol symbol)
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

        private static string GetFullType(ISymbol symbol)
        {
            var name = symbol.Name;
            var parent = symbol;

            while (true)
            {
                parent = parent.ContainingNamespace;

                if (parent is null || string.IsNullOrEmpty(parent.Name))
                {
                    break;
                }

                name = $"{parent.Name}.{name}";
            }

            return name;
        }

        private static string GetTableName(ISymbol attributeSymbol, ISymbol classSymbol)
        {
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
    }
}