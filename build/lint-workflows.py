"""Parse every workflow and syntax-check the shell inside it.

 Run from CI (the `workflow syntax` job) or locally:
     python3 build/lint-workflows.py

A workflow can be valid YAML and still fail on its first step because of a shell typo. bash -n costs
about nothing and catches that class before a push.
"""
import os
import pathlib
import subprocess
import sys

import yaml

REPO = pathlib.Path(__file__).resolve().parent.parent
failed = []

for workflow in sorted((REPO / ".github" / "workflows").glob("*.yml")):
    data = yaml.safe_load(workflow.read_text(encoding="utf-8"))
    jobs = data.get("jobs", {})
    print(f"{workflow.name}: {len(jobs)} job(s)")
    for job_id, job in jobs.items():
        steps = job.get("steps", [])
        runs = [s for s in steps if "run" in s]
        names = [s.get("name", "(unnamed)") for s in steps]
        print(f"  {job_id}: {len(steps)} steps, {len(runs)} run-blocks")
        for index, step in enumerate(runs):
            shell = str(step.get("shell", "bash"))
            script = step["run"]
            # Written under .tools/ and invoked by relative path: a Windows absolute path handed to
            # Git Bash does not survive translation, and a linter that cannot find its own input
            # reports success on nothing.
            tmp_dir = REPO / ".work" / "lint-tmp"
            tmp_dir.mkdir(parents=True, exist_ok=True)
            script_path = tmp_dir / f"step-{index}.sh"
            # newline="" pins LF: the default translates to the platform line ending, and bash then
            # reports the CRLF the linter introduced as a syntax error in the workflow.
            with script_path.open("w", encoding="utf-8", newline="") as handle:
                handle.write(script)
            rel = f".work/lint-tmp/step-{index}.sh"
            if "pwsh" in shell.lower() or "powershell" in shell.lower():
                check = subprocess.run(
                    ["pwsh", "-NoProfile", "-Command",
                     "if (Test-Path -LiteralPath $env:SDC_SCRIPT) { exit 0 } else { exit 3 }"],
                    capture_output=True, text=True, cwd=REPO,
                    env={**os.environ, "SDC_SCRIPT": str(script_path.resolve())})
                syntax = subprocess.run(
                    ["pwsh", "-NoProfile", "-Command",
                     "$t=$null; $e=$null; "
                     "[System.Management.Automation.Language.Parser]::ParseFile($env:SDC_SCRIPT, [ref]$t, [ref]$e) | Out-Null; "
                     "if ($e) { $e | ForEach-Object { Write-Error ($_.Extent.StartLineNumber.ToString() + ': ' + $_.Message) }; exit 1 }"],
                    capture_output=True, text=True, cwd=REPO,
                    env={**os.environ, "SDC_SCRIPT": str(script_path.resolve())})
                check = syntax
            else:
                check = subprocess.run(["bash", "-n", rel], capture_output=True, text=True, cwd=REPO)
            if check.returncode != 0:
                detail = (check.stderr or check.stdout).strip().splitlines()
                failed.append(f"{workflow.name} / {job_id} / {step.get('name')}: {detail[:4]}")
            script_path.unlink(missing_ok=True)

print()
if failed:
    print("SYNTAX PROBLEMS:")
    print("\n".join(failed))
    sys.exit(1)
print("all workflow shell blocks parse")
