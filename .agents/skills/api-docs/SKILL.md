---
name: api-docs
description: >
  Write and review XML API documentation following .NET guidelines.
  
  Triggers: "document class", "add XML docs", "write XML documentation", "add triple-slash comments",
  "review documentation quality", "check docs for errors", "fix doc issues", "fill in missing docs",
  "remove To be added placeholders", API documentation requests.
---

## Mission
Ensure every public API in the codebase is documented with clear, correct, and idiomatic XML documentation comments.

Produce small, focused pull requests that are easy for maintainers to review and merge.

---

## Responsibilities
- Scan C# projects for undocumented or poorly documented public APIs.
- Add or improve XML documentation comments for:
  - Classes, interfaces, structs, records
  - Methods, constructors, operators
  - Properties, fields, events
  - Generic type parameters
  - Exceptions thrown
- Maintain consistent style and terminology across the codebase.
- Keep PRs small and focused (e.g., one namespace or one file per PR).
- Provide clear commit messages and PR descriptions summarizing changes.

---

## Inputs
- C# source files (`.cs`)
- Project/solution files (`.csproj`, `.sln`, `.slnx`)
- Repository structure and branch naming conventions
- Developer‑provided architectural notes or domain context
- Existing XML documentation comments (to preserve tone and terminology)

---

## Outputs
- Updated `.cs` files containing:
  - `<summary>` blocks
  - `<param>` descriptions
  - `<typeparam>` descriptions
  - `<returns>` descriptions
  - `<exception>` tags where applicable
  - `<remarks>` when additional context is useful
- Pull requests containing:
  - Only documentation changes
  - A concise summary of what was updated
  - Notes on any ambiguous or unclear API surfaces

---

## Workflow

### 1. Analyze the codebase
- Identify undocumented public members.
- Detect incomplete or outdated XML comments.
- Group findings into small, coherent batches for PRs.

### 2. Generate documentation comments
- Write clear, concise summaries describing purpose and behavior.
- Document all parameters and type parameters.
- Describe return values meaningfully.
- Add `<exception>` tags for thrown exceptions.
- Add `<remarks>` for usage notes, constraints, or side effects.
- Preserve existing developer terminology and domain language.

### 3. Apply changes safely
- Modify only documentation comments.
- Avoid altering functional code.
- Keep diffs minimal and deterministic.

### 4. Validate changes
- Ensure XML documentation builds without warnings.
- Check for:
  - Missing tags
  - Incorrect parameter names
  - Grammar or clarity issues
  - Overly verbose or redundant descriptions

### 5. Create pull request
- Create a branch named:
  ```
  docs/xml-<area>-<timestamp>
  ```
- Commit only the documentation changes.
- Write a PR description including:
  - What was documented
  - Any unclear APIs needing developer input
  - Any recommended follow‑up areas

---

## Style Guidelines
- Use clear, direct language.
- Prefer active voice.
- Avoid restating method names in summaries.
- Summaries should start with a verb (e.g., “Gets…”, “Creates…”, “Determines…”).
- Keep summaries to one or two sentences.
- Use `<remarks>` for extended explanations.
- Use fenced code blocks in `<example>` tags when helpful.

---

## Constraints
- Never modify logic, signatures, or behavior.
- Never remove existing documentation unless it is clearly incorrect.
- Keep PRs small and easy to review.
- Keep a maximum of 10 pull requests open at a time to avoid overwhelming maintainers.
- Maintain deterministic formatting and ordering.
- Respect repository contribution guidelines and branch protection rules.

---

## Error Handling
- If a member’s behavior is unclear, add a TODO comment and flag it in the PR.
- If documentation cannot be generated (e.g., ambiguous API), include a diagnostic note in the PR.
- If build warnings occur, include them in the PR description.

---

## Automation Hooks
- Trigger on:
  - New commits to main or development branches
  - Changes to `.cs` files
- Optional scheduled run to gradually improve documentation coverage.

---
