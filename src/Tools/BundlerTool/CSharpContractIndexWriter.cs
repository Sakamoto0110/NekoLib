using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;

namespace BundlerTool
{
    internal static class CSharpContractIndexWriter
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        public static void WriteProject(ContractIndexDocument document, string markdownPath, string jsonPath)
        {
            var markdown = new StringBuilder();
            markdown.AppendLine("# C# Source Contract Index — " + document.ProjectName);
            markdown.AppendLine();
            AppendAnalysisNotice(markdown, document.Visibility);
            AppendProjectBody(markdown, document, 2);

            File.WriteAllText(markdownPath, NormalizeLineEndings(markdown.ToString()), Utf8WithoutBom);
            WriteJson(jsonPath, document);
        }

        public static void WriteMaster(
            string rootName,
            ContractVisibility visibility,
            IReadOnlyList<ContractIndexDocument> documents,
            string outputPath)
        {
            var markdown = new StringBuilder();
            markdown.AppendLine("# C# Source Contract Index — " + rootName);
            markdown.AppendLine();
            AppendAnalysisNotice(markdown, ContractAccessibilityRules.ToOptionText(visibility));

            markdown.AppendLine("## Projects");
            markdown.AppendLine();
            if (documents.Count == 0)
            {
                markdown.AppendLine("No C# projects with matching declarations were found.");
                markdown.AppendLine();
            }
            else
            {
                foreach (var document in documents)
                {
                    string safeProjectName = MakeSafeFileName(document.ProjectName);
                    markdown.AppendLine("- [" + EscapeMarkdown(document.ProjectName) + "](" +
                                        EscapeLinkSegment(safeProjectName) + "/" +
                                        EscapeLinkSegment(safeProjectName + "_API.md") + ")");
                }
                markdown.AppendLine();
            }

            foreach (var document in documents)
            {
                markdown.AppendLine("## Project `" + EscapeCode(document.ProjectName) + "`");
                markdown.AppendLine();
                AppendProjectBody(markdown, document, 3);
            }

            File.WriteAllText(outputPath, NormalizeLineEndings(markdown.ToString()), Utf8WithoutBom);
        }

        public static void WriteManifest(ContractIndexManifest manifest, string outputPath)
        {
            WriteJson(outputPath, manifest);
        }

        private static void AppendAnalysisNotice(StringBuilder markdown, string visibility)
        {
            markdown.AppendLine("> Source-derived contract index. This is not a compiled API compatibility report.");
            markdown.AppendLine("> Project build properties, target-framework symbols, generated code, and inactive preprocessor branches are not evaluated.");
            markdown.AppendLine();
            markdown.AppendLine("Visibility: `" + EscapeCode(visibility) + "`");
            markdown.AppendLine();
        }

        private static void AppendProjectBody(
            StringBuilder markdown,
            ContractIndexDocument document,
            int sectionLevel)
        {
            int memberCount = document.Types.Sum(type => type.Members.Count);
            int staticTypeCount = document.Types.Count(type => type.IsStatic);
            int staticMemberCount = document.Types.Sum(type => type.Members.Count(member => member.IsStatic));

            AppendHeading(markdown, sectionLevel, "Summary");
            markdown.AppendLine();
            markdown.AppendLine("| Types | Members | Static types | Static members | Parse diagnostics |");
            markdown.AppendLine("|---:|---:|---:|---:|---:|");
            markdown.AppendLine("| " + document.Types.Count + " | " + memberCount + " | " + staticTypeCount +
                                " | " + staticMemberCount + " | " + document.Diagnostics.Count + " |");
            markdown.AppendLine();

            AppendStaticApi(markdown, document, sectionLevel);

            var namespaceGroups = document.Types
                .GroupBy(type => type.NamespaceName ?? string.Empty)
                .OrderBy(group => group.Key, StringComparer.Ordinal);

            foreach (var namespaceGroup in namespaceGroups)
            {
                string namespaceLabel = string.IsNullOrEmpty(namespaceGroup.Key)
                    ? "(global namespace)"
                    : namespaceGroup.Key;
                AppendHeading(markdown, sectionLevel, "Namespace `" + EscapeCode(namespaceLabel) + "`");
                markdown.AppendLine();

                foreach (var type in namespaceGroup.OrderBy(item => item.FullName, StringComparer.Ordinal))
                {
                    AppendType(markdown, type, sectionLevel + 1);
                }
            }

            if (document.Diagnostics.Count > 0)
            {
                AppendHeading(markdown, sectionLevel, "Parse diagnostics");
                markdown.AppendLine();
                foreach (var diagnostic in document.Diagnostics)
                {
                    string location = diagnostic.Line > 0
                        ? diagnostic.File + ":" + diagnostic.Line
                        : diagnostic.File;
                    markdown.AppendLine("- `" + EscapeCode(location) + "` — " +
                                        EscapeMarkdown(diagnostic.Message));
                }
                markdown.AppendLine();
            }
        }

