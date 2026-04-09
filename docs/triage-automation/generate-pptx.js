const PptxGenJS = require("pptxgenjs");

const pptx = new PptxGenJS();

// -- Theme colors --
const BRAND_BLUE = "0078D4";
const DARK_BLUE = "003C71";
const ACCENT_GREEN = "107C10";
const ACCENT_RED = "D13438";
const ACCENT_ORANGE = "FF8C00";
const LIGHT_GRAY = "F3F3F3";
const WHITE = "FFFFFF";
const TEXT_DARK = "1A1A1A";
const TEXT_MUTED = "555555";

pptx.author = "Roslyn Triage Team";
pptx.title = "Roslyn Feedback Triage Automation";
pptx.subject = "Automation Plan Overview";

// ============================================================
// SLIDE 1 — Title Slide
// ============================================================
let slide1 = pptx.addSlide();
slide1.background = { fill: BRAND_BLUE };

slide1.addText("Roslyn Feedback\nTriage Automation", {
  x: 0.8, y: 1.0, w: 8.4, h: 2.5,
  fontSize: 36, fontFace: "Segoe UI Semibold", color: WHITE,
  lineSpacingMultiple: 1.2,
});

slide1.addText("Intelligent Read-Only Triage Assistant", {
  x: 0.8, y: 3.4, w: 8.4, h: 0.6,
  fontSize: 18, fontFace: "Segoe UI", color: WHITE, italic: true,
});

slide1.addShape(pptx.shapes.RECTANGLE, {
  x: 0.8, y: 4.2, w: 2.0, h: 0.05, fill: { color: WHITE },
});

slide1.addText("March 2026  |  VS Code + Copilot Agent + MCP", {
  x: 0.8, y: 4.5, w: 8.4, h: 0.5,
  fontSize: 12, fontFace: "Segoe UI", color: WHITE,
});

// ============================================================
// SLIDE 2 — Problem Statement
// ============================================================
let slide2 = pptx.addSlide();
slide2.background = { fill: WHITE };

slide2.addShape(pptx.shapes.RECTANGLE, {
  x: 0, y: 0, w: 10, h: 0.8, fill: { color: DARK_BLUE },
});
slide2.addText("The Problem", {
  x: 0.8, y: 0.1, w: 8, h: 0.6,
  fontSize: 22, fontFace: "Segoe UI Semibold", color: WHITE,
});

const problems = [
  "Manual triage of VS feedback items is time-consuming and repetitive",
  "Each item requires searching across Azure DevOps, Developer Community, and GitHub",
  "Finding the responsible code and related PRs in the Roslyn repo is tedious",
  "Classifying items as Roslyn vs. non-Roslyn requires domain knowledge",
  "No single tool connects all these data sources today",
];

problems.forEach((text, i) => {
  slide2.addShape(pptx.shapes.OVAL, {
    x: 1.0, y: 1.3 + i * 0.72, w: 0.18, h: 0.18,
    fill: { color: ACCENT_RED },
  });
  slide2.addText(text, {
    x: 1.4, y: 1.2 + i * 0.72, w: 7.8, h: 0.5,
    fontSize: 14, fontFace: "Segoe UI", color: TEXT_DARK,
  });
});

// ============================================================
// SLIDE 3 — Solution Overview
// ============================================================
let slide3 = pptx.addSlide();
slide3.background = { fill: WHITE };

slide3.addShape(pptx.shapes.RECTANGLE, {
  x: 0, y: 0, w: 10, h: 0.8, fill: { color: DARK_BLUE },
});
slide3.addText("Solution Overview", {
  x: 0.8, y: 0.1, w: 8, h: 0.6,
  fontSize: 22, fontFace: "Segoe UI Semibold", color: WHITE,
});

slide3.addText("VS Code Copilot Agent + Azure DevOps MCP Server", {
  x: 0.8, y: 1.1, w: 8.4, h: 0.5,
  fontSize: 16, fontFace: "Segoe UI Semibold", color: BRAND_BLUE,
});

const solutionPoints = [
  { label: "Read-Only", desc: "Agent gathers intelligence only — human makes all triage decisions" },
  { label: "Multi-Source", desc: "Searches Azure DevOps, GitHub, Developer Community, and local code simultaneously" },
  { label: "Automated", desc: 'Say "@triage-intel triage first 10 items" and get structured intel reports' },
  { label: "Secure", desc: "No customer diagnostic data processed — PII protection built-in" },
];

