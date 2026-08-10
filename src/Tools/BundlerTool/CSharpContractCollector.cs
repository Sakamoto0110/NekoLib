using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace BundlerTool
{
    public static partial class CSharpContractIndexEngine
    {
        private sealed class CSharpContractCollector
        {
            private readonly string _rootDirectory;
            private readonly string _projectName;
            private readonly Dictionary<string, ContractType> _types =
                new Dictionary<string, ContractType>(StringComparer.Ordinal);
            private readonly List<ContractDiagnostic> _diagnostics = new List<ContractDiagnostic>();

            public CSharpContractCollector(string rootDirectory, string projectName)
            {
                _rootDirectory = rootDirectory;
                _projectName = projectName;
            }

            public void CollectFile(string filePath)
            {
                string relativePath = GetRelativePath(_rootDirectory, filePath);

                try
                {
                    string source = File.ReadAllText(filePath);
                    var tree = CSharpSyntaxTree.ParseText(
                        SourceText.From(source, Encoding.UTF8),
                        ParseOptions,
                        filePath);
                    var root = tree.GetCompilationUnitRoot();

                    foreach (var diagnostic in tree.GetDiagnostics()
                        .Where(item => item.Severity == DiagnosticSeverity.Error))
                    {
                        var lineSpan = diagnostic.Location.GetLineSpan();
                        _diagnostics.Add(new ContractDiagnostic
                        {
                            File = relativePath,
                            Line = lineSpan.StartLinePosition.Line + 1,
                            Message = diagnostic.GetMessage()
                        });
                    }

                    CollectDeclarations(root.Members, string.Empty, null, null, relativePath);
                }
                catch (Exception ex)
                {
                    _diagnostics.Add(new ContractDiagnostic
                    {
                        File = relativePath,
                        Line = 0,
                        Message = "Unable to analyze file: " + ex.Message
                    });
                }
            }

            public ContractIndexDocument CreateDocument(ContractVisibility visibility)
            {
                foreach (var type in _types.Values)
                {
                    type.Declaration = BuildTypeDeclaration(type);
                    type.SourceLocations = SortDistinct(type.SourceLocations);
                    type.Members = type.Members
                        .OrderBy(member => GetMemberKindOrder(member.Kind))
                        .ThenBy(member => member.Signature, StringComparer.Ordinal)
                        .ToList();

                    foreach (var member in type.Members)
                    {
                        member.SourceLocations = SortDistinct(member.SourceLocations);
                    }
                }

                var includedTypeKeys = new HashSet<string>(StringComparer.Ordinal);
                bool changed;
                do
                {
                    changed = false;
                    foreach (var type in _types.Values)
                    {
                        if (includedTypeKeys.Contains(type.Key) ||
                            !ContractAccessibilityRules.Includes(type.Accessibility, visibility))
                        {
                            continue;
                        }

                        if (string.IsNullOrEmpty(type.ContainingTypeKey) ||
                            includedTypeKeys.Contains(type.ContainingTypeKey))
                        {
                            includedTypeKeys.Add(type.Key);
                            changed = true;
                        }
                    }
                }
                while (changed);

                var visibleTypes = _types.Values
                    .Where(type => includedTypeKeys.Contains(type.Key))
                    .Select(type => CopyFilteredType(type, visibility))
                    .OrderBy(type => type.NamespaceName ?? string.Empty, StringComparer.Ordinal)
                    .ThenBy(type => type.FullName, StringComparer.Ordinal)
                    .ThenBy(type => type.Kind, StringComparer.Ordinal)
                    .ToList();

                return new ContractIndexDocument
                {
                    ProjectName = _projectName,
                    Visibility = ContractAccessibilityRules.ToOptionText(visibility),
                    Types = visibleTypes,
                    Diagnostics = _diagnostics
                        .OrderBy(item => item.File, StringComparer.Ordinal)
                        .ThenBy(item => item.Line)
                        .ThenBy(item => item.Message, StringComparer.Ordinal)
                        .ToList()
                };
            }

            private void CollectDeclarations(
                SyntaxList<MemberDeclarationSyntax> declarations,
                string namespaceName,
                ContractType containingType,
                string containingTypeKind,
                string relativePath)
            {
                foreach (var declaration in declarations)
                {
                    var namespaceDeclaration = declaration as NamespaceDeclarationSyntax;
                    if (namespaceDeclaration != null)
                    {
                        CollectDeclarations(
                            namespaceDeclaration.Members,
                            CombineNamespace(namespaceName, Normalize(namespaceDeclaration.Name)),
                            containingType,
                            containingTypeKind,
                            relativePath);
                        continue;
                    }

                    var fileScopedNamespace = declaration as FileScopedNamespaceDeclarationSyntax;
                    if (fileScopedNamespace != null)
                    {
                        CollectDeclarations(
                            fileScopedNamespace.Members,
                            CombineNamespace(namespaceName, Normalize(fileScopedNamespace.Name)),
                            containingType,
                            containingTypeKind,
                            relativePath);
                        continue;
                    }

                    var typeDeclaration = declaration as TypeDeclarationSyntax;
                    if (typeDeclaration != null)
                    {
                        CollectType(typeDeclaration, namespaceName, containingType, containingTypeKind, relativePath);
                        continue;
                    }

                    var enumDeclaration = declaration as EnumDeclarationSyntax;
                    if (enumDeclaration != null)
                    {
                        CollectEnum(enumDeclaration, namespaceName, containingType, containingTypeKind, relativePath);
                        continue;
                    }

                    var delegateDeclaration = declaration as DelegateDeclarationSyntax;
                    if (delegateDeclaration != null)
                    {
                        CollectDelegate(delegateDeclaration, namespaceName, containingType, containingTypeKind, relativePath);
                    }
                }
            }

            private void CollectType(
                TypeDeclarationSyntax declaration,
                string namespaceName,
                ContractType containingType,
                string containingTypeKind,
                string relativePath)
            {
                string kind = GetTypeKind(declaration);
                string typeParameterList = declaration.TypeParameterList == null
                    ? string.Empty
                    : Normalize(declaration.TypeParameterList);
                string displayName = declaration.Identifier.ValueText + typeParameterList;
                string fullName = containingType == null
                    ? CombineNamespace(namespaceName, displayName)
                    : containingType.FullName + "." + displayName;
                string key = (containingType == null ? namespaceName : containingType.Key) + "|" +
                             kind + "|" + declaration.Identifier.ValueText + "|" +
                             (declaration.TypeParameterList?.Parameters.Count ?? 0);

                ContractType type;
                if (!_types.TryGetValue(key, out type))
                {
                    type = new ContractType
                    {
                        Key = key,
                        ContainingTypeKey = containingType?.Key,
                        NamespaceName = namespaceName,
                        ContainingTypeName = containingType?.FullName,
                        Kind = kind,
                        Name = declaration.Identifier.ValueText,
                        FullName = fullName,
                        Accessibility = GetTypeAccessibility(
                            declaration.Modifiers,
                            containingType != null,
                            containingTypeKind),
                        IsStatic = HasModifier(declaration.Modifiers, "static"),
                        TypeParameterList = typeParameterList,
                        Summary = GetSummary(declaration)
                    };
                    type.AccessibilityText = ContractAccessibilityRules.ToText(type.Accessibility);
                    _types.Add(key, type);
                }
                else if (string.IsNullOrEmpty(type.Summary))
                {
                    type.Summary = GetSummary(declaration);
                }

                MergeModifiers(type.Modifiers, declaration.Modifiers);
                MergeBaseTypes(type.BaseTypes, declaration.BaseList);
                MergeConstraints(type.Constraints, declaration.ConstraintClauses);
                AddDistinct(type.SourceLocations, GetSourceLocation(declaration, relativePath));

                var recordDeclaration = declaration as RecordDeclarationSyntax;
                if (recordDeclaration?.ParameterList != null)
                {
                    type.PrimaryConstructor = Normalize(recordDeclaration.ParameterList);
                    AddMember(type, new ContractMember
                    {
                        Kind = "Constructor",
                        Name = declaration.Identifier.ValueText,
                        Accessibility = type.Accessibility,
                        AccessibilityText = ContractAccessibilityRules.ToText(type.Accessibility),
                        Signature = FormatModifiers(type.Accessibility, default(SyntaxTokenList), false) + " " +
                                    declaration.Identifier.ValueText + Normalize(recordDeclaration.ParameterList) + ";",
                        SourceLocations = new List<string> { GetSourceLocation(declaration, relativePath) }
                    });
                }

                CollectTypeMembers(type, declaration.Members, kind, relativePath);
                CollectDeclarations(declaration.Members, namespaceName, type, kind, relativePath);
            }

            private void CollectEnum(
                EnumDeclarationSyntax declaration,
                string namespaceName,
                ContractType containingType,
                string containingTypeKind,
                string relativePath)
            {
                string displayName = declaration.Identifier.ValueText;
                string fullName = containingType == null
                    ? CombineNamespace(namespaceName, displayName)
                    : containingType.FullName + "." + displayName;
                string key = (containingType == null ? namespaceName : containingType.Key) + "|enum|" + displayName;
                var accessibility = GetTypeAccessibility(
                    declaration.Modifiers,
                    containingType != null,
                    containingTypeKind);

                ContractType type;
                if (!_types.TryGetValue(key, out type))
                {
                    type = new ContractType
                    {
                        Key = key,
                        ContainingTypeKey = containingType?.Key,
                        NamespaceName = namespaceName,
                        ContainingTypeName = containingType?.FullName,
                        Kind = "enum",
                        Name = displayName,
                        FullName = fullName,
                        Accessibility = accessibility,
                        AccessibilityText = ContractAccessibilityRules.ToText(accessibility),
                        Summary = GetSummary(declaration)
                    };
                    _types.Add(key, type);
                }

                MergeModifiers(type.Modifiers, declaration.Modifiers);
                MergeBaseTypes(type.BaseTypes, declaration.BaseList);
                AddDistinct(type.SourceLocations, GetSourceLocation(declaration, relativePath));

                foreach (var member in declaration.Members)
                {
                    string signature = member.Identifier.ValueText;
                    if (member.EqualsValue != null)
                        signature += " " + Normalize(member.EqualsValue);
                    signature += ";";

                    AddMember(type, new ContractMember
                    {
                        Kind = "Enum Member",
                        Name = member.Identifier.ValueText,
                        Accessibility = ContractAccessibility.Public,
                        AccessibilityText = "public",
                        Signature = signature,
                        Summary = GetSummary(member),
                        SourceLocations = new List<string> { GetSourceLocation(member, relativePath) }
                    });
                }
            }

            private void CollectDelegate(
                DelegateDeclarationSyntax declaration,
                string namespaceName,
                ContractType containingType,
                string containingTypeKind,
                string relativePath)
            {
                string typeParameterList = declaration.TypeParameterList == null
                    ? string.Empty
                    : Normalize(declaration.TypeParameterList);
                string displayName = declaration.Identifier.ValueText + typeParameterList;
                string fullName = containingType == null
                    ? CombineNamespace(namespaceName, displayName)
                    : containingType.FullName + "." + displayName;
                string key = (containingType == null ? namespaceName : containingType.Key) + "|delegate|" +
                             declaration.Identifier.ValueText + "|" +
                             (declaration.TypeParameterList?.Parameters.Count ?? 0);
                var accessibility = GetTypeAccessibility(
                    declaration.Modifiers,
                    containingType != null,
                    containingTypeKind);

                var type = new ContractType
                {
                    Key = key,
                    ContainingTypeKey = containingType?.Key,
                    NamespaceName = namespaceName,
                    ContainingTypeName = containingType?.FullName,
                    Kind = "delegate",
                    Name = declaration.Identifier.ValueText,
                    FullName = fullName,
                    Accessibility = accessibility,
                    AccessibilityText = ContractAccessibilityRules.ToText(accessibility),
                    TypeParameterList = typeParameterList,
                    DelegateReturnType = Normalize(declaration.ReturnType),
                    DelegateParameterList = Normalize(declaration.ParameterList),
                    Summary = GetSummary(declaration)
                };
                MergeModifiers(type.Modifiers, declaration.Modifiers);
                MergeConstraints(type.Constraints, declaration.ConstraintClauses);
                AddDistinct(type.SourceLocations, GetSourceLocation(declaration, relativePath));
                type.Declaration = BuildTypeDeclaration(type);
                _types[key] = type;
            }

            private void CollectTypeMembers(
                ContractType type,
                SyntaxList<MemberDeclarationSyntax> members,
                string containingTypeKind,
                string relativePath)
            {
                foreach (var member in members)
                {
                    var method = member as MethodDeclarationSyntax;
                    if (method != null)
                    {
                        var accessibility = GetMemberAccessibility(
                            method.Modifiers,
                            containingTypeKind,
                            method.ExplicitInterfaceSpecifier != null);
                        AddMember(type, CreateMember(
                            "Method",
                            method.Identifier.ValueText,
                            accessibility,
                            HasModifier(method.Modifiers, "static"),
                            method.ParameterList.Parameters.FirstOrDefault()?.Modifiers
                                .Any(token => token.Text == "this") == true,
                            BuildMethodSignature(method, accessibility),
                            method,
                            relativePath));
                        continue;
                    }

                    var constructor = member as ConstructorDeclarationSyntax;
                    if (constructor != null)
                    {
                        var accessibility = GetMemberAccessibility(
                            constructor.Modifiers,
                            containingTypeKind,
                            false);
                        AddMember(type, CreateMember(
                            "Constructor",
                            constructor.Identifier.ValueText,
                            accessibility,
                            HasModifier(constructor.Modifiers, "static"),
                            false,
                            BuildConstructorSignature(constructor, accessibility),
                            constructor,
                            relativePath));
                        continue;
                    }

                    var property = member as PropertyDeclarationSyntax;
                    if (property != null)
                    {
                        var accessibility = GetMemberAccessibility(
                            property.Modifiers,
                            containingTypeKind,
                            property.ExplicitInterfaceSpecifier != null);
                        AddMember(type, CreateMember(
                            "Property",
                            property.Identifier.ValueText,
                            accessibility,
                            HasModifier(property.Modifiers, "static"),
                            false,
                            BuildPropertySignature(property, accessibility),
                            property,
                            relativePath));
                        continue;
                    }

                    var indexer = member as IndexerDeclarationSyntax;
                    if (indexer != null)
                    {
                        var accessibility = GetMemberAccessibility(
                            indexer.Modifiers,
                            containingTypeKind,
                            indexer.ExplicitInterfaceSpecifier != null);
                        AddMember(type, CreateMember(
                            "Indexer",
                            "this",
                            accessibility,
                            HasModifier(indexer.Modifiers, "static"),
                            false,
                            BuildIndexerSignature(indexer, accessibility),
                            indexer,
                            relativePath));
                        continue;
                    }

                    var field = member as FieldDeclarationSyntax;
                    if (field != null)
                    {
                        var accessibility = GetMemberAccessibility(field.Modifiers, containingTypeKind, false);
                        foreach (var variable in field.Declaration.Variables)
                        {
                            bool isConst = HasModifier(field.Modifiers, "const");
                            string signature = FormatModifiers(accessibility, field.Modifiers, false) + " " +
                                               Normalize(field.Declaration.Type) + " " + variable.Identifier.ValueText;
                            if (isConst && variable.Initializer != null)
                                signature += " " + Normalize(variable.Initializer);
                            signature += ";";

                            AddMember(type, CreateMember(
                                "Field",
                                variable.Identifier.ValueText,
                                accessibility,
                                isConst || HasModifier(field.Modifiers, "static"),
                                false,
                                signature,
                                variable,
                                relativePath,
                                GetSummary(field)));
                        }
                        continue;
                    }

                    var eventField = member as EventFieldDeclarationSyntax;
                    if (eventField != null)
                    {
                        var accessibility = GetMemberAccessibility(eventField.Modifiers, containingTypeKind, false);
                        foreach (var variable in eventField.Declaration.Variables)
                        {
                            string signature = FormatModifiers(accessibility, eventField.Modifiers, false) + " event " +
                                               Normalize(eventField.Declaration.Type) + " " + variable.Identifier.ValueText + ";";
                            AddMember(type, CreateMember(
                                "Event",
                                variable.Identifier.ValueText,
                                accessibility,
                                HasModifier(eventField.Modifiers, "static"),
                                false,
                                signature,
                                variable,
                                relativePath,
                                GetSummary(eventField)));
                        }
                        continue;
                    }

                    var eventDeclaration = member as EventDeclarationSyntax;
                    if (eventDeclaration != null)
                    {
                        var accessibility = GetMemberAccessibility(
                            eventDeclaration.Modifiers,
                            containingTypeKind,
                            eventDeclaration.ExplicitInterfaceSpecifier != null);
                        string explicitInterface = eventDeclaration.ExplicitInterfaceSpecifier == null
                            ? string.Empty
                            : Normalize(eventDeclaration.ExplicitInterfaceSpecifier);
                        string signature = FormatModifiers(accessibility, eventDeclaration.Modifiers, true) + " event " +
                                           Normalize(eventDeclaration.Type) + " " + explicitInterface +
                                           eventDeclaration.Identifier.ValueText + " " +
                                           BuildAccessorList(eventDeclaration.AccessorList, false);
                        AddMember(type, CreateMember(
                            "Event",
                            eventDeclaration.Identifier.ValueText,
                            accessibility,
                            HasModifier(eventDeclaration.Modifiers, "static"),
                            false,
                            signature,
                            eventDeclaration,
                            relativePath));
                        continue;
                    }

                    var operatorDeclaration = member as OperatorDeclarationSyntax;
                    if (operatorDeclaration != null)
                    {
                        var accessibility = GetMemberAccessibility(operatorDeclaration.Modifiers, containingTypeKind, false);
                        string signature = FormatModifiers(accessibility, operatorDeclaration.Modifiers, false) + " " +
                                           Normalize(operatorDeclaration.ReturnType) + " operator " +
                                           operatorDeclaration.OperatorToken.Text +
                                           Normalize(operatorDeclaration.ParameterList) + ";";
                        AddMember(type, CreateMember(
                            "Operator",
                            "operator " + operatorDeclaration.OperatorToken.Text,
                            accessibility,
                            true,
                            false,
                            signature,
                            operatorDeclaration,
                            relativePath));
                        continue;
                    }

                    var conversionOperator = member as ConversionOperatorDeclarationSyntax;
                    if (conversionOperator != null)
                    {
                        var accessibility = GetMemberAccessibility(conversionOperator.Modifiers, containingTypeKind, false);
                        string name = conversionOperator.ImplicitOrExplicitKeyword.Text + " operator " +
                                      Normalize(conversionOperator.Type);
                        string signature = FormatModifiers(accessibility, conversionOperator.Modifiers, false) + " " +
                                           name + Normalize(conversionOperator.ParameterList) + ";";
                        AddMember(type, CreateMember(
                            "Operator",
                            name,
                            accessibility,
                            true,
                            false,
                            signature,
                            conversionOperator,
                            relativePath));
                    }
                }
            }

            private ContractMember CreateMember(
                string kind,
                string name,
                ContractAccessibility accessibility,
                bool isStatic,
                bool isExtensionMethod,
                string signature,
                SyntaxNode declaration,
                string relativePath,
                string summary = null)
            {
                return new ContractMember
                {
                    Kind = kind,
                    Name = name,
                    Accessibility = accessibility,
                    AccessibilityText = ContractAccessibilityRules.ToText(accessibility),
                    IsStatic = isStatic,
                    IsExtensionMethod = isExtensionMethod,
                    Signature = signature.Trim(),
                    Summary = summary ?? GetSummary(declaration),
                    SourceLocations = new List<string> { GetSourceLocation(declaration, relativePath) }
                };
            }

            private static ContractType CopyFilteredType(ContractType source, ContractVisibility visibility)
            {
                return new ContractType
                {
                    NamespaceName = source.NamespaceName,
                    ContainingTypeName = source.ContainingTypeName,
                    Kind = source.Kind,
                    Name = source.Name,
                    FullName = source.FullName,
                    Accessibility = source.Accessibility,
                    AccessibilityText = source.AccessibilityText,
                    IsStatic = source.IsStatic,
                    Declaration = source.Declaration,
                    Summary = source.Summary,
                    SourceLocations = new List<string>(source.SourceLocations),
                    Members = source.Members
                        .Where(member => ContractAccessibilityRules.Includes(member.Accessibility, visibility))
                        .Select(member => member)
                        .ToList()
                };
            }

            private static void AddMember(ContractType type, ContractMember member)
            {
                var existing = type.Members.FirstOrDefault(item =>
                    string.Equals(item.Kind, member.Kind, StringComparison.Ordinal) &&
                    string.Equals(item.Signature, member.Signature, StringComparison.Ordinal));

                if (existing == null)
                {
                    type.Members.Add(member);
                    return;
                }

                foreach (string location in member.SourceLocations)
                    AddDistinct(existing.SourceLocations, location);
                if (string.IsNullOrEmpty(existing.Summary))
                    existing.Summary = member.Summary;
            }

            private static string BuildTypeDeclaration(ContractType type)
            {
                string modifiers = FormatTypeModifiers(type);

                if (type.Kind == "delegate")
                {
                    string declaration = modifiers + " delegate " + type.DelegateReturnType + " " + type.Name +
                                         type.TypeParameterList + type.DelegateParameterList;
                    if (type.Constraints.Count > 0)
                        declaration += " " + string.Join(" ", type.Constraints.OrderBy(item => item, StringComparer.Ordinal));
                    return declaration.Trim() + ";";
                }

                string result = modifiers + " " + type.Kind + " " + type.Name + type.TypeParameterList;
                if (!string.IsNullOrEmpty(type.PrimaryConstructor))
                    result += type.PrimaryConstructor;
                if (type.BaseTypes.Count > 0)
                    result += " : " + string.Join(", ", type.BaseTypes.OrderBy(item => item, StringComparer.Ordinal));
                if (type.Constraints.Count > 0)
                    result += " " + string.Join(" ", type.Constraints.OrderBy(item => item, StringComparer.Ordinal));
                return result.Trim() + ";";
            }

            private static string BuildMethodSignature(MethodDeclarationSyntax method, ContractAccessibility accessibility)
            {
                string explicitInterface = method.ExplicitInterfaceSpecifier == null
                    ? string.Empty
                    : Normalize(method.ExplicitInterfaceSpecifier);
                string typeParameters = method.TypeParameterList == null
                    ? string.Empty
                    : Normalize(method.TypeParameterList);
                string constraints = method.ConstraintClauses.Count == 0
                    ? string.Empty
                    : " " + string.Join(" ", method.ConstraintClauses.Select(Normalize));

                return (FormatModifiers(accessibility, method.Modifiers, true) + " " +
                        Normalize(method.ReturnType) + " " + explicitInterface + method.Identifier.ValueText +
                        typeParameters + Normalize(method.ParameterList) + constraints + ";").Trim();
            }

            private static string BuildConstructorSignature(
                ConstructorDeclarationSyntax constructor,
                ContractAccessibility accessibility)
            {
                return (FormatModifiers(accessibility, constructor.Modifiers, false) + " " +
                        constructor.Identifier.ValueText + Normalize(constructor.ParameterList) + ";").Trim();
            }

            private static string BuildPropertySignature(
                PropertyDeclarationSyntax property,
                ContractAccessibility accessibility)
            {
                string explicitInterface = property.ExplicitInterfaceSpecifier == null
                    ? string.Empty
                    : Normalize(property.ExplicitInterfaceSpecifier);
                return (FormatModifiers(accessibility, property.Modifiers, true) + " " +
                        Normalize(property.Type) + " " + explicitInterface + property.Identifier.ValueText + " " +
                        BuildAccessorList(property.AccessorList, property.ExpressionBody != null)).Trim();
            }

            private static string BuildIndexerSignature(
                IndexerDeclarationSyntax indexer,
                ContractAccessibility accessibility)
            {
                string explicitInterface = indexer.ExplicitInterfaceSpecifier == null
                    ? string.Empty
                    : Normalize(indexer.ExplicitInterfaceSpecifier);
                return (FormatModifiers(accessibility, indexer.Modifiers, true) + " " +
                        Normalize(indexer.Type) + " " + explicitInterface + "this" +
                        Normalize(indexer.ParameterList) + " " +
                        BuildAccessorList(indexer.AccessorList, indexer.ExpressionBody != null)).Trim();
            }

            private static string BuildAccessorList(AccessorListSyntax accessorList, bool hasExpressionBody)
            {
                if (accessorList == null)
                    return hasExpressionBody ? "{ get; }" : "{ }";

                var accessors = accessorList.Accessors.Select(accessor =>
                {
                    string modifiers = string.Join(" ", accessor.Modifiers.Select(token => token.Text));
                    return (string.IsNullOrEmpty(modifiers) ? string.Empty : modifiers + " ") +
                           accessor.Keyword.Text + ";";
                });
                return "{ " + string.Join(" ", accessors) + " }";
            }

            private static ContractAccessibility GetTypeAccessibility(
                SyntaxTokenList modifiers,
                bool isNested,
                string containingTypeKind)
            {
                var explicitAccessibility = GetExplicitAccessibility(modifiers);
                if (explicitAccessibility.HasValue)
                    return explicitAccessibility.Value;

                return isNested && containingTypeKind == "interface"
                    ? ContractAccessibility.Public
                    : isNested
                        ? ContractAccessibility.Private
                        : ContractAccessibility.Internal;
            }

            private static ContractAccessibility GetMemberAccessibility(
                SyntaxTokenList modifiers,
                string containingTypeKind,
                bool isExplicitInterfaceImplementation)
            {
                if (isExplicitInterfaceImplementation)
                    return ContractAccessibility.ExplicitInterface;

                var explicitAccessibility = GetExplicitAccessibility(modifiers);
                if (explicitAccessibility.HasValue)
                    return explicitAccessibility.Value;

                return containingTypeKind == "interface"
                    ? ContractAccessibility.Public
                    : ContractAccessibility.Private;
            }

            private static ContractAccessibility? GetExplicitAccessibility(SyntaxTokenList modifiers)
            {
                bool hasPublic = HasModifier(modifiers, "public");
                bool hasPrivate = HasModifier(modifiers, "private");
                bool hasProtected = HasModifier(modifiers, "protected");
                bool hasInternal = HasModifier(modifiers, "internal");

                if (HasModifier(modifiers, "file"))
                    return ContractAccessibility.File;
                if (hasPublic)
                    return ContractAccessibility.Public;
                if (hasPrivate && hasProtected)
                    return ContractAccessibility.PrivateProtected;
                if (hasProtected && hasInternal)
                    return ContractAccessibility.ProtectedInternal;
                if (hasProtected)
                    return ContractAccessibility.Protected;
                if (hasInternal)
                    return ContractAccessibility.Internal;
                if (hasPrivate)
                    return ContractAccessibility.Private;
                return null;
            }

            private static string FormatTypeModifiers(ContractType type)
            {
                var parts = new List<string> { ContractAccessibilityRules.ToText(type.Accessibility) };
                parts.AddRange(type.Modifiers
                    .Where(modifier => !IsAccessibilityModifier(modifier) && modifier != "file")
                    .OrderBy(GetModifierOrder)
                    .ThenBy(modifier => modifier, StringComparer.Ordinal));
                return string.Join(" ", parts);
            }

            private static string FormatModifiers(
                ContractAccessibility accessibility,
                SyntaxTokenList modifiers,
                bool omitForExplicitInterface)
            {
                var parts = new List<string>();
                if (!(omitForExplicitInterface && accessibility == ContractAccessibility.ExplicitInterface))
                    parts.Add(ContractAccessibilityRules.ToText(accessibility));

                parts.AddRange(modifiers
                    .Select(token => token.Text)
                    .Where(modifier => !IsAccessibilityModifier(modifier) &&
                                       modifier != "file" &&
                                       modifier != "async")
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(GetModifierOrder)
                    .ThenBy(modifier => modifier, StringComparer.Ordinal));
                return string.Join(" ", parts);
            }

            private static bool IsAccessibilityModifier(string modifier)
            {
                return modifier == "public" || modifier == "protected" || modifier == "internal" ||
                       modifier == "private";
            }

            private static int GetModifierOrder(string modifier)
            {
                switch (modifier)
                {
                    case "new": return 0;
                    case "static": return 1;
                    case "abstract": return 2;
                    case "virtual": return 3;
                    case "override": return 4;
                    case "sealed": return 5;
                    case "readonly": return 6;
                    case "ref": return 7;
                    case "unsafe": return 8;
                    case "extern": return 9;
                    case "partial": return 10;
                    case "required": return 11;
                    case "const": return 12;
                    case "volatile": return 13;
                    default: return 100;
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

            private static string GetTypeKind(TypeDeclarationSyntax declaration)
            {
                var record = declaration as RecordDeclarationSyntax;
                if (record != null)
                    return record.ClassOrStructKeyword.Text == "struct" ? "record struct" : "record";
                if (declaration is InterfaceDeclarationSyntax)
                    return "interface";
                if (declaration is StructDeclarationSyntax)
                    return "struct";
                return "class";
            }

            private static void MergeModifiers(HashSet<string> target, SyntaxTokenList modifiers)
            {
                foreach (string modifier in modifiers.Select(token => token.Text))
                    target.Add(modifier);
            }

            private static void MergeBaseTypes(HashSet<string> target, BaseListSyntax baseList)
            {
                if (baseList == null)
                    return;
                foreach (var baseType in baseList.Types)
                    target.Add(Normalize(baseType.Type));
            }

            private static void MergeConstraints(
                HashSet<string> target,
                SyntaxList<TypeParameterConstraintClauseSyntax> constraints)
            {
                foreach (var constraint in constraints)
                    target.Add(Normalize(constraint));
            }

            private static string GetSummary(SyntaxNode declaration)
            {
                var documentation = declaration.GetLeadingTrivia()
                    .Select(trivia => trivia.GetStructure())
                    .OfType<DocumentationCommentTriviaSyntax>()
                    .LastOrDefault();
                if (documentation == null)
                    return null;

                var summaryElement = documentation.Content
                    .OfType<XmlElementSyntax>()
                    .FirstOrDefault(element =>
                        string.Equals(element.StartTag.Name.LocalName.ValueText, "summary", StringComparison.OrdinalIgnoreCase));
                if (summaryElement == null)
                    return null;

                string text = string.Concat(summaryElement.DescendantTokens()
                    .Where(token => token.IsKind(SyntaxKind.XmlTextLiteralToken))
                    .Select(token => token.ValueText));
                text = Regex.Replace(text, @"\s+", " ").Trim();
                return string.IsNullOrEmpty(text) ? null : text;
            }

            private static string GetSourceLocation(SyntaxNode declaration, string relativePath)
            {
                int line = declaration.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                return relativePath + ":" + line;
            }

            private static string Normalize(SyntaxNode node)
            {
                return node.WithoutTrivia()
                    .NormalizeWhitespace(string.Empty, " ", false)
                    .ToFullString()
                    .Trim();
            }

            private static bool HasModifier(SyntaxTokenList modifiers, string value)
            {
                return modifiers.Any(token => string.Equals(token.Text, value, StringComparison.Ordinal));
            }

            private static string CombineNamespace(string left, string right)
            {
                if (string.IsNullOrEmpty(left))
                    return right ?? string.Empty;
                if (string.IsNullOrEmpty(right))
                    return left;
                return left + "." + right;
            }

            private static void AddDistinct(List<string> values, string value)
            {
                if (!values.Contains(value, StringComparer.Ordinal))
                    values.Add(value);
            }

            private static List<string> SortDistinct(IEnumerable<string> values)
            {
                return values
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(GetLocationPath, StringComparer.Ordinal)
                    .ThenBy(GetLocationLine)
                    .ToList();
            }

            private static string GetLocationPath(string value)
            {
                int separator = value.LastIndexOf(':');
                return separator < 0 ? value : value.Substring(0, separator);
            }

            private static int GetLocationLine(string value)
            {
                int separator = value.LastIndexOf(':');
                int line;
                return separator >= 0 && int.TryParse(value.Substring(separator + 1), out line)
                    ? line
                    : 0;
            }
        }
    }
}