        private static void AppendStaticApi(
            StringBuilder markdown,
            ContractIndexDocument document,
            int sectionLevel)
        {
            var staticTypes = document.Types
                .Where(type => type.IsStatic)
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToList();
            var staticMembers = document.Types
                .SelectMany(type => type.Members
                    .Where(member => member.IsStatic)
                    .Select(member => new { Type = type, Member = member }))
                .OrderBy(item => item.Type.FullName, StringComparer.Ordinal)
                .ThenBy(item => item.Member.Signature, StringComparer.Ordinal)
                .ToList();

            AppendHeading(markdown, sectionLevel, "Static API");
            markdown.AppendLine();

            if (staticTypes.Count == 0 && staticMembers.Count == 0)
            {
                markdown.AppendLine("No static API declarations matched the selected visibility.");
                markdown.AppendLine();
                return;
            }

            foreach (var type in staticTypes)
                markdown.AppendLine("- Static type: `" + EscapeCode(type.FullName) + "`");
            foreach (var item in staticMembers)
            {
                string extensionLabel = item.Member.IsExtensionMethod ? " (extension method)" : string.Empty;
                markdown.AppendLine("- `" + EscapeCode(item.Type.FullName) + "`: `" +
                                    EscapeCode(item.Member.Signature) + "`" + extensionLabel);
            }
            markdown.AppendLine();
        }

        private static void AppendType(StringBuilder markdown, ContractType type, int headingLevel)
        {
            AppendHeading(markdown, headingLevel, "Type `" + EscapeCode(type.FullName) + "`");
            markdown.AppendLine();
            markdown.AppendLine("```csharp");
            markdown.AppendLine(type.Declaration);
            markdown.AppendLine("```");
            markdown.AppendLine();

            if (!string.IsNullOrEmpty(type.Summary))
            {
                markdown.AppendLine(EscapeMarkdown(type.Summary));
                markdown.AppendLine();
            }

            if (type.SourceLocations.Count == 1)
            {
                markdown.AppendLine("Source: `" + EscapeCode(type.SourceLocations[0]) + "`");
                markdown.AppendLine();
            }
            else if (type.SourceLocations.Count > 1)
            {
                markdown.AppendLine("Sources:");
                markdown.AppendLine();
                foreach (string location in type.SourceLocations)
                    markdown.AppendLine("- `" + EscapeCode(location) + "`");
                markdown.AppendLine();
            }

            foreach (var memberGroup in type.Members
                .GroupBy(member => member.Kind)
                .OrderBy(group => GetMemberKindOrder(group.Key)))
            {
                AppendHeading(markdown, headingLevel + 1, GetMemberGroupTitle(memberGroup.Key));
                markdown.AppendLine();
                markdown.AppendLine("```csharp");
                foreach (var member in memberGroup.OrderBy(item => item.Signature, StringComparer.Ordinal))
                {
                    if (!string.IsNullOrEmpty(member.Summary))
                        markdown.AppendLine("// " + member.Summary.Replace("\r", " ").Replace("\n", " "));
                    markdown.AppendLine(member.Signature);
                }
                markdown.AppendLine("```");
                markdown.AppendLine();
            }
        }

        private static string GetMemberGroupTitle(string kind)
        {
            switch (kind)
            {
                case "Property": return "Properties";
                case "Indexer": return "Indexers";
                case "Enum Member": return "Enum members";
                default: return kind + "s";
            }
        }

        private static int GetMemberKindOrder(string kind)
        {
            switch (kind)
            {
                case "Constructor": return 0;
                case "Field": return 1;
                case "Enum Member": return 2;
                case "Property": return 3;
                case "Indexer": return 4;
                case "Event": return 5;
                case "Method": return 6;
                case "Operator": return 7;
                default: return 100;
            }
        }

        private static void AppendHeading(StringBuilder markdown, int level, string text)
        {
            markdown.AppendLine(new string('#', Math.Min(level, 6)) + " " + text);
        }

        private static void WriteJson<T>(string outputPath, T value)
        {
            string compactJson;
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, value);
                compactJson = Encoding.UTF8.GetString(stream.ToArray());
            }

            File.WriteAllText(outputPath, PrettyPrintJson(compactJson), Utf8WithoutBom);
        }

        private static string PrettyPrintJson(string json)
        {
            var output = new StringBuilder(json.Length + 256);
            int indent = 0;
            bool inString = false;
            bool escaped = false;

            foreach (char character in json)
            {
                if (inString)
                {
                    output.Append(character);
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (character == '\\')
                    {
                        escaped = true;
                    }
                    else if (character == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                switch (character)
                {
                    case '"':
                        inString = true;
                        output.Append(character);
                        break;
                    case '{':
                    case '[':
                        output.Append(character);
                        output.Append('\n');
                        indent++;
                        AppendIndent(output, indent);
                        break;
                    case '}':
                    case ']':
                        output.Append('\n');
                        indent--;
                        AppendIndent(output, indent);
                        output.Append(character);
                        break;
                    case ',':
                        output.Append(character);
                        output.Append('\n');
                        AppendIndent(output, indent);
                        break;
                    case ':':
                        output.Append(": ");
                        break;
                    default:
                        if (!char.IsWhiteSpace(character))
                            output.Append(character);
                        break;
                }
            }

            output.Append('\n');
            return output.ToString();
        }

        private static void AppendIndent(StringBuilder output, int indent)
        {
            output.Append(' ', Math.Max(indent, 0) * 2);
        }

        private static string EscapeMarkdown(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.Replace("\\", "\\\\").Replace("`", "\\`");
        }

        private static string EscapeCode(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("`", "'");
        }

        private static string EscapeLinkSegment(string value)
        {
            return Uri.EscapeDataString(value).Replace("%2F", "/");
        }

        private static string MakeSafeFileName(string value)
        {
            var invalidCharacters = new HashSet<char>(Path.GetInvalidFileNameChars());
            var builder = new StringBuilder(value.Length);
            foreach (char character in value)
                builder.Append(invalidCharacters.Contains(character) ? '_' : character);
            string result = builder.ToString().Trim();
            return string.IsNullOrEmpty(result) ? "UnnamedProject" : result;
        }

        private static string NormalizeLineEndings(string value)
        {
            return value.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);
        }
    }
}
