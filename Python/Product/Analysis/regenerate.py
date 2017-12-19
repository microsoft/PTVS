import ast
import subprocess
import sys

from pathlib import Path

SCRAPE_PY = Path(sys.argv[1])
OUTPUT = Path(sys.argv[2])
INTERPRETER = sys.argv[3:]
OUTPUT.mkdir(parents=True, exist_ok=True)

print(f"Scraping {' '.join(INTERPRETER)} to {OUTPUT}")

def scrape_one(module_name, out_name=None):
    args = INTERPRETER + [str(SCRAPE_PY), '-u8']
    if module_name:
        args.append(module_name)
    if not out_name:
        if not module_name:
            raise ValueError("out_name must be provided if no module name")
        out_name = f"{module_name}.pyi"

    proc = subprocess.Popen(args, stdout=subprocess.PIPE)
    with open(OUTPUT / out_name, 'wb') as f:
        b = proc.stdout.read(4096)
        while b:
            f.write(b)
            b = proc.stdout.read(4096)
    return OUTPUT / out_name

def _read_builtin_module_names(pyi_file):
    with pyi_file.open('rb') as f:
        tree = ast.parse(f.read())
    assigns = [n for n in tree.body if isinstance(n, ast.Assign)]
    bmn = next(n for n in assigns if n.targets[0].id == '__builtin_module_names__')
    return bmn.value.s.split(',')

# scrape builtins first
wrote_to = scrape_one(None, f"python.pyi")
print(f"Wrote builtins to {wrote_to}")

# scrape builtin modules
for mod in _read_builtin_module_names(wrote_to):
    fn = scrape_one(mod, f"python.{mod}.pyi")
    print(f"Wrote {mod} to {fn}")

# scrape other modules
INCLUDE_MODULES = [
    'functools', 'unittest', 'unittest.case',
]

for mod in INCLUDE_MODULES:
    fn = scrape_one(mod)
    print(f"Wrote {mod} to {fn}")
