/* eslint-disable no-undef */
"use strict";

const $ = (sel, root = document) => root.querySelector(sel);
const $$ = (sel, root = document) => Array.from(root.querySelectorAll(sel));

// ---- Model selection state ----
const MODEL_STATE = {
  /** Currently chosen model id ("heuristic" or "<provider>/<model>"). */
  current: localStorage.getItem("triage-model") || "heuristic",
  /** Last fetched providers payload from /api/models. */
  providers: null,
};

function setCurrentModel(id, label) {
  MODEL_STATE.current = id;
  localStorage.setItem("triage-model", id);
  const btn = $("#model-button-label");
  if (btn) btn.textContent = label || (id === "heuristic" ? "Heuristic only" : id);
}

function findModelLabel(id) {
  if (!id || id === "heuristic") return "Heuristic only";
  const ps = MODEL_STATE.providers;
  if (!ps) return id;
  for (const p of ps) {
    const m = p.models?.find((x) => x.id === id);
    if (m) return `${m.label} — ${p.label}`;
  }
  return id;
}

// ---- DOM helpers ----
function el(tag, attrs = {}, ...children) {
  const node = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) {
    if (v == null || v === false) continue;
    if (k === "class") node.className = v;
    else if (k === "dataset") Object.assign(node.dataset, v);
    else if (k === "style") Object.assign(node.style, v);
    else if (k.startsWith("on") && typeof v === "function") node.addEventListener(k.slice(2).toLowerCase(), v);
    else node.setAttribute(k, v === true ? "" : v);
  }
  for (const c of children.flat()) {
    if (c == null || c === false) continue;
    node.appendChild(typeof c === "string" || typeof c === "number" ? document.createTextNode(String(c)) : c);
  }
  return node;
}

function fmtElapsed(ms) {
  if (ms == null) return "";
  if (ms < 1000) return `${ms} ms`;
  return `${(ms / 1000).toFixed(2)} s`;
}

