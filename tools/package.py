"""Package a Release build, excluding the game's reference libraries."""
from pathlib import Path
import hashlib
import json
import zipfile

ROOT = Path(__file__).resolve().parents[1]


def main():
    meta = json.loads((ROOT / 'modinfo.json').read_text())
    dll = ROOT / 'bin/Release/net10.0/Navy1851.dll'
    if not dll.is_file():
        raise SystemExit('No Release build. Run python3 tools/build.py first.')
    dist = ROOT / 'dist'
    dist.mkdir(exist_ok=True)
    output = dist / f"navy1851-{meta['version']}-vs{meta['dependencies']['game']}.zip"
    paths = [('modinfo.json', ROOT / 'modinfo.json'), ('Navy1851.dll', dll)]
    paths += [(p.relative_to(ROOT).as_posix(), p) for p in sorted((ROOT / 'assets').rglob('*'))
              if p.is_file() and p.suffix in {'.json', '.png', '.ogg'}]
    with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for name, path in paths:
            if path.suffix == '.json':
                json.loads(path.read_text())
            entry = zipfile.ZipInfo(name, (2026, 9, 5, 0, 0, 0))
            entry.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(entry, path.read_bytes())
    with zipfile.ZipFile(output) as archive:
        if archive.testzip() is not None:
            raise RuntimeError('ZIP integrity check failed.')
        names = archive.namelist()
        if 'modinfo.json' not in names or [n for n in names if n.endswith('.dll')] != ['Navy1851.dll']:
            raise RuntimeError('Package root or assembly allowlist is incorrect.')
    digest = hashlib.sha256(output.read_bytes()).hexdigest()
    output.with_suffix('.zip.sha256').write_text(digest + '  ' + output.name + '\n')
    print(f'{output.name}: {len(paths)} files, {output.stat().st_size} bytes, sha256 {digest}')


if __name__ == '__main__':
    main()