solutionPoints.forEach((item, i) => {
  slide3.addShape(pptx.shapes.ROUNDED_RECTANGLE, {
    x: 0.8, y: 1.8 + i * 0.9, w: 1.5, h: 0.55,
    fill: { color: BRAND_BLUE }, rectRadius: 0.1,
  });
  slide3.addText(item.label, {
    x: 0.8, y: 1.8 + i * 0.9, w: 1.5, h: 0.55,
    fontSize: 11, fontFace: "Segoe UI Semibold", color: WHITE, align: "center", valign: "middle",
  });
  slide3.addText(item.desc, {
    x: 2.5, y: 1.8 + i * 0.9, w: 6.7, h: 0.55,
    fontSize: 13, fontFace: "Segoe UI", color: TEXT_DARK, valign: "middle",
  });
});

// ============================================================
// SLIDE 4 — Architecture
// ============================================================
let slide4 = pptx.addSlide();
slide4.background = { fill: WHITE };

slide4.addShape(pptx.shapes.RECTANGLE, {
  x: 0, y: 0, w: 10, h: 0.8, fill: { color: DARK_BLUE },
});
slide4.addText("Architecture", {
  x: 0.8, y: 0.1, w: 8, h: 0.6,
  fontSize: 22, fontFace: "Segoe UI Semibold", color: WHITE,
});

// Central agent box
slide4.addShape(pptx.shapes.ROUNDED_RECTANGLE, {
  x: 2.8, y: 1.2, w: 4.4, h: 1.2,
  fill: { color: BRAND_BLUE }, rectRadius: 0.15,
  shadow: { type: "outer", blur: 6, offset: 2, color: "999999" },
});
slide4.addText("VS Code Copilot Agent\n(triage-intel)", {
  x: 2.8, y: 1.2, w: 4.4, h: 1.2,
  fontSize: 14, fontFace: "Segoe UI Semibold", color: WHITE, align: "center", valign: "middle",
});

// Four data source boxes
const sources = [
  { x: 0.3, label: "Azure DevOps\nMCP Server", color: ACCENT_ORANGE },
  { x: 2.7, label: "GitHub\nMCP Server", color: ACCENT_GREEN },
  { x: 5.1, label: "Developer\nCommunity", color: BRAND_BLUE },
  { x: 7.5, label: "Local Roslyn\nWorkspace", color: DARK_BLUE },
];

sources.forEach((s) => {
  slide4.addShape(pptx.shapes.ROUNDED_RECTANGLE, {
    x: s.x, y: 3.2, w: 2.2, h: 0.9,
    fill: { color: s.color }, rectRadius: 0.1,
  });
  slide4.addText(s.label, {
    x: s.x, y: 3.2, w: 2.2, h: 0.9,
    fontSize: 11, fontFace: "Segoe UI Semibold", color: WHITE, align: "center", valign: "middle",
  });
  // Arrow line
  slide4.addShape(pptx.shapes.LINE, {
    x: s.x + 1.1, y: 2.4, w: 0, h: 0.8,
    line: { color: "999999", width: 1.5, dashType: "dash" },
  });
});

// Bottom labels
const bottomLabels = [
  { x: 0.3, text: "REST API\n(Read-Only PAT)" },
  { x: 2.7, text: "Issues, PRs\nSearch" },
  { x: 5.1, text: "Similar Reports\nWeb Search" },
  { x: 7.5, text: "Code Search\ngit log" },
];

bottomLabels.forEach((b) => {
  slide4.addText(b.text, {
    x: b.x, y: 4.25, w: 2.2, h: 0.6,
    fontSize: 9, fontFace: "Segoe UI", color: TEXT_MUTED, align: "center",
  });
});

// ============================================================
// SLIDE 5 — Agent Capabilities
// ============================================================
let slide5 = pptx.addSlide();
slide5.background = { fill: WHITE };

slide5.addShape(pptx.shapes.RECTANGLE, {
  x: 0, y: 0, w: 10, h: 0.8, fill: { color: DARK_BLUE },
});
slide5.addText("Agent Capabilities", {
  x: 0.8, y: 0.1, w: 8, h: 0.6,
  fontSize: 22, fontFace: "Segoe UI Semibold", color: WHITE,
});

// CAN do
slide5.addText("What the Agent CAN Do", {
  x: 0.5, y: 1.0, w: 4.3, h: 0.4,
  fontSize: 14, fontFace: "Segoe UI Semibold", color: ACCENT_GREEN,
});

const canDo = [
  "Read work item metadata from Azure DevOps",
  "Search Developer Community for similar feedback",
  "Search GitHub for related issues and PRs",
  "Search Roslyn source code for relevant files",
  "Search git history for related commits",
  "Classify feedback (Roslyn vs. non-Roslyn)",
  "Suggest area path for transfers",
  "Generate structured intel reports",
];

