const docx = require("docx");
const fs = require("fs");

const {
  Document, Packer, Paragraph, TextRun, HeadingLevel,
  TableRow, TableCell, Table, WidthType, BorderStyle,
  AlignmentType, ShadingType, convertInchesToTwip,
  PageBreak,
} = docx;

// -- Colors --
const BRAND_BLUE = "0078D4";
const DARK_BLUE = "003C71";
const ACCENT_GREEN = "107C10";
const ACCENT_RED = "D13438";
const ACCENT_ORANGE = "FF8C00";
const LIGHT_GRAY = "F3F3F3";
const WHITE = "FFFFFF";

// Helper: styled heading
function heading(text, level = HeadingLevel.HEADING_1) {
  return new Paragraph({
    heading: level,
    spacing: { before: 300, after: 120 },
    children: [new TextRun({ text, font: "Segoe UI", color: DARK_BLUE, bold: true })],
  });
}

// Helper: bullet point
function bullet(text, opts = {}) {
  return new Paragraph({
    bullet: { level: 0 },
    spacing: { after: 60 },
    children: [new TextRun({ text, font: "Segoe UI", size: 22, ...opts })],
  });
}

// Helper: body paragraph
function body(text) {
  return new Paragraph({
    spacing: { after: 100 },
    children: [new TextRun({ text, font: "Segoe UI", size: 22 })],
  });
}

// Helper: table cell
function cell(text, opts = {}) {
  return new TableCell({
    shading: opts.shading ? { type: ShadingType.SOLID, color: opts.shading } : undefined,
    width: opts.width ? { size: opts.width, type: WidthType.PERCENTAGE } : undefined,
    children: [
      new Paragraph({
        spacing: { before: 40, after: 40 },
        children: [
          new TextRun({
            text,
            font: "Segoe UI",
            size: 20,
            bold: opts.bold || false,
            color: opts.color || "1A1A1A",
          }),
        ],
      }),
    ],
  });
}

