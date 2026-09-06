#!/usr/bin/env node
// PreToolUse hook — fa rispettare tecnicamente il protocollo del lock di AGENTS.md.
//
// Regole (vedi AGENTS.md, sezione "Prima di toccare qualsiasi file di codice"):
//   1. Scritture dentro User.PluginSdkDemoEdit/ (il codice C#) sono permesse solo se il blocco
//      LOCK in .ai/PROJECT_STATE.md ha owner: NONE oppure owner: claude.
//   2. Scritture dentro Hardware/ sono SEMPRE negate, indipendentemente dal lock (vedi Y-53:
//      territorio di Andreas, fuori scope per qualunque agente).
// Tutto il resto (.ai/, root docs, ecc.) non è soggetto a questo hook: proporre piani e
// aggiornare handoff/stato non richiede il lock, per costruzione di AGENTS.md.

const fs = require('fs');
const path = require('path');

const CODE_PREFIX = 'User.PluginSdkDemoEdit' + path.sep;
const HARDWARE_PREFIX = 'Hardware' + path.sep;
const LOCK_FILE = path.join(process.cwd(), '.ai', 'PROJECT_STATE.md');

function readStdin() {
  try {
    return fs.readFileSync(0, 'utf8');
  } catch (e) {
    return '';
  }
}

function extractFilePaths(input) {
  const toolInput = input.tool_input || {};
  const paths = [];
  if (typeof toolInput.file_path === 'string') paths.push(toolInput.file_path);
  if (Array.isArray(toolInput.edits)) {
    for (const e of toolInput.edits) {
      if (e && typeof e.file_path === 'string') paths.push(e.file_path);
    }
  }
  return paths;
}

function relToProject(p) {
  const abs = path.isAbsolute(p) ? p : path.join(process.cwd(), p);
  return path.relative(process.cwd(), abs);
}

function getLockOwner() {
  let content;
  try {
    content = fs.readFileSync(LOCK_FILE, 'utf8');
  } catch (e) {
    // Se il file di stato non si legge, non blocchiamo per un problema di tooling:
    // meglio un lock silenzioso che un progetto bloccato da un hook rotto.
    return 'UNKNOWN';
  }
  const match = content.match(/```yaml\s*\r?\nowner:\s*(\S+)/);
  return match ? match[1] : 'UNKNOWN';
}

function allow() {
  console.log(JSON.stringify({
    hookSpecificOutput: { hookEventName: 'PreToolUse', permissionDecision: 'allow' }
  }));
  process.exit(0);
}

function deny(reason) {
  console.log(JSON.stringify({
    hookSpecificOutput: {
      hookEventName: 'PreToolUse',
      permissionDecision: 'deny',
      permissionDecisionReason: reason
    }
  }));
  process.exit(0);
}

let raw;
try {
  raw = JSON.parse(readStdin() || '{}');
} catch (e) {
  allow();
}

const filePaths = extractFilePaths(raw).map(relToProject);
if (filePaths.length === 0) allow();

const touchesHardware = filePaths.some(p => p.startsWith(HARDWARE_PREFIX));
if (touchesHardware) {
  deny(
    'Hardware/ e\' territorio di Andreas (vedi Y-53 in .ai/PROJECT_STATE.md): nessun agente ci scrive, indipendentemente dal lock.'
  );
}

const touchesCode = filePaths.some(p => p.startsWith(CODE_PREFIX));
if (touchesCode) {
  const owner = getLockOwner();
  if (owner !== 'NONE' && owner !== 'claude' && owner !== 'UNKNOWN') {
    deny(
      `Il lock in .ai/PROJECT_STATE.md e\' di "${owner}", non tuo: per AGENTS.md non puoi scrivere codice in User.PluginSdkDemoEdit/. Puoi leggere, analizzare, o proporre un piano in .ai/plans/.`
    );
  }
}

allow();
