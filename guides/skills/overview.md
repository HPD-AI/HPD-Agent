# Skills Overview

A skill gives an agent task-specific instructions and capabilities only when they are needed. Use a skill when a task has a workflow, rules, or supporting material that should not occupy every model request.

Before activation, the model sees only the skill name and its short discovery description. When the model invokes the skill, HPD returns the detailed instructions. On the next model iteration, HPD exposes that skill's functions, resources, and compatible scripts.

```text
Before activation
    data_analysis()

After activation
    validate_dataset(...)
    read_validation_guide()
    normalize_dataset()
```

The activation tool is hidden after activation. Activation currently lasts for the remainder of the message turn.

## When To Use A Skill

Use an ordinary `[AIFunction]` for one directly selectable operation. Use a skill when the model needs instructions plus a focused group of capabilities to complete a workflow.

A skill always belongs to a tool harness. If that harness uses `[Collapse]`, the model must first open the harness and then activate the skill. Skills do not bypass their owning harness.

HPD supports three authoring paths:

- [Create the skill in C#](./code-first.md).
- [Load it from a `SKILL.md` directory](./directories.md).
- [Install a managed package](./managed-skills.md).

All three produce the same model-facing activation behavior.

Complete samples are available in the repository cookbook:

- [Code-first skill](https://github.com/HPD-AI/HPD-Agent/blob/main/cookbook/Skills/01-code-first-skill.cs)
- [Directory skill](https://github.com/HPD-AI/HPD-Agent/blob/main/cookbook/Skills/02-directory-skill.cs)
- [Resources and script runner](https://github.com/HPD-AI/HPD-Agent/blob/main/cookbook/Skills/03-resources-and-script-runner.cs)
- [Managed skill store](https://github.com/HPD-AI/HPD-Agent/blob/main/cookbook/Skills/03-managed-skill-store.cs)
- [Update and reload](https://github.com/HPD-AI/HPD-Agent/blob/main/cookbook/Skills/04-update-and-reload.cs)

## What Goes Where

- Put the short selection hint in `description`.
- Put the complete workflow in `instructions`.
- Keep C# operations as `[AIFunction]` methods.
- Use resources for read-only supporting content.
- Use scripts only for external executable content.

Child capability names and detailed instructions are not appended to the pre-activation description.
