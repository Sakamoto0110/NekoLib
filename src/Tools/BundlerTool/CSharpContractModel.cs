using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace BundlerTool
{
    public enum ContractVisibility
    {
        PublicApi,
        PublicAndInternal,
        AllDeclarations
    }

    internal enum ContractAccessibility
    {
        Public,
        Protected,
        ProtectedInternal,
        Internal,
        PrivateProtected,
        Private,
        File,
        ExplicitInterface
    }

    [DataContract]
    internal sealed class ContractIndexDocument
    {
        [DataMember(Name = "schemaVersion", Order = 1)]
        public int SchemaVersion { get; set; } = 1;

        [DataMember(Name = "kind", Order = 2)]
        public string Kind { get; set; } = "csharp-source-contract-index";

        [DataMember(Name = "project", Order = 3)]
        public string ProjectName { get; set; }

        [DataMember(Name = "visibility", Order = 4)]
        public string Visibility { get; set; }

        [DataMember(Name = "analysisLevel", Order = 5)]
        public string AnalysisLevel { get; set; } = "syntax-only";

        [DataMember(Name = "types", Order = 6)]
        public List<ContractType> Types { get; set; } = new List<ContractType>();

        [DataMember(Name = "diagnostics", Order = 7)]
        public List<ContractDiagnostic> Diagnostics { get; set; } = new List<ContractDiagnostic>();
    }

    [DataContract]
    internal sealed class ContractType
    {
        [DataMember(Name = "namespace", Order = 1)]
        public string NamespaceName { get; set; }

        [DataMember(Name = "containingType", Order = 2, EmitDefaultValue = false)]
        public string ContainingTypeName { get; set; }

        [DataMember(Name = "kind", Order = 3)]
        public string Kind { get; set; }

        [DataMember(Name = "name", Order = 4)]
        public string Name { get; set; }

        [DataMember(Name = "fullName", Order = 5)]
        public string FullName { get; set; }

        [DataMember(Name = "accessibility", Order = 6)]
        public string AccessibilityText { get; set; }

        [DataMember(Name = "static", Order = 7)]
        public bool IsStatic { get; set; }

        [DataMember(Name = "declaration", Order = 8)]
        public string Declaration { get; set; }

        [DataMember(Name = "summary", Order = 9, EmitDefaultValue = false)]
        public string Summary { get; set; }

        [DataMember(Name = "sourceLocations", Order = 10)]
        public List<string> SourceLocations { get; set; } = new List<string>();

        [DataMember(Name = "members", Order = 11)]
        public List<ContractMember> Members { get; set; } = new List<ContractMember>();

        internal string Key { get; set; }
        internal string ContainingTypeKey { get; set; }
        internal ContractAccessibility Accessibility { get; set; }
        internal HashSet<string> Modifiers { get; } = new HashSet<string>(StringComparer.Ordinal);
        internal HashSet<string> BaseTypes { get; } = new HashSet<string>(StringComparer.Ordinal);
        internal HashSet<string> Constraints { get; } = new HashSet<string>(StringComparer.Ordinal);
        internal string TypeParameterList { get; set; }
        internal string PrimaryConstructor { get; set; }
        internal string DelegateReturnType { get; set; }
        internal string DelegateParameterList { get; set; }
    }

    [DataContract]
    internal sealed class ContractMember
    {
        [DataMember(Name = "kind", Order = 1)]
        public string Kind { get; set; }

        [DataMember(Name = "name", Order = 2)]
        public string Name { get; set; }

        [DataMember(Name = "accessibility", Order = 3)]
        public string AccessibilityText { get; set; }

        [DataMember(Name = "static", Order = 4)]
        public bool IsStatic { get; set; }

        [DataMember(Name = "extensionMethod", Order = 5)]
        public bool IsExtensionMethod { get; set; }

        [DataMember(Name = "signature", Order = 6)]
        public string Signature { get; set; }

        [DataMember(Name = "summary", Order = 7, EmitDefaultValue = false)]
        public string Summary { get; set; }

        [DataMember(Name = "sourceLocations", Order = 8)]
        public List<string> SourceLocations { get; set; } = new List<string>();

        internal ContractAccessibility Accessibility { get; set; }
    }

    [DataContract]
    internal sealed class ContractDiagnostic
    {
        [DataMember(Name = "file", Order = 1)]
        public string File { get; set; }

        [DataMember(Name = "line", Order = 2)]
        public int Line { get; set; }

        [DataMember(Name = "message", Order = 3)]
        public string Message { get; set; }
    }

    [DataContract]
    internal sealed class ContractIndexManifest
    {
        [DataMember(Name = "schemaVersion", Order = 1)]
        public int SchemaVersion { get; set; } = 1;

        [DataMember(Name = "kind", Order = 2)]
        public string Kind { get; set; } = "csharp-source-contract-index-manifest";

        [DataMember(Name = "sourceRoot", Order = 3)]
        public string SourceRoot { get; set; }

        [DataMember(Name = "visibility", Order = 4)]
        public string Visibility { get; set; }

        [DataMember(Name = "analysisLevel", Order = 5)]
        public string AnalysisLevel { get; set; } = "syntax-only";

        [DataMember(Name = "projects", Order = 6)]
        public List<ContractManifestProject> Projects { get; set; } = new List<ContractManifestProject>();
    }

    [DataContract]
    internal sealed class ContractManifestProject
    {
        [DataMember(Name = "name", Order = 1)]
        public string Name { get; set; }

        [DataMember(Name = "files", Order = 2)]
        public List<ContractManifestFile> Files { get; set; } = new List<ContractManifestFile>();
    }

    [DataContract]
    internal sealed class ContractManifestFile
    {
        [DataMember(Name = "path", Order = 1)]
        public string Path { get; set; }

        [DataMember(Name = "sha256", Order = 2)]
        public string Sha256 { get; set; }
    }

    internal static class ContractAccessibilityRules
    {
        public static bool Includes(ContractAccessibility accessibility, ContractVisibility visibility)
        {
            if (visibility == ContractVisibility.AllDeclarations)
                return true;

            if (accessibility == ContractAccessibility.Public ||
                accessibility == ContractAccessibility.Protected ||
                accessibility == ContractAccessibility.ProtectedInternal)
            {
                return true;
            }

            return visibility == ContractVisibility.PublicAndInternal &&
                   (accessibility == ContractAccessibility.Internal ||
                    accessibility == ContractAccessibility.PrivateProtected);
        }

        public static string ToText(ContractAccessibility accessibility)
        {
            switch (accessibility)
            {
                case ContractAccessibility.Public:
                    return "public";
                case ContractAccessibility.Protected:
                    return "protected";
                case ContractAccessibility.ProtectedInternal:
                    return "protected internal";
                case ContractAccessibility.Internal:
                    return "internal";
                case ContractAccessibility.PrivateProtected:
                    return "private protected";
                case ContractAccessibility.File:
                    return "file";
                case ContractAccessibility.ExplicitInterface:
                    return "explicit interface";
                default:
                    return "private";
            }
        }

        public static string ToOptionText(ContractVisibility visibility)
        {
            switch (visibility)
            {
                case ContractVisibility.PublicAndInternal:
                    return "public-and-internal";
                case ContractVisibility.AllDeclarations:
                    return "all-declarations";
                default:
                    return "public-api";
            }
        }
    }
}