const doc = new Document({
  creator: "Roslyn Triage Team",
  title: "Roslyn Feedback Triage Automation Plan",
  description: "Detailed plan for automating feedback triage using VS Code Copilot Agent and Azure DevOps MCP Server",
  styles: {
    default: {
      document: {
        run: { font: "Segoe UI", size: 22 },
      },
      heading1: {
        run: { font: "Segoe UI", size: 32, bold: true, color: DARK_BLUE },
        paragraph: { spacing: { before: 360, after: 120 } },
      },
      heading2: {
        run: { font: "Segoe UI", size: 26, bold: true, color: BRAND_BLUE },
        paragraph: { spacing: { before: 280, after: 100 } },
      },
      heading3: {
        run: { font: "Segoe UI", size: 22, bold: true, color: DARK_BLUE },
        paragraph: { spacing: { before: 200, after: 80 } },
      },
    },
  },
  sections: [
    {
      properties: {
        page: {
          margin: {
            top: convertInchesToTwip(1),
            bottom: convertInchesToTwip(1),
            left: convertInchesToTwip(1.2),
            right: convertInchesToTwip(1.2),
          },
        },
      },
      children: [
        // ---- TITLE ----
        new Paragraph({
          spacing: { before: 600, after: 0 },
          children: [
            new TextRun({ text: "Roslyn Feedback", font: "Segoe UI", size: 48, bold: true, color: BRAND_BLUE }),
          ],
        }),
        new Paragraph({
          spacing: { before: 0, after: 200 },
          children: [
            new TextRun({ text: "Triage Automation Plan", font: "Segoe UI", size: 48, bold: true, color: BRAND_BLUE }),
          ],
        }),
        new Paragraph({
          spacing: { after: 60 },
          children: [
            new TextRun({ text: "Intelligent Read-Only Triage Assistant", font: "Segoe UI", size: 24, italics: true, color: "555555" }),
          ],
        }),
        new Paragraph({
          spacing: { after: 400 },
          children: [
            new TextRun({ text: "March 2026  |  VS Code + Copilot Agent + MCP", font: "Segoe UI", size: 20, color: "888888" }),
          ],
        }),

        // ---- OVERVIEW ----
        heading("1. Overview"),
        body("This plan automates the intelligence-gathering phase of triaging Visual Studio feedback items from Azure DevOps. It uses a VS Code Copilot Agent backed by an Azure DevOps MCP (Model Context Protocol) server."),
        body("The agent is read-only — it gathers intel and presents findings. A human reviewer makes all triage decisions and updates work items manually."),

        // ---- PROBLEM ----
        heading("2. The Problem"),
        bullet("Manual triage of VS feedback items is time-consuming and repetitive"),
        bullet("Each item requires searching across Azure DevOps, Developer Community, and GitHub"),
        bullet("Finding the responsible code and related PRs in the Roslyn repo is tedious"),
        bullet("Classifying items as Roslyn vs. non-Roslyn requires domain knowledge"),
        bullet("No single tool connects all these data sources today"),

        // ---- SOLUTION ----
        heading("3. Solution Overview"),
        body("VS Code Copilot Agent + Azure DevOps MCP Server"),
        new Paragraph({
          spacing: { before: 120, after: 60 },
          children: [new TextRun({ text: "Key Characteristics:", font: "Segoe UI", size: 22, bold: true })],
        }),
        bullet("Read-Only — agent gathers intelligence only; human makes all triage decisions"),
        bullet("Multi-Source — searches Azure DevOps, GitHub, Developer Community, and local code simultaneously"),
        bullet('Automated — say "@triage-intel triage first 10 items" and get structured intel reports'),
        bullet("Secure — no customer diagnostic data processed; PII protection built-in"),

        // ---- ARCHITECTURE ----
        heading("4. Architecture"),
        body("The system consists of a VS Code Copilot Agent that orchestrates four data sources:"),
        new Paragraph({ spacing: { after: 80 }, children: [] }),

        new Table({
          width: { size: 100, type: WidthType.PERCENTAGE },
          rows: [
            new TableRow({
              children: [
                cell("Component", { bold: true, shading: DARK_BLUE, color: WHITE }),
                cell("Technology", { bold: true, shading: DARK_BLUE, color: WHITE }),
                cell("Purpose", { bold: true, shading: DARK_BLUE, color: WHITE }),
              ],
            }),
            new TableRow({
              children: [
                cell("Azure DevOps MCP Server"),
                cell("REST API + Read-Only PAT"),
                cell("Fetch untriaged work items"),
              ],
            }),
            new TableRow({
              children: [
                cell("GitHub MCP Server", { shading: LIGHT_GRAY }),
                cell("GitHub API", { shading: LIGHT_GRAY }),
                cell("Search issues and PRs", { shading: LIGHT_GRAY }),
              ],
            }),
            new TableRow({
              children: [
                cell("Developer Community"),
                cell("Web search"),
                cell("Find similar reported issues"),
              ],
            }),
            new TableRow({
              children: [
                cell("Local Roslyn Workspace", { shading: LIGHT_GRAY }),
                cell("grep, semantic search, git log", { shading: LIGHT_GRAY }),
                cell("Find responsible code and PRs", { shading: LIGHT_GRAY }),
              ],
            }),
          ],
        }),

        // ---- AGENT CAPABILITIES ----
        heading("5. Agent Capabilities"),
        heading("What the Agent CAN Do", HeadingLevel.HEADING_2),
        bullet("Read work item metadata (title, description, repro steps) from Azure DevOps"),
        bullet("Search Developer Community for similar feedback"),
        bullet("Search GitHub dotnet/roslyn for related issues and PRs"),
        bullet("Search Roslyn source code for relevant files"),
        bullet("Search git history for related commits"),
        bullet("Classify feedback into Roslyn area vs. non-Roslyn"),
        bullet("Suggest area path for transfer candidates"),
        bullet("Generate structured intel reports"),

        heading("What the Agent CANNOT Do (By Design)", HeadingLevel.HEADING_2),
        bullet("Update work items in Azure DevOps", { color: ACCENT_RED }),
        bullet("Download or analyze customer attachments", { color: ACCENT_RED }),
        bullet("Access ETL traces, dump files, or screenshots", { color: ACCENT_RED }),
        bullet("Create or modify GitHub issues", { color: ACCENT_RED }),
        bullet("Assign work items to individuals", { color: ACCENT_RED }),
        bullet("Close or resolve feedback items", { color: ACCENT_RED }),

        // ---- TRIAGE WORKFLOW ----
        heading("6. Triage Workflow (Per Item)"),
        new Paragraph({
          spacing: { after: 60 },
          children: [
            new TextRun({ text: "Step 1 — FETCH: ", font: "Segoe UI", size: 22, bold: true, color: ACCENT_ORANGE }),
            new TextRun({ text: "Get work item metadata from Azure DevOps", font: "Segoe UI", size: 22 }),
          ],
        }),
        new Paragraph({
          spacing: { after: 60 },
          children: [
            new TextRun({ text: "Step 2 — SEARCH: ", font: "Segoe UI", size: 22, bold: true, color: BRAND_BLUE }),
            new TextRun({ text: "Developer Community for similar reports", font: "Segoe UI", size: 22 }),
          ],
        }),
        new Paragraph({
          spacing: { after: 60 },
          children: [
            new TextRun({ text: "Step 3 — SEARCH: ", font: "Segoe UI", size: 22, bold: true, color: ACCENT_GREEN }),
            new TextRun({ text: "GitHub dotnet/roslyn for related issues & PRs", font: "Segoe UI", size: 22 }),
          ],
        }),
        new Paragraph({
          spacing: { after: 60 },
          children: [
            new TextRun({ text: "Step 4 — CLASSIFY: ", font: "Segoe UI", size: 22, bold: true, color: DARK_BLUE }),
            new TextRun({ text: "Roslyn area or transfer to another team", font: "Segoe UI", size: 22 }),
          ],
        }),
        new Paragraph({
          spacing: { after: 60 },
          children: [
            new TextRun({ text: "Step 5 — CODE: ", font: "Segoe UI", size: 22, bold: true, color: BRAND_BLUE }),
            new TextRun({ text: "Find responsible source files in Roslyn repo", font: "Segoe UI", size: 22 }),
          ],
        }),
        new Paragraph({
          spacing: { after: 60 },
          children: [
            new TextRun({ text: "Step 6 — HISTORY: ", font: "Segoe UI", size: 22, bold: true, color: ACCENT_ORANGE }),
            new TextRun({ text: "Find related PRs and commit authors", font: "Segoe UI", size: 22 }),
          ],
        }),
        new Paragraph({
          spacing: { after: 120 },
          children: [
            new TextRun({ text: "Step 7 — REPORT: ", font: "Segoe UI", size: 22, bold: true, color: ACCENT_GREEN }),
            new TextRun({ text: "Compile structured intel for human reviewer", font: "Segoe UI", size: 22 }),
          ],
        }),

        // ---- PII PROTECTION ----
        heading("7. PII & Customer Data Protection"),

        heading("Data Classification", HeadingLevel.HEADING_2),

        new Table({
          width: { size: 100, type: WidthType.PERCENTAGE },
          rows: [
            new TableRow({
              children: [
                cell("Data Type", { bold: true, shading: DARK_BLUE, color: WHITE }),
                cell("Risk Level", { bold: true, shading: DARK_BLUE, color: WHITE }),
                cell("Agent Access", { bold: true, shading: DARK_BLUE, color: WHITE }),
              ],
            }),
            new TableRow({
              children: [
                cell("Title / Description / Repro Steps"),
                cell("Low", { color: ACCENT_GREEN }),
                cell("Allowed", { color: ACCENT_GREEN }),
              ],
            }),
            new TableRow({
              children: [
                cell("Area Path / State / Tags", { shading: LIGHT_GRAY }),
                cell("None", { color: ACCENT_GREEN, shading: LIGHT_GRAY }),
                cell("Allowed", { color: ACCENT_GREEN, shading: LIGHT_GRAY }),
              ],
            }),
            new TableRow({
              children: [
                cell("Customer Screenshots"),
                cell("High", { color: ACCENT_RED, bold: true }),
                cell("BLOCKED", { color: ACCENT_RED, bold: true }),
              ],
            }),
            new TableRow({
              children: [
                cell("ETL Trace Files", { shading: LIGHT_GRAY }),
                cell("High", { color: ACCENT_RED, bold: true, shading: LIGHT_GRAY }),
                cell("BLOCKED", { color: ACCENT_RED, bold: true, shading: LIGHT_GRAY }),
              ],
            }),
            new TableRow({
              children: [
                cell("Dump Files"),
                cell("Critical", { color: ACCENT_RED, bold: true }),
                cell("BLOCKED", { color: ACCENT_RED, bold: true }),
              ],
            }),
          ],
        }),

        heading("Protection Measures", HeadingLevel.HEADING_2),
        bullet("Read-only PAT — no write access to Azure DevOps, even if token is compromised"),
        bullet("No attachment endpoints — MCP server never calls the attachments API"),
        bullet("Field allowlist — only safe metadata fields are returned to the agent"),
        bullet("PII scrubbing — optional stripping of emails and names from text fields"),
        bullet("Local processing — all code search runs on developer machine only"),
        bullet("PAT stored in environment variables — never committed to source control"),

        heading("Compliance Alignment", HeadingLevel.HEADING_3),
        bullet("Follows Microsoft Privacy & Data Handling policies for internal tooling"),
        bullet("Customer diagnostic data never sent to AI/LLM services"),
        bullet("Work item metadata (title, description) is low-risk for internal search"),
        bullet("Agent outputs (intel reports) are transient chat responses — no persistent storage"),

        // ---- WHAT TO BUILD ----
        heading("8. What Needs to Be Built"),

        heading("Component 1: Azure DevOps MCP Server", HeadingLevel.HEADING_2),
        body("Lightweight Node.js/TypeScript server wrapping Azure DevOps REST API. Enforces field allowlist and PII scrubbing. Estimated size: ~300–400 lines of TypeScript."),

        heading("Component 2: Triage Intel Agent Definition", HeadingLevel.HEADING_2),
        body("Agent markdown file (.agent.md) defining the triage workflow, classification rules, area path mappings, and output format templates. Estimated size: ~100 lines."),

        heading("Component 3: VS Code MCP Configuration", HeadingLevel.HEADING_2),
        body("MCP server registration in VS Code settings connecting the PAT, organization, and project. Estimated size: ~15 lines of JSON."),

        // ---- NEXT STEPS ----
        heading("9. Next Steps"),
        new Paragraph({
          numbering: { reference: "next-steps", level: 0 },
          spacing: { after: 60 },
          children: [new TextRun({ text: "Generate a read-only PAT on devdiv.visualstudio.com", font: "Segoe UI", size: 22 })],
        }),
        new Paragraph({
          numbering: { reference: "next-steps", level: 0 },
          spacing: { after: 60 },
          children: [new TextRun({ text: "Build the Azure DevOps MCP Server (~300 lines TypeScript)", font: "Segoe UI", size: 22 })],
        }),
        new Paragraph({
          numbering: { reference: "next-steps", level: 0 },
          spacing: { after: 60 },
          children: [new TextRun({ text: "Create the triage-intel agent definition file", font: "Segoe UI", size: 22 })],
        }),
        new Paragraph({
          numbering: { reference: "next-steps", level: 0 },
          spacing: { after: 60 },
          children: [new TextRun({ text: "Register the MCP server in VS Code settings", font: "Segoe UI", size: 22 })],
        }),
        new Paragraph({
          numbering: { reference: "next-steps", level: 0 },
          spacing: { after: 60 },
          children: [new TextRun({ text: "Test on 3–5 real feedback items interactively", font: "Segoe UI", size: 22 })],
        }),
        new Paragraph({
          numbering: { reference: "next-steps", level: 0 },
          spacing: { after: 60 },
          children: [new TextRun({ text: "Iterate on classification rules and search quality", font: "Segoe UI", size: 22 })],
        }),
      ],
    },
  ],
  numbering: {
    config: [
      {
        reference: "next-steps",
        levels: [
          {
            level: 0,
            format: docx.LevelFormat.DECIMAL,
            text: "%1.",
            alignment: AlignmentType.START,
            style: {
              paragraph: {
                indent: { left: convertInchesToTwip(0.5), hanging: convertInchesToTwip(0.25) },
              },
            },
          },
        ],
      },
    ],
  },
});

const outPath = "Roslyn-Triage-Automation-Plan.docx";
Packer.toBuffer(doc).then((buffer) => {
  fs.writeFileSync(outPath, buffer);
  console.log(`Word document saved: ${outPath}`);
}).catch((err) => {
  console.error("Error:", err);
});
