using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace OpenApiSourceGenerator;

[Generator]
public class OpenApiGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var yamlFiles = context.AdditionalTextsProvider
            .Where(f => Path.GetFileName(f.Path) == "openapi.yaml");

        context.RegisterSourceOutput(yamlFiles, (ctx, file) =>
        {
            var text = file.GetText(ctx.CancellationToken)?.ToString();
            if (string.IsNullOrWhiteSpace(text)) return;

            var spec = ParseSpec(text!);
            var schemaDict = spec.Schemas.ToDictionary(s => s.Name);

            // Schemas used as merged request bodies (path params + body) are inlined
            // into the endpoint request record, so skip emitting them separately.
            var mergedSchemas = new HashSet<string>();
            foreach (var op in spec.Operations)
                if (op.PathParameters.Count > 0 && op.RequestBodySchema != null)
                    mergedSchemas.Add(op.RequestBodySchema);

            foreach (var schema in spec.Schemas)
            {
                if (!mergedSchemas.Contains(schema.Name))
                    ctx.AddSource(
                        $"{schema.Name}.g.cs",
                        SourceText.From(EmitSchema(schema), Encoding.UTF8));
            }

            foreach (var op in spec.Operations)
                ctx.AddSource(
                    $"{op.OperationId}Endpoint.g.cs",
                    SourceText.From(EmitEndpoint(op, schemaDict), Encoding.UTF8));
        });
    }

    // ── Lightweight YAML parser ────────────────────────────────────────────
    // Handles mappings, sequences, and scalars for the OpenAPI YAML subset.

    private static YNode ParseYaml(string yaml)
    {
        var root = new YNode("__root__", null);
        var lines = yaml.Replace("\r\n", "\n").Split('\n')
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("#"))
            .ToList();
        int pos = 0;
        ParseChildren(lines, ref pos, -1, root);
        return root;
    }

    private static void ParseChildren(List<string> lines, ref int pos, int parentIndent, YNode parent)
    {
        while (pos < lines.Count)
        {
            var raw = lines[pos];
            var indent = Indent(raw);
            var trimmed = raw.TrimStart();

            if (indent <= parentIndent) return;

            if (trimmed.StartsWith("- "))
            {
                // Sequence item: `- key: value` — first key starts the item node,
                // subsequent sibling keys parsed as its children.
                pos++;
                var content = trimmed.Substring(2).TrimStart();
                var colonIdx = content.IndexOf(':');
                YNode item;
                if (colonIdx >= 0)
                {
                    var key = content.Substring(0, colonIdx).Trim();
                    var val = content.Substring(colonIdx + 1).Trim().Trim('"', '\'');
                    item = new YNode(key, val.Length > 0 ? val : null);
                }
                else
                {
                    item = new YNode("__item__", content.Trim('"', '\''));
                }
                parent.Children.Add(item);
                // Siblings of the first key are children at indent+2
                ParseChildren(lines, ref pos, indent, item);
            }
            else
            {
                var colonIdx = trimmed.IndexOf(':');
                if (colonIdx < 0) { pos++; continue; }

                var key = trimmed.Substring(0, colonIdx).Trim();
                var rest = trimmed.Substring(colonIdx + 1).Trim().Trim('"', '\'');
                var node = new YNode(key, rest.Length > 0 ? rest : null);
                parent.Children.Add(node);
                pos++;

                if (node.Value == null)
                    ParseChildren(lines, ref pos, indent, node);
            }
        }
    }

    private static int Indent(string line) => line.Length - line.TrimStart().Length;

    // ── Spec extraction ────────────────────────────────────────────────────

    private static OpenApiSpec ParseSpec(string yaml)
    {
        var spec = new OpenApiSpec();
        var root = ParseYaml(yaml);

        // components/schemas
        var schemasNode = root.Get("components")?.Get("schemas");
        if (schemasNode != null)
        {
            foreach (var schemaNode in schemasNode.Children)
            {
                var props = new List<SchemaProp>();
                var propsNode = schemaNode.Get("properties");
                if (propsNode != null)
                {
                    foreach (var prop in propsNode.Children)
                        props.Add(new SchemaProp(prop.Key, ToCSharpType(prop.Get("type")?.Value ?? "string")));
                }
                spec.Schemas.Add(new SchemaDefinition(schemaNode.Key, props));
            }
        }

        // paths
        var pathsNode = root.Get("paths");
        if (pathsNode != null)
        {
            foreach (var pathNode in pathsNode.Children)
            {
                foreach (var methodNode in pathNode.Children)
                {
                    var method = methodNode.Key.ToUpperInvariant();
                    var operationId = methodNode.Get("operationId")?.Value
                        ?? method + pathNode.Key.Replace("/", "_").Replace("{", "").Replace("}", "");

                    var pathParams = new List<string>();
                    foreach (var paramItem in methodNode.Get("parameters")?.Children ?? new List<YNode>())
                    {
                        // paramItem: Key="name", Value="id", Children include "in" etc.
                        var paramName = paramItem.Key == "name" ? paramItem.Value : paramItem.Get("name")?.Value;
                        var inVal = paramItem.Get("in")?.Value;
                        if (paramName != null && inVal == "path")
                            pathParams.Add(paramName);
                    }

                    string? requestBodySchema = null;
                    var rbNode = methodNode.Get("requestBody");
                    if (rbNode != null)
                    {
                        var refVal = rbNode.Get("content")
                            ?.Children.FirstOrDefault()
                            ?.Get("schema")
                            ?.Get("$ref")
                            ?.Value;
                        if (refVal != null)
                            requestBodySchema = refVal.Split('/').Last();
                    }

                    spec.Operations.Add(new OperationDefinition(
                        operationId, method, pathNode.Key, pathParams, requestBodySchema));
                }
            }
        }

        return spec;
    }

    private static string ToCSharpType(string t) => t switch
    {
        "integer" => "int",
        "number"  => "double",
        "boolean" => "bool",
        _         => "string",
    };

    // ── Code emission ──────────────────────────────────────────────────────

    private static string EmitSchema(SchemaDefinition s)
    {
        var ctorParams = string.Join(", ", s.Properties.Select(p => $"{p.CSharpType} {Cap(p.Name)}"));
        return $"// <auto-generated/>\n#nullable enable\nnamespace WeatherApi.Models;\npublic record {s.Name}({ctorParams});\n";
    }

    private static string EmitEndpoint(
        OperationDefinition op,
        IReadOnlyDictionary<string, SchemaDefinition> schemas)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using FastEndpoints;");
        sb.AppendLine("using WeatherApi.Models;");
        sb.AppendLine();
        sb.AppendLine("namespace WeatherApi.Endpoints;");
        sb.AppendLine();

        bool hasPathParams = op.PathParameters.Count > 0;
        bool hasBody = op.RequestBodySchema != null;
        string requestType;

        if (!hasPathParams && !hasBody)
        {
            requestType = "EmptyRequest";
        }
        else if (!hasPathParams)
        {
            // Body only — use the generated schema record directly.
            requestType = op.RequestBodySchema!;
        }
        else if (!hasBody)
        {
            // Path params only (GET /{id}, DELETE /{id}).
            requestType = EmitRouteRequest(sb, op.OperationId + "Request", op.PathParameters, null, null);
        }
        else
        {
            // Path params + body (PUT /{id}) — merge both into one record named
            // after the body schema so it replaces the standalone schema record.
            schemas.TryGetValue(op.RequestBodySchema!, out var bodySchema);
            requestType = EmitRouteRequest(sb, op.RequestBodySchema!, op.PathParameters, op.RequestBodySchema, bodySchema);
        }

        string responseType = op.HttpMethod == "DELETE"
            ? "object"
            : (op.HttpMethod == "GET" && !op.Route.Contains("{") ? "List<WeatherRecord>" : "WeatherRecord");

        sb.AppendLine($"public partial class {op.OperationId}Endpoint");
        sb.AppendLine($"    : Endpoint<{requestType}, {responseType}>");
        sb.AppendLine("{");
        sb.AppendLine("    public override void Configure()");
        sb.AppendLine("    {");
        sb.AppendLine($"        {CsMethod(op.HttpMethod)}(\"{op.Route}\");");
        sb.AppendLine("        AllowAnonymous();");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string EmitRouteRequest(
        StringBuilder sb,
        string typeName,
        List<string> pathParams,
        string? bodySchemaName,
        SchemaDefinition? bodySchema)
    {
        sb.AppendLine($"public record {typeName}");
        sb.AppendLine("{");
        foreach (var p in pathParams)
            sb.AppendLine($"    public int {Cap(p)} {{ get; init; }}");
        if (bodySchema != null)
        {
            foreach (var prop in bodySchema.Properties)
                sb.AppendLine($"    public {prop.CSharpType}? {Cap(prop.Name)} {{ get; init; }}");
        }
        sb.AppendLine("}");
        sb.AppendLine();
        return typeName;
    }

    private static string CsMethod(string m) => m switch
    {
        "GET" => "Get", "POST" => "Post", "PUT" => "Put",
        "DELETE" => "Delete", "PATCH" => "Patch", _ => m,
    };

    private static string Cap(string s) =>
        s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    // ── Internal model ─────────────────────────────────────────────────────

    private sealed class YNode
    {
        public string Key { get; }
        public string? Value { get; }
        public List<YNode> Children { get; } = new List<YNode>();

        public YNode(string key, string? value) { Key = key; Value = value; }
        public YNode? Get(string key) => Children.FirstOrDefault(c => c.Key == key);
    }

    private sealed class OpenApiSpec
    {
        public List<SchemaDefinition> Schemas { get; } = new List<SchemaDefinition>();
        public List<OperationDefinition> Operations { get; } = new List<OperationDefinition>();
    }

    private sealed class SchemaDefinition
    {
        public string Name { get; }
        public List<SchemaProp> Properties { get; }
        public SchemaDefinition(string n, List<SchemaProp> p) { Name = n; Properties = p; }
    }

    private sealed class SchemaProp
    {
        public string Name { get; }
        public string CSharpType { get; }
        public SchemaProp(string n, string t) { Name = n; CSharpType = t; }
    }

    private sealed class OperationDefinition
    {
        public string OperationId { get; }
        public string HttpMethod { get; }
        public string Route { get; }
        public List<string> PathParameters { get; }
        public string? RequestBodySchema { get; }

        public OperationDefinition(string id, string method, string route,
            List<string> pp, string? rb)
        {
            OperationId = id; HttpMethod = method; Route = route;
            PathParameters = pp; RequestBodySchema = rb;
        }
    }
}
