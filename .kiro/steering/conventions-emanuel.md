\# Change Guidelines

## Tradeoff Analysis Before Suggesting or Applying Changes

Before suggesting or applying any change — whether to code, configuration, documentation, or workspace files (including steering files) — evaluate the tradeoff between the current state and the proposed one. Do not apply a change just because it seems cleaner or more general — context and intent matter.

### How to frame suggestions

When a tradeoff exists, always:

1. Explain what the current code does and why it may be intentional.
2. Describe the risk or downside of the current approach, if any.
3. Describe the risk or downside of the suggested change.
4. Only recommend the change if the benefit clearly outweighs the cost.

If the tradeoff is not clearly favorable, flag it as an observation rather than a problem.

## Steering File Authoring

When creating or updating steering files, keep the content as general as possible:

- Describe patterns, principles, and intent — not specific names, paths, or implementations.
- Avoid referencing specific file names, folder structures, class names, or module names that are likely to change.
- Prefer guidelines that remain valid regardless of how the project evolves.
- If specific context is needed, use `fileMatch` inclusion so it only loads when relevant.

## Steering File Creation

Avoid creating new steering files with `inclusion: auto` unless strictly necessary. Every auto-included file adds cost and context to every interaction. Prefer:

- Adding content to an existing auto-included file when the guideline is general enough.
- Using `fileMatch` inclusion for context that is only relevant to specific file types.
- Using `manual` inclusion for context that is only needed on demand.

## Placement Decisions — Ask Before Acting

When the user asks to record something (a convention, a rule, a note) and suggests a specific location, do not simply comply. First evaluate whether that location is appropriate:

- Will the content actually be loaded in the contexts where it matters?
- Does the `fileMatchPattern` of the target file cover the scenarios where this content is relevant?
- Is the scope of the target file aligned with the scope of the content being added?

If there is a mismatch — for example, adding test conventions to a steering file that only loads when a production file is open — flag it explicitly before writing anything. Propose the correct location and explain why.
