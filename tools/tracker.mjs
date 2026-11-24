import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../', import.meta.url));
const dir = path.join(root, 'docs/tracker');
const snapshot = path.join(dir, 'java.jsonl');
const read = file => fs.readFileSync(file, 'utf8');
const rows = [];
const compare = (a, b) => a < b ? -1 : a > b ? 1 : 0;
const arg = process.argv.indexOf('--java');
if (arg >= 0) {
  const source = fs.realpathSync(process.argv[arg + 1]);
  const files = fs.readdirSync(source, {recursive:true}).map(f => f.replaceAll('\\', '/'))
    .filter(f => f.endsWith('.java') && !f.startsWith('bridging/')).sort(compare);
  if (!files.includes('simrskhanza/frmUtama.java')) throw Error('Pilih folder src Khanza.');
  for (const file of files) {
    const code = read(path.join(source, file));
    const labels = new Map([...code.matchAll(/(\w+)\.setText\("([^"\n]*)"\)/g)].map(m => [m[1], m[2]]));
    const children = new Map(), covered = new Set();
    for (const m of code.matchAll(/(\w+)\.add\((\w+)\);/g)) {
      if (!children.has(m[1])) children.set(m[1], new Map());
      children.get(m[1]).set(m[2], m.index);
    }
    const entries = [];
    function add(kind, ids, offset) {
      const id = [kind, file, ...ids].join('|');
      entries.push({id, file, line:code.slice(0, offset).split('\n').length, kind,
        route:ids.map(k => labels.get(k) || k).join(' > ')});
    }
    function walk(ids, deferred) {
      for (const [k, offset] of children.get(ids.at(-1)) || []) {
        if (ids.includes(k) || /separator/i.test(k)) continue;
        const skip = deferred || /bridging/i.test(labels.get(k) || k);
        const chain = [...ids, k];
        if (children.has(k)) walk(chain, skip);
        else { covered.add(k); if (!skip) add('popup', chain, offset); }
      }
    }
    for (const popup of new Set([...code.matchAll(/(\w+)\s*=\s*new\s+(?:[\w.]+\.)?(?:JPopupMenu|PopupMenu)\(/g)].map(m => m[1]))) walk([popup], false);
    for (const m of code.matchAll(/\b(\w+)\.addActionListener\s*\(/g)) {
      const k = m[1];
      if (covered.has(k) || (file.endsWith('/frmUtama.java') && tileKeys().has(k))) continue;
      covered.add(k);
      add('aksi', [k], m.index);
    }
    if (/\bextends\s+(?:[\w.]+\.)?(?:JDialog|JFrame)\b/.test(code)) add('jendela', [path.basename(file,'.java')], 0);
    rows.push(...entries);
  }
  fs.writeFileSync(snapshot, rows.map(r => JSON.stringify(r)).join('\n')+'\n');
} else rows.push(...read(snapshot).trim().split('\n').map(line => JSON.parse(line)));

function catalog() {
  return read(path.join(root,'src/SimrsKhanza.UI/Modules/Shared/LegacyMenuCatalog.cs')).split('[][] Tiles')[1];
}
function tileKeys() {
  return tileKeys.cache ??= new Set([...catalog().matchAll(/\("([^"]+)",/g)].map(m => m[1]));
}
// Tile -> layar: kunci tile (btnXxx) yang muncul sebagai literal di kode produksi selain katalog dan peta hak akses.
const routedBy = new Map();
for (const rel of fs.readdirSync(path.join(root, 'src'), {recursive:true}).map(f => f.replaceAll('\\', '/'))) {
  if (!rel.endsWith('.cs') || /(^|\/)(bin|obj|SimrsKhanza\.Tests)\//.test(rel) || /LegacyMenuCatalog\.cs$|LegacyTilePermissions\.g\.cs$|DashboardIcons\.cs$/.test(rel)) continue;
  for (const m of read(path.join(root, 'src', rel)).matchAll(/"([Bb]tn\w+)"/g)) {
    const key = m[1].toLowerCase();
    if (!routedBy.has(key)) routedBy.set(key, path.basename(rel));
  }
}
const menuTotals = new Map();
let category = 0;
for (const line of catalog().split('\n')) {
  if (/^ {8}\[\s*$/.test(line)) category++;
  const m = line.match(/\("([^"]+)", "(.*)"\)/);
  if (!m) continue;
  const group = String.fromCharCode(64+category);
  const screen = routedBy.get(m[1].toLowerCase());
  const [total, routed] = menuTotals.get(group) ?? [0, 0];
  menuTotals.set(group, [total + 1, routed + (screen && group !== 'L' ? 1 : 0)]);
  rows.push({kind:'tile',id:['tile',group,m[1]].join('|'),file:'LegacyMenuCatalog.cs',route:group+' > '+m[2],deferred:group==='L',screen});
}
const status = JSON.parse(read(path.join(dir,'status.json')));
const ids = new Set(rows.map(r => r.id));
if (ids.size !== rows.length) throw Error('ID fitur duplikat');
const states = ['belum diaudit','layar tersedia','kurang','sebagian','selesai','ditunda'];
for (const [id, entry] of Object.entries(status)) {
  if (!ids.has(id) || !states.includes(entry.status) || !entry.bukti?.trim()) throw Error('Periksa status dan bukti: '+id);
}
const totals = new Map();
const output = ['ID\tJenis\tJalur\tSumber\tStatus\tBukti'];
for (const row of rows) {
  const review = status[row.id];
  const auto = row.deferred ? 'ditunda' : row.screen ? 'layar tersedia' : 'belum diaudit';
  const state = review?.status ?? auto;
  if (!totals.has(row.kind)) totals.set(row.kind, Array(states.length).fill(0));
  totals.get(row.kind)[states.indexOf(state)]++;
  output.push([row.id,row.kind,row.route,row.file+(row.line ? ':'+row.line : ''),state,review?.bukti||(!review && row.screen && !row.deferred ? 'Rute layar di '+row.screen+'. Kesetaraan fungsi belum diaudit.' : '')].map(v=>v.replace(/[\t\r\n]+/g,' ')).join('\t'));
}
fs.writeFileSync(path.join(dir,'fitur.tsv'),output.join('\n')+'\n');
// JavaScript data file works when the dashboard is opened directly with file://.
// JSON escaping also makes the data safe if it is ever embedded in an HTML script.
const grafik = rows.map(row => ({
  kind: row.kind, route: row.route, file: row.file, line: row.line || 0,
  state: status[row.id]?.status ?? (row.deferred ? 'ditunda' : row.screen ? 'layar tersedia' : 'belum diaudit'),
  evidence: status[row.id]?.bukti || (!status[row.id] && row.screen && !row.deferred ? 'Rute layar di '+row.screen+'. Kesetaraan fungsi belum diaudit.' : '')
}));
fs.writeFileSync(path.join(dir,'grafik-data.js'), 'window.TRACKER_DATA='+JSON.stringify(grafik).replaceAll('<','\\u003c')+';\n');
const table = ['| Jenis | '+states.join(' | ')+' | Total |','|---|---:|---:|---:|---:|---:|---:|---:|'];
for (const [kind, counts] of totals) table.push('| '+[kind,...counts,counts.reduce((a,b)=>a+b)].join(' | ')+' |');
const menus = ['| Menu | Tile | Punya layar | Belum |', '|---|---:|---:|---:|'];
let allTiles = 0, allRouted = 0;
for (const [group, [total, routed]] of [...menuTotals].sort((a, b) => compare(a[0], b[0]))) {
  menus.push('| '+[group, total, routed, total - routed].join(' | ')+' |');
  allTiles += total; allRouted += routed;
}
menus.push('| **Semua** | **'+allTiles+'** | **'+allRouted+'** | **'+(allTiles - allRouted)+'** |');
const summary = '# Status fitur Khanza\n\n'+table.join('\n')+'\n\n## Cakupan layar per menu dashboard\n\nDihitung otomatis: tile dianggap punya layar bila kuncinya dirutekan di kode. Ini hanya cakupan layar, bukan kesetaraan fungsi; statusnya tetap "layar tersedia" sampai diaudit.\n\n'+menus.join('\n')+'\n\nSatu baris mewakili satu jalur atau kontrol pada sumber. Jendela, aksi, popup, dan tile dapat masuk ke alur kerja yang sama. Status selesai memerlukan bukti pada status.json.\n';
fs.writeFileSync(path.join(dir,'RINGKASAN.md'), summary);
console.log(summary);

const registrationMap = ['# Jalur klik kanan Registrasi', '',
  'Sumber Java: simrskhanza/DlgReg.java. Nomor baris menunjukkan lokasi pemasangan pilihan menu. Bukti Avalonia per ID tersedia di fitur.tsv.', ''];
const popupTabs = {jPopupMenu1:'Registrasi Awal', jPopupMenu2:'Rujukan Internal Poli'};
let previousTab, previousGroup;
for (const row of rows.filter(r => r.kind === 'popup' && r.file === 'simrskhanza/DlgReg.java')) {
  const [tab, group, ...nested] = row.route.split(' > ');
  if (tab !== previousTab) {
    registrationMap.push('## '+(popupTabs[tab] ?? tab), '');
    previousTab = tab;
    previousGroup = undefined;
  }
  if (group !== previousGroup) {
    registrationMap.push('### '+group, '', '| Pilihan dan jalur submenu | Status | Baris Java |', '|---|---|---:|');
    previousGroup = group;
  }
  registrationMap.push('| '+(nested.join(' → ') || group)+' | '+(status[row.id]?.status ?? 'belum diaudit')+' | '+row.line+' |');
}
fs.writeFileSync(path.join(dir,'POPUP_REGISTRASI.md'), registrationMap.join('\n').replace(/\|\n#/g,'|\n\n#')+'\n');
