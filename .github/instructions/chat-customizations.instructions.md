---
description: "Use when creating or updating PTVS chat customization files, including SKILL.md files, .claude/skills, .github/skills, prompts, instructions, and custom agents."
applyTo: [".claude/skills/**/SKILL.md", ".github/skills/**/SKILL.md", ".agents/skills/**/SKILL.md"]
---
# PTVS Chat Customization Guidelines

- Put project skills under `.claude/skills/<skill-name>/SKILL.md` unless the user explicitly asks for another host location.
- Do not leave duplicate skill copies under `.github/skills` or `.agents/skills` after moving a skill to `.claude/skills`.
- Keep chat customization changes scoped to customization files; do not modify product, build, test, or package files while handling a customization-only request.
- Link to existing repo documentation instead of copying it into customization files.
- After creating or moving a skill, validate that the folder name matches the `name` frontmatter and that relative links still resolve from the new location.