canDo.forEach((text, i) => {
  slide5.addText("✓", {
    x: 0.5, y: 1.5 + i * 0.42, w: 0.3, h: 0.35,
    fontSize: 13, fontFace: "Segoe UI", color: ACCENT_GREEN, bold: true,
  });
  slide5.addText(text, {
    x: 0.9, y: 1.5 + i * 0.42, w: 4.0, h: 0.35,
    fontSize: 11, fontFace: "Segoe UI", color: TEXT_DARK,
  });
});

// CANNOT do
slide5.addText("What the Agent CANNOT Do", {
  x: 5.2, y: 1.0, w: 4.3, h: 0.4,
  fontSize: 14, fontFace: "Segoe UI Semibold", color: ACCENT_RED,
});

const cannotDo = [
  "Update work items in Azure DevOps",
  "Download customer attachments",
  "Access ETL, dump files, or screenshots",
  "Create or modify GitHub issues",
  "Assign work items to people",
  "Close or resolve feedback items",
];

cannotDo.forEach((text, i) => {
  slide5.addText("✗", {
    x: 5.2, y: 1.5 + i * 0.42, w: 0.3, h: 0.35,
    fontSize: 13, fontFace: "Segoe UI", color: ACCENT_RED, bold: true,
  });
  slide5.addText(text, {
    x: 5.6, y: 1.5 + i * 0.42, w: 4.0, h: 0.35,
    fontSize: 11, fontFace: "Segoe UI", color: TEXT_DARK,
  });
});

// ============================================================
// SLIDE 6 — Triage Data Flow
// ============================================================
let slide6 = pptx.addSlide();
slide6.background = { fill: WHITE };

slide6.addShape(pptx.shapes.RECTANGLE, {
  x: 0, y: 0, w: 10, h: 0.8, fill: { color: DARK_BLUE },
});
slide6.addText("Triage Workflow (Per Item)", {
  x: 0.8, y: 0.1, w: 8, h: 0.6,
  fontSize: 22, fontFace: "Segoe UI Semibold", color: WHITE,
});

const steps = [
  { num: "1", title: "FETCH", desc: "Get work item metadata from Azure DevOps", color: ACCENT_ORANGE },
  { num: "2", title: "SEARCH", desc: "Developer Community for similar reports", color: BRAND_BLUE },
  { num: "3", title: "SEARCH", desc: "GitHub dotnet/roslyn for related issues & PRs", color: ACCENT_GREEN },
  { num: "4", title: "CLASSIFY", desc: "Roslyn area or transfer to another team", color: DARK_BLUE },
  { num: "5", title: "CODE", desc: "Find responsible source files in Roslyn repo", color: BRAND_BLUE },
  { num: "6", title: "HISTORY", desc: "Find related PRs and commit authors", color: ACCENT_ORANGE },
  { num: "7", title: "REPORT", desc: "Compile structured intel for human reviewer", color: ACCENT_GREEN },
];

steps.forEach((step, i) => {
  const yPos = 1.1 + i * 0.58;

  // Number circle
  slide6.addShape(pptx.shapes.OVAL, {
    x: 0.8, y: yPos, w: 0.45, h: 0.45,
    fill: { color: step.color },
  });
  slide6.addText(step.num, {
    x: 0.8, y: yPos, w: 0.45, h: 0.45,
    fontSize: 14, fontFace: "Segoe UI Semibold", color: WHITE, align: "center", valign: "middle",
  });

  // Step title
  slide6.addText(step.title, {
    x: 1.5, y: yPos, w: 1.5, h: 0.45,
    fontSize: 12, fontFace: "Segoe UI Semibold", color: step.color, valign: "middle",
  });

  // Step description
  slide6.addText(step.desc, {
    x: 3.0, y: yPos, w: 6.2, h: 0.45,
    fontSize: 12, fontFace: "Segoe UI", color: TEXT_DARK, valign: "middle",
  });

  // Connector line
  if (i < steps.length - 1) {
    slide6.addShape(pptx.shapes.LINE, {
      x: 1.025, y: yPos + 0.45, w: 0, h: 0.13,
      line: { color: "CCCCCC", width: 1.5 },
    });
  }
});

// ============================================================
// SLIDE 7 — PII & Data Protection
// ============================================================
let slide7 = pptx.addSlide();
slide7.background = { fill: WHITE };

