# Load SKILL.md Directories

Use `DirectorySkillSource` for skills maintained as files:

```text
skills/
└── data-analysis/
    ├── SKILL.md
    ├── skill.json
    ├── references/
    │   └── validation-guide.md
    └── scripts/
        └── normalize.py
```

Register the source on the owning harness:

```csharp
var agent = await new AgentBuilder()
    .WithToolHarness<DataTools>(options =>
        options.AddSkillSource(
            new DirectorySkillSource(
                "./skills",
                SkillDirectoryImportMode.Strict)))
    .BuildAsync();
```

The root may be one skill directory or a parent containing several skill directories.

## SKILL.md

```md
---
id: data-analysis
name: data_analysis
description: Helps validate and analyze datasets.
version: 1
---

Read the validation guide first.
Normalize the active dataset.
Validate it before analysis.
```

The frontmatter supplies discovery metadata. The Markdown body becomes the activation instructions.

## Describe Packaged Capabilities

In strict mode, describe resources and scripts in `skill.json`:

```json
{
  "resources": {
    "references/validation-guide.md":
      "Reads the validation requirements used before analysis."
  },
  "scripts": {
    "scripts/normalize.py": {
      "description": "Normalizes the active dataset and returns the resulting artifact.",
      "runtime": "python",
      "requiresPermission": true,
      "parameters": {
        "type": "object",
        "properties": {
          "inputFile": { "type": "string" },
          "maximumRows": {
            "type": "integer",
            "minimum": 1,
            "maximum": 100000,
            "default": 1000
          }
        },
        "required": ["inputFile"],
        "additionalProperties": false
      }
    }
  }
}
```

Files under `references/` become parameterless read functions. Every file under `scripts/` requires an explicit `parameters` schema and a compatible `ISkillScriptRunner`. A script with no arguments still declares:

```json
{
  "type": "object",
  "properties": {},
  "required": [],
  "additionalProperties": false
}
```

Missing or invalid `parameters` metadata invalidates the directory snapshot; HPD does not silently substitute an empty contract.

## Generated Contract Sidecars

When a directory script uses the same contract as compiled code, export the
generated contract rather than maintaining duplicate JSON:

```csharp
await AIInputContract.ExportSchemaAsync(
    NormalizeInput.AIContract,
    "./skills/data-analysis/contracts/NormalizeInput.schema.json");
```

Run this from an explicit build/export project or build target that references
the contract type. Export performs no assembly scanning, reflection, generated
registry lookup, or runtime type discovery.

Reference the exported sidecar from `skill.json`:

```json
{
  "scripts": {
    "scripts/normalize.py": {
      "description": "Normalizes one dataset.",
      "runtime": "python",
      "parameters": {
        "$hpdContract": "contracts/NormalizeInput.schema.json"
      }
    }
  }
}
```

`$hpdContract` is HPD packaging syntax, not general JSON Schema `$ref`.
References must be local paths beneath the individual skill root. Absolute
paths, remote URIs, traversal, and symbolic-link escapes are rejected. HPD
loads the complete sidecar through the same canonical-schema compiler and
publishes the resolved schema, not the reference object.

`Strict` mode is recommended. `Compatibility` mode derives missing descriptions from filenames, which is convenient but usually produces weaker model selection.

Directory changes are watched automatically. HPD reloads complete valid definitions and retains the last valid catalog when an editor leaves a partial or invalid write. Symbolic links, absolute paths, and paths escaping the configured root are rejected. Importing a skill never executes its scripts.
