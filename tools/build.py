"""Build and verify using pinned official game references. Python standard library only."""
from pathlib import Path
import argparse
import hashlib
import json
import os
import subprocess
import sys
import tarfile
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
REFERENCES = ('VintagestoryAPI.dll', 'Lib/Newtonsoft.Json.dll', 'Lib/protobuf-net.dll')


def game_references(config):
    cache = ROOT / 'work' / 'references' / config['gameVersion']
    cache.mkdir(parents=True, exist_ok=True)
    archive = cache / 'server.tar.gz'
    if not archive.exists() or hashlib.sha256(archive.read_bytes()).hexdigest() != config['serverSha256']:
        print('Downloading official Vintage Story ' + config['gameVersion'] + ' references', flush=True)
        with urllib.request.urlopen(config['serverUrl'], timeout=90) as response:
            data = response.read()
        if hashlib.sha256(data).hexdigest() != config['serverSha256']:
            raise RuntimeError('Official archive checksum changed; review the version pin before building.')
        archive.write_bytes(data)
    # Extract only three named reference DLLs, never scripts or arbitrary archive paths.
    with tarfile.open(archive) as tar:
        members = {member.name.removeprefix('./'): member for member in tar.getmembers()}
        for name in REFERENCES:
            member = members.get(name)
            if member is None or not member.isfile():
                raise RuntimeError('Missing regular reference file: ' + name)
            target = cache / name
            target.parent.mkdir(parents=True, exist_ok=True)
            stream = tar.extractfile(member)
            if stream is None:
                raise RuntimeError('Could not read ' + name)
            with stream:
                target.write_bytes(stream.read())
    return cache


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game-path', type=Path, help='Use your installed game instead of downloading references.')
    parser.add_argument('--dotnet', default='dotnet', help='Path to the .NET 10 SDK executable.')
    args = parser.parse_args()
    config = json.loads((ROOT / 'build-config.json').read_text())
    info = json.loads((ROOT / 'modinfo.json').read_text())
    if info['dependencies']['game'] != config['gameVersion']:
        raise RuntimeError('modinfo.json and build-config.json game versions must agree.')
    game = args.game_path.resolve() if args.game_path else game_references(config)
    for name in REFERENCES:
        if not (game / name).is_file():
            raise RuntimeError('Game reference missing: ' + str(game / name))
    env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_GENERATE_ASPNET_CERTIFICATE='false')
    subprocess.run([args.dotnet, 'build', 'tests/Verify.csproj', '-c', 'Release',
                    '-p:VintageStoryPath=' + str(game), '--configfile', 'NuGet.Config',
                    '--nologo', '-warnaserror'], cwd=ROOT, env=env, check=True)
    subprocess.run([args.dotnet, 'tests/bin/Release/net10.0/Verify.dll', str(ROOT)],
                   cwd=ROOT, env=env, check=True)
    subprocess.run([sys.executable, 'tools/package.py'], cwd=ROOT, check=True)
    sdk = subprocess.check_output([args.dotnet, '--version'], cwd=ROOT, env=env, text=True).strip()
    provenance = {'modVersion': info['version'], 'gameVersion': config['gameVersion'],
                  'sdk': sdk, 'sourceCommit': os.environ.get('GITHUB_SHA'),
                  'referenceSource': 'installed game' if args.game_path else config['serverUrl'],
                  'referenceArchiveSha256': None if args.game_path else config['serverSha256'],
                  'inGameTested': False}
    (ROOT / 'dist' / 'build-info.json').write_text(json.dumps(provenance, indent=2) + '\n')


if __name__ == '__main__':
    main()