slide7.addShape(pptx.shapes.RECTANGLE, {
  x: 0, y: 0, w: 10, h: 0.8, fill: { color: ACCENT_RED },
});
slide7.addText("PII & Customer Data Protection", {
  x: 0.8, y: 0.1, w: 8, h: 0.6,
  fontSize: 22, fontFace: "Segoe UI Semibold", color: WHITE,
});

// Data classification table
const tableRows = [
  [
    { text: "Data Type", options: { bold: true, color: WHITE, fill: { color: DARK_BLUE }, fontSize: 11, fontFace: "Segoe UI Semibold" } },
    { text: "Risk Level", options: { bold: true, color: WHITE, fill: { color: DARK_BLUE }, fontSize: 11, fontFace: "Segoe UI Semibold" } },
    { text: "Agent Access", options: { bold: true, color: WHITE, fill: { color: DARK_BLUE }, fontSize: 11, fontFace: "Segoe UI Semibold" } },
  ],
  [
    { text: "Title / Description / Repro Steps", options: { fontSize: 10, fontFace: "Segoe UI" } },
    { text: "Low", options: { fontSize: 10, fontFace: "Segoe UI", color: ACCENT_GREEN } },
    { text: "✓  Allowed", options: { fontSize: 10, fontFace: "Segoe UI", color: ACCENT_GREEN } },
  ],
  [
    { text: "Area Path / State / Tags", options: { fontSize: 10, fontFace: "Segoe UI" } },
    { text: "None", options: { fontSize: 10, fontFace: "Segoe UI", color: ACCENT_GREEN } },
    { text: "✓  Allowed", options: { fontSize: 10, fontFace: "Segoe UI", color: ACCENT_GREEN } },
  ],
  [
    { text: "Customer Screenshots", options: { fontSize: 10, fontFace: "Segoe UI" } },
    { text: "High", options: { fontSize: 10, fontFace: "Segoe UI", color: ACCENT_ORANGE, bold: true } },
    { text: "✗  Blocked", options: { fontSize: 10, fontFace: "Segoe UI", color: ACCENT_RED, bold: true } },
  ],
  [
    { text: "ETL Trace Files", options: { fontSize: 10, fontFace: "Segoe UI" } },
    { text: "High", options: { fontSize: 10, fontFace: "Segoe UI", color: ACCENT_ORANGE, bold: true } },
    { text: "✗  Blocked", options: { fontSize: 10, fontFace: "Segoe UI", color: ACCENT_RED, bold: true } },
  ],
  [
    { text: "Dump Files", options: { fontSize: 10, fontFace: "Segoe UI" } },
    { text: "Critical", options: { fontSize: 10, fontFace: "Segoe UI", color: ACCENT_RED, bold: true } },
    { text: "✗  Blocked", options: { fontSize: 10, fontFace: "Segoe UI", color: ACCENT_RED, bold: true } },
  ],
];

slide7.addTable(tableRows, {
  x: 0.5, y: 1.0, w: 9.0,
  border: { type: "solid", pt: 0.5, color: "CCCCCC" },
  rowH: [0.4, 0.35, 0.35, 0.35, 0.35, 0.35],
  colW: [4.0, 2.0, 3.0],
});

// Protection measures
slide7.addText("Key Protection Measures", {
  x: 0.5, y: 3.4, w: 9, h: 0.4,
  fontSize: 14, fontFace: "Segoe UI Semibold", color: DARK_BLUE,
});

const measures = [
  "Read-only PAT — no write access to Azure DevOps, even if token is compromised",
  "No attachment endpoints — MCP server never calls the attachments API",
  "Field allowlist — only safe metadata fields are returned to the agent",
  "PII scrubbing — optional stripping of emails and names from text fields",
  "Local processing — all code search runs on developer machine only",
  "PAT stored in environment variables — never committed to source control",
];

measures.forEach((text, i) => {
  slide7.addShape(pptx.shapes.ROUNDED_RECTANGLE, {
    x: 0.5, y: 3.9 + i * 0.38, w: 0.22, h: 0.22,
    fill: { color: ACCENT_GREEN }, rectRadius: 0.03,
  });
  slide7.addText(text, {
    x: 0.9, y: 3.85 + i * 0.38, w: 8.5, h: 0.34,
    fontSize: 10, fontFace: "Segoe UI", color: TEXT_DARK,
  });
});

// ============================================================
// SLIDE 8 — What Needs to Be Built
// ============================================================
let slide8 = pptx.addSlide();
slide8.background = { fill: WHITE };

