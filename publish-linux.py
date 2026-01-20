#!/usr/bin/env python3
import subprocess
import os
import sys
import platform
import argparse
import shutil
import zipfile
import hashlib
from datetime import datetime


def run_publish(project_csproj: str, working_dir: str, config: str, out_dir: str,os: str) -> int:
    cmd = ["dotnet", "publish", project_csproj, "-c", config, "-o", out_dir, "--self-contained", "true", "--os", os]
    print("Running:", " ".join(cmd))
    res = subprocess.run(cmd, cwd=working_dir)
    return res.returncode


def run_cleanup(script_path: str, target_dir: str, platform: str) -> int:
    # If an external script is provided and exists, run it. Otherwise use internal cleaner.
    if script_path and os.path.exists(script_path):
        print(f"Running external cleanup script: {script_path}")
        res = subprocess.run(["python", script_path, target_dir])
        return res.returncode
    else:
        print("External cleanup script not found, using internal cleanup logic")
        return clean_publish_folder(target_dir, platform)


def clean_publish_folder(release_dir=None, platform=None):
    from pathlib import Path
    import fnmatch

    if release_dir is None:
        release_dir = Path(__file__).parent / "Release"
    else:
        release_dir = Path(release_dir)

    if not release_dir.exists():
        print(f"Release folder does not exist: {release_dir}")
        return 0

    print(f"Starting cleanup of folder: {release_dir}")

    deleted_files = 0
    deleted_folders = 0

    # 1. 删除 .pdb 文件
    for pdb_file in release_dir.rglob("*.pdb"):
        try:
            pdb_file.unlink()
            print(f"Deleted PDB file: {pdb_file.name}")
            deleted_files += 1
        except Exception as e:
            print(f"Failed to delete {pdb_file}: {e}")

    # 2. 删除调试和诊断相关的XML文件（而不是所有XML文件）
    debug_xml_patterns = [
        "*Microsoft*.xml", "*System*.xml", "*osu*.xml", "*Veldrid*.xml", "*MongoDB*.xml", "*Newtonsoft*.xml", "*TagLib*.xml", "*HtmlAgilityPack*.xml", "*DiscordRPC*.xml", "*FFmpeg*.xml", "*Sentry*.xml", "*Realm*.xml", "*NuGet*.xml"
    ]
    for pattern in debug_xml_patterns:
        for xml_file in release_dir.rglob(pattern):
            try:
                xml_file.unlink()
                print(f"Deleted documentation file: {xml_file.name}")
                deleted_files += 1
            except Exception as e:
                print(f"Failed to delete {xml_file}: {e}")

    # 清理 runtime 文件夹
    runtime_dir = release_dir / "runtimes"
    if runtime_dir.exists():
        print(f"Processing runtime folder: {runtime_dir}")
        # choose keep list based on platform if provided
        if platform is None:
            keep_runtimes = {"win-x64", "win-x86"}
        else:
            if platform == 'windows':
                keep_runtimes = {"win-x64", "win-x86"}
            elif platform == 'linux':
                keep_runtimes = {"linux-x64"}
            elif platform == 'macos':
                keep_runtimes = {"osx-x64", "osx-arm64"}
            else:
                keep_runtimes = {"win-x64", "win-x86"}

        for runtime_folder in runtime_dir.iterdir():
            if runtime_folder.is_dir():
                runtime_name = runtime_folder.name
                if runtime_name not in keep_runtimes:
                    try:
                        shutil.rmtree(runtime_folder)
                        print(f"Deleted runtime folder: {runtime_name}")
                        deleted_folders += 1
                    except Exception as e:
                        print(f"Failed to delete runtime folder {runtime_name}: {e}")
                else:
                    print(f"Keeping runtime folder: {runtime_name}")

    print(f"\nCleanup complete!")
    print(f"Deleted files: {deleted_files}")
    print(f"Deleted folders: {deleted_folders}")
    return 0


def _compute_sha256(path: str) -> str:
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for chunk in iter(lambda: f.read(8192), b''):
            h.update(chunk)
    return h.hexdigest()


