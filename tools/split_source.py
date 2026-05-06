from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
src_path = root / "Shield.cs"
text = src_path.read_text(encoding="utf-8-sig")

m = re.search(r"namespace\s+MROSDShield\s*\{", text)
if not m:
    raise SystemExit("namespace MROSDShield not found")

usings = text[:m.start()].strip() + "\n"
body = text[m.end():]
last = body.rfind("}")
if last < 0:
    raise SystemExit("namespace closing brace not found")
body = body[:last]

decl_re = re.compile(
    r"(?m)^    (?:(?:static|public|internal|sealed|partial|abstract)\s+)*class\s+([A-Za-z_][A-Za-z0-9_]*)"
    r"|^    (?:(?:static|public|internal|sealed|partial|abstract)\s+)*struct\s+([A-Za-z_][A-Za-z0-9_]*)"
)

matches = []
for mm in decl_re.finditer(body):
    name = mm.group(1) or mm.group(2)
    matches.append((name, mm.start()))

if not matches:
    raise SystemExit("no top-level types found")

chunks = {}
for i, (name, start) in enumerate(matches):
    end = matches[i + 1][1] if i + 1 < len(matches) else len(body)
    chunks[name] = body[start:end].strip("\n") + "\n"

groups = {
    "src/AppInfo.cs": ["AppInfo"],
    "src/Program.cs": ["Program"],
    "src/App.cs": ["App"],
    "src/Backend/Engine.cs": ["Engine", "StatusInfo"],
    "src/Backend/Preferences.cs": ["Pref"],
    "src/Backend/AutoStart.cs": ["AS"],
    "src/Infrastructure/Log.cs": ["Log"],
    "src/Infrastructure/Localization.cs": ["L"],
    "src/Infrastructure/ThemeColors.cs": ["Co"],
    "src/Frontend/Controls/ToggleSwitch.cs": ["ToggleSwitch"],
    "src/Frontend/Controls/GlowCard.cs": ["GlowCard"],
    "src/Frontend/MainForm.cs": ["MainForm"],
}

generated = set()
for rel, names in groups.items():
    missing = [n for n in names if n not in chunks]
    if missing:
        raise SystemExit(f"missing chunks for {rel}: {missing}")

    content = usings + "\nnamespace MROSDShield\n{\n"
    for n in names:
        content += chunks[n] + "\n"
        generated.add(n)
    content += "}\n"

    out = root / rel
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(content, encoding="utf-8")

extra = sorted(set(chunks) - generated)
if extra:
    raise SystemExit("unassigned chunks: " + ", ".join(extra))

legacy = """// MR OSD Shield source has been split into src/.
// This file is intentionally kept as a migration note for older releases.
// Build with compile.bat, which compiles all .cs files under src/.

"""
src_path.write_text(legacy, encoding="utf-8")

print("Split complete:")
for rel in groups:
    print(" - " + rel)