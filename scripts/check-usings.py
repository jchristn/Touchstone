#!/usr/bin/env python3
"""Check (or with --fix, sort) using directives in every tracked C# file under src/ and test/.

Rule (CLAUDE.md, CODE_STYLE.md): using directives sit inside the namespace block. System directives come first as
one group, followed by a blank line and every other directive as a second group, each group sorted ordinally.
"""
import re
import subprocess
import sys

USING = re.compile(r"^(\s+)using (static )?([A-Za-z0-9_.]+)( = [^;]+)?;\s*$")


def groups(lines):
    start = next((i for i, l in enumerate(lines) if USING.match(l)), None)
    if start is None:
        return None
    end = start
    while end < len(lines) and (USING.match(lines[end]) or lines[end].strip() == ""):
        end += 1
    while lines[end - 1].strip() == "":
        end -= 1
    return start, end


def normalize(block):
    entries = [l for l in block if l.strip()]
    indent = USING.match(entries[0]).group(1)
    names = sorted(set(USING.match(l).group(3) + (USING.match(l).group(4) or "") for l in entries), key=lambda n: n)
    system = [n for n in names if n == "System" or n.startswith("System.")]
    other = [n for n in names if n not in system]
    result = [indent + "using " + n + ";" for n in system]
    if system and other:
        result.append("")
    result += [indent + "using " + n + ";" for n in other]
    return result


def main():
    fix = "--fix" in sys.argv
    files = subprocess.run(["git", "ls-files", "-co", "--exclude-standard", "src/*.cs", "test/*.cs"], capture_output=True, text=True).stdout.split()
    bad = []
    for path in files:
        with open(path, encoding="utf-8") as handle:
            text = handle.read()
        lines = text.split("\n")
        found = groups(lines)
        if found is None:
            continue
        start, end = found
        block = lines[start:end]
        if any(USING.match(l) and USING.match(l).group(2) for l in block):
            continue
        expected = normalize(block)
        if block != expected:
            bad.append(path)
            if fix:
                lines[start:end] = expected
                with open(path, "w", encoding="utf-8") as handle:
                    handle.write("\n".join(lines))
    for path in bad:
        print(("FIXED " if fix else "UNSORTED ") + path)
    print("Files checked: " + str(len(files)) + ", unsorted: " + str(len(bad)))
    return 0 if fix or not bad else 1


if __name__ == "__main__":
    sys.exit(main())
