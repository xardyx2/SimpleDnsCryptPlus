set -e
f=SimpleDnsCrypt/SimpleDnsCrypt.csproj
pkg="$1"; from="$2"; to="$3"
old="Include=\"$pkg\" Version=\"$from\""
new="Include=\"$pkg\" Version=\"$to\""
n=$(grep -Fc "$old" "$f" || true)
if [ "$n" != "1" ]; then echo "ABORT: pola cocok $n kali (harus 1)"; exit 1; fi
python - "$f" "$old" "$new" <<'PY'
import sys
p,old,new=sys.argv[1],sys.argv[2],sys.argv[3]
t=open(p,encoding='utf-8-sig').read()
assert t.count(old)==1, f"count={t.count(old)}"
open(p,'w',encoding='utf-8-sig',newline='').write(t.replace(old,new))
PY
echo "== bumped $pkg $from -> $to"
echo "restore issues: $(dotnet restore SimpleDnsCrypt.sln 2>&1 | grep -cE 'NU1701|error NU' || true)"
dotnet build SimpleDnsCrypt.sln -c Release --no-incremental 2>&1 | grep -oE "error [A-Z]+[0-9]+|warning (CS|NU|MSB)[0-9]+|Build succeeded|Build FAILED" | sort | uniq -c | sort -rn | head -6
dotnet test Tests/Tests.csproj -c Release --no-build 2>&1 | grep -E "Passed!|Failed!" | tail -1
