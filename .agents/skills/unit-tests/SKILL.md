---
name: unit-tests
description: >
  Write unit tests for this repository using NUnit following repository best practices.

  Triggers: "write unit tests", "add tests", "create tests", "add unit tests",
  "test coverage", "missing tests", any GitHub issue number referencing unit tests.
---

## Mission
Write high-quality, maintainable NUnit unit tests for the Catel codebase.

Produce small, focused pull requests — a maximum of **25 new tests per PR** — so that maintainers can review and merge them efficiently.

---

## Responsibilities
- Identify untested or undertested public members
- Write NUnit test cases that cover:
  - Happy paths (expected inputs → expected outputs)
  - Edge cases (boundary values, empty collections, null inputs)
  - Error paths (expected exceptions)
- Keep each PR limited to **10 new test methods** to stay reviewable.
- Use partial classes and partial files to split large test fixtures when a single testable type accumulates too many tests.
- Maintain consistent naming, formatting, and structure across all test files.

---

## Inputs
- C# source files
- Existing test files in the test project (conventions reference)
- Test project file
- Repository contribution guidelines (`AGENTS.md`, `CONTRIBUTING.md`)

---

## Outputs
- New or updated `.cs` test files inside the test project, mirroring the production namespace hierarchy.
- Pull requests containing:
  - At most **10 new test methods**
  - A concise description of what was tested and why
  - Notes on any members that require special setup or are not yet testable

---

## Workflow

### 1. Identify what to test
- Browse the production source.
- Search for public types and members that have no corresponding test class or insufficient coverage.
- Prioritize:
  1. Public methods with complex branching logic.
  2. Methods that validate arguments (null / empty checks).
  3. Methods that fire events or change observable state.

### 2. Locate or create the test file
- Check if the component relative test project first (e.g. `Catel.Core` => `Catel.Core.Tests`). If the relative test project does not exist, use the generic test project (e.g. `Catel.Core` => `Catel.Tests`).
- Test files live in the test project and mirror the production namespace.
  - Example: `Catel.Configuration.ConfigurationService` → `src/Catel.Tests/Configuration/ConfigurationServiceFacts.cs`
- If the file does not exist, create it.
- If the existing file already contains many tests, create a new **partial file** named with a descriptive suffix:
  ```
  ConfigurationServiceFacts.SetValue.cs
  ConfigurationServiceFacts.GetValue.cs
  ```
- Every partial file must declare `public partial class <TypeName>Facts` at its top level.

### 3. Write the tests

#### File and namespace layout
```csharp
namespace Catel.Tests.<Namespace>;

using NUnit.Framework;
// additional using statements as needed

public partial class <TypeName>Facts
{
    [TestFixture]
    public class The_<MemberName>_<Kind>   // e.g. The_GetValue_Method, The_ConfigurationChanged_Event
    {
        [Test]
        public void <Scenario_Description>()   // Pascal_Snake_Case
        {
            // Arrange
            // Act
            // Assert
        }
    }
}
```

#### Naming rules
| Element | Convention | Example |
|---------|-----------|---------|
| Facts class | `<TypeName>Facts` | `ConfigurationServiceFacts` |
| Inner fixture | `The_<MemberName>_<Kind>` | `The_GetValue_Method` |
| Test method | `PascalCase_Snake_Words` | `Returns_Default_Value_For_Non_Existing_Key` |

#### Inner fixture naming
Use the prefix `The_` followed by the member name and a kind suffix:

| Member kind | Suffix |
|-------------|--------|
| Regular method | `_Method` |
| Property getter/setter | `_Property` |
| Constructor | `_Constructor` |
| Event | `_Event` |
| Indexer | `_Indexer` |

#### Assertions
- Always use the constraint model: `Assert.That(actual, Is.EqualTo(expected))`.
- Never use the legacy classic model (`Assert.AreEqual`, `Assert.IsTrue`, etc.).
- Use `Assert.Throws<TException>(() => ...)` for exception tests.
- For async code use `Assert.ThrowsAsync<TException>(async () => ...)`.

