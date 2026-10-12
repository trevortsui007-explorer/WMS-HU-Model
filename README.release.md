# Release WMS-FG Git Repository

This repository tracks the WMS-FG module embedded in the Learun release host.

## Standalone development host

The existing WMS/Modules remain Learun BaseApi modules. A separate net48 / Nancy 1.4.5 solution (shared map sources remain net461-compatible) now compiles WMS/Host.Modules and the shared business sources. See [README.standalone.md](README.standalone.md) and open WMS.Standalone.sln. Existing Learun modules remain unchanged; adding the new map modules requires explicit Compile entries and trusted identity/permission integration. The build/history notes below describe the imported release baseline.

## Baseline and History

- Local baseline: eleven scoped commits containing repository documentation
  and the 247 existing release source files, followed by a history merge.
- Remote: `https://github.com/trevortsui007-explorer/WMS-HU-Model.git`.
- Imported upstream main: `57b15fb460018ba99def38c10a7de62b1f1b7a30`.
- Local branch: `main`, tracking `origin/main`.
- Integration date: 2026-10-06.

The integration preserves the semantics of
`git merge --allow-unrelated-histories -s ours`: the merge has the release
baseline as its first parent and upstream main as its second parent.
It joins both histories while retaining the release file tree in full.
Upstream-only modules, documentation, tests, and standalone project files were
not introduced. They remain accessible through `origin/main` and Git history.
This was a history integration, not a port of upstream-only functionality.

Local commit subjects use `feat(english-scope): Chinese description`.
The baseline scopes are repository, infrastructure, master-data, hu, location,
weekcode, inbound, inventory, repack, outbound, and audit. The final merge uses
integration. These commits record existing code rather than newly developed
features; intermediate baseline commits are not standalone build targets.

## Build Context

These sources are compiled by the surrounding
`Learun.Application.WebApi/Learun.Application.WebApi.csproj` project. This
directory is not a standalone solution. The release host targets .NET Framework
4.6.1 and references Nancy 1.4.5, unlike the standalone upstream project.
Host configuration, project inclusion lists, shared BaseApi code, and the WMS
Web frontend remain outside this repository. Changes to those files must still
be managed in the surrounding release workspace.

The enclosing release workspace uses SVN. Keep this nested `.git` directory
out of SVN commits. No enclosing SVN properties were changed during setup.

## Future Integration

Review upstream changes before merging them into this host-specific module.
The initial `ours` merge records the selected upstream revision as integrated;
future merges will not automatically reintroduce its intentionally omitted files.
Port such functionality explicitly when required.

No remote push was performed during setup. Pushing local `main` to upstream
`main` would make the release module tree the remote branch's current content,
including removing standalone upstream files from that branch's current tree.
Review that scope before publishing; use a separate release branch when the
standalone upstream main must remain available unchanged.
