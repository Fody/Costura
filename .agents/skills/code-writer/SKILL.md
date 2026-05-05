---
name: code-writer
description: >
  Write production C# code following repository coding standards and architecture.

  Triggers: "write code", "implement feature", "add feature", "fix bug", "create class",
  "add method", "implement interface", "refactor", "add property", "extend functionality",
  any GitHub issue number referencing a feature or bug fix.
---

## Mission
Implement high-quality, maintainable production C# code that fully
respects the project's coding standards, architecture, and ABI stability requirements.

Produce small, focused pull requests so that maintainers can review and merge them efficiently.

---

## Responsibilities
- Implement new features and bug fixes in the correct project.
- Follow all coding conventions enforced by `.editorconfig` and `AGENTS.md`.
- Maintain ABI/API stability: never remove or modify existing public members.
- Write XML documentation comments for every new public member.
- Ensure all existing and new tests pass before submitting a PR.
- Keep PRs small, focused, and reviewable.

---

## Inputs
- GitHub issue description or feature request.
- C# source files in the projects.
- Existing test files in the test projects (for understanding expected behavior).
- Project/solution files (`.csproj`, `.slnx`).
- Repository contribution guidelines (`AGENTS.md`, `CONTRIBUTING.md`).
- `.editorconfig` (authoritative formatting and style rules).

---

## Outputs
- New or updated `.cs` source files inside the source code projects.
- Corresponding new or updated test files inside the test projects.
- Pull requests containing:
  - A concise description of what was implemented or fixed.
  - References to the issue being resolved.
  - Notes on any design decisions or trade-offs.

---

## Workflow

### 1. Understand the requirement
- Read the issue or feature request thoroughly.
- Identify which layer is affected.
- Study adjacent code to understand patterns, naming, and existing conventions.
- Determine whether the change is additive (safe) or requires modifying existing members.

### 2. Plan the implementation
- Prefer adding new overloads, methods, or classes over modifying existing signatures.
- Never use default parameters in **public** APIs — use overloads instead. Default parameters are acceptable in private or internal methods. Note: public properties with field initializers are generally safe, but avoid default values that encode business logic visible to callers.
- Identify interfaces that need to be updated and ensure all implementations are updated too.
- Never edit `*.generated.cs` or `*.generated.xaml` files — these are auto-generated.

### 3. Implement the code

#### File layout
```csharp
namespace <Namespace>;    // file-scoped namespace

using System;
// other using directives (inside namespace, sorted — System.* first)

/// <summary>
/// Summary of the type.
/// </summary>
public class MyClass
{
    private readonly IService _service;    // private fields: _camelCase

    /// <summary>
    /// Initializes a new instance of the <see cref="MyClass"/> class.
    /// </summary>
    public MyClass(IService service)
    {
        _service = service;
    }

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <value>The value.</value>
    public string Value { get; private set; }

    /// <summary>
    /// Does the work.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentNullException">The <paramref name="input"/> is <c>null</c>.</exception>
    public string DoWork(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // implementation
        return input;
    }
}
```

#### Naming conventions
| Element | Convention | Example |
|---------|-----------|---------|
| Namespace, class, struct, enum, delegate | PascalCase | `MyClass` |
| Interface | `I` + PascalCase | `IMyClass` |
| Public/internal/protected member | PascalCase | `GetValue`, `IsEnabled` |
| Parameter | camelCase | `propertyName`, `container` |
| Private instance field | `_` + camelCase | `_logger`, `_lockObject` |
| Private static field | PascalCase | `Logger` |
| Constant | PascalCase | `DefaultTimeout` |
| Local variable | `var` (preferred) | `var result = ...` |

#### Formatting rules
- **Indentation**: 4 spaces for `.cs` files; 2 spaces for XML/XAML/JSON/project files.
- **Braces**: Always required — even for single-line `if`, `for`, `foreach`, `while`, `do-while`.
- **Brace style**: Opening brace on a new line (`csharp_new_line_before_open_brace = all`).
- **`using` directives**: Inside the namespace, `System.*` first.
- **Trailing whitespace**: None.
- **Final newline**: Required on every `.cs` file.
- **Encoding**: UTF-8 with BOM for `.cs` files.
- **Expression-bodied members**: Preferred for properties, indexers, and accessors; **not** for methods or constructors.
- **`var`**: Prefer `var` for local variables throughout.

