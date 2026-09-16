#!/usr/bin/env node
// The build entry point the parent catalog calls: `npm run build` in games/flappy-voice.
//
// This repo is a submodule, not a folder inside variant-originals' working tree, so it cannot
// import ../../scripts/unity/tooling.mjs — a plain clone has no parent above it. The contract
// checks it would have run are reimplemented here against the same rules, and the build itself is
// FlappyVoice.Editor.WebBuilder.BuildWeb, which mirrors the parent's OriginalsWebBuild.

import { execFileSync, spawnSync } from "node:child_process";
import { existsSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const projectRoot = fileURLToPath(new URL(".", import.meta.url));
const BUILD_METHOD = "FlappyVoice.Editor.WebBuilder.BuildWeb";

/**
 * Checks the catalog entry against the rules the parent's publish step enforces for a Unity game,
 * so a contract break surfaces here in seconds instead of after a ten-minute build.
 * Returns the parsed config.
 */
function readGameConfig() {
  const configPath = path.join(projectRoot, "game.json");
  if (!existsSync(configPath)) throw new Error(`Missing ${configPath}`);
  const config = JSON.parse(readFileSync(configPath, "utf8"));
  if (config.engine !== "unity") throw new Error('game.json must declare "engine": "unity".');
  if (config.publishDir !== "dist") throw new Error('game.json must declare "publishDir": "dist".');
  if (config.orientation !== "portrait" && config.orientation !== "landscape") {
    throw new Error('game.json must declare "orientation" as portrait or landscape.');
  }
  return config;
}

/**
 * Reads the editor version this project pins, so the version is never typed at a prompt and the
 * build cannot quietly run on whatever editor happens to be installed.
 */
function readUnityVersion() {
  const versionFile = path.join(projectRoot, "ProjectSettings", "ProjectVersion.txt");
  if (!existsSync(versionFile)) throw new Error(`Unity project is missing ${versionFile}`);
  const match = readFileSync(versionFile, "utf8").match(/^m_EditorVersion:\s*(.+)$/m);
  if (!match) throw new Error(`Could not read the pinned Unity version from ${versionFile}`);
  return match[1].trim();
}

/**
 * Locates the pinned editor binary. Delegates to Tools/unity-path.sh rather than repeating its
 * search, because that script already handles a Hub install root the user has relocated — which
 * the parent's own resolver does not. UNITY_EDITOR_PATH is accepted under the parent's name.
 */
function findUnityEditor() {
  const override = process.env.UNITY_EDITOR_PATH || process.env.UNITY_PATH;
  if (override) return override;
  return execFileSync(path.join(projectRoot, "Tools", "unity-path.sh"), {
    encoding: "utf8",
    stdio: ["ignore", "pipe", "inherit"],
  }).trim();
}

/**
 * Reports the version of the editor binary itself, or null when it cannot be determined.
 * On macOS the app bundle states it outright; elsewhere the Hub's directory layout is the only
 * marker available without launching the editor.
 */
function editorVersion(editorPath) {
  if (process.platform === "darwin") {
    const infoPlist = path.resolve(path.dirname(editorPath), "..", "Info.plist");
    if (existsSync(infoPlist)) {
      try {
        return execFileSync("/usr/libexec/PlistBuddy", ["-c", "Print :CFBundleVersion", infoPlist], {
          encoding: "utf8",
          stdio: ["ignore", "pipe", "ignore"],
        }).trim();
      } catch {
        // Fall through to the path check.
      }
    }
  }
  // The Hub installs every editor under a directory named for its version.
  const versionLike = editorPath.split(path.sep).find((part) => /^\d+\.\d+\.\d+[abfp]\d+$/.test(part));
  return versionLike ?? null;
}

/**
 * Refuses to build on an editor that is not the pinned one. `Tools/unity-path.sh` can only find
 * the pinned version, but UNITY_EDITOR_PATH bypasses it entirely — and Unity's project format and
 * its Web output both move between versions, so "close enough" silently ships something else.
 */
function assertPinnedEditor(editorPath, pinnedVersion) {
  const found = editorVersion(editorPath);
  if (found === null) {
    console.warn(
      `WARNING: could not determine the version of ${editorPath}.\n` +
        `         This project pins Unity ${pinnedVersion}; building on anything else is unsupported.`,
    );
    return;
  }
  if (found !== pinnedVersion) {
    throw new Error(
      `This project pins Unity ${pinnedVersion}, but ${editorPath} is ${found}.\n` +
        "Install the pinned version from the Hub, or correct UNITY_EDITOR_PATH. If you mean to " +
        "move the project to a new editor, change ProjectSettings/ProjectVersion.txt — that file " +
        "is the pin, and everything else reads it.",
    );
  }
}

/**
 * Fails early when the Web module is missing. Without this the module surfaces late, as a failed
 * build target switch that reads like a code error rather than a missing Hub download.
 */
function assertWebBuildSupport(editorPath) {
  let ancestor = path.dirname(editorPath);
  while (ancestor !== path.dirname(ancestor)) {
    if (existsSync(path.join(ancestor, "PlaybackEngines", "WebGLSupport"))) return;
    ancestor = path.dirname(ancestor);
  }
  throw new Error(
    `Unity Web Build Support is missing for ${editorPath}.\n` +
      "Install it from the Unity Hub (Installs -> the ... menu on the version -> Add modules " +
      "-> WebGL Build Support), then run this again.",
  );
}

/**
 * Refuses to start while the Editor holds the project. Two editors on one project fail with an
 * error that reads like a build failure, and it is not one.
 */
function assertProjectUnlocked() {
  if (existsSync(path.join(projectRoot, "Temp", "UnityLockfile"))) {
    throw new Error(
      "The Editor has this project open, and a batch build cannot share it. Close it first.\n" +
        "(Or build from the menu: Flappy Voice -> Build Web Player.)",
    );
  }
}

/**
 * Warns when the project's copy of the shared Web template has drifted from the parent's canonical
 * one. Only a warning: a plain clone has no parent to compare against, and that must not fail.
 */
function checkTemplateFreshness() {
  const canonical = path.resolve(
    projectRoot,
    "..",
    "..",
    "scripts/unity/package/Editor/Templates/Variant/index.html",
  );
  if (!existsSync(canonical)) return;
  const local = path.join(projectRoot, "Assets/WebGLTemplates/Variant/index.html");
  if (!existsSync(local) || !readFileSync(canonical).equals(readFileSync(local))) {
    console.warn(
      `WARNING: Assets/WebGLTemplates/Variant/index.html differs from ${canonical}.\n` +
        "         Copy the canonical file over it, or the build ships a stale shell.",
    );
  }
}

/**
 * Snapshots the files Unity is allowed to rewrite while it builds, and returns a restore function.
 * Mirrors the parent's snapshotUnityBuildInputs: a release build flips a dozen PlayerSettings, and
 * leaving that churn in the working tree makes every build look like an edit.
 * Returns a no-op outside a git checkout, so a plain tarball still builds.
 */
function snapshotBuildInputs() {
  let tracked;
  try {
    tracked = execFileSync("git", ["ls-files", "-z"], { cwd: projectRoot })
      .toString("utf8")
      .split("\0")
      .filter(Boolean);
  } catch {
    return () => {};
  }

  const isBuildInput = (relativePath) =>
    relativePath.startsWith("ProjectSettings/") ||
    relativePath.startsWith("Packages/") ||
    relativePath.startsWith("Assets/WebGLTemplates/") ||
    // The editor rewrites dotnet.defaultSolution to match whatever .slnx it generates, and the
    // generated solution is gitignored, so this file lands dirty after every build.
    relativePath.startsWith(".vscode/") ||
    relativePath.endsWith(".meta");

  const originals = new Map();
  for (const relativePath of tracked.filter(isBuildInput)) {
    const absolutePath = path.join(projectRoot, relativePath);
    originals.set(absolutePath, existsSync(absolutePath) ? readFileSync(absolutePath) : null);
  }

  // git ls-files cannot see the template before it is committed, and a first build would
  // otherwise be free to rewrite it. Cover it by path rather than by tracking status.
  const template = path.join(projectRoot, "Assets/WebGLTemplates/Variant/index.html");
  if (!originals.has(template)) {
    originals.set(template, existsSync(template) ? readFileSync(template) : null);
  }

  return () => {
    for (const [absolutePath, contents] of originals) {
      if (contents === null) {
        if (existsSync(absolutePath)) rmSync(absolutePath);
      } else if (!existsSync(absolutePath) || !readFileSync(absolutePath).equals(contents)) {
        writeFileSync(absolutePath, contents);
      }
    }
  };
}

let restoreBuildInputs;
let exitCode = 1;
try {
  const config = readGameConfig();
  assertProjectUnlocked();
  checkTemplateFreshness();
  restoreBuildInputs = snapshotBuildInputs();
  const unityVersion = readUnityVersion();
  const editorPath = findUnityEditor();
  assertPinnedEditor(editorPath, unityVersion);
  assertWebBuildSupport(editorPath);

  console.log(`Building ${config.name} with Unity ${unityVersion}`);
  console.log(`  editor: ${editorPath}`);
  const result = spawnSync(
    editorPath,
    [
      "-batchmode",
      "-nographics",
      "-projectPath",
      projectRoot,
      "-buildTarget",
      "WebGL",
      "-executeMethod",
      BUILD_METHOD,
      "-quit",
      "-logFile",
      "-",
    ],
    { cwd: projectRoot, env: process.env, stdio: "inherit" },
  );
  if (result.error) throw result.error;
  exitCode = result.status ?? 1;
} catch (error) {
  console.error(error.message);
} finally {
  restoreBuildInputs?.();
}

process.exitCode = exitCode;
