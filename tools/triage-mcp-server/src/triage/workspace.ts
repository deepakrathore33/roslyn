import { execFile } from "child_process";
import * as path from "path";
import * as fs from "fs";

/**
 * Find the Roslyn workspace root. Tries WORKSPACE_ROOT env var first,
 * then walks up from the current file looking for `Roslyn.slnx`.
 */
export function findWorkspaceRoot(): string | null {
  if (process.env.WORKSPACE_ROOT && fs.existsSync(process.env.WORKSPACE_ROOT)) {
    return process.env.WORKSPACE_ROOT;
  }
  let dir = path.resolve(__dirname, "..", "..", "..");
  for (let i = 0; i < 8; i++) {
    if (fs.existsSync(path.join(dir, "Roslyn.slnx"))) return dir;
    const parent = path.dirname(dir);
    if (parent === dir) break;
    dir = parent;
  }
  return null;
}

interface ExecResult { stdout: string; stderr: string; }

function exec(cmd: string, args: string[], opts: { cwd: string; timeoutMs?: number; maxBuffer?: number }): Promise<ExecResult> {
  return new Promise((resolve, reject) => {
    execFile(cmd, args, {
      cwd: opts.cwd,
      timeout: opts.timeoutMs ?? 15_000,
      maxBuffer: opts.maxBuffer ?? 4 * 1024 * 1024,
      windowsHide: true,
    }, (err, stdout, stderr) => {
      if (err && !stdout) {
        // git grep returns exit code 1 when nothing matches — that's fine
        const code = (err as NodeJS.ErrnoException & { code?: number }).code;
        if (code === 1) return resolve({ stdout: "", stderr });
        return reject(err);
      }
      resolve({ stdout: String(stdout), stderr: String(stderr) });
    });
  });
}

/**
 * Use `git grep -l` to quickly list files mentioning each term.
 * Returns up to `maxFiles` distinct files, with the matching terms.
 */
export async function gitGrep(
  cwd: string,
  terms: string[],
  options: { maxFiles?: number; pathSpec?: string[]; timeoutMs?: number } = {}
): Promise<Array<{ file: string; matchedTerms: string[] }>> {
  const maxFiles = options.maxFiles ?? 12;
  const fileScores = new Map<string, Set<string>>();

  for (const term of terms.slice(0, 6)) {
    if (term.length < 4) continue;
    const args = [
      "grep", "-l", "-i", "-I", "--max-count=1", "-F", term,
      "--", ...(options.pathSpec ?? ["src/**", "*.md"]),
    ];
    try {
      const { stdout } = await exec("git", args, { cwd, timeoutMs: options.timeoutMs ?? 8000 });
      const files = stdout.split(/\r?\n/).filter(Boolean);
      for (const f of files) {
        if (!fileScores.has(f)) fileScores.set(f, new Set());
        fileScores.get(f)!.add(term);
      }
    } catch {
      // ignore — git not available, etc.
    }
  }

  return [...fileScores.entries()]
    .map(([file, set]) => ({ file, matchedTerms: [...set] }))
    .sort((a, b) => b.matchedTerms.length - a.matchedTerms.length)
    .slice(0, maxFiles);
}

/**
 * Get recent commits touching a file or set of files. Returns one-line summaries
 * + parsed PR numbers (GitHub squash-merge format: "(#12345)").
 */
export async function gitLogForFiles(
  cwd: string,
  files: string[],
  max = 8
): Promise<Array<{ sha: string; subject: string; date: string; pr: number | null; file: string }>> {
  const out: Array<{ sha: string; subject: string; date: string; pr: number | null; file: string }> = [];
  for (const file of files.slice(0, 6)) {
    try {
      const { stdout } = await exec("git", [
        "log", `-${max}`, "--pretty=format:%h\t%ad\t%s", "--date=short", "--", file,
      ], { cwd, timeoutMs: 6000 });
      for (const line of stdout.split(/\r?\n/).filter(Boolean)) {
        const [sha, date, ...rest] = line.split("\t");
        const subject = rest.join("\t");
        const m = subject.match(/\(#(\d{3,6})\)\s*$/);
        out.push({ sha, subject, date, pr: m ? Number(m[1]) : null, file });
      }
    } catch {
      // skip file
    }
  }
  // Dedupe by sha
  const seen = new Set<string>();
  return out.filter((c) => {
    if (seen.has(c.sha)) return false;
    seen.add(c.sha);
    return true;
  }).slice(0, max);
}
