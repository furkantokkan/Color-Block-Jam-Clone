# README hunks for DEC-162 (level content updates)

At 10:59 on 2026-09-28, before the coordinator's README freeze, this task
appended the two hunks below to `README.md`. A later rewrite of `README.md`
should keep both, reworded if needed.

## 1. "Authored data" bullet, after the Addressable database verification bullet

```markdown
- Level JSON fixes can ship after release as an Addressables content update
  (DEC-162). The `Level Database` group stays local and Cannot Change Post
  Release, so every build carries its levels. The game checks the remote
  catalog once at startup; a failed or offline check logs one warning and plays
  the cached or shipped levels. `Color Block Jam/Data/Build Level Content
  Update` validates the JSON and builds the update. It refuses while the
  `Release` profile's URL is still the placeholder. Workflow, recovery and
  pending device checks: [Level Content Updates](docs/Level-Content-Updates.md).
```

## 2. Runtime operations table row

This row belongs in the table of runtime operations, with their owners and
stop points (Performance Rule 6). Add it after the "Environment direct-Play
preview" row.

```markdown
| Level content update check (Addressables catalog check, catalog update and JSON bundle download, once, before the level JSON loads) | ProjectContext build callback registered by `ProjectScopeInstaller` | Returns before Init routes to Hub; the build token cancels it; requests time out after 10 s; nothing polls afterwards (DEC-162) |
```
