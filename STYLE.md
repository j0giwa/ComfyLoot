# Coding Style Guide

These are guidelines.
The most important aspect of style is **consistency**.
Match the existing project style.
Project maintainers have discretion to interpret these rules as appropriate.

> "Code is more often read than written. Write it like you're writing for someone who knows nothing but has to understand everything." – Adapted from suckless & Bell Labs traditions.

## Recommended Reading

The following contain good information, some of which is repeated below, some of which is contradicted below.
These references come from C, but the spirit applies to minimalist and maintainable C# code.

- https://man.openbsd.org/style
- http://doc.cat-v.org/bell_labs/pikestyle
- https://www.kernel.org/doc/Documentation/process/coding-style.rst
- https://suckless.org/coding_style/

---

## About AI generated content.

TL;DR: don't!

Lets get the elephant out of the room: Code written without understanding is just bloat with extra steps. 
AI-slopware compiles, passes tests it accidentally satisfies and fails the ones that matter in production.
You get layers of abstractions nobody chose, dependencies nobody audits, and bugs nobody can reason about.
Not to mention the blatant security liabity question.

No AI generated code should make it in the stable branch of the Dalamud repository, 
usage is, albeit barely, acceptable to outline proof of concepts for experimental features.
(Reminder: experimental featurs will only make it into stable once users deemed them a nessecity)
This code should however still match the style guide, this purposefully contains quirks that ai by default does not do, unless specificaly instructed.

## File Layout

- Start every file with a short comment and LICENSE info.
- File layout should generally follow this order:

```
1. Using directives
2. Namespace declaration
3. Class/struct declarations
4. Constants and static readonly fields
5. Fields
6. Constructors
7. Internal/private methods
8. Public methods
9. Main (if any)
10. Disposers/Finalizsers
```

- Avoid multiple classes per file unless **trivial** or **strongly related.**

---

## C# Features

- Use **standard C# without dependencies** on platform-specific extensions or experimental features.
- Avoid LINQ or reflection unless it simplifies the code significantly.
- Avoid unnessesary syntactic suggar
- Avoid async/await unless concurrency is essential.
- Do not mix declarations and logic unnecessarily.

---

## Comments

- Use Doc comments `///` to summarize functions
- Prefer `/* block comments */` over `//`.
- Document intent, not mechanics.
- Only comment what isn’t obvious from the code itself.
  - Functions should be written as trivial as possible

---

## Blocks and Bracing

- All variable declarations should be at the top of the block.
- Opening `{` goes **on the same line**, except for method declarations.
- Closing `}` is always on its **own line**, unless continuing a compound statement.
- Avoid unnecessary braces when a single statement is sufficient and clear.

```csharp
if (foo)
	bar();
else
	baz();
```

- Use blocks when either:
  - One branch requires it.
  - It improves clarity or consistency.

---

## Indentation and Whitespace

- Use tabs for indentation, spaces for alignment.
  - Never mix tabs/spaces for indentation.
  - Do not indent `#region`, `#define`, etc., with tabs—use spaces if alignment is needed.
- No trailing whitespace.
- No space between method name and `(`, but a space after keywords:

```csharp
if (condition) {
	Foo();
}
```

- No space inside parentheses:

```csharp
Bar(x, y);
```

---

## Functions and Methods

- Return type and modifiers **on their own line**.
- Function name and argument list on next line. This allows to grep for function names simply using grep ^functionname(
- Opening { on own line.

```csharp
private static int
ComputeSum(int a, int b)
{
	return a + b;
}
```

- Declare private methods as `static` where applicable.
- Order method definitions logically, matching declaration order.
- Functions should be **short and focused**. Prefer functions that:
  - Fit within one or two screens (80x24).
  - Do one thing, and do it well.
- The maximum acceptable length of a function is **inversely proportional to its complexity and nesting level**. A simple function may be longer (e.g. a long but flat `switch`), but a deeply nested or complex function must be shorter.

---

## Variables

- Use `static` for fields/methods not accessed from instance.
- Avoid Hungarian notation or prefixes like `m_` or `_`.
- Otherwise use typical C# conventions without noise.

---

## Keywords and Syntax

- Always use braces for clarity unless a single statement is genuinely clearer without them.
- Use compound assignments or combined expressions where short and clear:

```csharp
if ((stream = OpenFile(path)) == null) {
	Fail();
}
```

---

## Switch Statements

- Do **not** add an extra indent for `case` blocks.
- Use `/* FALLTHROUGH */` comments where intentional.
- Use `default` as a fallback.

```csharp
switch (value) {
case 0: /* FALLTHROUGH */
case 1:
	DoSomething();
	break;
default:
	DoDefault();
	break;
}
```

---

## Using Directives

- Sort using directives **alphabetically**.
- Place **system/usual libraries first**, followed by an empty line, then local/project-specific ones.

```csharp
using System;
using System.Collections.Generic;

using MyApp.Core;
```

---

## Naming and Types

- Use **PascalCase** for type names.
- Use **camelCase** for variables and parameters.
- Do **not typedef or alias** built-in types (e.g., don’t create `using MyInt = int;`).

---

## Line Length

- Try to keep lines at reasonable length (max 79 characters).
    - exceptions can be made (C# does have some long ClassNames)
- Break long expressions logically and cleanly.

---

## Booleans and Conditions

- Avoid implicit `bool` conversions when it hides intent.
- Use `== true` or `!= null` if clarity benefits.

```csharp
if (isValid) {
	Process();
}
```

- Prefer early returns to reduce nesting.

```csharp
if (!CheckInput(input)) {
	return;
}
```

---

## Error Handling

- Prefer `try/catch` for expected runtime errors, **but do not overuse**.
- On critical failure, fail early.
- **Do not swallow exceptions silently.**

```csharp
try {
	DoWork();
} catch (IOException e) {
	LogError(e.Message);
	throw;
}
```

---

## Enums and Constants

- Use `enum` for semantically related groups.
- Use `const` or `static readonly` for other constants.

```csharp
private enum Direction {
	DIRECTION_X,
	DIRECTION_Y,
	DIRECTION_Z
}

private const int MaxBufferSize = 4096;
private const int MagicNumber = 0xDEADBEEF;
```

---

## Miscellaneous

- Avoid unnecessary object orientation. Prefer **plain classes with methods** over complex hierarchies.
- Use records/structs **only when immutability or performance demands it**.
- Avoid auto-generated code and designer files when possible.
- **Do not optimize prematurely**.
