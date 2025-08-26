# Contributing Guide

Thank you for your interest in contributing! This document explains how to report issues, propose changes, and submit pull requests.

If anything is unclear, please open an issue to ask questions—we’re happy to help.

## Quick start (TL;DR)

- Fork the repository and create your feature branch:
  - git checkout -b feat/your-descriptive-branch-name
- Set up the project locally (see Setup).
- Run tests and linters to ensure everything passes.
- Make your changes with small, focused commits following Conventional Commits.
- Push your branch and open a pull request (PR), filling out the PR template and linking any relevant issues.

---

## Code of Conduct

This project follows a Code of Conduct to foster an open and welcoming community.

- Please read CODE_OF_CONDUCT.md before contributing.
- By participating, you agree to uphold its terms.

If CODE_OF_CONDUCT.md isn’t present yet, we recommend adopting the Contributor Covenant v2.1.

---

## How to contribute

- Report bugs: Open an issue with steps to reproduce and expected vs. actual behavior.
- Suggest features: Open an issue describing the problem, the proposed solution, and alternatives you’ve considered.
- Improve docs: Fix typos, clarify instructions, or add examples.
- Submit code: Pick an existing issue or open one to discuss your idea first.

Look for issues labeled “good first issue” or “help wanted” to get started quickly.

---

## Development workflow

### Prerequisites

- Git
- Recommended: Node.js LTS, Python 3.x, or language/tooling used by this repo
- Package managers/tools used by the project (e.g., npm/pnpm/yarn, pip/poetry, Go, Java/Gradle, etc.)

Replace the commands below with the appropriate ones for this project if it differs.

### Setup

1. Fork the repo and clone your fork:
   - git clone https://github.com/your-username/repo.git
   - cd repo
2. Add the upstream remote:
   - git remote add upstream https://github.com/ORIGINAL_OWNER/REPO.git
   - git fetch upstream
3. Create a branch:
   - git checkout -b feat/short-description
4. Install dependencies:
   - JavaScript/TypeScript: npm ci or pnpm i or yarn install
   - Python: python -m venv .venv && source .venv/bin/activate && pip install -r requirements.txt
   - Other: Follow project-specific instructions

### Run locally

- Application:
  - JS/TS example: npm run dev
  - Python example: python app.py (or flask run / uvicorn ...)
- Tests:
  - npm test or pytest
- Lint/format:
  - npm run lint && npm run format
  - or flake8/ruff and black/isort
- Type checks (if applicable):
  - tsc --noEmit or mypy

If the project uses a different stack, see the README or project docs for exact commands.

---

## Style and standards

### Commit messages (Conventional Commits)

Use Conventional Commits to make history and changelogs easier:

- Types: feat, fix, docs, style, refactor, perf, test, build, ci, chore, revert
- Format: type(scope)?: short summary
- Examples:
  - feat(api): add pagination to list endpoint
  - fix(ui): prevent modal flicker on open
  - docs(readme): clarify local setup steps

If the change impacts users, include a footer with BREAKING CHANGE: and a brief description.

### Branch naming

- Features: feat/short-description
- Fixes: fix/short-description
- Docs: docs/short-description
- Chores: chore/short-description

### Code quality

- Follow existing patterns and file structures.
- Add or update tests for new/changed behavior.
- Keep functions small and focused; prefer clarity over cleverness.
- Keep PRs small and cohesive.

### Linting and formatting

- Use the configured linters/formatters. Common examples:
  - ESLint + Prettier (JS/TS)
  - Ruff/Flake8 + Black + isort (Python)
- Run the provided scripts before committing:
  - npm run lint && npm run format
  - or pre-commit run --all-files

---

## Tests

- Write unit tests for new logic and update tests when changing behavior.
- Aim to cover edge cases and error conditions.
- Ensure tests are deterministic and fast.
- Run tests locally before opening a PR:
  - npm test or pytest -q

If the project enforces coverage thresholds, please ensure your changes keep coverage above the threshold.

---

## Opening issues

Before opening a new issue:
- Search existing issues to avoid duplicates.
- For bug reports, include:
  - Version(s) affected
  - OS/runtime environment
  - Steps to reproduce (minimal reproducible example if possible)
  - Expected vs. actual behavior
  - Logs, stack traces, or screenshots
- For feature requests, include:
  - Problem statement and motivation
  - Proposed solution and alternatives
  - Any breaking changes or migration thoughts

Use the issue templates if available.

---

## Pull requests

1. Keep PRs focused and as small as practical.
2. Link related issues using keywords like “Fixes #123” or “Closes #456”.
3. Update docs and examples when user-facing behavior changes.
4. Include tests for new behavior and ensure all checks pass.
5. Fill out the PR template checklist.

### PR checklist

- [ ] Linked related issue(s)
- [ ] Added/updated tests
- [ ] Updated documentation (README/docs/CHANGELOG if applicable)
- [ ] Ran linters/formatters
- [ ] Verified local build/tests pass
- [ ] Considered performance and security implications
- [ ] No unnecessary files/changes included

### Reviews and merging

- At least one approval from a maintainer (or per repo rules).
- All required CI checks must pass.
- Resolve review comments and keep a friendly tone.
- Squash or rebase merges may be used (maintainers decide). Keep a clean commit history.

---

## Releases and versioning

- We follow Semantic Versioning (SemVer): MAJOR.MINOR.PATCH.
- If using Conventional Commits, release notes can be generated automatically.
- Maintainers handle releasing; contributors don’t need to bump versions in PRs unless requested.

---

## Security

Please do not report security vulnerabilities via public issues.

- Refer to SECURITY.md for our disclosure policy and contact details.
- If SECURITY.md is not present, email security@example.com (replace with the correct address).

---

## License and contributor terms

By contributing, you agree that your contributions will be licensed under the repository’s LICENSE.

- If this project uses a CLA or DCO, please follow the documented process.

---

## Communication

- Questions: open a “question” issue or use Discussions if enabled.
- Real-time chat (if applicable): link to Slack/Discord/etc.

---

## Tips for success

- Start with an issue and align on scope before coding.
- Keep changes small and submit early for feedback.
- Write clear commit messages and PR descriptions.
- Be respectful and collaborative—reviews are about the code, not the person.

---

## Maintainers’ guide (optional)

- Triaging: label issues, prioritize, and assign.
- Automation: keep CI green, enforce status checks.
- Docs: keep README and CONTRIBUTING up to date.
- Releases: document release steps and changelog updates.

---

Thank you for contributing!
