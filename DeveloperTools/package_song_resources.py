#!/usr/bin/env python3
"""Package offline song files; no release versions or Unity metadata.

A delta ZIP carries the complete target file inventory, but only changed bytes.
The importer requires omitted files to exist; hashes are packaging-time delta metadata only.
"""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_ROOT = ROOT / 'SongResources'


def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda: f.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def allowed(path):
    return (path == 'catalog.json' or path.startswith(('OpenWDS/StandardCharts/', 'OpenWDS/AnotherNotations/'))) and all(
        part not in ('', '.', '..') for part in path.split('/')) and not any(c in path for c in '\\:') and not path.lower().endswith('.meta')


def dependencies(root):
    catalog = json.loads((root / 'catalog.json').read_text(encoding='utf-8'))
    special = json.loads((root / 'OpenWDS/AnotherNotations/catalog.json').read_text(encoding='utf-8'))
    result = {'catalog.json', 'OpenWDS/AnotherNotations/catalog.json'}
    for music in catalog['Musics'] + [e['Music'] for e in special['Entries']]:
        mid = music['Id']
        result.add(music['JacketAssetPath'])
        result.add(music.get('MusicAcbPath') or f'OpenWDS/StandardCharts/{mid}/cri/music_{mid}.acb.bundle')
        result.add(music.get('PreviewAcbPath') or f'OpenWDS/StandardCharts/{mid}/cri/musicpreview_{mid}.acb.bundle')
        for live in music['Lives']:
            result.update([live['DebugNotationAssetPath'], live['DebugMusicConfigAssetPath']])
    return result


def inventory(root):
    paths = sorted(p for p in root.rglob('*') if p.is_file())
    rows = []
    for path in paths:
        rel = path.relative_to(root).as_posix()
        if rel == 'manifest.json':
            continue
        if not allowed(rel) or path.is_symlink():
            raise ValueError(f'Unexpected song file: {path}')
        rows.append({'Path': rel, 'Size': path.stat().st_size, 'Sha256': digest(path)})
    available = {r['Path'] for r in rows}
    missing = dependencies(root) - available
    if missing:
        raise ValueError(f'Incomplete song dependencies: {sorted(missing)}')
    return {'Format': 1, 'Files': rows}


def package(root, output, base=None):
    manifest = inventory(root)
    old = {r['Path']: r for r in base['Files']} if base else {}
    changed = [r for r in manifest['Files'] if old.get(r['Path']) != r]
    output.parent.mkdir(parents=True, exist_ok=True)
    temp = output.with_suffix(output.suffix + '.tmp')
    try:
        with zipfile.ZipFile(temp, 'w', compression=zipfile.ZIP_STORED, allowZip64=True) as z:
            z.writestr('manifest.json', json.dumps(manifest, ensure_ascii=False, separators=(',', ':')))
            for row in changed:
                z.write(root / row['Path'], row['Path'])
        temp.replace(output)
    finally:
        temp.unlink(missing_ok=True)
    output.with_suffix('.manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n')
    return manifest, len(changed)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=DEFAULT_ROOT)
    parser.add_argument('--output', type=Path, default=ROOT / 'Build/Resources/OpenWDS-resources.zip')
    parser.add_argument('--base-manifest', type=Path, help='Optional previous file inventory, for a delta ZIP (not a version number)')
    parser.add_argument('--check', action='store_true', help='Validate dependencies and inventory without writing a ZIP')
    args = parser.parse_args()
    if args.check:
        manifest = inventory(args.root)
        print(f"SONG_RESOURCES_VALID files={len(manifest['Files'])} bytes={sum(r['Size'] for r in manifest['Files'])}")
    else:
        base = json.loads(args.base_manifest.read_text()) if args.base_manifest else None
        manifest, changed = package(args.root, args.output, base)
        print(f"SONG_RESOURCES_PACKAGED files={len(manifest['Files'])} included={changed} output={args.output}")


if __name__ == '__main__':
    main()
