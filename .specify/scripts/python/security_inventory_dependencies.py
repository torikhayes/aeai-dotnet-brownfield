#!/usr/bin/env python3
"""Enumerate DependencyComponent records (FR-003): NuGet packages resolved via central
package management, plus Aspire-managed container/infra components.

See specs/008-adversarial-security-review/data-model.md for the DependencyComponent
schema and specs/008-adversarial-security-review/research.md §2 for the sourcing
rationale (Directory.Packages.props + *.csproj for NuGet; eShop.AppHost/Program.cs for
infra).
"""
import argparse
import glob
import json
import os
import re
import xml.etree.ElementTree as ET

# Default Aspire container images when a resource is added without an explicit
# .WithImage()/.WithImageTag() override — these are the images the corresponding
# Aspire.Hosting.* NuGet package pulls in. Kept as a small, explicit table rather than
# inspecting the NuGet package internals, since that's what actually ships at runtime
# for the common case (no override).
DEFAULT_ASPIRE_IMAGES = {
    "AddRedis": "docker.io/library/redis",
    "AddRabbitMQ": "docker.io/library/rabbitmq",
    "AddPostgres": "docker.io/library/postgres",
}


_MSBUILD_PROPERTY_REF = re.compile(r"\$\(([A-Za-z_][A-Za-z0-9_]*)\)")


def _resolve_msbuild_property(value, properties, _depth=0):
    """Resolve $(PropertyName) references, including chains (Prop A references Prop B)."""
    if _depth > 10:
        return value  # cycle or unexpectedly deep chain; bail out rather than loop forever
    match = _MSBUILD_PROPERTY_REF.fullmatch(value) if value else None
    if not match:
        return value
    prop_name = match.group(1)
    if prop_name not in properties:
        return value  # unresolved (defined elsewhere, e.g. Directory.Build.props) — leave as-is
    return _resolve_msbuild_property(properties[prop_name], properties, _depth + 1)


def _parse_central_package_versions(props_path):
    tree = ET.parse(props_path)
    ns = ""
    m = re.match(r"\{(.*)\}", tree.getroot().tag)
    if m:
        ns = "{" + m.group(1) + "}"

    properties = {}
    for pg in tree.getroot().iter(f"{ns}PropertyGroup"):
        for prop in pg:
            tag = prop.tag[len(ns):] if prop.tag.startswith(ns) else prop.tag
            if prop.text and prop.text.strip():
                properties[tag] = prop.text.strip()

    versions = {}
    for pv in tree.getroot().iter(f"{ns}PackageVersion"):
        name = pv.get("Include")
        version = pv.get("Version")
        if name and version:
            versions[name] = _resolve_msbuild_property(version, properties)
    return versions


def _project_name(csproj_path):
    return os.path.splitext(os.path.basename(csproj_path))[0]


def _parse_csproj_references(csproj_path):
    tree = ET.parse(csproj_path)
    ns = ""
    m = re.match(r"\{(.*)\}", tree.getroot().tag)
    if m:
        ns = "{" + m.group(1) + "}"
    refs = []
    for pr in tree.getroot().iter(f"{ns}PackageReference"):
        name = pr.get("Include")
        if not name:
            continue
        version = pr.get("Version")  # explicit per-project override, if any
        refs.append((name, version))
    return refs


def inventory_nuget(repo_root):
    central_versions = _parse_central_package_versions(os.path.join(repo_root, "Directory.Packages.props"))

    components = {}  # (name, version) -> {usedBy: set, shipsInRuntime: bool}
    for pattern in ("src/**/*.csproj", "tests/**/*.csproj"):
        for csproj in glob.glob(os.path.join(repo_root, pattern), recursive=True):
            project = _project_name(csproj)
            is_shipped = pattern.startswith("src/")
            for name, explicit_version in _parse_csproj_references(csproj):
                version = explicit_version or central_versions.get(name)
                if version is None:
                    # No central or explicit version found (e.g. an SDK-implied
                    # reference) — still inventory it, but mark version unknown
                    # rather than guessing.
                    version = "unknown"
                key = (name, version)
                entry = components.setdefault(key, {"usedBy": set(), "shipsInRuntime": False})
                entry["usedBy"].add(project)
                entry["shipsInRuntime"] = entry["shipsInRuntime"] or is_shipped

    return [
        {
            "name": name,
            "ecosystem": "nuget",
            "version": version,
            "usedBy": sorted(entry["usedBy"]),
            "shipsInRuntime": entry["shipsInRuntime"],
        }
        for (name, version), entry in sorted(components.items())
    ]


def inventory_aspire_infra(repo_root, apphost_program_cs="src/eShop.AppHost/Program.cs"):
    path = os.path.join(repo_root, apphost_program_cs)
    if not os.path.isfile(path):
        return []

    with open(path, "r", encoding="utf-8") as f:
        source = f.read()

    components = []
    call_pattern = re.compile(r'builder\.(AddRedis|AddRabbitMQ|AddPostgres)\("([^"]+)"\)(.*?);', re.DOTALL)
    for match in call_pattern.finditer(source):
        call, resource_name, tail = match.group(1), match.group(2), match.group(3)
        image_match = re.search(r'\.WithImage\("([^"]+)"\)', tail)
        tag_match = re.search(r'\.WithImageTag\("([^"]+)"\)', tail)
        image = image_match.group(1) if image_match else DEFAULT_ASPIRE_IMAGES[call]
        tag = tag_match.group(1) if tag_match else "aspire-managed-default"
        components.append(
            {
                "name": f"{image} ({resource_name})",
                "ecosystem": "container",
                "version": tag,
                "usedBy": ["eShop.AppHost"],
                "shipsInRuntime": True,
            }
        )
    return components


def main():
    parser = argparse.ArgumentParser(prog="security-inventory-dependencies")
    parser.add_argument("--repo-root", required=True)
    parser.add_argument("--out", required=True, help="path to write dependency-components.json")
    parser.add_argument(
        "--apphost-program-cs",
        default="src/eShop.AppHost/Program.cs",
        help="repo-root-relative path to the Aspire AppHost's Program.cs (overridable for testing)",
    )
    args = parser.parse_args()

    components = inventory_nuget(args.repo_root) + inventory_aspire_infra(args.repo_root, args.apphost_program_cs)
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(components, f, indent=2)
        f.write("\n")
    print(json.dumps({"count": len(components), "path": args.out}))


if __name__ == "__main__":
    main()