#### Test data / parameterization
- Use `[TestCase]` for simple parameterized tests.
- Use `[TestCaseSource]` for complex or reusable data sets.

#### Arrange / Act / Assert
- Separate the three phases with a blank line; do **not** add inline comments (`// Arrange`, etc.) unless the test body is long enough to warrant orientation.

#### Verify.NUnit

For complex verifications (such as rendered json, etc), use VerifyTests (see https://github.com/VerifyTests/Verify). 

An example:

```csharp
public async Task Created_Serializer_Produces_Valid_Json()
{
	var factory = new JsonSerializerFactory();
	var serializer = factory.CreateSerializer();
	var data = new 
	{ 
		Id = 1, 
		Name = "Factory" 
	};

	using var stream = new MemoryStream();
	serializer.Serialize(stream, data);

	var json = Encoding.UTF8.GetString(stream.ToArray());

	await Verifier.Verify(json);
}
```

#### Coding practices
- Namespaces are considered "feature containers". Don't add specific folder names to namespaces such as '.Models', '.Exceptions', '.EventArgs', '.Interfaces', '.Services'

#### Example
```csharp
public class ConfigurationServiceFacts
{
	[TestFixture]
	public class The_GetValue_Method
	{
		[TestCase(ConfigurationContainer.Local)]
		[TestCase(ConfigurationContainer.Roaming)]
		public async Task Throws_ArgumentException_For_Null_Key(ConfigurationContainer container)
		{
			var service = await GetConfigurationServiceAsync();

			Assert.Throws<ArgumentException>(() => service.GetValue<string>(container, null));
		}

		[TestCase(ConfigurationContainer.Local)]
		[TestCase(ConfigurationContainer.Roaming)]
		public async Task Returns_Default_Value_For_Non_Existing_Key(ConfigurationContainer container)
		{
			var service = await GetConfigurationServiceAsync();

			Assert.That(service.GetValue(container, "missing", "default"), Is.EqualTo("default"));
		}
	}
}
```

### 4. Validate changes
- Build the test project:
  ```bash
  dotnet cake --target=build
  ```
- Run only the new tests:
  ```bash
  dotnet cake --target=test
  ```
- Ensure **all** tests pass before opening a PR. Do **not** skip or comment out failing tests.

### 5. Create a pull request
- Create a feature branch:
  ```
  feature/tests-<area>-<short-description>
  ```
- Commit only the new/updated test files.
- Write a PR description that includes:
  - Which type was tested.
  - Which members were covered.
  - Any members skipped and why.
  - Any follow-up test areas planned.

---

## Partial-file strategy

When a single `*Facts` class grows large, split it across multiple files by member group:

```
src/Catel.Tests/Configuration/
  ConfigurationServiceFacts.cs               ← shared helpers + constructor tests
  ConfigurationServiceFacts.GetValue.cs     ← The_GetValue_Method fixture
  ConfigurationServiceFacts.SetValue.cs     ← The_SetValue_Method fixture
  ConfigurationServiceFacts.events.cs        ← event fixtures
```

Every file must:
1. Declare the same `namespace`.
2. Declare `public partial class ConfigurationServiceFacts`.
3. Include only the inner `[TestFixture]` classes relevant to that member group.

---

## Constraints
- **Maximum 10 new test methods per PR.** If more are needed, open additional PRs.
- Never modify production source code unless there is a clear bug directly blocking testability.
- Never remove or disable existing tests.
- Always use NUnit; do not introduce xUnit or MSTest.
- Always use the constraint-model assertion API.
- Respect branch protection rules: never commit directly to `master` or `develop`.
- Follow the repository coding style (4-space indentation, `var` for locals, no trailing whitespace).

---

## Error Handling
- If a member cannot be tested due to missing infrastructure, add a TODO comment in the PR description and skip that member.
- If the build or tests fail, fix the issue before submitting the PR.
- If test setup requires mocking, use the Moq library already present in the test project.

---
