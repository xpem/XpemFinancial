# Copilot Commit Review Instructions

These instructions are meant to be used by GitHub Copilot Chat
to review staged git changes before committing.

# Commit Logic Review

You are reviewing a commit before it is pushed to the repository.

## Goal
Verify whether the implemented code changes match the proposed logic described in the commit message or task description.

## Context
- Limit the review strictly to the changes made and avoid reviewing untouched files or pre-existing issues.
- The code changes currently staged for commit should be analyzed.
- The commit message describes the intended behavior change.
- The repository follows clean architecture principles.
## Instructions

1. Read the commit message.
2. Analyze the modified files and their diffs.
3. Verify if the implemented logic matches the intention described in the commit message.
4. Identify inconsistencies between the proposed logic and the actual implementation.

## Checklist

Check the following points:

- [ ] The implementation matches the logic described in the commit message.
- [ ] No unrelated changes were included in the commit.
- [ ] Variable and method names reflect the intended behavior.
- [ ] Edge cases were considered.
- [ ] Error handling is consistent with the rest of the project.
- [ ] No temporary debug code was left in the commit.
- [ ] No commented-out code was left behind.
- [ ] The code follows the project architecture.

## Output Format

Provide a structured review with:

### 1. Summary
Short summary of whether the commit matches the proposed logic.

### 2. Potential Problems
List any inconsistencies found.

### 3. Suggested Improvements
Suggest improvements if necessary.

### 4. Final Verdict
- READY TO COMMIT
or
- REVIEW REQUIRED
