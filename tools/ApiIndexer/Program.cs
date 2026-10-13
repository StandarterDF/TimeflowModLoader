using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Mono.Cecil;

namespace ApiIndexer
{
    /// <summary>
    /// Reads a managed assembly with Mono.Cecil and emits a complete, deterministic index of every
    /// type and its members. Three outputs are produced in the chosen directory:
    ///   * api_index.json  - one JSON array, ideal for tooling / neural nets
    ///   * api_index.tsv   - flat "type \t member \t signature" table, ideal for grep
    ///   * api_index.md    - human-readable listing grouped by namespace
    ///
    /// Usage: ApiIndexer &lt;assembly.dll&gt; &lt;output-dir&gt;
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("usage: ApiIndexer <assembly.dll> <output-dir>");
                return 2;
            }

            var assemblyPath = Path.GetFullPath(args[0]);
            var outputDir = Path.GetFullPath(args[1]);
            Directory.CreateDirectory(outputDir);

            var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);
            var types = new List<TypeInfo>();
            foreach (var type in assembly.MainModule.Types)
                Walk(type, types);

            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(
                Path.Combine(outputDir, "api_index.json"),
                JsonSerializer.Serialize(types, jsonOptions),
                new UTF8Encoding(false));

            WriteTsv(Path.Combine(outputDir, "api_index.tsv"), types);
            WriteMarkdown(Path.Combine(outputDir, "api_index.md"), types, assemblyPath);

            Console.WriteLine($"Assembly : {assemblyPath}");
            Console.WriteLine($"Types    : {types.Count}");
            Console.WriteLine($"Members  : {types.Sum(t => t.Members.Count)}");
            Console.WriteLine($"Output   : {outputDir}");
            return 0;
        }

        private static void Walk(TypeDefinition type, List<TypeInfo> sink)
        {
            sink.Add(Describe(type));
            foreach (var nested in type.NestedTypes)
                Walk(nested, sink);
        }

        private static TypeInfo Describe(TypeDefinition type)
        {
            var info = new TypeInfo
            {
                Name = type.FullName,
                ShortName = type.Name,
                Namespace = type.Namespace ?? string.Empty,
                Kind = TypeKind(type),
                Visibility = VisibilityOf(type),
                IsAbstract = type.IsAbstract,
                IsSealed = type.IsSealed,
                IsGeneric = type.HasGenericParameters,
                BaseType = type.BaseType == null ? null : Nice(type.BaseType),
                Interfaces = type.Interfaces.Select(i => Nice(i.InterfaceType)).ToList(),
                IsExtensionHost = type.CustomAttributes.Any(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.ExtensionAttribute"),
                Members = new List<MemberInfo>()
            };

            foreach (var field in type.Fields)
                info.Members.Add(DescribeField(field));

            foreach (var method in type.Methods)
                info.Members.Add(DescribeMethod(method));

            foreach (var property in type.Properties)
                info.Members.Add(DescribeProperty(property));

            foreach (var evt in type.Events)
                info.Members.Add(DescribeEvent(evt));

            return info;
        }

        private static MemberInfo DescribeField(FieldDefinition field)
        {
            var info = new MemberInfo
            {
                Kind = field.IsLiteral ? "const" : "field",
                Name = field.Name,
                IsStatic = field.IsStatic,
                Visibility = VisibilityOf(field),
                Type = Nice(field.FieldType),
                Signature = $"{Nice(field.FieldType)} {field.Name}"
            };

            if (field.IsLiteral && field.HasConstant)
            {
                info.ConstValue = field.Constant == null
                    ? "null"
                    : Convert.ToString(field.Constant, System.Globalization.CultureInfo.InvariantCulture);
                info.Signature = $"{Nice(field.FieldType)} {field.Name} = {info.ConstValue}";
            }

            return info;
        }

        private static MemberInfo DescribeMethod(MethodDefinition method)
        {
            var kind =
                method.IsConstructor ? "ctor" :
                method.IsGetter || method.IsSetter ? "accessor" :
                "method";

            var parameters = string.Join(", ", method.Parameters.Select(p => $"{Nice(p.ParameterType)} {p.Name}"));
            var generics = method.HasGenericParameters
                ? "<" + string.Join(", ", method.GenericParameters.Select(g => g.Name)) + ">"
                : string.Empty;

            var info = new MemberInfo
            {
                Kind = kind,
                Name = method.Name,
                IsStatic = method.IsStatic,
                Visibility = VisibilityOf(method),
                IsVirtual = method.IsVirtual && !method.IsFinal,
                IsExtension = method.CustomAttributes.Any(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.ExtensionAttribute"),
                Returns = Nice(method.ReturnType),
                Parameters = method.Parameters.Select(p => Nice(p.ParameterType)).ToList(),
                Signature = $"{Nice(method.ReturnType)} {method.Name}{generics}({parameters})"
            };
            return info;
        }

        private static MemberInfo DescribeProperty(PropertyDefinition property)
        {
            var accessors = new List<string>();
            if (property.GetMethod != null)
                accessors.Add("get;");
            if (property.SetMethod != null)
                accessors.Add("set;");

            var visibility = property.GetMethod != null
                ? VisibilityOf(property.GetMethod)
                : property.SetMethod != null ? VisibilityOf(property.SetMethod) : "private";

            var origin = property.GetMethod != null ? property.GetMethod : property.SetMethod;

            return new MemberInfo
            {
                Kind = "property",
                Name = property.Name,
                IsStatic = property.GetMethod != null ? property.GetMethod.IsStatic : property.SetMethod != null && property.SetMethod.IsStatic,
                Visibility = visibility,
                Type = Nice(property.PropertyType),
                IsVirtual = origin != null && origin.IsVirtual && !origin.IsFinal,
                Signature = $"{Nice(property.PropertyType)} {property.Name} {{ {string.Join(" ", accessors)} }}"
            };
        }

        private static MemberInfo DescribeEvent(EventDefinition evt)
        {
            return new MemberInfo
            {
                Kind = "event",
                Name = evt.Name,
                IsStatic = evt.AddMethod != null && evt.AddMethod.IsStatic,
                Visibility = evt.AddMethod != null ? VisibilityOf(evt.AddMethod) : "private",
                Type = Nice(evt.EventType),
                Signature = $"event {Nice(evt.EventType)} {evt.Name}"
            };
        }

        private static string TypeKind(TypeDefinition type)
        {
            if (type.IsInterface) return "interface";
            if (type.IsEnum) return "enum";
            if (type.IsValueType) return "struct";
            if (type.BaseType != null && type.BaseType.FullName == "System.MulticastDelegate") return "delegate";
            return "class";
        }

        private static string VisibilityOf(TypeDefinition type)
        {
            if (type.IsPublic || type.IsNestedPublic) return "public";
            if (type.IsNestedFamily) return "protected";
            if (type.IsNestedFamilyOrAssembly) return "protected internal";
            if (type.IsNestedPrivate) return "private";
            return "internal";
        }

        private static string VisibilityOf(MethodDefinition method)
        {
            if (method.IsPublic) return "public";
            if (method.IsFamily) return "protected";
            if (method.IsFamilyOrAssembly) return "protected internal";
            if (method.IsFamilyAndAssembly) return "private protected";
            if (method.IsPrivate) return "private";
            return "internal";
        }

        private static string VisibilityOf(FieldDefinition field)
        {
            if (field.IsPublic) return "public";
            if (field.IsFamily) return "protected";
            if (field.IsFamilyOrAssembly) return "protected internal";
            if (field.IsFamilyAndAssembly) return "private protected";
            if (field.IsPrivate) return "private";
            return "internal";
        }

        private static string Nice(TypeReference type)
        {
            if (type == null)
                return string.Empty;

            var name = type.FullName;
            name = name.Replace('/', '.');
            name = name.Replace("`1", "").Replace("`2", "").Replace("`3", "");
            return name;
        }

        private static void WriteTsv(string path, List<TypeInfo> types)
        {
            using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
            writer.WriteLine("type\tshortName\tnamespace\tkind\tvisibility\tmemberKind\tstatic\tmemberVisibility\tmemberSignature");
            foreach (var type in types)
            {
                writer.WriteLine($"{type.Name}\t{type.ShortName}\t{type.Namespace}\t{type.Kind}\t{type.Visibility}\tTYPE\t\t\t{type.Kind} {type.Name}");
                foreach (var member in type.Members)
                {
                    writer.WriteLine($"{type.Name}\t{type.ShortName}\t{type.Namespace}\t{type.Kind}\t{type.Visibility}\t{member.Kind}\t{member.IsStatic}\t{member.Visibility}\t{member.Signature}");
                }
            }
        }

        private static void WriteMarkdown(string path, List<TypeInfo> types, string assemblyPath)
        {
            using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
            writer.WriteLine($"# API index — `{Path.GetFileName(assemblyPath)}`");
            writer.WriteLine();
            writer.WriteLine("Generated by `tools/ApiIndexer`. One section per namespace; nested types are shown");
            writer.WriteLine("with their declaring type. Only the shape (signatures) is listed — no decompiled bodies.");
            writer.WriteLine();
            writer.WriteLine($"- Types: **{types.Count}**");
            writer.WriteLine($"- Members: **{types.Sum(t => t.Members.Count)}**");
            writer.WriteLine();

            var byNamespace = types
                .GroupBy(t => string.IsNullOrEmpty(t.Namespace) ? "(global namespace)" : t.Namespace)
                .OrderBy(g => g.Key == "(global namespace)" ? 0 : 1)
                .ThenBy(g => g.Key, StringComparer.Ordinal);

            foreach (var group in byNamespace)
            {
                writer.WriteLine($"## {group.Key}");
                writer.WriteLine();
                writer.WriteLine($"_{group.Count()} type(s)_");
                writer.WriteLine();

                foreach (var type in group.OrderBy(t => t.Name, StringComparer.Ordinal))
                {
                    var modifiers = string.Join(" ", new[]
                    {
                        type.Visibility,
                        type.IsSealed ? "sealed" : null,
                        type.IsAbstract && type.Kind == "class" ? "abstract" : null,
                        type.Kind
                    }.Where(x => x != null));

                    var baseInfo = type.BaseType != null && type.BaseType != "System.Object"
                        ? $" : {type.BaseType}"
                        : string.Empty;

                    writer.WriteLine($"### {type.Name}");
                    writer.WriteLine();
                    writer.WriteLine($"`{modifiers} {type.ShortName}{baseInfo}`");
                    if (type.Interfaces.Count > 0)
                        writer.WriteLine($"implements: {string.Join(", ", type.Interfaces)}");
                    writer.WriteLine();

                    var byKind = type.Members
                        .GroupBy(m => m.Kind)
                        .OrderBy(g => KindOrder(g.Key));

                    foreach (var memberGroup in byKind)
                    {
                        foreach (var member in memberGroup.Where(m => m.Visibility == "public" || m.Visibility == "protected")
                                                         .OrderBy(m => m.Name, StringComparer.Ordinal))
                        {
                            var flags = member.IsStatic ? "static " : string.Empty;
                            writer.WriteLine($"- `{flags}{member.Signature}`");
                        }
                    }
                    writer.WriteLine();
                }
            }
        }

        private static int KindOrder(string kind) => kind switch
        {
            "const" => 0,
            "field" => 1,
            "property" => 2,
            "event" => 3,
            "ctor" => 4,
            "method" => 5,
            "accessor" => 6,
            _ => 7
        };
    }

    internal sealed class TypeInfo
    {
        public string Name { get; set; }
        public string ShortName { get; set; }
        public string Namespace { get; set; }
        public string Kind { get; set; }
        public string Visibility { get; set; }
        public bool IsAbstract { get; set; }
        public bool IsSealed { get; set; }
        public bool IsGeneric { get; set; }
        public bool IsExtensionHost { get; set; }
        public string BaseType { get; set; }
        public List<string> Interfaces { get; set; }
        public List<MemberInfo> Members { get; set; }
    }

    internal sealed class MemberInfo
    {
        public string Kind { get; set; }
        public string Name { get; set; }
        public bool IsStatic { get; set; }
        public bool IsVirtual { get; set; }
        public bool IsExtension { get; set; }
        public string Visibility { get; set; }
        public string Type { get; set; }
        public string Returns { get; set; }
        public List<string> Parameters { get; set; }
        public string ConstValue { get; set; }
        public string Signature { get; set; }
    }
}
