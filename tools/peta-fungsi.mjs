// Peta fungsi Khanza -> Gramedika dari analisis statis kedua source.
//
//   node tools/peta-fungsi.mjs --java ../simrs-khanza/src
//   node tools/peta-fungsi.mjs --java ../simrs-khanza/src --spek spek.json   (pakai dump yang sudah ada)
//
// Membaca tabel yang ditulis dan dibaca tiap jendela Java, lalu membandingkannya dengan tabel yang ditulis dan dibaca
// kode Gramedika. Hasil "tidak ditulis" adalah bukti kuat fungsi belum ada. Hasil "ditulis" hanya batas atas: tabel yang
// sama bisa ditulis alur lain dengan aturan berbeda. Penilaian kesetaraan tetap di status.json.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../', import.meta.url));
const keluaran = path.join(root, 'docs/tracker');
const arg = nama => { const i = process.argv.indexOf('--' + nama); return i >= 0 ? process.argv[i + 1] : null; };
const baca = f => fs.readFileSync(f, 'utf8');
const daftar = (dir, akhiran) => fs.readdirSync(dir, { recursive: true }).map(f => f.replaceAll('\\', '/'))
  .filter(f => f.endsWith(akhiran) && !/(^|\/)(bin|obj)\//.test(f)).sort();
const javaDir = arg('java');
if (!javaDir || !fs.existsSync(path.join(javaDir, 'simrskhanza/frmUtama.java'))) throw Error('Beri --java <folder src Khanza>.');

// ---------- tabel yang dikenal ----------
const tabelDikenal = new Set();
for (const f of ['docs/audit/schema_sik.sql', ...daftar(path.join(root, 'docs/migrations'), '.sql').map(f => 'docs/migrations/' + f)])
  for (const m of baca(path.join(root, f)).matchAll(/CREATE TABLE (?:IF NOT EXISTS )?`?(\w+)`?/gi)) tabelDikenal.add(m[1].toLowerCase());
// Tabel bantu cetak, penampung, dan catatan sistem tidak dihitung sebagai fungsi. Jurnal dan riwayat stok dinilai sebagai tanda.
const abaikan = t => /^(temporary|temp_|tamp)/.test(t) || ['trackersql', 'jurnal', 'detailjurnal', 'riwayat_barang_medis'].includes(t);

/** Membuang komentar dan mengumpulkan literal string; literal yang disambung "+" digabung. */
function urai(kode, csharp) {
  let bersih = '', i = 0;
  const literal = [];
  let akhirLiteral = -1;
  const tambah = teks => {
    if (akhirLiteral >= 0 && /^\s*\+\s*$/.test(bersih.slice(akhirLiteral))) literal[literal.length - 1] += teks;
    else literal.push(teks);
  };
  while (i < kode.length) {
    const c = kode[i], d = kode[i + 1];
    if (c === '/' && d === '/') { while (i < kode.length && kode[i] !== '\n') i++; continue; }
    if (c === '/' && d === '*') { const e = kode.indexOf('*/', i + 2); i = e < 0 ? kode.length : e + 2; bersih += ' '; continue; }
    if (csharp && kode.startsWith('"""', i)) {
      let n = 3; while (kode[i + n] === '"') n++;
      const e = kode.indexOf('"'.repeat(n), i + n);
      const isi = kode.slice(i + n, e < 0 ? kode.length : e);
      tambah(isi); i = e < 0 ? kode.length : e + n; bersih += '""'; akhirLiteral = bersih.length; continue;
    }
    if (csharp && c === '@' && d === '"' || csharp && (c === '$' && d === '@' || c === '@' && d === '$') && kode[i + 2] === '"') {
      i = kode.indexOf('"', i) + 1; let isi = '';
      while (i < kode.length) { if (kode[i] === '"') { if (kode[i + 1] === '"') { isi += '"'; i += 2; continue; } i++; break; } isi += kode[i++]; }
      tambah(isi); bersih += '""'; akhirLiteral = bersih.length; continue;
    }
    if (c === '"') {
      let isi = ''; i++;
      while (i < kode.length && kode[i] !== '"' && kode[i] !== '\n') { if (kode[i] === '\\') { isi += kode[i + 1] ?? ''; i += 2; } else isi += kode[i++]; }
      i++; tambah(isi); bersih += '"' + isi.replaceAll('"', ' ') + '"'; akhirLiteral = bersih.length; continue;
    }
    if (c === "'") { let j = i + 1; while (j < kode.length && kode[j] !== "'" && kode[j] !== '\n') j += kode[j] === '\\' ? 2 : 1; bersih += kode.slice(i, j + 1); i = j + 1; continue; }
    bersih += c; i++;
  }
  return { bersih, literal };
}

const OPS = ['tambah', 'ubah', 'hapus'];
function dariSql(sql, tulis, baca_) {
  const catat = (t, op) => { t = t.toLowerCase(); if (tabelDikenal.has(t)) tulis(t, op); };
  for (const m of sql.matchAll(/\b(?:insert\s+(?:ignore\s+)?into|replace\s+into)\s+`?(\w+)`?/gi)) {
    catat(m[1], 'tambah');
    if (/on\s+duplicate\s+key\s+update/i.test(sql)) catat(m[1], 'ubah');
  }
  for (const m of sql.matchAll(/\bupdate\s+`?(\w+)`?(?:\s+(?!set\b)\w+)?\s+(?:set\b|(?:inner\s+|left\s+)?join\b)/gi)) catat(m[1], 'ubah');
  for (const m of sql.matchAll(/\bdelete\s+(?:\w+\s+)?from\s+`?(\w+)`?/gi)) catat(m[1], 'hapus');
  for (const m of sql.matchAll(/\b(?:from|join)\s+`?(\w+)`?/gi)) { const t = m[1].toLowerCase(); if (tabelDikenal.has(t)) baca_(t); }
}

// ---------- sisi Java ----------
const jendela = new Map();
const kelasJava = new Map();
const berkasJava = daftar(javaDir, '.java');
for (const file of berkasJava) kelasJava.set(path.basename(file, '.java'), file);
const dibukaOleh = new Map();
let panggilanTabelVariabel = 0;
for (const file of berkasJava) {
  const { bersih, literal } = urai(baca(path.join(javaDir, file)), false);
  const nama = path.basename(file, '.java');
  for (const m of bersih.matchAll(/\bnew\s+(?:[\w]+\.)*([A-Z]\w+)\s*\(/g)) if (kelasJava.has(m[1]) && m[1] !== nama) {
    if (!dibukaOleh.has(m[1])) dibukaOleh.set(m[1], new Set());
    dibukaOleh.get(m[1]).add(nama);
  }
  if (!/\bextends\s+(?:[\w.]+\.)?(?:JDialog|JFrame)\b/.test(bersih)) continue;
  const tulis = new Map(), dibaca = new Set();
  const catat = (t, op) => { if (!tulis.has(t)) tulis.set(t, new Set()); tulis.get(t).add(op); };
  for (const m of bersih.matchAll(/\b(?:Sequel|sek|sekuel)\.(menyimpan\w*|mengedit\w*|meghapus\w*)\(\s*([^,)]+)/g)) {
    const t = /^"(\w+)"$/.exec(m[2].trim())?.[1]?.toLowerCase();
    if (!t) { panggilanTabelVariabel++; continue; }
    if (tabelDikenal.has(t)) catat(t, m[1].startsWith('menyimpan') ? 'tambah' : m[1].startsWith('mengedit') ? 'ubah' : 'hapus');
  }
  for (const sql of literal) dariSql(sql, catat, t => dibaca.add(t));
  jendela.set(nama, {
    nama, file, modul: file.split('/')[0], tulis, dibaca,
    jurnal: /\bjur\.simpanJurnal\b/.test(bersih) || literal.some(s => /^tampjurnal/i.test(s)),
    stok: /\b(?:Trackobat|Trackbarang)\.catatRiwayat\(/.test(bersih),
    cetak: new Set(literal.flatMap(s => [...s.matchAll(/\b(rpt\w+)\.jasper/g)].map(m => m[1]))).size,
  });
}
const bridging = j => j.modul === 'bridging' || /bpjs|satusehat|inacbg|pcare|sisrute|sirs|aplicare|mobilejkn|bridging|briva|orthanc|lica|sysmex|vansale/i.test(j.nama);

// ---------- tile menu utama ----------
const utama = urai(baca(path.join(javaDir, 'simrskhanza/frmUtama.java')), false).bersih;
const katalog = baca(path.join(root, 'src/SimrsGramedika.UI/Modules/Shared/LegacyMenuCatalog.cs')).split('[][] Tiles')[1];
const tile = [];
let grup = 0;
for (const baris of katalog.split('\n')) {
  if (/^ {8}\[\s*$/.test(baris)) grup++;
  const m = baris.match(/\("([^"]+)", "(.*)"\)/);
  if (m) tile.push({ kunci: m[1], judul: m[2], menu: String.fromCharCode(64 + grup) });
}
const metode = new Map();
const kepala = [...utama.matchAll(/private void (\w+)ActionPerformed\(java\.awt\.event\.ActionEvent \w+\)\s*\{/g)];
kepala.forEach((m, i) => metode.set(m[1].toLowerCase(), utama.slice(m.index + m[0].length, kepala[i + 1]?.index ?? m.index + 6000)));
for (const t of tile) {
  const isi = metode.get(t.kunci.toLowerCase()) ?? '';
  t.kelas = [...isi.matchAll(/\bnew\s+(?:\w+\.)*([A-Z]\w+)\s*\(/g)].map(m => m[1]).find(k => jendela.has(k)) ?? null;
}

// ---------- sisi Gramedika ----------
let spekPath = arg('spek');
if (!spekPath) {
  spekPath = path.join(os.tmpdir(), 'simrs-spek.json');
  fs.rmSync(spekPath, { force: true });
  execFileSync('dotnet', ['test', 'src/SimrsGramedika.Tests/SimrsGramedika.Tests.csproj', '-m:1', '-p:NuGetAudit=false', '-nologo', '-v', 'quiet',
    '--filter', 'FullyQualifiedName~PetaFungsiDumpTests'],
    { cwd: root, stdio: 'inherit', env: { ...process.env, SIMRS_PETA_DUMP: spekPath, MSBuildEnableWorkloadResolver: 'false' } });
}
const spek = JSON.parse(baca(spekPath));

const cs = ['Config', 'Core', 'Data', 'UI'].flatMap(p => daftar(path.join(root, 'src/SimrsGramedika.' + p), '.cs')
  .map(f => ({ proyek: p, file: `SimrsGramedika.${p}/${f}` })));
for (const b of cs) {
  Object.assign(b, urai(baca(path.join(root, 'src', b.file)), true));
  b.tipe = [...b.bersih.matchAll(/\b(?:class|record(?:\s+(?:struct|class))?|struct|interface|enum)\s+([A-Z]\w+)/g)].map(m => m[1]);
}
// Kode Data yang tidak pernah dirujuk dari antarmuka tidak boleh dihitung sebagai fungsi.
const hidup = new Set(cs.filter(b => b.proyek === 'UI'));
for (let tambah = true; tambah;) {
  tambah = false;
  const kata = new Set([...hidup].flatMap(b => b.bersih.match(/\b[A-Z]\w+\b/g) ?? []));
  for (const b of cs) if (!hidup.has(b) && b.tipe.some(t => kata.has(t))) { hidup.add(b); tambah = true; }
}
const mati = cs.filter(b => !hidup.has(b) && b.proyek === 'Data').map(b => b.file);

const tulisG = new Map(), bacaG = new Set();
const catatG = (t, op, file) => {
  const k = t + '|' + op;
  if (!tulisG.has(k)) tulisG.set(k, new Set());
  tulisG.get(k).add(file);
};
// DbSet -> tabel, untuk pembacaan lewat EF Core.
const konteks = baca(path.join(root, 'src/SimrsGramedika.Data/SimrsGramedikaDbContext.cs'));
const tabelEntitas = new Map();
for (const blok of konteks.split(/(?=\bEntity<\w+>)/)) {
  const e = /^Entity<(\w+)>/.exec(blok)?.[1], t = /ToTable\("(\w+)"\)/.exec(blok)?.[1];
  if (e && t) tabelEntitas.set(e, t.toLowerCase());
}
const dbSet = new Map([...konteks.matchAll(/DbSet<(\w+)>\s+(\w+)/g)].filter(m => tabelEntitas.has(m[1])).map(m => [m[2], tabelEntitas.get(m[1])]));
for (const b of hidup) {
  // Katalog spesifikasi dinilai dari dump, bukan dari literalnya, tetapi SQL pembacaannya tetap berlaku.
  for (const sql of b.literal) dariSql(sql, (t, op) => catatG(t, op, b.file), t => bacaG.add(t));
  if (!b.file.endsWith('SimrsGramedikaDbContext.cs'))
    for (const [set, t] of dbSet) if (new RegExp(`\\.${set}\\b`).test(b.bersih)) bacaG.add(t);
}

// Penulisan yang nama tabelnya dirakit saat jalan, atau lewat EF Core. Diperiksa tangan; perbarui bila kodenya berubah.
const T = 'tambah', U = 'ubah', H = 'hapus';
const manual = {
  'SimrsGramedika.Data/OrderRalanService.cs': [
    ['permintaan_lab permintaan_labpa permintaan_labmb permintaan_radiologi', [T, U, H]],
    ['permintaan_pemeriksaan_lab permintaan_pemeriksaan_labpa permintaan_pemeriksaan_labmb permintaan_pemeriksaan_radiologi permintaan_detail_permintaan_lab permintaan_detail_permintaan_labmb', [T, H]],
    ['resep_obat resep_dokter resep_dokter_racikan resep_dokter_racikan_detail', [H]]],
  'SimrsGramedika.Data/TindakanRalanInputService.cs': [['rawat_jl_dr rawat_jl_pr rawat_jl_drpr', [H]]],
  'SimrsGramedika.Data/Farmasi/ValidasiResepService.cs': [['detail_obat_racikan obat_racikan detail_pemberian_obat', [H]]],
  'SimrsGramedika.Data/PemeriksaanObgynService.cs': [['pemeriksaan_obstetri_ralan pemeriksaan_ginekologi_ralan', [T, U, H]]],
  'SimrsGramedika.Data/Pengaturan/MenuURepository.cs': [['runtext runtextapotek', [T, U]]],
  'SimrsGramedika.Data/PasienBaruService.cs': [['pasien', [T, U]], ['kelurahan kecamatan kabupaten propinsi', [T]]],
  'SimrsGramedika.Data/PasienRepository.cs': [['pasien', [T]]],
  'SimrsGramedika.Data/IgdRepository.cs': [['data_triase_igd', [T]]],
  'SimrsGramedika.Data/AntrianRepository.cs': [
    ['antrilabpk antrilabpk2 antrilabpa antrilabpa2 antrilabmb antrilabmb2 antriradiologi antriradiologi2', [T, H]],
    ['antriapotek2 antriapotek3', [U]]],
  'SimrsGramedika.Data/LabPeriksaRepository.cs': [['antrilabpk2 antrilabpa2 antrilabmb2', [T, H]]],
  'SimrsGramedika.Data/LabPermintaanRepository.cs': [
    ['permintaan_lab permintaan_labpa permintaan_labmb', [U, H]],
    ['permintaan_pemeriksaan_lab permintaan_pemeriksaan_labpa permintaan_pemeriksaan_labmb permintaan_detail_permintaan_lab permintaan_detail_permintaan_labmb', [H]]],
  'SimrsGramedika.Data/RawatInapRepository.cs': [['kamar_inap', [T, U]], ['reg_periksa kamar', [U]]],
  'SimrsGramedika.Data/RegistrasiRepository.cs': [['reg_periksa', [U]]],
  'SimrsGramedika.Data/KasirRepository.cs': [['reg_periksa', [U]]],
};
// Berkas baru yang merakit nama tabel harus didaftar di atas, supaya petanya tidak diam-diam kurang hitung.
const generik = ['Master/MasterSpec.cs', 'RekamMedis/FormKlinisService.cs', 'Inventaris/LogisticsMasterSpecs.cs'].map(f => 'SimrsGramedika.Data/' + f);
const takTerdaftar = [...hidup].filter(b => !manual[b.file] && !generik.includes(b.file) &&
  b.literal.some(s => /\b(insert\s+(ignore\s+)?into|replace\s+into|delete\s+from|update)\s+`?\{(?!0\})[A-Za-z_]/i.test(s) && /\b(into|from|set)\b/i.test(s)));
if (takTerdaftar.length) throw Error('Penulisan dengan nama tabel dirakit, belum didaftar: ' + takTerdaftar.map(b => b.file).join(', '));
for (const [file, isi] of Object.entries(manual)) {
  if (!cs.some(b => b.file === file)) throw Error('Daftar manual menunjuk berkas yang tidak ada: ' + file);
  if (![...hidup].some(b => b.file === file)) continue;
  for (const [tabel, ops] of isi) for (const t of tabel.split(' ')) {
    if (!tabelDikenal.has(t)) throw Error('Daftar manual: tabel tidak dikenal ' + t);
    for (const op of ops) catatG(t, op, file);
  }
}
for (const s of spek) {
  const asal = `spesifikasi:${s.Katalog}:${s.Tile}`;
  for (const sql of s.Sql) dariSql(sql, (t, op) => catatG(t, op, asal), t => bacaG.add(t));
  for (const t of s.Tabel.map(x => x.toLowerCase())) {
    if (s.Tambah) catatG(t, T, asal);
    if (s.Ubah) catatG(t, U, asal);
    if (s.Hapus) catatG(t, H, asal);
    bacaG.add(t);
  }
}
const berkasMap = new Map(cs.map(b => [b.file, b]));
const pakai = (file, pola) => pola.test(berkasMap.get(file)?.bersih ?? '') || berkasMap.get(file)?.literal.some(s => pola.test(s));
const tabelDitulisG = new Set([...tulisG.keys()].map(k => k.split('|')[0]));

// ---------- klasifikasi jendela ----------
const alurInti = new Set(['DlgReg', 'DlgIGD', 'DlgRawatJalan', 'DlgRawatInap', 'DlgKamarInap', 'DlgKasirRalan', 'DlgBilingRalan', 'DlgBilingRanap',
  'DlgPeriksaLaboratorium', 'DlgPeriksaRadiologi', 'DlgDaftarPermintaanResep', 'DlgPasien']);
const hasil = [];
for (const j of jendela.values()) {
  const wajib = [...j.tulis].filter(([t]) => !abaikan(t)).flatMap(([t, ops]) => [...ops].map(op => t + '|' + op)).sort();
  const dibaca = [...j.dibaca].filter(t => !abaikan(t)).sort();
  const ada = wajib.filter(k => tulisG.has(k));
  const kurang = wajib.filter(k => !tulisG.has(k));
  const tidakDibaca = dibaca.filter(t => !bacaG.has(t) && !tabelDitulisG.has(t));
  let status;
  if (bridging(j)) status = 'bridging';
  else if (wajib.length) status = !kurang.length ? 'tulis ada' : ada.length ? 'tulis sebagian'
    : wajib.some(k => bacaG.has(k.split('|')[0])) ? 'hanya baca' : 'belum ada';
  else if (dibaca.length) status = !tidakDibaca.length ? 'baca ada' : tidakDibaca.length < dibaca.length ? 'baca sebagian' : 'belum ada';
  else status = 'tanpa data';
  // Layar transaksi (memposting jurnal atau riwayat stok) yang tulisannya hanya "tercakup" oleh editor master generik,
  // misalnya stok barang yang bisa diketik di master barang, bukan alur yang sama.
  if (status === 'tulis sebagian' && (j.jurnal || j.stok) && ada.every(k => [...tulisG.get(k)].every(f => f.startsWith('spesifikasi:'))))
    status = 'hanya baca';
  const tanda = [];
  if (status.startsWith('tulis')) {
    const berkas = new Set(ada.flatMap(k => [...tulisG.get(k)]));
    if (j.jurnal && ![...berkas].some(f => pakai(f, /\b(JurnalPoster|PenunjangJurnal)\b/))) tanda.push('jurnal');
    if (j.stok && ![...berkas].some(f => pakai(f, /riwayat_barang_medis|\bCatatAsync\b|\bMutasiResepAsync\b/))) tanda.push('riwayat stok');
  }
  const pembuka = [...(dibukaOleh.get(j.nama) ?? [])];
  hasil.push({ ...j, wajib, ada, kurang, dibaca, tidakDibaca, status, tanda,
    inti: pembuka.filter(p => alurInti.has(p)), pembuka: pembuka.length,
    tile: tile.filter(t => t.kelas === j.nama).map(t => t.menu + ':' + t.kunci) });
}
hasil.sort((a, b) => a.file < b.file ? -1 : 1);

// ---------- klasifikasi tile ----------
const mainWindow = baca(path.join(root, 'src/SimrsGramedika.UI/MainWindow.axaml.cs'));
const khusus = new Set([
  ...[...mainWindow.matchAll(/(?:\bcase|\bor|\bis|==)\s+"([Bb]tn\w+)"|"([Bb]tn\w+)"\s*(?:=>|or\b)/g)].map(m => m[1] ?? m[2]),
  // Sebagian layar buatan sendiri dirutekan lewat konstanta kunci di katalognya.
  ...cs.flatMap(b => [...baca(path.join(root, 'src', b.file)).matchAll(/const string \w*Key = "([Bb]tn\w+)"/g)].map(m => m[1])),
].map(k => k.toLowerCase()));
const spekTile = new Map();
for (const s of spek) if (s.Tile) {
  const k = s.Tile.toLowerCase();
  if (!spekTile.has(k)) spekTile.set(k, []);
  spekTile.get(k).push(s);
}
const perStatus = new Map(hasil.map(h => [h.nama, h]));
for (const t of tile) {
  const k = t.kunci.toLowerCase(), s = spekTile.get(k) ?? [], j = t.kelas ? perStatus.get(t.kelas) : null;
  t.layar = t.menu === 'L' ? 'ditunda'
    : s.some(x => x.Jenis === 'formulir') ? 'formulir'
    : khusus.has(k) ? 'khusus'
    : s.some(x => x.Jenis === 'master' && (x.Tambah || x.Ubah || x.Hapus)) ? 'master'
    : s.some(x => x.Jenis === 'laporan' || x.Jenis === 'master') ? 'baca saja'
    : 'lain';
  t.javaMenulis = j ? j.wajib.length > 0 : null;
  t.status = j?.status ?? '';
}

// ---------- keluaran ----------
const bersihkan = v => String(v).replace(/[\t\r\n]+/g, ' ');
fs.writeFileSync(path.join(keluaran, 'peta-fungsi.tsv'), [
  'Modul\tJendela\tStatus\tTanda\tTulis ada\tTulis belum\tTabel dibaca tapi tidak disentuh Gramedika\tCetak Jasper\tDibuka dari alur inti\tTile\tSumber',
  ...hasil.map(h => [h.modul, h.nama, h.status, h.tanda.join(', '), h.ada.join(' '), h.kurang.join(' '), h.tidakDibaca.join(' '), h.cetak,
    h.inti.join(' '), h.tile.join(' '), h.file].map(bersihkan).join('\t')),
].join('\n') + '\n');
fs.writeFileSync(path.join(keluaran, 'peta-tile.tsv'), ['Menu\tTile\tJudul\tJendela Java\tJava menulis\tLayar Gramedika\tStatus jendela',
  ...tile.map(t => [t.menu, t.kunci, t.judul, t.kelas ?? '', t.javaMenulis === null ? '?' : t.javaMenulis ? 'ya' : 'tidak', t.layar, t.status].map(bersihkan).join('\t'))]
  .join('\n') + '\n');

const urutan = ['tulis ada', 'tulis sebagian', 'hanya baca', 'baca ada', 'baca sebagian', 'belum ada', 'tanpa data', 'bridging'];
const hitung = (list, f) => list.filter(f).length;
const persen = (a, b) => b ? Math.round(a / b * 100) + '%' : '-';
const tabel = (judul, baris) => ['| ' + judul.join(' | ') + ' |', '|' + judul.map((_, i) => i ? '---:' : '---').join('|') + '|', ...baris.map(b => '| ' + b.join(' | ') + ' |')].join('\n');

const menulis = hasil.filter(h => h.wajib.length && h.status !== 'bridging');
const pasangan = new Set(menulis.flatMap(h => h.wajib));
const pasanganAda = [...pasangan].filter(k => tulisG.has(k));
const tabelTulisJava = new Set([...pasangan].map(k => k.split('|')[0]));
const tabelTulisAda = [...tabelTulisJava].filter(t => tabelDitulisG.has(t));

const modul = [...new Set(hasil.map(h => h.modul))].sort();
const perModul = modul.map(m => {
  const w = menulis.filter(h => h.modul === m);
  const p = new Set(w.flatMap(h => h.wajib));
  return [m, hitung(hasil, h => h.modul === m), w.length, hitung(w, h => h.status === 'tulis ada'), hitung(w, h => h.status === 'tulis sebagian'),
    hitung(w, h => h.status === 'hanya baca'), hitung(w, h => h.status === 'belum ada'), persen([...p].filter(k => tulisG.has(k)).length, p.size)];
});
const tileInti = tile.filter(t => t.menu !== 'L');
const menu = [...new Set(tile.map(t => t.menu))];
const perMenu = menu.map(m => {
  const t = tile.filter(x => x.menu === m), w = t.filter(x => x.javaMenulis);
  return [m, t.length, w.length, hitung(w, x => x.layar === 'master' || x.layar === 'formulir'), hitung(w, x => x.layar === 'khusus'),
    hitung(w, x => x.layar === 'baca saja'), hitung(w, x => x.layar === 'ditunda' || x.layar === 'lain'), hitung(t, x => x.javaMenulis === null)];
});
const bobot = h => h.inti.length * 10 + h.pembuka;
const daftarGap = list => tabel(['Jendela', 'Modul', 'Alur inti', 'Pembuka', 'Tulis belum ada (tabel|aksi)'], list.map(h =>
  [h.nama, h.modul, h.inti.length, h.pembuka, h.kurang.slice(0, 5).join(', ').replaceAll('|', ' ') + (h.kurang.length > 5 ? ` (+${h.kurang.length - 5})` : '')]));
const gap = menulis.filter(h => h.kurang.length).sort((a, b) => bobot(b) - bobot(a) || a.nama.localeCompare(b.nama));
const tandaList = menulis.filter(h => h.tanda.length).sort((a, b) => bobot(b) - bobot(a));
const tabelHilang = new Map();
for (const h of menulis) for (const t of new Set(h.kurang.map(k => k.split('|')[0]))) if (!tabelDitulisG.has(t)) tabelHilang.set(t, (tabelHilang.get(t) ?? 0) + 1);

// Pengelompokan celah menurut cara mengerjakannya.
const belumLengkap = menulis.filter(h => h.status !== 'tulis ada');
const tabelInti = h => new Set(h.wajib.map(k => k.split('|')[0]).filter(t => !t.startsWith('antri') && !t.startsWith('bukti_')));
const keluarga = [
  ['Transaksi stok (riwayat stok, sebagian besar juga jurnal)', h => h.stok],
  ['Pembayaran, tagihan dan jurnal tanpa stok', h => !h.stok && h.jurnal],
  ['Formulir rekam medis dengan satu tabel isi', h => h.modul === 'rekammedis' && tabelInti(h).size === 1],
  ['Formulir rekam medis dengan tabel tambahan', h => h.modul === 'rekammedis'],
  ['Surat dan persetujuan', h => h.modul === 'surat'],
  ['Restore data terhapus', h => h.modul === 'restore'],
  ['Lainnya (master, permintaan, pengaturan, pelayanan)', () => true],
];
const sisa = new Set(belumLengkap);
const perKeluarga = keluarga.map(([nama, cocok]) => {
  const isi = [...sisa].filter(cocok);
  isi.forEach(h => sisa.delete(h));
  return [nama, isi.length, hitung(isi, h => h.status === 'tulis sebagian'), hitung(isi, h => h.inti.length > 0)];
});
const hanyaMaster = menulis.filter(h => h.status === 'tulis ada' && h.ada.every(k => [...tulisG.get(k)].some(f => f.startsWith('spesifikasi:') && !f.includes('FormKlinis'))));
const inti = [...alurInti].map(n => perStatus.get(n)).filter(Boolean).map(h =>
  [h.nama, h.status, `${h.ada.length}/${h.wajib.length}`, [...new Set(h.kurang.map(k => k.split('|')[0]))].slice(0, 6).join(', ') || '-']);

const md = `# Peta fungsi Khanza → Gramedika

Dihasilkan \`node tools/peta-fungsi.mjs --java ../simrs-khanza/src\`. Jangan diubah tangan. Rincian per jendela ada di
[peta-fungsi.tsv](peta-fungsi.tsv), per tile di [peta-tile.tsv](peta-tile.tsv). Cara membaca dan hasil cek sampel ada di
[AUDIT_PETA_FUNGSI.md](AUDIT_PETA_FUNGSI.md).

**Ini analisis statis pada tingkat tabel.** "Belum ada" cukup dapat dipercaya. "Ada" adalah batas atas: tabelnya ditulis
Gramedika, tetapi validasi, jurnal, cetak dan cabang layarnya belum tentu setara.

## Ringkasan

| Ukuran | Ada | Total | Persen |
|---|---:|---:|---:|
| Pasangan tabel dan aksi tulis (tambah/ubah/hapus) yang dilakukan jendela Java | ${pasanganAda.length} | ${pasangan.size} | ${persen(pasanganAda.length, pasangan.size)} |
| Tabel yang ditulis jendela Java dan juga ditulis Gramedika | ${tabelTulisAda.length} | ${tabelTulisJava.size} | ${persen(tabelTulisAda.length, tabelTulisJava.size)} |
| Jendela Java yang menulis data, seluruh tulisannya ada | ${hitung(menulis, h => h.status === 'tulis ada')} | ${menulis.length} | ${persen(hitung(menulis, h => h.status === 'tulis ada'), menulis.length)} |
| Jendela Java yang menulis data, minimal sebagian ada | ${hitung(menulis, h => h.status.startsWith('tulis'))} | ${menulis.length} | ${persen(hitung(menulis, h => h.status.startsWith('tulis')), menulis.length)} |

Angka di atas tidak memasukkan bridging, tabel sementara, catatan sistem, jurnal dan riwayat stok.

Dari ${hitung(menulis, h => h.status === 'tulis ada')} jendela yang seluruh tulisannya ada, ${hanyaMaster.length} tercakup oleh layar master generik (tambah/ubah/hapus satu tabel).

## Jendela alur inti

Aksi tulis adalah pasangan tabel dan tambah/ubah/hapus. "Ada" di sini juga batas atas.

${tabel(['Jendela', 'Status', 'Aksi tulis ada', 'Contoh tabel yang aksinya belum ada'], inti)}

## Sisa pekerjaan menurut jenis

Jendela yang menulis data dan belum seluruhnya ada: ${belumLengkap.length}. Setiap jendela dihitung sekali, pada baris pertama yang cocok.

${tabel(['Jenis', 'Jendela', 'Di antaranya sebagian', 'Dibuka dari alur inti'], perKeluarga)}

## Semua jendela Java

${tabel(['Status', 'Jendela'], urutan.map(s => [s, hitung(hasil, h => h.status === s)]).concat([['**Total**', hasil.length]]))}

## Per folder source Java

${tabel(['Folder', 'Jendela', 'Menulis data', 'Tulis ada', 'Sebagian', 'Hanya baca', 'Belum ada', 'Aksi tulis tercakup'], perModul)}

## Per menu dashboard

Hanya menghitung tile yang jendela Java-nya menulis data. "Khusus" berarti tile dibuka dengan layar buatan sendiri; kemampuan
tulisnya lihat status jendelanya. "?" berarti jendela Java tidak terpetakan dari \`frmUtama\`.

${tabel(['Menu', 'Tile', 'Java menulis', 'Bisa isi (master/formulir)', 'Layar khusus', 'Hanya tampil', 'Ditunda/lain', '?'], perMenu)}

Tile di luar menu L: ${tileInti.length}. Yang di Java menulis data tetapi tile Gramedika hanya menampilkan:
**${hitung(tileInti, t => t.javaMenulis && t.layar === 'baca saja')}**.

## Celah terpenting

Diurutkan menurut berapa alur inti (registrasi, IGD, rawat jalan, rawat inap, kamar, kasir, penunjang, farmasi, pasien) yang
membuka jendela itu, lalu jumlah pembuka. Urutan ini ukuran keterhubungan, bukan frekuensi pemakaian di rumah sakit.

${daftarGap(gap.slice(0, 60))}

## Jurnal atau riwayat stok belum terlihat

Jendela yang sebagian tulisannya ada, memposting jurnal atau riwayat stok di Java, tetapi berkas Gramedika penulisnya tidak
melakukannya. Perlu diperiksa satu per satu; ${tandaList.length} jendela.

${tabel(['Jendela', 'Modul', 'Tanda', 'Alur inti'], tandaList.slice(0, 40).map(h => [h.nama, h.modul, h.tanda.join(', '), h.inti.length]))}

## Tabel yang sama sekali belum pernah ditulis

${tabelHilang.size} tabel. Tiga puluh yang paling banyak dipakai jendela Java: ${[...tabelHilang].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0])).slice(0, 30).map(([t, n]) => `\`${t}\` (${n})`).join(', ')}.

## Batas analisis

- Kode Data yang tidak dirujuk antarmuka tidak dihitung (${mati.length} berkas${mati.length ? ': ' + mati.map(f => '\`' + f.replace('SimrsGramedika.Data/', '') + '\`').join(', ') : ''}).
- ${panggilanTabelVariabel} panggilan simpan/ubah/hapus di jendela Java memakai nama tabel dari variabel dan tidak terbaca.
- Layar Java yang memposting jurnal atau riwayat stok dan hanya tersentuh editor master generik dinilai "hanya baca".
  Angka pasangan dan tabel tidak dikoreksi dengan aturan ini.
- Penulisan Gramedika yang nama tabelnya dirakit saat jalan atau lewat EF Core didaftar tangan di skrip.
- Perintah "kosongkan tabel" Menu U bisa menghapus tabel apa pun dan sengaja tidak dihitung.
- Logika di kelas Java yang bukan jendela (misalnya pembantu jurnal dan stok) tidak dihitung ke jendela pemanggilnya.
- Cetakan Jasper, validasi, hak akses, bridging dan pengaturan tidak dibandingkan.
`;
fs.writeFileSync(path.join(keluaran, 'PETA_FUNGSI.md'), md);
console.log(md.split('## Celah terpenting')[0]);
console.log('mati:', mati.join(', '));
if (process.env.PETA_DEBUG) fs.writeFileSync(process.env.PETA_DEBUG, JSON.stringify({
  hasil: hasil.map(h => ({ ...h, tulis: undefined, dibaca: h.dibaca })),
  tulisG: [...tulisG].map(([k, v]) => [k, [...v]]),
}));
