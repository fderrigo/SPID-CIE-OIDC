#!/usr/bin/env python3
"""Generate SpidCieOidc.sln so that EVERY folder/subfolder and file of the repo is
visible in Solution Explorer, mirroring the filesystem.

- The real project (src/SpidCieOidc.Web) stays a buildable project node, nested under a
  "src" solution folder.
- Every other directory becomes a (nested) solution folder; every file becomes a
  SolutionItem under its folder.
- bin/obj/.git/.vs and generated/secret-runtime files are skipped.

Re-run after adding files:  python scripts/gen-solution.py
"""
import os
import uuid

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SLN = os.path.join(ROOT, "SpidCieOidc.sln")
PROJECT_REL = os.path.join("src", "SpidCieOidc.Web", "SpidCieOidc.Web.csproj")
PROJECT_DIR_REL = os.path.join("src", "SpidCieOidc.Web")

FOLDER_TYPE = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}"
CSPROJ_TYPE = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"
NS = uuid.UUID("6ba7b811-9dad-11d1-80b4-00c04fd430c8")

EXCLUDE_DIRS = {".git", ".vs", "bin", "obj", "__pycache__"}

def guid(path: str) -> str:
    return "{" + str(uuid.uuid5(NS, path.replace("\\", "/"))).upper() + "}"

def rel(p: str) -> str:
    return os.path.relpath(p, ROOT).replace("/", "\\")

# Stable GUID for the project (matches previous .sln so VS keeps user settings).
PROJECT_GUID = "{18A10E34-B466-4EB9-AD1E-3AA4C5E64D5A}"

projects = []        # (type, name, path, guid)
nested = []          # (child_guid, parent_guid)
solution_items = {}  # folder_guid -> list[rel paths]

# "src" solution folder holds the real project.
src_guid = guid("dir:src")
projects.append((FOLDER_TYPE, "src", "src", src_guid))
projects.append((CSPROJ_TYPE, "SpidCieOidc.Web", PROJECT_REL, PROJECT_GUID))
nested.append((PROJECT_GUID, src_guid))

# Root-level files -> "Solution Items"
sol_items_guid = guid("dir:__solution_items__")
projects.append((FOLDER_TYPE, "Solution Items", "Solution Items", sol_items_guid))
solution_items[sol_items_guid] = []
for name in sorted(os.listdir(ROOT)):
    full = os.path.join(ROOT, name)
    if os.path.isfile(full) and name != "SpidCieOidc.sln":
        solution_items[sol_items_guid].append(name)

# Every top-level directory (except src, handled above) is mirrored recursively.
for top in sorted(os.listdir(ROOT)):
    full = os.path.join(ROOT, top)
    if not os.path.isdir(full) or top in EXCLUDE_DIRS or top == "src":
        continue
    for dirpath, dirnames, filenames in os.walk(full):
        dirnames[:] = sorted(d for d in dirnames if d not in EXCLUDE_DIRS)
        r = rel(dirpath)
        g = guid("dir:" + r)
        name = os.path.basename(dirpath)
        projects.append((FOLDER_TYPE, name, name, g))
        # nest under parent folder (or root)
        parent = os.path.dirname(dirpath)
        if os.path.abspath(parent) != os.path.abspath(ROOT):
            nested.append((g, guid("dir:" + rel(parent))))
        solution_items[g] = [os.path.join(r, f) for f in sorted(filenames)]

# --- emit ---
lines = []
lines.append("Microsoft Visual Studio Solution File, Format Version 12.00")
lines.append("# Visual Studio Version 17")
lines.append("VisualStudioVersion = 17.0.31903.59")
lines.append("MinimumVisualStudioVersion = 10.0.40219.1")
for ptype, name, path, g in projects:
    lines.append(f'Project("{ptype}") = "{name}", "{path}", "{g}"')
    if g in solution_items and solution_items[g]:
        lines.append("\tProjectSection(SolutionItems) = preProject")
        for item in solution_items[g]:
            lines.append(f"\t\t{item} = {item}")
        lines.append("\tEndProjectSection")
    lines.append("EndProject")
lines.append("Global")
lines.append("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution")
lines.append("\t\tDebug|Any CPU = Debug|Any CPU")
lines.append("\t\tRelease|Any CPU = Release|Any CPU")
lines.append("\tEndGlobalSection")
lines.append("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution")
for cfg in ("Debug", "Release"):
    lines.append(f"\t\t{PROJECT_GUID}.{cfg}|Any CPU.ActiveCfg = {cfg}|Any CPU")
    lines.append(f"\t\t{PROJECT_GUID}.{cfg}|Any CPU.Build.0 = {cfg}|Any CPU")
lines.append("\tEndGlobalSection")
lines.append("\tGlobalSection(SolutionProperties) = preSolution")
lines.append("\t\tHideSolutionNode = FALSE")
lines.append("\tEndGlobalSection")
if nested:
    lines.append("\tGlobalSection(NestedProjects) = preSolution")
    for child, parent in nested:
        lines.append(f"\t\t{child} = {parent}")
    lines.append("\tEndGlobalSection")
lines.append("EndGlobal")

with open(SLN, "w", encoding="utf-8-sig", newline="\r\n") as f:
    f.write("\n".join(lines) + "\n")

print(f"Wrote {SLN}: {sum(1 for p in projects if p[0]==FOLDER_TYPE)} folders, "
      f"{sum(len(v) for v in solution_items.values())} files.")
