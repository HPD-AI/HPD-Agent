# Bundled Workspace Search

HPD Environment owns the optional ripgrep executable used to accelerate
workspace search. The coding harness does not resolve or launch `rg` directly.

## Execution Flow

```text
CodingToolHarness.Grep
  -> RuntimeWorkspaceBinding.Files.SearchTextAsync
  -> selected Environment workspace store
  -> Environment-provisioned ripgrep
  -> typed workspace search results
```

The selected backend owns the process boundary:

- Host Process executes through `HPD-Environment.HostProcess`.
- Host Containers executes inside the retained sandbox container.
- Apple Virtualization executes through the guest process provider.

The provisioned binary is invoked by absolute path. Workspace and host `PATH`
entries are not trusted when a bundle is available.

## Bundled Targets

`HPD-Environment.Search.Ripgrep` embeds verified bundles for:

| Target | Bundle |
| --- | --- |
| macOS arm64 | Native ripgrep release |
| macOS x64 | Native ripgrep release |
| Linux x64 | Statically linked musl ripgrep release |
| Linux arm64 musl | Alpine ripgrep with pinned musl, libgcc, and PCRE2 runtime files |

The package manifest records the upstream version and SHA-256 digest of every
generated bundle.

## Provisioning

The first search materializes the target bundle into a random owner-only
temporary directory inside the execution unit. The same `IProcessProvider`
that will run workspace commands provisions and executes the tool.

For containers and virtual machines, target selection is based on the
execution environment rather than the caller's host. This matters when a macOS
host runs a Linux container or VM.

## Managed Fallback

Environment falls back to its managed search implementation when:

- the target platform has no bundled executable;
- the execution environment cannot run POSIX shell tools;
- provisioning fails; or
- the provisioned executable cannot be verified or started.

The fallback preserves Grep results and bounds, but it does not provide
ripgrep acceleration.

## Build And Packaging

The binaries are generated during package preparation rather than downloaded
at runtime:

```bash
dotnet run --file \
  dotnet/HPD-Environment.Framework/src/HPD-Environment.Search.Ripgrep/pack/prepare-ripgrep-binaries.cs \
  -- \
  --output-root \
  dotnet/HPD-Environment.Framework/src/HPD-Environment.Search.Ripgrep/obj/RipgrepPack
```

Package publishing runs this step before packing Environment packages. A
private build that needs guaranteed acceleration must run it before packing as
well.

The Agent-owned ripgrep wrapper and bundled-binary manifest from earlier
releases have been removed. Search ownership now belongs to Environment.