#### Coding practices
- Validate arguments using `ArgumentNullException.ThrowIfNull(arg)` or `Argument.IsNotNullOrWhitespace(...)` as appropriate.
- Prefer `ArgumentNullException.ThrowIfNull` for null checks in modern code.
- Use `Catel.Argument` helpers for composite validations (e.g., `IsNotNullOrWhitespace`).
- Use the Catel `LogManager` for logging. Declare a static logger field at the top of each class:
  ```csharp
  private static readonly ILogger Logger = LogManager.GetLogger(typeof(MyClass));
  ```
  Use `ILogger<T>` from Microsoft.Extensions.Logging only in classes that receive it via dependency injection (as in `MessageMediator`).
- Prefer `readonly` fields where possible (`dotnet_style_readonly_field = true:error`).
- Always specify accessibility modifiers on every member (`dotnet_style_require_accessibility_modifiers = always`).
- Avoid `this.` qualification unless required to disambiguate.
- Use language keywords (`int`, `string`, `bool`) instead of framework type names (`Int32`, `String`, `Boolean`).
- Prefer modern C# patterns: pattern matching over `is` + cast, null-conditional operators, null-coalescing, `throw` expressions.
- Never add `default` parameter values to public API methods — add a new overload instead.

#### ABI / API stability
| Allowed ✅ | Never ❌ |
|-----------|---------|
| Add new overloads | Modify existing method signatures |
| Add new public methods or properties | Remove existing public members |
| Add new classes or interfaces | Change return types |
| Add interface default implementations only when all existing target frameworks support it (C# 8.0+ / .NET Core 3.0+, .NET Standard 2.1+, or .NET 5+) | Break existing callers |

### 4. Write or update tests
- Add or update tests in the test projects, mirroring the production namespace.
- Follow the NUnit conventions described in the `unit-tests` skill.
- Ensure every new public method has at least:
  - A happy-path test.
  - A null/invalid argument test.
- Build and run the full test suite before submitting.

### 5. Validate changes
- Build the solution:
  ```bash
  dotnet cake --target=build
  ```
- Run all tests:
  ```bash
  dotnet cake --target=test
  ```
- **All tests must pass.** Do NOT skip or comment out failing tests.

### 6. Create a pull request
- Create a feature branch:
  ```
  feature/issue-NNNN-short-description
  ```
- Never commit directly to `master` or `develop`.
- Commit message format (from `CONTRIBUTING.md`):
  ```
  CTL-NNNN Short description (~50 chars)

  Explain why the change was made and what it does differently
  from the previous behavior. Use present tense ("Fix" not "Fixed").
  ```
- Write a PR description that includes:
  - What was implemented or fixed.
  - References to the issue (`Closes #NNNN`).
  - Any design decisions or known limitations.
  - Follow-up work if applicable.

---

## Constraints
- **Never** commit directly to `master` or `develop`.
- **Never** edit `*.generated.cs` or `*.generated.xaml` files.
- **Never** use default parameter values on public API methods.
- **Never** remove or change existing public member signatures.
- **Never** submit a PR with failing tests.
- **Never** reformat code that is not directly related to the change.
- Keep PRs small and focused on a single concern.
- Keep a maximum of 10 pull requests open at a time.
- Respect repository contribution guidelines (`CONTRIBUTING.md`).

---

## Error Handling
- If a feature cannot be implemented without breaking ABI, flag this in the PR and propose alternatives.
- If the build fails after changes, fix the build before submitting the PR.
- If tests fail due to pre-existing issues unrelated to your change, document them in the PR but do not modify them.
- If an interface needs a new member that would break existing implementations, consider adding an extension method instead, or use a default interface implementation only if the codebase target framework supports it.

---