slide8.addShape(pptx.shapes.RECTANGLE, {
  x: 0, y: 0, w: 10, h: 0.8, fill: { color: DARK_BLUE },
});
slide8.addText("What We Need to Build", {
  x: 0.8, y: 0.1, w: 8, h: 0.6,
  fontSize: 22, fontFace: "Segoe UI Semibold", color: WHITE,
});

const components = [
  {
    title: "Azure DevOps MCP Server",
    desc: "Lightweight Node.js/TypeScript server wrapping Azure DevOps REST API with field filtering and PII scrubbing",
    size: "~300–400 lines",
    color: ACCENT_ORANGE,
  },
  {
    title: "Triage Intel Agent Definition",
    desc: "Agent markdown file defining workflow, classification rules, area mappings, and output templates",
    size: "~100 lines",
    color: BRAND_BLUE,
  },
  {
    title: "VS Code MCP Configuration",
    desc: "MCP server registration in VS Code settings connecting PAT and target org/project",
    size: "~15 lines",
    color: ACCENT_GREEN,
  },
];

components.forEach((comp, i) => {
  const yPos = 1.1 + i * 1.4;

  slide8.addShape(pptx.shapes.ROUNDED_RECTANGLE, {
    x: 0.5, y: yPos, w: 9.0, h: 1.15,
    fill: { color: LIGHT_GRAY }, rectRadius: 0.1,
    line: { color: comp.color, width: 2 },
  });

  slide8.addShape(pptx.shapes.ROUNDED_RECTANGLE, {
    x: 0.7, y: yPos + 0.15, w: 0.25, h: 0.85,
    fill: { color: comp.color }, rectRadius: 0.05,
  });

  slide8.addText(comp.title, {
    x: 1.2, y: yPos + 0.1, w: 6.0, h: 0.4,
    fontSize: 14, fontFace: "Segoe UI Semibold", color: TEXT_DARK,
  });

  slide8.addText(comp.desc, {
    x: 1.2, y: yPos + 0.5, w: 6.5, h: 0.5,
    fontSize: 11, fontFace: "Segoe UI", color: TEXT_MUTED,
  });

  slide8.addShape(pptx.shapes.ROUNDED_RECTANGLE, {
    x: 7.8, y: yPos + 0.3, w: 1.4, h: 0.5,
    fill: { color: comp.color }, rectRadius: 0.08,
  });
  slide8.addText(comp.size, {
    x: 7.8, y: yPos + 0.3, w: 1.4, h: 0.5,
    fontSize: 10, fontFace: "Segoe UI Semibold", color: WHITE, align: "center", valign: "middle",
  });
});

// ============================================================
// SLIDE 9 — Next Steps
// ============================================================
let slide9 = pptx.addSlide();
slide9.background = { fill: BRAND_BLUE };

slide9.addText("Next Steps", {
  x: 0.8, y: 0.5, w: 8.4, h: 0.7,
  fontSize: 28, fontFace: "Segoe UI Semibold", color: WHITE,
});

slide9.addShape(pptx.shapes.RECTANGLE, {
  x: 0.8, y: 1.2, w: 2.0, h: 0.04, fill: { color: WHITE },
});

const nextSteps = [
  { num: "1", text: "Generate a read-only PAT on devdiv.visualstudio.com" },
  { num: "2", text: "Build the Azure DevOps MCP Server (~300 lines TypeScript)" },
  { num: "3", text: "Create the triage-intel agent definition file" },
  { num: "4", text: "Register the MCP server in VS Code settings" },
  { num: "5", text: "Test on 3–5 real feedback items interactively" },
  { num: "6", text: "Iterate on classification rules and search quality" },
];

nextSteps.forEach((step, i) => {
  slide9.addShape(pptx.shapes.OVAL, {
    x: 1.0, y: 1.6 + i * 0.7, w: 0.45, h: 0.45,
    fill: { color: WHITE },
  });
  slide9.addText(step.num, {
    x: 1.0, y: 1.6 + i * 0.7, w: 0.45, h: 0.45,
    fontSize: 16, fontFace: "Segoe UI Semibold", color: BRAND_BLUE, align: "center", valign: "middle",
  });
  slide9.addText(step.text, {
    x: 1.7, y: 1.6 + i * 0.7, w: 7.5, h: 0.45,
    fontSize: 14, fontFace: "Segoe UI", color: WHITE, valign: "middle",
  });
});

// ============================================================
// Save
// ============================================================
const outPath = "Roslyn-Triage-Automation-Plan.pptx";
pptx.writeFile({ fileName: outPath }).then(() => {
  console.log(`PowerPoint saved: ${outPath}`);
}).catch(err => {
  console.error("Error:", err);
});
