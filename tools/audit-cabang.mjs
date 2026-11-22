import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const repo = fileURLToPath(new URL('../', import.meta.url));
const source = path.resolve(process.argv[2] ?? '../simrs-khanza/src');
const output = path.join(repo, 'docs/tracker');
if (!fs.existsSync(path.join(source,'simrskhanza/DlgRawatJalan.java'))) throw Error('Pilih folder src Khanza.');
const files = fs.readdirSync(source,{recursive:true}).map(f=>f.replaceAll('\\','/'))
  .filter(f=>f.endsWith('.java') && !f.startsWith('bridging/')).sort();
const classes = new Set(files.map(f=>path.basename(f,'.java')));
const totals = new Map();
const mapped = new Set();
const lines = ['File Java\tBaris\tMetode atau kelas\tJenis hubungan\tSumber\tTujuan'];
for (const file of files) {
  const code = fs.readFileSync(path.join(source,file),'utf8').split(/\r?\n/);
  const methods = new Set();
  const declaration = /^\s*(?:public|private|protected)\s+(?:(?:static|final|synchronized)\s+)*(?:[\w.<>\[\],?]+\s+)?(\w+)\s*\([^;]*$/;
  for (const line of code) {
    const match = line.match(declaration);
    if (match) methods.add(match[1]);
  }
  let scope = path.basename(file,'.java');
  for (const [i,line] of code.entries()) {
    const clean = line.trim();
    if (/^(\/\/|\/\*|\*)/.test(clean)) continue;
    const def = line.match(declaration);
    if (def) scope = def[1];
    function edge(kind, from, to) {
      mapped.add(file);
      totals.set(kind,(totals.get(kind) ?? 0)+1);
      lines.push([file,i+1,scope,kind,from,to].join('\t'));
    }
    for (const m of line.matchAll(/(\w+)\.addTab\("([^"]*)"\s*,\s*(\w+)/g)) edge('Tab',m[1],m[2]+' ('+m[3]+')');
    for (const m of line.matchAll(/(\w+)\.add(Action|Key|Mouse|Item|Change|ListSelection|Focus)Listener\((.*)/g)) edge('Listener '+m[2],m[1],m[3].includes('::') ? m[3].split('::')[1].split(')')[0] : m[3].trim());
    for (const m of line.matchAll(/\bnew\s+(?:\w+\.)*(\w+)\s*\(/g)) if (classes.has(m[1]) && /^(Dlg|RM|RMCari|frm|Panel)/i.test(m[1])) edge('Instansiasi layar',scope,m[1]);
    for (const m of line.matchAll(/\bSequel\.(menyimpan\w*|mengedit\w*|meghapus\w*|menghapus\w*)\(\s*"([\w]+)"/gi)) edge('Operasi tabel',m[1],m[2]);
    for (const m of line.matchAll(/\b(insert\s+into|delete\s+from|update)\s+`?(\w+)`?\s*(?=\(|set\b|where\b|values\b)/gi)) edge('SQL tulis',m[1],m[2]);
    if (!def && scope!=='initComponents') {
      for (const m of line.matchAll(/(?<![.\w])(?:this\.)?(\w+)\s*\(/g)) {
        if (methods.has(m[1]) && m[1]!==scope && !['setVisible','getData'].includes(scope)) edge('Panggilan lokal',scope,m[1]);
      }
    }
  }
}
fs.writeFileSync(path.join(output,'CABANG_SOURCE.tsv'),lines.join('\n')+'\n');
console.log(JSON.stringify({files:files.length,mapped:mapped.size,edges:lines.length-1,totals:Object.fromEntries(totals)}));
