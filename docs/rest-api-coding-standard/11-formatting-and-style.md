# Formatting And Style

## EditorConfig

Use an `.editorconfig` file at the repository root to enforce consistent code style.

```ini
root = true

[*.cs]
indent_style = space
indent_size = 4
csharp_style_var_for_built_in_types = true:error
csharp_style_var_when_type_is_apparent = true:error
csharp_style_expression_bodied_methods = true:silent
csharp_style_pattern_matching_over_is_with_cast_check = true:error
csharp_style_prefer_not_pattern = true:error
csharp_style_prefer_pattern_matching = true:error
dotnet_diagnostic.IDE0044.severity = warning  # Make field readonly
dotnet_diagnostic.CA1822.severity = warning    # Member does not access instance data
```

Rules:

- Commit `.editorconfig` to the repository.
- Set severity to `error` for style rules that must be followed.
- Set severity to `warning` or `suggestion` for style rules that are guidelines.
- Do not override the `.editorconfig` in individual projects without a documented reason.

## .NET Analyzers And StyleCop

Enable built-in .NET analyzers and StyleCop to catch code quality issues during build.

```xml
<PropertyGroup>
  <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  <AnalysisLevel>latest</AnalysisLevel>
  <AnalysisMode>Recommended</AnalysisMode>
</PropertyGroup>
```

For StyleCop, add the NuGet package:

```xml
<PackageReference Include="StyleCop.Analyzers" PrivateAssets="all" Condition="'$(Configuration)' == 'Debug'" />
```

Rules:

- Enable `EnforceCodeStyleInBuild` for all projects.
- Set `AnalysisLevel` to `latest` to catch new diagnostics.
- Use `AnalysisMode` of `Recommended` or `All` based on the team's tolerance for warnings.
- Configure StyleCop rules through `.editorconfig` or a `stylecop.json` file at the project root.
- Treat analyzer warnings as build errors in CI.

## Nullable Reference Types

Enable nullable reference types for all projects.

```xml
<PropertyGroup>
  <Nullable>enable</Nullable>
</PropertyGroup>
```

Rules:

- Enable nullable reference types in every project.
- Annotate reference types that can be null with `?` (e.g., `string?`, `ProductDto?`).
- Use null-forgiving operator (`!`) only when the compiler cannot infer non-nullness and the value is guaranteed non-null by the application logic.
- Do not disable nullable reference types with `#nullable disable` unless required by a generated file or a third-party tool.

## Global Usings And Implicit Usings

Use global usings for namespaces that are used across most files in a project.

```csharp
// GlobalUsings.cs
global using System;
global using System.Collections.Generic;
global using System.Threading;
global using System.Threading.Tasks;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.EntityFrameworkCore;
```

Rules:

- Place global usings in a dedicated `GlobalUsings.cs` file.
- Use `<ImplicitUsings>enable</ImplicitUsings>` in the project file for common ASP.NET Core and System namespaces.
- Do not add global usings for rarely used namespaces. Use explicit `using` directives in the file that needs them.
- Remove unused `using` directives regularly (IDE0005).

## File Organization

Organize source files with a consistent member order.

```text
For a class file:
1. Fields (private)
2. Constructor
3. Properties (public)
4. Public methods
5. Private methods
6. Nested types (if any)
```

Rules:

- Keep one public type per file unless the types are tightly coupled and small.
- Name the file after the primary public type it contains.
- Order members by visibility: public, internal, protected, private.
- Keep methods short and focused on a single responsibility.
- Do not put multiple independent public types in a single file.

## General Style Rules

- Enable nullable reference types.
- Keep files focused on one public type where practical.
- Prefer clear names over abbreviations.
- Keep methods short and purpose-specific.
- Use expression-bodied members only when readability improves.
- Prefer guard clauses for invalid or missing data.
- Avoid comments that repeat the code. Comment only non-obvious decisions.
- Use `var` when the type is obvious from the right-hand side.
- Prefer `switch` expressions over `switch` statements for simple pattern matching.
- Prefer collection expressions (`[]`) over `Array.Empty<T>()` or `new List<T>()` in .NET 8+.
- Use file-scoped namespaces (`namespace MyNamespace;`), not block-scoped namespaces.
