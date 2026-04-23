import subprocess
import os
import sys
import argparse
import shutil
import zipfile
import hashlib
import platform
from datetime import datetime


def run_publish(project_csproj: str, working_dir: str, config: str, out_dir: str, os_id: str = None) -> int:
    # Map simple os identifier to RID when possible (pass via -r)
    def _rid_for(os_id: str) -> str | None:
        if not os_id:
            return None
        low = str(os_id).lower()
        machine = platform.machine().lower()
        arch = 'x64'
        if 'arm' in machine or 'aarch' in machine:
            arch = 'arm64'
        if low in ('osx', 'macos', 'darwin'):
            return f'osx-{arch}'
        if low in ('linux',):
            return f'linux-{arch}'
        if low in ('win', 'windows'):
            return f'win-{arch}'
        # allow full RID passthrough
        return os_id

    rid = _rid_for(os_id)
    cmd = ["dotnet", "publish", project_csproj, "-c", config, "-o", out_dir, "--self-contained", "true"]
    if rid:
        cmd.extend(["-r", rid])
    print("Running:", " ".join(cmd))
    # Capture output for diagnostics in CI, but only print on failure to avoid excessive logs
    res = subprocess.run(cmd, cwd=working_dir, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if res.returncode != 0:
        try:
            out = res.stdout.decode('utf-8', errors='replace')
            err = res.stderr.decode('utf-8', errors='replace')
            print('dotnet publish failed (stdout):')
            print(out)
            print('dotnet publish failed (stderr):')
            print(err)
        except Exception:
            print('dotnet publish failed but output decoding failed')
    else:
        print('dotnet publish succeeded')
    return res.returncode


def run_cleanup(script_path: str, target_dir: str, platform: str = None) -> int:
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


def _dir_has_files(path: str) -> bool:
    for _, _, files in os.walk(path):
        if files:
            return True
    return False


def _find_best_publish_candidate(search_root: str):
    # Look for likely publish outputs under bin/Release
    candidates = []
    for root, dirs, files in os.walk(search_root):
        # speed: only consider paths containing bin and Release
        lower = root.lower()
        if 'bin' in lower and 'release' in lower and files:
            candidates.append(root)
    if not candidates:
        return None

    def _size(p):
        total = 0
        for rt, ds, fs in os.walk(p):
            for f in fs:
                try:
                    total += os.path.getsize(os.path.join(rt, f))
                except Exception:
                    pass
        return total

    candidates.sort(key=_size, reverse=True)
    return candidates[0]


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
    parser.add_argument('--release-only', action='store_true', default=True, help='Only publish the release package and skip debug output')
    parser.add_argument('--appimage', action='store_true', help='Build AppImage for linux from release output')
    parser.add_argument('--os', default=None, help='OS identifier forwarded to dotnet publish')
    parser.add_argument('--platform', default=None, help='Platform to include in package name')
    parser.add_argument('--tag', default=None, help='Optional tag to include in asset name')
    parser.add_argument('--deps-path', default=None, help='Path to folder containing dependency DLLs to include')
    parser.add_argument('--deps-pattern', default='*.dll', help='Glob pattern for dependency files to copy')
    parser.add_argument('--deps-source', choices=['local','github','none'], default='local', help='Where to get dependency DLLs')
    parser.add_argument('--deps-github-repo', default='SK-la/osu-framework', help='GitHub repo (owner/repo) to clone when --deps-source=github')
    parser.add_argument('--deps-github-branch', default='master', help='Branch or ref to checkout when cloning deps github repo')
    parser.add_argument('--deps-github-project', default='osu.Framework/osu.Framework.csproj', help='Path to csproj inside cloned deps repo to build')
    parser.add_argument('--resources-github-repo', default='SK-la/osu-resources', help='GitHub repo for resources to clone')
    parser.add_argument('--resources-github-path', default='osu.Game.Resources/Resources', help='Path inside resources repo to copy')
    parser.add_argument('--resources-path', default=None, help='Local path to resources to include in package')
    args = parser.parse_args()

    # Interactive prompt when script run with no arguments (help is handled by argparse)
    if len(sys.argv) == 1:
        print("No arguments provided. Enter target platform (windows/linux/macos).")
        print("Press Enter to use this host platform. This will perform a Release-only build.")
        try:
            user_in = input("Platform: ").strip()
        except EOFError:
            user_in = ''
        if user_in:
            args.platform = user_in
        else:
            args.platform = None
        # interactive invocation should default to release-only
        args.release_only = True

    # Enforce that a tag is provided to avoid any implicit fallback tag generation
    if not args.tag:
        # default to today's date tag if not provided when running locally
        today = datetime.utcnow()
        args.tag = f"{today.year}-{today.month}-{today.day}"
        print(f"No --tag provided; defaulting to {args.tag}")

    tag_suffix = f"_{args.tag}"

    # determine host platform canonical name
    host_raw = platform.system().lower()
    if host_raw.startswith('win'):
        host_platform = 'windows'
    elif host_raw.startswith('darwin'):
        host_platform = 'macos'
    else:
        host_platform = 'linux' if 'linux' in host_raw else host_raw

    # infer platform: priority CLI --platform, --os, then script filename, otherwise host
    explicit = False
    if args.platform:
        target_platform = args.platform
        explicit = True
    elif args.os:
        target_platform = args.os
        explicit = True
    else:
        # When invoked with command-line arguments, ignore filename shortcut
        # (wrapper behavior). Only apply filename-based default when no args.
        if len(sys.argv) > 1:
            target_platform = host_platform
        else:
            name = os.path.basename(__file__).lower()
            if 'linux' in name:
                target_platform = 'linux'
            elif 'mac' in name or 'osx' in name or 'darwin' in name:
                target_platform = 'macos'
            elif 'win' in name:
                target_platform = 'windows'
            else:
                target_platform = host_platform

    # If platform was inferred from filename and doesn't match host, skip to avoid accidental runs
    if not explicit and target_platform != host_platform:
        print(f"Inferred platform '{target_platform}' from filename but current host is '{host_platform}'; skipping publish to avoid accidental cross-platform build.")
        sys.exit(0)

    # fixed folder names
    # Place all outputs under <outroot>/artifacts for a unified layout across platforms
    base_out = args.outroot
    artifacts_dir = os.path.join(base_out, 'artifacts')
    try:
        os.makedirs(artifacts_dir, exist_ok=True)
    except PermissionError:
        fallback = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'artifacts')
        print(f"Permission denied creating {artifacts_dir}, falling back to {fallback}")
        os.makedirs(fallback, exist_ok=True)
        artifacts_dir = fallback

    # determine architecture for naming (x64 vs arm64)
    machine = platform.machine().lower()
    arch_name = 'arm64' if ('arm' in machine or 'aarch' in machine) else 'x64'

    # Use the target platform (not host) for artifact folder and asset naming
    release_dir = os.path.join(artifacts_dir, f'Ez2Lazer_release_{target_platform}_{arch_name}')
    debug_dir = os.path.join(artifacts_dir, f'Ez2Lazer_debug_{target_platform}_{arch_name}')

    # remove existing folders to ensure deterministic output
    for d in (release_dir, debug_dir):
        if os.path.exists(d):
            print(f"Removing existing directory: {d}")
            shutil.rmtree(d)

    # publish
    print('Publishing Release...')
    rc = run_publish(args.project, args.workdir, 'Release', release_dir, target_platform)
    if rc != 0:
        print('Release publish failed with code', rc)
    else:
        print('Release publish succeeded')
        # optional cleanup (pass target platform so we don't remove required runtimes)
        run_cleanup(args.cleanup_release, release_dir, target_platform)
        # cleanup done; removed verbose release_dir listing to reduce log noise

    if not args.release_only:
        print('Publishing Debug...')
        rc2 = run_publish(args.project, args.workdir, 'Debug', debug_dir, target_platform)
        if rc2 != 0:
            print('Debug publish failed with code', rc2)
        else:
            print('Debug publish succeeded')
            run_cleanup(args.cleanup_debug, debug_dir, target_platform)

    # Use asset names that match workflow-normalized names when tag present
    release_zip = os.path.join(artifacts_dir, f"Ez2Lazer_release_{target_platform}_{arch_name}{tag_suffix}.zip")
    debug_zip = os.path.join(artifacts_dir, f"Ez2Lazer_debug_{target_platform}_{arch_name}{tag_suffix}.zip")

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
            # For macOS, create a minimal .app bundle containing the published files,
            # then zip the .app so Releases contain a mac-friendly bundle.
            if str(target_platform).lower().startswith('mac') or 'osx' in str(target_platform).lower():
                try:
                    print('Building .app bundle from release output')
                    app_name = 'Ez2Lazer'
                    app_bundle = os.path.join(artifacts_dir, f'{app_name}.app')
                    # Clean existing
                    shutil.rmtree(app_bundle, ignore_errors=True)
                    contents = os.path.join(app_bundle, 'Contents')
                    macos_dir = os.path.join(contents, 'MacOS')
                    resources_dir = os.path.join(contents, 'Resources')
                    app_resources = os.path.join(resources_dir, 'app')
                    os.makedirs(macos_dir, exist_ok=True)
                    os.makedirs(app_resources, exist_ok=True)

                    # copy published files into Resources/app
                    for item in os.listdir(release_dir):
                        s = os.path.join(release_dir, item)
                        d = os.path.join(app_resources, item)
                        if os.path.isdir(s):
                            shutil.copytree(s, d, dirs_exist_ok=True)
                        else:
                            shutil.copy2(s, d)

                    # attempt to find executable inside the published files
                    exe_candidate = None
                    # 1) find an executable file
                    for f in os.listdir(app_resources):
                        fp = os.path.join(app_resources, f)
                        if os.path.isfile(fp) and os.access(fp, os.X_OK):
                            exe_candidate = f
                            break
                    # 2) fallback: match project name
                    if not exe_candidate:
                        proj_base = os.path.splitext(os.path.basename(args.project))[0]
                        for f in os.listdir(app_resources):
                            if f.lower().startswith(proj_base.lower()):
                                exe_candidate = f
                                break
                    # 3) final fallback: pick first file with no extension
                    if not exe_candidate:
                        for f in os.listdir(app_resources):
                            if os.path.isfile(os.path.join(app_resources, f)) and '.' not in f:
                                exe_candidate = f
                                break

                    if not exe_candidate:
                        # leave as-is: use a launcher that runs the managed DLL via dotnet if necessary
                        launcher_exec = os.path.join(macos_dir, app_name)
                        with open(launcher_exec, 'w', encoding='utf-8') as f:
                            f.write('#!/bin/sh\n')
                            f.write('HERE="$(dirname "$(dirname "$(readlink -f "$0"))")"\n')
                            f.write('exec "${HERE}/Resources/app/osu.Desktop" "$@"\n')
                        try:
                            os.chmod(launcher_exec, 0o755)
                        except Exception:
                            pass
                        bundle_executable_name = app_name
                    else:
                        # create launcher that execs the discovered executable
                        launcher_exec = os.path.join(macos_dir, app_name)
                        with open(launcher_exec, 'w', encoding='utf-8') as f:
                            f.write('#!/bin/sh\n')
                            f.write('HERE="$(dirname "$(dirname "$(readlink -f "$0"))")"\n')
                            f.write('exec "${HERE}/Resources/app/%s" "$@"\n' % exe_candidate)
                        try:
                            os.chmod(launcher_exec, 0o755)
                        except Exception:
                            pass
                        bundle_executable_name = app_name

                    # minimal Info.plist
                    info_path = os.path.join(contents, 'Info.plist')
                    plist = f"""<?xml version=\"1.0\" encoding=\"UTF-8\"?>
<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">
<plist version=\"1.0\">\n<dict>\n  <key>CFBundleName</key>\n  <string>{app_name}</string>\n  <key>CFBundleExecutable</key>\n  <string>{bundle_executable_name}</string>\n  <key>CFBundleIdentifier</key>\n  <string>com.example.ez2lazer</string>\n  <key>CFBundleVersion</key>\n  <string>{args.tag}</string>\n  <key>CFBundlePackageType</key>\n  <string>APPL</string>\n</dict>\n</plist>"""
                    with open(info_path, 'w', encoding='utf-8') as f:
                        f.write(plist)

                    # zip the .app bundle
                    print('Zipping .app ->', release_zip)
                    zip_folder(app_bundle, release_zip)
                except Exception as e:
                    print('Failed to build .app bundle:', e)
                    print('Falling back to zipping release folder')
                    # If release_dir missing or empty, try to find publish outputs elsewhere and copy them
                    if not os.path.exists(release_dir) or not _dir_has_files(release_dir):
                        print('Release folder missing or empty, attempting to find publish outputs...')
                        candidate = _find_best_publish_candidate(args.workdir)
                        if candidate:
                            try:
                                print('Found candidate publish output:', candidate)
                                shutil.copytree(candidate, release_dir, dirs_exist_ok=True)
                                print('Copied publish output to', release_dir)
                            except Exception as e:
                                print('Failed to copy candidate publish output:', e)
                        else:
                            print('No publish candidate found; skipping zip')

                    if os.path.exists(release_dir) and _dir_has_files(release_dir):
                        print('Zipping release ->', release_zip)
                        zip_folder(release_dir, release_zip)
                    else:
                        print('Release folder still missing or empty after fallback; skipping zip')
            else:
                print('Zipping release ->', release_zip)
                zip_folder(release_dir, release_zip)
            # Optionally build AppImage (Linux)
            if args.appimage and (str(target_platform).lower().startswith('linux') or 'linux' in str(target_platform).lower()):
                try:
                    print('Building AppImage from', release_dir)
                    appdir = os.path.join(base_out, 'AppDir')
                    shutil.rmtree(appdir, ignore_errors=True)
                    os.makedirs(os.path.join(appdir, 'usr', 'bin'), exist_ok=True)
                    os.makedirs(os.path.join(appdir, 'usr', 'share', 'applications'), exist_ok=True)
                    os.makedirs(os.path.join(appdir, 'usr', 'share', 'icons', 'hicolor', '256x256', 'apps'), exist_ok=True)

                    # copy published files into AppDir/usr/bin
                    for item in os.listdir(release_dir):
                        s = os.path.join(release_dir, item)
                        d = os.path.join(appdir, 'usr', 'bin', item)
                        if os.path.isdir(s):
                            shutil.copytree(s, d, dirs_exist_ok=True)
                        else:
                            shutil.copy2(s, d)

                    # AppRun
                    apprun_path = os.path.join(appdir, 'AppRun')
                    with open(apprun_path, 'w', encoding='utf-8') as f:
                        f.write('#!/bin/sh\n')
                        f.write('HERE="$(dirname "$(readlink -f "$0")")"\n')
                        f.write('exec "$HERE"/usr/bin/osu.Desktop "$@"\n')
                    try:
                        os.chmod(apprun_path, 0o755)
                    except Exception:
                        pass

                    # desktop file
                    desktop_path = os.path.join(appdir, 'usr', 'share', 'applications', 'ez2lazer.desktop')
                    with open(desktop_path, 'w', encoding='utf-8') as f:
                        f.write('[Desktop Entry]\n')
                        f.write('Type=Application\n')
                        f.write('Name=Ez2Lazer\n')
                        f.write('Exec=osu.Desktop %u\n')
                        f.write('Icon=ez2lazer\n')
                        f.write('Categories=Game;\n')
                        f.write('Terminal=false\n')

                    # copy icon if available
                    icon_src = None
                    if args.resources_path and os.path.exists(os.path.join(args.resources_path, 'Icons', 'ez2lazer-256.png')):
                        icon_src = os.path.join(args.resources_path, 'Icons', 'ez2lazer-256.png')
                    else:
                        candidate = os.path.join(os.path.dirname(__file__), 'resources', 'Icons', 'ez2lazer-256.png')
                        if os.path.exists(candidate):
                            icon_src = candidate
                    if icon_src:
                        shutil.copy2(icon_src, os.path.join(appdir, 'usr', 'share', 'icons', 'hicolor', '256x256', 'apps', 'ez2lazer.png'))

                    # download appimagetool
                    import urllib.request
                    tool = os.path.join(base_out, 'appimagetool-x86_64.AppImage')
                    uri = 'https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage'
                    try:
                        print('Downloading appimagetool...')
                        urllib.request.urlretrieve(uri, tool)
                    except Exception:
                        print('urllib failed, trying curl...')
                        rc = subprocess.run(['curl', '-L', uri, '-o', tool])
                        if rc.returncode != 0:
                            print('Failed to download appimagetool')
                            raise
                    try:
                        os.chmod(tool, 0o755)
                    except Exception:
                        pass

                    # run appimagetool
                    # Run appimagetool and capture output for diagnostics
                    rc_tool = subprocess.run([tool, appdir], cwd=base_out, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
                    if rc_tool.returncode != 0:
                        try:
                            out = rc_tool.stdout.decode('utf-8', errors='replace')
                            err = rc_tool.stderr.decode('utf-8', errors='replace')
                            print('appimagetool failed (stdout):')
                            print(out)
                            print('appimagetool failed (stderr):')
                            print(err)
                        except Exception:
                            print('appimagetool failed but output decoding failed')
                        print('appimagetool failed', rc_tool.returncode)
                    else:
                        # find generated AppImage
                        import glob
                        matches = glob.glob(os.path.join(base_out, '*.AppImage'))
                        # Exclude the downloaded appimagetool itself from results
                        try:
                            tool_name = os.path.basename(tool)
                            matches = [m for m in matches if os.path.basename(m) != tool_name]
                        except Exception:
                            pass
                        if matches:
                            matches.sort(key=os.path.getmtime, reverse=True)
                            gen = matches[0]
                            artifacts_dir = os.path.join(base_out, 'artifacts')
                            os.makedirs(artifacts_dir, exist_ok=True)
                            dest = os.path.join(artifacts_dir, f'Ez2Lazer_release_linux_{arch_name}.AppImage')
                            shutil.copy2(gen, dest)
                            print('Created AppImage at', dest)
                        else:
                            print('No AppImage generated (only appimagetool found or generation failed)')
                        # cleanup downloaded tool so CI doesn't mistake it for the generated AppImage
                        try:
                            if os.path.exists(tool):
                                os.remove(tool)
                        except Exception:
                            pass
                except Exception as e:
                    print('AppImage build failed:', e)
        else:
            print('Release folder missing, skipping zip')

        if not args.release_only and os.path.exists(debug_dir):
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
