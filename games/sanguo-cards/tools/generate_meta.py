#!/usr/bin/env python3
"""Creates missing Unity .meta files under Assets/ with deterministic GUIDs.

Unity would generate random GUIDs on first import; committing stable ones up front keeps asset
references identical on every machine. Existing .meta files are never modified.

Usage: python3 tools/generate_meta.py   (run from games/sanguo-cards)
"""
import hashlib
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "Assets")

FOLDER = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
MONO = """fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
ASMDEF = """fileFormatVersion: 2
guid: {guid}
AssemblyDefinitionImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
ASMREF = """fileFormatVersion: 2
guid: {guid}
AssemblyDefinitionReferenceImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
TEXT = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
DEFAULT = """fileFormatVersion: 2
guid: {guid}
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

ANDROID_LIB = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
PluginImporter:
  externalObjects: {{}}
  serializedVersion: 2
  iconMap: {{}}
  executionOrder: {{}}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Android: Android
    second:
      enabled: 1
      settings: {{}}
  - first:
      Any: 
    second:
      enabled: 0
      settings: {{}}
  - first:
      Editor: Editor
    second:
      enabled: 0
      settings:
        DefaultValueInitialized: true
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

TEMPLATES = {".cs": MONO, ".asmdef": ASMDEF, ".asmref": ASMREF, ".json": TEXT, ".txt": TEXT, ".md": TEXT, ".xml": TEXT}


def guid_for(rel_path):
    return hashlib.md5(("sanguo-cards/" + rel_path.replace(os.sep, "/")).encode("utf-8")).hexdigest()


def write_meta(path, template):
    meta = path + ".meta"
    if os.path.exists(meta):
        return 0
    rel = os.path.relpath(path, ROOT)
    with open(meta, "w", encoding="utf-8", newline="\n") as f:
        f.write(template.format(guid=guid_for(rel)))
    return 1


def main():
    created = 0
    for dirpath, dirnames, filenames in os.walk(ASSETS):
        dirnames[:] = [d for d in dirnames if not d.startswith(".")]
        for d in dirnames:
            template = ANDROID_LIB if d.endswith(".androidlib") else FOLDER
            created += write_meta(os.path.join(dirpath, d), template)
        for name in filenames:
            if name.endswith(".meta") or name.startswith("."):
                continue
            ext = os.path.splitext(name)[1].lower()
            created += write_meta(os.path.join(dirpath, name), TEMPLATES.get(ext, DEFAULT))
    # Stale metas (asset deleted) confuse Unity; report them.
    stale = []
    for dirpath, _, filenames in os.walk(ASSETS):
        for name in filenames:
            if name.endswith(".meta") and not os.path.exists(os.path.join(dirpath, name[:-5])):
                stale.append(os.path.join(dirpath, name))
    print("created %d meta files" % created)
    for s in stale:
        print("stale meta: " + os.path.relpath(s, ROOT))
    return 1 if stale else 0


if __name__ == "__main__":
    sys.exit(main())