def zip_folder(src_dir: str, zip_path: str):
    """Create a deterministic zip of src_dir at zip_path.

    Determinism achieved by:
    - adding files in sorted order
    - setting a fixed timestamp on all ZipInfo entries
    - using ZIP_DEFLATED consistently
    """
    if os.path.exists(zip_path):
        os.remove(zip_path)

    FIXED_DATETIME = (1980, 1, 1, 0, 0, 0)  # year >= 1980 required by zip spec

    def _iter_files(root_dir):
        for root, dirs, files in os.walk(root_dir):
            dirs.sort()
            files.sort()
            for f in files:
                full = os.path.join(root, f)
                rel = os.path.relpath(full, root_dir)
                # normalize to forward slashes inside zip
                arcname = rel.replace(os.path.sep, '/')
                yield full, arcname

    compression = zipfile.ZIP_DEFLATED
    with zipfile.ZipFile(zip_path, 'w', compression=compression) as zf:
        for full, arcname in _iter_files(src_dir):
            zi = zipfile.ZipInfo(arcname)
            zi.date_time = FIXED_DATETIME
            # set external attributes to a reasonable default (rw-r--r--)
            zi.external_attr = 0o644 << 16
            with open(full, 'rb') as fh:
                data = fh.read()
            zf.writestr(zi, data, compress_type=compression)

    # print diagnostics: size and sha256
    try:
        size = os.path.getsize(zip_path)
        sha256 = _compute_sha256(zip_path)
        print(f"Created zip: {zip_path}")
        print(f"ZIP size: {size} bytes")
        print(f"ZIP SHA256: {sha256}")
    except Exception as e:
        print(f"Created zip but failed to compute diagnostics: {e}")