// ---- ID parsing (mirror of server) ----
function parseId(raw) {
  if (!raw) return null;
  const t = String(raw).trim();
  if (!t) return null;
  if (/^\d+$/.test(t)) return Number(t);
  const azdo = t.match(/_workitems\/edit\/(\d+)/i);
  if (azdo) return Number(azdo[1]);
  const dc = t.match(/developercommunity[^?#]*?\/(\d+)(?:\D|$)/i);
  if (dc) return Number(dc[1]);
  const fb = t.match(/(\d{4,})(?!.*\d{4,})/);
  return fb ? Number(fb[1]) : null;
}

// ---- Badge tone helpers ----
function badge(label, tone = "slate", title) {
  return el("span", { class: "badge", "data-tone": tone, title: title || label }, label);
}
function stateTone(state) {
  const s = String(state || "").toLowerCase();
  if (["new", "proposed"].includes(s)) return "amber";
  if (s.includes("active") || s.includes("investigation") || s.includes("committed") || s.includes("approved")) return "blue";
  if (["resolved", "completed", "fixed"].includes(s) || s.includes("fixed")) return "emerald";
  if (["closed", "removed", "cut", "open"].includes(s)) return "slate";
  return "violet";
}
function severityTone(sev) {
  const m = String(sev || "").match(/^(\d)/);
  const n = m ? Number(m[1]) : null;
  if (n === 1) return "red";
  if (n === 2) return "orange";
  if (n === 3) return "amber";
  return "slate";
}
function priorityTone(p) {
  const n = Number(p);
  if (n === 1) return "red";
  if (n === 2) return "orange";
  if (n === 3) return "amber";
  return "slate";
}
function simTone(p) {
  if (p >= 0.6) return "red";
  if (p >= 0.4) return "orange";
  if (p >= 0.25) return "amber";
  return "slate";
}
function confTone(p) {
  if (p >= 0.75) return "emerald";
  if (p >= 0.5)  return "amber";
  return "red";
}
function confLabel(p) {
  if (p >= 0.75) return "High confidence";
  if (p >= 0.5)  return "Medium confidence";
  return "Low confidence";
}

function renderHeaderBadges(badgeList) {
  return (badgeList || []).map((b) => {
    const idx = b.indexOf(":");
    const k = idx > 0 ? b.slice(0, idx) : "tag";
    const v = idx > 0 ? b.slice(idx + 1) : b;
    let tone = "slate", label = v;
    if (k === "type")          tone = "violet";
    else if (k === "state")    tone = stateTone(v);
    else if (k === "sev")      { tone = severityTone(v); label = `Sev ${v}`; }
    else if (k === "pri")      { tone = priorityTone(v); label = `P${v}`; }
    else if (k === "source")   tone = "pink";
    else if (k === "product")  tone = "blue";
    else if (k === "votes")    { tone = "amber"; label = `▲ ${v}`; }
    else if (k === "area")     tone = "cyan";
    return badge(label, tone, b);
  });
}

// ---- Pipeline rendering ----
const pipelineEl = () => $("#pipeline");
const resultsEl  = () => $("#results");
const statusEl   = () => $("#status");

function showStatus(kind, msg) {
  const s = statusEl();
  s.hidden = false;
  s.className = `status ${kind}`;
  s.textContent = "";
  if (kind === "loading") {
    s.append(el("div", { class: "spinner" }), document.createTextNode(msg));
  } else {
    s.textContent = msg;
  }
}
function hideStatus() { statusEl().hidden = true; }

function startPipeline(id) {
  const p = pipelineEl();
  p.hidden = false;
  p.innerHTML = "";
  resultsEl().innerHTML = "";
  hideStatus();
  const tpl = $("#tpl-pipeline").content.cloneNode(true);
  const card = tpl.querySelector(".pipeline-card");
  $("[data-bind='title']", card).textContent = `Triaging #${id}…`;
  $("[data-bind='sub']", card).textContent = "Streaming progress from the MCP server";
  const overall = $("[data-bind='overall']", card);
  overall.dataset.tone = "blue";
  overall.textContent = "running";
  p.appendChild(tpl);
  return card;
}

function upsertStep(card, ev) {
  const list = $("[data-bind='steps']", card);
  let li = list.querySelector(`li.step[data-step="${ev.step}"]`);
  if (!li) {
    const tpl = $("#tpl-step").content.cloneNode(true);
    li = tpl.querySelector(".step");
    li.dataset.step = ev.step;
    list.appendChild(tpl);
  }
  li.dataset.status = ev.status;
  li.dataset.mode = ev.mode || "heuristic";
  $("[data-bind='label']", li).textContent = ev.label;
  $("[data-bind='detail']", li).textContent = ev.detail || "";
  const elapsed = $("[data-bind='elapsed']", li);
  elapsed.textContent = ev.status === "queued" ? "" : fmtElapsed(ev.elapsedMs);

  const modeEl = $("[data-bind='mode']", li);
  if (modeEl) {
    if (ev.mode === "llm") {
      modeEl.hidden = false;
      const usage = ev.usage
        ? ` · ${ev.usage.promptTokens ?? "?"} in / ${ev.usage.completionTokens ?? "?"} out`
        : "";
      modeEl.innerHTML = `<span class="mode-icon">🤖</span><span class="mode-label">${ev.model || "LLM"}${usage}</span>`;
    } else {
      modeEl.hidden = true;
      modeEl.textContent = "";
    }
  }

  const marker = li.querySelector(".step-marker");
  marker.innerHTML = "";
  if (ev.status === "running") marker.appendChild(el("span", { class: "step-spinner" }));
  else if (ev.status === "done") marker.textContent = "✓";
  else if (ev.status === "skipped") marker.textContent = "–";
  else if (ev.status === "error") marker.textContent = "!";
  else marker.textContent = "·";

  // Hide stream pane once the step is no longer running (keep contents visible if any).
  const stream = $("[data-bind='stream']", li);
  if (stream && ev.status !== "running" && !stream.textContent) {
    stream.hidden = true;
  }
}

function appendStreamToken(card, ev) {
  const li = card.querySelector(`li.step[data-step="${ev.step}"]`);
  if (!li) return;
  const stream = $("[data-bind='stream']", li);
  if (!stream) return;
  stream.hidden = false;
  stream.textContent += ev.delta || "";
  // Auto-scroll the pane.
  stream.scrollTop = stream.scrollHeight;
}

function finishPipeline(card, kind, msg) {
  const overall = $("[data-bind='overall']", card);
  overall.textContent = kind;
  overall.dataset.tone = kind === "complete" ? "emerald" : kind === "error" ? "red" : "amber";
  if (msg) $("[data-bind='sub']", card).textContent = msg;
}

// ---- Report rendering (final result event) ----

function renderClassification(c) {
  const wrap = el("div", { class: "classif" });

  wrap.appendChild(el("div", { class: "classif-headline" },
    badge(c.isRoslyn ? "Roslyn" : "NOT Roslyn", c.isRoslyn ? "emerald" : "red"),
    badge(c.area || "Unknown", "cyan"),
    badge(`${Math.round((c.confidence ?? 0) * 100)}%`, confTone(c.confidence ?? 0)),
    badge(confLabel(c.confidence ?? 0), confTone(c.confidence ?? 0)),
  ));

  if (c.suggestedTransfer) {
    wrap.appendChild(el("div", { class: "transfer" },
      el("strong", {}, "Suggested transfer: "),
      el("span", {}, c.suggestedTransfer),
    ));
  }
  if (c.rawAreaPath) {
    wrap.appendChild(el("div", { class: "subtle" }, `Area path: ${c.rawAreaPath}`));
  }
  if (c.reasons?.length) {
    wrap.appendChild(el("ul", { class: "reasons" }, c.reasons.map((r) => el("li", {}, r))));
  }
  return wrap;
}

function simBarColor(p) {
  return ({
    red: "var(--c-red)",
    orange: "var(--c-orange)",
    amber: "var(--c-amber)",
    slate: "var(--c-slate)",
  })[simTone(p)];
}

function renderDuplicates(dups) {
  if (!dups || dups.length === 0) return el("div", { class: "empty" }, "No likely duplicates found");
  return el("div", { class: "dup-list" },
    dups.map((d) => {
      const pct = Math.round((d.similarity ?? 0) * 100);
      const numberLabel = d.number ? `#${d.number}` : "?";
      const stateBadge = d.state ? badge(d.state, stateTone(d.state)) : null;
      const kindBadge = d.isPr
        ? badge("PR", "violet")
        : badge(d.source === "comments" ? "comment" : "issue", "slate");
      const link = d.url
        ? el("a", { href: d.url, target: "_blank", rel: "noreferrer noopener", class: "dup-title" }, d.title)
        : el("span", { class: "dup-title" }, d.title);
      return el("div", { class: "dup-item" },
        el("div", { class: "dup-row" },
          badge(`${pct}% match`, simTone(d.similarity ?? 0)),
          badge(numberLabel, "slate"),
          kindBadge,
          stateBadge,
        ),
        link,
        el("div", { class: "sim-bar" },
          el("div", { class: "sim-fill", style: { width: `${Math.max(4, pct)}%`, background: simBarColor(d.similarity ?? 0) } }),
        ),
      );
    }),
  );
}

function renderCodePaths(paths) {
  if (!paths || paths.length === 0) return el("div", { class: "empty" }, "No related files found");
  return el("ul", { class: "code-files" },
    paths.map((p) => el("li", {},
      el("code", {}, p.file),
      el("span", { class: "subtle" }, ` matched: ${p.matchedTerms.join(", ")}`),
    )),
  );
}

function renderPullRequests(commits) {
  if (!commits || commits.length === 0) return el("div", { class: "empty" }, "No related PRs / commits");
  return el("ul", { class: "pr-items" },
    commits.map((c) => el("li", {},
      el("span", { class: "pr-sha" }, c.sha),
      el("span", { class: "pr-date" }, c.date),
      c.pr ? el("a", { href: c.prUrl, target: "_blank", rel: "noreferrer noopener", class: "pr-link" }, ` #${c.pr}`) : null,
      el("span", { class: "pr-subject" }, " — " + c.subject),
      el("div", { class: "subtle pr-file" }, c.file),
    )),
  );
}

function renderRootCause(rc) {
  if (!rc) return el("div", { class: "empty" }, "No root-cause language detected in comments");
  return el("div", { class: "rc" },
    el("div", { class: "rc-line" }, badge("hint", "violet"), el("strong", {}, rc.description)),
    rc.prRefs?.length ? el("div", { class: "rc-prs" },
      el("strong", {}, "Linked PRs/issues: "),
      rc.prRefs.map((n) => el("a", {
        href: `https://github.com/dotnet/roslyn/pull/${n}`,
        target: "_blank", rel: "noreferrer noopener", class: "pr-link",
      }, ` #${n}`)),
    ) : null,
    rc.evidence?.length ? el("ul", { class: "rc-evidence" },
      rc.evidence.map((e) => el("li", {}, e)),
    ) : null,
  );
}

function renderExpectedActual(ea) {
  if (!ea || (!ea.expected && !ea.actual)) return null;
  const out = el("div", { class: "ea-grid" });
  if (ea.expected) out.appendChild(el("div", { class: "ea-cell" },
    el("h4", {}, "Expected"), el("p", {}, ea.expected)));
  if (ea.actual)   out.appendChild(el("div", { class: "ea-cell ea-actual" },
    el("h4", {}, "Actual"), el("p", {}, ea.actual)));
  return out;
}

function renderReport(report) {
  const tpl = $("#tpl-report").content.cloneNode(true);
  const root = tpl.querySelector(".report");

  $("[data-bind='id']", root).textContent = `#${report.id}`;
  $("[data-bind='title']", root).textContent = report.title || "(no title)";

  const az = $("[data-bind-href='azdoLink']", root); az.href = report.azdoLink;
  const dc = $("[data-bind-href='devCommunityLink']", root);
  if (report.devCommunityLink) dc.href = report.devCommunityLink;
  else dc.remove();

  const badgeRow = $("[data-bind='badges']", root);
  renderHeaderBadges(report.badges).forEach((b) => badgeRow.appendChild(b));

  // Summary
  $("[data-bind='summary']", root).textContent = report.summary || "(no summary)";
  const eaSlot = $("[data-bind='expectedActual']", root);
  const eaNode = renderExpectedActual(report.expectedActual);
  if (eaNode) eaSlot.replaceWith(eaNode); else eaSlot.remove();

  // Classification
  $("[data-bind='classifConfidence']", root).textContent = `${Math.round((report.classification?.confidence ?? 0) * 100)}%`;
  $("[data-bind='classification']", root).replaceWith(renderClassification(report.classification || {}));

  // Duplicates
  $("[data-bind='dupCount']", root).textContent = `${report.duplicates?.length ?? 0}`;
  $("[data-bind='duplicates']", root).replaceWith(renderDuplicates(report.duplicates || []));

  // Root cause
  $("[data-bind='rcStatus']", root).textContent = report.rootCause ? "found" : "none";
  $("[data-bind='rootCause']", root).replaceWith(renderRootCause(report.rootCause));

  // Code paths
  $("[data-bind='codeCount']", root).textContent = `${report.codePaths?.length ?? 0}`;
  $("[data-bind='codePaths']", root).replaceWith(renderCodePaths(report.codePaths || []));

  // PRs / commits
  $("[data-bind='prCount']", root).textContent = `${report.pullRequests?.length ?? 0}`;
  $("[data-bind='pullRequests']", root).replaceWith(renderPullRequests(report.pullRequests || []));

  return tpl;
}

// ---- SSE driver ----
function runTriage(input) {
  const id = parseId(input);
  if (!id) {
    showStatus("error", "Could not parse a work item ID from that input.");
    return;
  }
  const card = startPipeline(id);
  let eventSource;
  const url = MODEL_STATE.current && MODEL_STATE.current !== "heuristic"
    ? `/api/triage/${id}?model=${encodeURIComponent(MODEL_STATE.current)}`
    : `/api/triage/${id}`;
  try {
    eventSource = new EventSource(url);
  } catch (err) {
    finishPipeline(card, "error", err.message);
    return;
  }

  eventSource.addEventListener("step", (e) => {
    try { upsertStep(card, JSON.parse(e.data)); } catch { /* ignore */ }
  });
  eventSource.addEventListener("token", (e) => {
    try { appendStreamToken(card, JSON.parse(e.data)); } catch { /* ignore */ }
  });
  eventSource.addEventListener("result", (e) => {
    try {
      const report = JSON.parse(e.data);
      resultsEl().innerHTML = "";
      resultsEl().appendChild(renderReport(report));
      const lastElapsed = report.steps?.length ? report.steps[report.steps.length - 1].elapsedMs : null;
      finishPipeline(card, "complete", `Done in ${fmtElapsed(lastElapsed)}.`);
    } catch (err) {
      finishPipeline(card, "error", err.message);
    }
  });
  eventSource.addEventListener("error", (e) => {
    let msg = "Stream error";
    try { if (e.data) msg = JSON.parse(e.data).message || msg; } catch { /* ignore */ }
    finishPipeline(card, "error", msg);
    eventSource.close();
  });
  eventSource.addEventListener("done", () => eventSource.close());
}

// ---- Batch tab (clicking a row drills into single triage) ----
async function api(path) {
  const resp = await fetch(path);
  const ct = resp.headers.get("content-type") || "";
  const data = ct.includes("json") ? await resp.json() : await resp.text();
  if (!resp.ok) {
    const msg = (data && typeof data === "object" && data.error) ? data.error : (typeof data === "string" ? data : `HTTP ${resp.status}`);
    throw new Error(msg);
  }
  return data;
}

async function loadBatch({ queryId, ids, wiql, maxItems }) {
  resultsEl().innerHTML = "";
  pipelineEl().hidden = true;
  showStatus("loading", "Fetching list…");
  const params = new URLSearchParams();
  if (ids) params.set("ids", ids);
  else if (wiql) params.set("wiql", wiql);
  else if (queryId) params.set("queryId", queryId);
  params.set("maxItems", String(maxItems || 25));

  try {
    const data = await api(`/api/work-items?${params.toString()}`);
    hideStatus();
    const items = data.items || [];
    const total = data.totalCount ?? items.length;
    if (items.length === 0) {
      resultsEl().appendChild(el("div", { class: "card empty" }, "No items found"));
      return;
    }

    const card = el("article", { class: "card" });
    card.appendChild(el("header", {
      style: { padding: "14px 18px", borderBottom: "1px solid var(--border)", display: "flex", justifyContent: "space-between", alignItems: "center" },
    },
      el("strong", {}, `${items.length} item${items.length === 1 ? "" : "s"}`),
      badge(`total: ${total}`, "slate"),
    ));
    const list = el("div", { class: "list-rows" });
    items.forEach((it) => {
      const tpl = $("#tpl-list-row").content.cloneNode(true);
      const row = tpl.querySelector(".list-row");
      const f = it.fields || {};
      const id = f["System.Id"] || it.id;
      $("[data-bind='id']", row).textContent = `#${id}`;
      $("[data-bind='title']", row).textContent = f["System.Title"] || "(no title)";
      const badges = $("[data-bind='badges']", row);
      const state = f["System.State"]; if (state) badges.appendChild(badge(state, stateTone(state)));
      const sev = f["Microsoft.VSTS.Common.Severity"];
      if (sev) badges.appendChild(badge(`S${String(sev).match(/\d/)?.[0] || ""}`, severityTone(sev)));
      row.addEventListener("click", () => {
        $("#single-input").value = String(id);
        $$(".tab").forEach((b) => {
          const isSingle = b.dataset.tab === "single";
          b.classList.toggle("active", isSingle);
          b.setAttribute("aria-selected", isSingle ? "true" : "false");
        });
        $$(".tab-panel").forEach((p) => { p.hidden = p.dataset.panel !== "single"; });
        runTriage(String(id));
      });
      list.appendChild(tpl);
    });
    card.appendChild(list);
    resultsEl().appendChild(card);
  } catch (err) {
    showStatus("error", `Failed to load list: ${err.message}`);
  }
}

// ---- Init ----
function initTabs() {
  $$(".tab").forEach((btn) => {
    btn.addEventListener("click", () => {
      $$(".tab").forEach((b) => {
        b.classList.toggle("active", b === btn);
        b.setAttribute("aria-selected", b === btn ? "true" : "false");
      });
      const tab = btn.dataset.tab;
      $$(".tab-panel").forEach((p) => { p.hidden = p.dataset.panel !== tab; });
    });
  });
}

function initTheme() {
  const stored = localStorage.getItem("triage-theme");
  if (stored) document.body.dataset.theme = stored;
  const updateIcons = () => {
    const t = document.body.dataset.theme;
    $$('[data-theme-icon]').forEach((e) => { e.hidden = e.dataset.themeIcon !== t; });
  };
  updateIcons();
  $("#theme-toggle").addEventListener("click", () => {
    const next = document.body.dataset.theme === "dark" ? "light" : "dark";
    document.body.dataset.theme = next;
    localStorage.setItem("triage-theme", next);
    updateIcons();
  });
}

async function initHealth() {
  const b = $("#health-badge");
  try {
    const h = await api("/api/health");
    b.textContent = `${h.org}/${h.project} · ${h.auth}`;
    b.className = "badge";
    b.dataset.tone = "emerald";
    b.title = h.defaultQueryId ? `Default query: ${h.defaultQueryId}` : "No default query configured";
  } catch (err) {
    b.textContent = "server error";
    b.dataset.tone = "red";
    b.title = err.message;
  }
}

// ---- Model selector ----
async function initModelSelector() {
  const btn = $("#model-button");
  const menu = $("#model-menu");
  if (!btn || !menu) return;

  // Initial label from cached state.
  setCurrentModel(MODEL_STATE.current, findModelLabel(MODEL_STATE.current));

  const closeMenu = () => {
    menu.hidden = true;
    btn.setAttribute("aria-expanded", "false");
  };
  const openMenu = () => {
    menu.hidden = false;
    btn.setAttribute("aria-expanded", "true");
  };

  btn.addEventListener("click", async (e) => {
    e.stopPropagation();
    if (!menu.hidden) { closeMenu(); return; }
    await refreshModelMenu();
    openMenu();
  });
  document.addEventListener("click", (e) => {
    if (!menu.contains(e.target) && e.target !== btn) closeMenu();
  });
  document.addEventListener("keydown", (e) => {
    if (e.key === "Escape") closeMenu();
  });

  // Pre-fetch once so the cached label resolves correctly on next page load.
  refreshModelMenu().catch(() => { /* ignore */ });
}

async function refreshModelMenu() {
  const menu = $("#model-menu");
  if (!menu) return;
  menu.innerHTML = "";
  menu.appendChild(el("div", { class: "model-loading" }, el("span", { class: "spinner spinner-sm" }), " loading models…"));

  try {
    const data = await api("/api/models");
    MODEL_STATE.providers = data.providers || [];
    setCurrentModel(MODEL_STATE.current, findModelLabel(MODEL_STATE.current));
    renderModelMenu(menu);
  } catch (err) {
    menu.innerHTML = "";
    menu.appendChild(el("div", { class: "model-empty" }, "Failed to load models: ", err.message));
  }
}

function renderModelMenu(menu) {
  menu.innerHTML = "";

  // "Heuristic only" group at the top.
  const heuristicSection = el("div", { class: "model-group" });
  heuristicSection.appendChild(el("div", { class: "model-group-head" }, "No LLM"));
  heuristicSection.appendChild(modelOption({
    id: "heuristic",
    label: "Heuristic only",
    sub: "fastest · always available",
    ready: true,
    recommended: false,
  }));
  menu.appendChild(heuristicSection);

  // Each provider as its own group.
  for (const p of MODEL_STATE.providers || []) {
    const section = el("div", { class: "model-group" });
    const head = el("div", { class: "model-group-head" },
      el("span", {}, p.label),
      el("span", { class: "model-group-status", "data-tone": p.ready ? "emerald" : "slate" },
        p.ready ? "ready" : (p.reason ? "auth required" : "unavailable")),
    );
    section.appendChild(head);

    const models = p.models || [];
    if (models.length === 0) {
      section.appendChild(el("div", { class: "model-empty" }, p.reason || "no models"));
    } else {
      for (const m of models) {
        section.appendChild(modelOption({
          id: m.id,
          label: m.label,
          sub: m.contextLen ? `${(m.contextLen / 1000).toFixed(0)}K context` : "",
          ready: m.ready,
          recommended: !!m.recommended,
          reason: m.reason,
        }));
      }
    }
    menu.appendChild(section);
  }
}

function modelOption({ id, label, sub, ready, recommended, reason }) {
  const isCurrent = id === MODEL_STATE.current;
  const node = el("button", {
    type: "button",
    class: `model-option${isCurrent ? " is-current" : ""}${ready ? "" : " is-disabled"}`,
    "data-id": id,
    role: "option",
    "aria-selected": isCurrent ? "true" : "false",
    title: ready ? label : (reason || "Not available"),
    disabled: ready ? null : true,
  },
    el("div", { class: "model-option-row" },
      el("span", { class: "model-option-label" }, label),
      recommended ? el("span", { class: "model-option-tag" }, "★") : null,
      isCurrent ? el("span", { class: "model-option-check" }, "✓") : null,
    ),
    sub || (!ready && reason)
      ? el("div", { class: "model-option-sub" }, !ready && reason ? reason : sub)
      : null,
  );
  if (ready) {
    node.addEventListener("click", () => {
      setCurrentModel(id, label);
      $("#model-menu").hidden = true;
      $("#model-button").setAttribute("aria-expanded", "false");
    });
  }
  return node;
}

function initForms() {
  $("#single-form").addEventListener("submit", (e) => {
    e.preventDefault();
    runTriage($("#single-input").value);
  });
  $("#batch-form").addEventListener("submit", (e) => {
    e.preventDefault();
    loadBatch({
      queryId: $("#batch-query").value,
      ids: $("#batch-ids").value,
      wiql: $("#batch-wiql").value,
      maxItems: Number($("#batch-max").value) || 25,
    });
  });
  const urlParams = new URLSearchParams(location.search);
  const initId = urlParams.get("id");
  if (initId) {
    $("#single-input").value = initId;
    runTriage(initId);
  }
}

initTabs();
initTheme();
initForms();
initHealth();
initModelSelector();
