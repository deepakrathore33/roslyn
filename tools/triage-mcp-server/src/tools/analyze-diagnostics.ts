import * as fs from "fs";
import * as path from "path";
import { execSync } from "child_process";

/**
 * MCP tool: analyze_dump
 *
 * Analyzes a .dmp crash dump file using dotnet-dump.
 * Extracts thread stacks, exception info, and module list.
 * Requires dotnet-dump to be installed: dotnet tool install -g dotnet-dump
 */
export async function analyzeDump(
  args: { dumpPath: string; commands?: string[] }
): Promise<{
  dumpPath: string;
  analysis: Record<string, string>;
  error?: string;
}> {
  if (!args.dumpPath) {
    throw new Error("dumpPath must be provided");
  }

  if (!fs.existsSync(args.dumpPath)) {
    throw new Error(`Dump file not found: ${args.dumpPath}`);
  }

  // Default analysis commands
  const commands = args.commands ?? [
    "clrstack",
    "pe",           // print exception
    "threads",
    "clrmodules",
  ];

  const analysis: Record<string, string> = {};

  // Check if dotnet-dump is available
  try {
    execSync("dotnet-dump --version", { encoding: "utf8", timeout: 10000 });
  } catch {
    return {
      dumpPath: args.dumpPath,
      analysis: {},
      error: "dotnet-dump is not installed. Install with: dotnet tool install -g dotnet-dump",
    };
  }

  for (const cmd of commands) {
    try {
      const output = execSync(
        `dotnet-dump analyze "${args.dumpPath}" -c "${cmd}"`,
        {
          encoding: "utf8",
          timeout: 60000, // 60s timeout per command
          maxBuffer: 10 * 1024 * 1024, // 10MB buffer
        }
      );
      // Trim to prevent massive outputs
      analysis[cmd] = output.length > 50000
        ? output.substring(0, 50000) + "\n... [output truncated at 50KB]"
        : output;
    } catch (err) {
      analysis[cmd] = `Error running '${cmd}': ${err instanceof Error ? err.message : String(err)}`;
    }
  }

  return { dumpPath: args.dumpPath, analysis };
}

/**
 * MCP tool: analyze_etl
 *
 * Provides basic info about an ETL trace file.
 * Full ETL analysis requires PerfView/WPA, but we can extract
 * file size and basic metadata.
 */
export async function analyzeEtl(
  args: { etlPath: string }
): Promise<{
  etlPath: string;
  fileSizeMB: number;
  note: string;
}> {
  if (!args.etlPath) {
    throw new Error("etlPath must be provided");
  }

  if (!fs.existsSync(args.etlPath)) {
    throw new Error(`ETL file not found: ${args.etlPath}`);
  }

  const stats = fs.statSync(args.etlPath);
  const sizeMB = Math.round((stats.size / (1024 * 1024)) * 100) / 100;

  return {
    etlPath: args.etlPath,
    fileSizeMB: sizeMB,
    note: `ETL file downloaded (${sizeMB} MB). For detailed analysis, open in PerfView or Windows Performance Analyzer (WPA). File location: ${args.etlPath}`,
  };
}