def main():
    parser = argparse.ArgumentParser(description="Publish and package Ez2Lazer builds.")
    # Prefer the GITHUB_WORKSPACE env when present (CI), otherwise use the script directory
    script_dir = os.path.dirname(os.path.abspath(__file__))
    gh_workspace = os.environ.get('GITHUB_WORKSPACE', script_dir)
    parser.add_argument('--project', default=os.path.join(gh_workspace, 'osu.Desktop', 'osu.Desktop.csproj'))
    parser.add_argument('--workdir', default=gh_workspace)
    parser.add_argument('--cleanup-release', default=None)
    parser.add_argument('--cleanup-debug', default=None)
    parser.add_argument('--outroot', default=gh_workspace)
    # Note: no local-only root option to keep publish.py CI-friendly
    parser.add_argument('--zip-only', action='store_true', help='Only create zip files locally and do not attempt any remote operations')
    parser.add_argument('--no-zip', action='store_true', help='Do not create zip files')
    parser.add_argument('--tag', default=None, help='Optional tag to include in asset name')
    parser.add_argument('--deps-path', default=None, help='Path to folder containing dependency DLLs to include')
    parser.add_argument('--deps-pattern', default='*.dll', help='Glob pattern for dependency files to copy')
    parser.add_argument('--deps-source', choices=['local','github','none'], default='local', help='Where to get dependency DLLs')
    parser.add_argument('--deps-github-repo', default='SK-la/osu-framework', help='GitHub repo (owner/repo) to clone when --deps-source=github')
    parser.add_argument('--deps-github-branch', default='locmain', help='Branch or ref to checkout when cloning deps github repo')
    parser.add_argument('--deps-github-project', default='osu.Framework/osu.Framework.csproj', help='Path to csproj inside cloned deps repo to build')
    parser.add_argument('--resources-github-repo', default='SK-la/osu-resources', help='GitHub repo for resources to clone')
    parser.add_argument('--resources-github-path', default='osu.Game.Resources/Resources', help='Path inside resources repo to copy')
    parser.add_argument('--resources-path', default=None, help='Local path to resources to include in package')
    parser.add_argument('--platform', default=None, help='Platform to include in package name')
    args = parser.parse_args()

    # Enforce that a tag is provided to avoid any implicit fallback tag generation
    if not args.tag:
        # default to today's date tag if not provided when running locally
        today = datetime.utcnow()
        args.tag = f"{today.year}-{today.month}-{today.day}"
        print(f"No --tag provided; defaulting to {args.tag}")

    tag_suffix = f"_{args.tag}"

    # fixed folder names
    # If running in zip-only (local) mode, place artifacts under the user-specified local root
    base_out = args.outroot

    release_dir = os.path.join(base_out, 'Ez2Lazer_release_x64')
    debug_dir = os.path.join(base_out, 'Ez2Lazer_debug_x64')

    # remove existing folders to ensure deterministic output
    for d in (release_dir, debug_dir):
        if os.path.exists(d):
            print(f"Removing existing directory: {d}")
            shutil.rmtree(d)

    target_platform = args.platform or platform.system().lower()
    print("building for platform", target_platform)
    # publish
    print('Publishing Release...')
    rc = run_publish(args.project, args.workdir, 'Release', release_dir,target_platform)
    if rc != 0:
        print('Release publish failed with code', rc)
    else:
        print('Release publish succeeded')
        # optional cleanup
        run_cleanup(args.cleanup_release, release_dir,target_platform)

    print('Publishing Debug...')
    rc2 = run_publish(args.project, args.workdir, 'Debug', debug_dir,target_platform)
    if rc2 != 0:
        print('Debug publish failed with code', rc2)
    else:
        print('Debug publish succeeded')
        run_cleanup(args.cleanup_debug, debug_dir,target_platform)

    # create zips with fixed base name + tag
    artifacts_dir = os.path.join(base_out, 'artifacts')
    # Try to create artifacts dir; if permission denied (e.g. running from system32),
    # fall back to a safe location next to this script.
    try:
        os.makedirs(artifacts_dir, exist_ok=True)
    except PermissionError:
        fallback = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'artifacts')
        print(f"Permission denied creating {artifacts_dir}, falling back to {fallback}")
        os.makedirs(fallback, exist_ok=True)
        artifacts_dir = fallback

    # Use asset names that match workflow-normalized names when tag present
    release_zip = os.path.join(artifacts_dir, f"Ez2Lazer_release_x64{tag_suffix}.zip")
    debug_zip = os.path.join(artifacts_dir, f"Ez2Lazer_debug_x64{tag_suffix}.zip")

    if not args.no_zip:
        if os.path.exists(release_dir):
            # handle deps source
            temp_dirs = []
            deps_to_cleanup = []
            try:
                if args.deps_source == 'local':
                    deps_src_path = args.deps_path
                elif args.deps_source == 'github':
                    # clone deps repo and build; try to auto-detect csproj if path not exact
                    import tempfile
                    deps_owner, deps_repo = args.deps_github_repo.split('/')
                    tmp = tempfile.mkdtemp(prefix='deps-')
                    temp_dirs.append(tmp)
                    print(f"Cloning {args.deps_github_repo}@{args.deps_github_branch} into {tmp}")
                    res = subprocess.run(["git","clone","--depth","1","--branch",args.deps_github_branch,f"https://github.com/{args.deps_github_repo}.git", tmp])
                    if res.returncode != 0:
                        raise RuntimeError('git clone failed')

                    # Candidate project path if provided
                    candidate = os.path.join(tmp, *args.deps_github_project.split('/'))
                    proj_to_build = None
                    if os.path.exists(candidate):
                        proj_to_build = candidate
                    else:
                        # search for csproj files and prefer ones with osu.Framework in name or path
                        csproj_matches = []
                        for root, dirs, files in os.walk(tmp):
                            for f in files:
                                if f.endswith('.csproj'):
                                    csproj_matches.append(os.path.join(root, f))
                        if csproj_matches:
                            preferred = None
                            for p in csproj_matches:
                                if 'osu.Framework' in os.path.basename(p) or 'osu.Framework' in p:
                                    preferred = p
                                    break
                            proj_to_build = preferred or csproj_matches[0]

                    if proj_to_build:
                        print('Building dependency project', proj_to_build)
                        bres = subprocess.run(["dotnet","build",proj_to_build,"-c","Release","-f","net8.0"])
                        if bres.returncode != 0:
                            raise RuntimeError('dotnet build of deps failed')
                        deps_src_path = os.path.join(os.path.dirname(proj_to_build), 'bin', 'Release', 'net8.0')
                        if not os.path.exists(deps_src_path):
                            # sometimes the project is in a subfolder; search upwards
                            parent = os.path.dirname(proj_to_build)
                            found = False
                            for _ in range(4):
                                candidate_bin = os.path.join(parent, 'bin', 'Release', 'net8.0')
                                if os.path.exists(candidate_bin):
                                    deps_src_path = candidate_bin
                                    found = True
                                    break
                                parent = os.path.dirname(parent)
                            if not found:
                                print('Warning: could not find built outputs in expected locations')
                    else:
                        print('Project path not found in cloned repo, attempting to find bin folder...')
                        deps_src_path = os.path.join(tmp, 'bin', 'Release', 'net8.0')
                else:
                    deps_src_path = None

                # If resources repo requested when using github, try clone and copy resources
                if args.deps_source == 'github' and args.resources_github_repo:
                    import tempfile
                    tmpres = tempfile.mkdtemp(prefix='res-')
                    temp_dirs.append(tmpres)
                    print(f"Cloning resources {args.resources_github_repo}@{args.deps_github_branch} into {tmpres}")
                    subprocess.run(["git","clone","--depth","1","--branch",args.deps_github_branch,f"https://github.com/{args.resources_github_repo}.git", tmpres])
                    srcres = os.path.join(tmpres, args.resources_github_path.replace('/','\\' if os.name=='nt' else '/'))
                    if os.path.exists(srcres):
                        destres = os.path.join(release_dir, 'Resources')
                        shutil.rmtree(destres, ignore_errors=True)
                        shutil.copytree(srcres, destres)
                        print(f"Copied resources to {destres}")

                # copy dependency DLLs from deps_src_path if available
                if deps_src_path and os.path.exists(deps_src_path):
                    import glob
                    print(f"Copying dependency files from {deps_src_path} to release folder")
                    for f in glob.glob(os.path.join(deps_src_path, args.deps_pattern)):
                        try:
                            shutil.copy(f, release_dir)
                            print('Copied', f)
                        except Exception as e:
                            print('Copy failed', f, e)
                # copy resources from explicit resources path if provided
                if args.resources_path and os.path.exists(args.resources_path):
                    srcres = args.resources_path
                    destres = os.path.join(release_dir, 'Resources')
                    try:
                        shutil.rmtree(destres, ignore_errors=True)
                        shutil.copytree(srcres, destres)
                        print(f"Copied resources to {destres}")
                    except Exception as e:
                        print('Resource copy failed', e)
                else:
                    print('No dependency source path available, skipping deps copy')
            finally:
                # cleanup temp dirs
                for d in temp_dirs:
                    try:
                        shutil.rmtree(d)
                    except Exception:
                        pass
            print('Zipping release ->', release_zip)
            zip_folder(release_dir, release_zip)
        else:
            print('Release folder missing, skipping zip')

        if os.path.exists(debug_dir):
            # for debug, repeat similar deps copy if deps_source is local
            if args.deps_source == 'local' and args.deps_path and os.path.exists(args.deps_path):
                import glob
                print(f"Copying dependency files from {args.deps_path} to debug folder")
                for f in glob.glob(os.path.join(args.deps_path, args.deps_pattern)):
                    try:
                        shutil.copy(f, debug_dir)
                        print('Copied', f)
                    except Exception as e:
                        print('Copy failed', f, e)
            print('Zipping debug ->', debug_zip)
            zip_folder(debug_dir, debug_zip)
        else:
            print('Debug folder missing, skipping zip')

    print('Done.')


if __name__ == '__main__':
    main()
