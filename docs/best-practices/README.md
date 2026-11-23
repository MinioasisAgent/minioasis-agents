- **Access:** Level 2
- **Last Updated:** 2026-11-22

# Best practices

How code and tests are written in Minioasis. Every rule applies to every capability; what a particular capability does belongs in its brief under `docs/briefs/`. Every change to this folder is a Level 2 change.

## Index

| File | Prefix | Rules | Covers |
| --- | --- | --- | --- |
| [`architecture.md`](architecture.md) | `AR` | 13 | The three projects, what depends on what, folder layout, and naming |
| [`code-design.md`](code-design.md) | `CD` | 9 | How a type or method is shaped: responsibility, validation, outcomes, style |
| [`errors.md`](errors.md) | `ER` | 11 | Defining errors, the HTTP error contract, logging, and cancellation |
| [`external-systems.md`](external-systems.md) | `EX` | 13 | Contracts and generated clients, authentication to Koha, retries, deadlines |
| [`testing.md`](testing.md) | `TS` | 13 | What to test, where, with which test doubles, and when to ask the test Koha |

## Which rules apply to a change

| The change touches | Read and review against |
| --- | --- |
| Any C# file | `architecture.md`, `code-design.md` |
| An error definition, a result, a failure path, an endpoint's response, or `Minioasis.Api/Resources/` | also `errors.md` |
| `Minioasis.Koha/`, `contracts/`, or the client generator's configuration | also `external-systems.md` and `errors.md` |
| Any test project | also `testing.md` |

When unsure whether a file applies, include it. Read the files before you write. A review reports every rule in them that the change breaks (`.claude/agents/reviewer.md`). Cite a rule by its ID, for example "`CD-002`: the adapter validates caller input".

## Existing code

The rules apply to code that a change adds or modifies. Older code is brought into line only through work the maintainer approves, and a review does not fail a pull request for code it did not touch. When approved work moves or renames code, keep references, namespaces, tests, and test discovery consistent.

## Exceptions

A rule may be set aside only when all of these hold:

1. **Following it would break something more important,** such as correctness, security, a requirement of an approved brief, or another rule, and the reason is overwhelming.
2. **The exception is as small as it can be:** one rule, named by its ID, at one named place in the code. Every other rule still applies there.
3. **It is written down twice:** under "Deviations" in the pull request, and in a comment at that place in the code. Both give the rule ID and the reason.
4. **The maintainer accepts it.** The pull request is Level 2. The reviewer still reports the rule as broken and cannot grant an exception.

An exception does not change the rule and is no precedent. If the same exception is needed a second time, propose a change to the rule instead.

## Rule format

Every rule has these parts, in this order:

1. **An ID,** such as `CD-002`. An ID is never changed or reused, so a review can cite it for ever.
2. **A title written as an instruction.**
3. **Why,** in a few sentences.
4. **An example:** a bad and a good version where code can show the rule; otherwise the concrete table, layout, or procedure.
5. **Checked by,** only when a test, a tool, or the maintainer checks the rule. The line names what checks it. Every rule is also checked in the review, so a rule that only the review checks has no such line.

````markdown
## XX-000 Title written as an instruction

Why this matters, in a few sentences.

```csharp
// BAD: …

// GOOD: …
```

**Checked by:** tests of …, and review
````

**About the examples.** They use a made-up capability, renewing a loan, with names such as `RenewLoanUseCase` and `LoanId`. No example refers to existing code. An example shows one shape that satisfies the rule, not a design to copy; whoever writes the code chooses the real names, following `AR-011`.

## Adding, changing, and retiring a rule

1. **Add a rule only for a real reason:** a review found a problem that no rule covers, or the maintainer asked for it. Do not add a rule for a problem that has not happened.
2. **Check for a duplicate first.** If an existing rule nearly covers the case, sharpen that rule.
3. **Take the next free ID** in the file's series.
4. **Write every part.** A rule without an example is not finished.
5. **Retire a rule by marking it:** add `(retired)` to its title and one sentence saying why. Never delete or renumber a rule, because earlier reviews cite its ID.
6. **Update the rule count** in the index above.

A reviewer proposes a missing rule in the review report and does not add it.
