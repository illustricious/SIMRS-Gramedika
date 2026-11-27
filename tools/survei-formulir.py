"""Bahan menambah formulir rekam medis dari sumber Java (Python 3).

  python tools/survei-formulir.py                      jendela satu tabel yang belum ditulis, per kelompok
  python tools/survei-formulir.py RMxxx [RMyyy ...]    kolom, urutan simpan, label, isian wajib, batas panjang, pilihan

Sumber Java diharapkan di ../simrs-khanza/src. Daftar jendela dibaca dari docs/tracker/peta-fungsi.tsv, jadi buat
ulang peta (node tools/peta-fungsi.mjs --java ../simrs-khanza/src) sesudah menambah formulir.
Ini pencocokan pola teks: label, aturan simpan/ubah/hapus, rumus skor dan kolom pemilik tetap harus dibaca dari
layar Java dan skema. "Cocok pola" berarti bentuk kunci, pemilik dan kolom tanggalnya didukung mesin formulir.
"""
import collections
import csv
import io
import pathlib
import re
import sys

REPO = pathlib.Path(__file__).resolve().parent.parent
SUMBER = REPO.parent / 'simrs-khanza' / 'src'
SKEMA = (REPO / 'docs/audit/schema_sik.sql').read_text(encoding='utf-8', errors='replace')

DDL = {}
for m in re.finditer(r'CREATE TABLE `(\w+)` \((.*?)\)\s*ENGINE', SKEMA, re.S):
    DDL.setdefault(m.group(1), m.group(2))

# Urutan menentukan kelompok bila nama jendela memuat beberapa kata.
KELOMPOK = [
    ('Checklist', 'checklist'), ('SignIn', 'bedah'), ('SignOut', 'bedah'), ('TimeOut', 'bedah'),
    ('PreAnestesi', 'anestesi'), ('PreInduksi', 'anestesi'), ('PreOperasi', 'bedah'), ('Anestesi', 'anestesi'),
    ('Medis', 'penilaian medis'), ('Keperawatan', 'keperawatan'), ('Risiko', 'risiko/skrining'),
    ('Jatuh', 'risiko/skrining'), ('Dekubitus', 'risiko/skrining'), ('Nyeri', 'nyeri'), ('Skrining', 'risiko/skrining'),
    ('EWS', 'EWS'), ('EarlyWarning', 'EWS'), ('Pemantauan', 'pemantauan'), ('Observasi', 'pemantauan'),
    ('Monitoring', 'pemantauan'), ('Followup', 'pemantauan'), ('Gizi', 'gizi'), ('Psikolog', 'psikologi'),
    ('Skor', 'skor'), ('Skala', 'skor'), ('Hasil', 'hasil pemeriksaan'), ('Catatan', 'catatan'),
    ('Konseling', 'konseling'), ('Edukasi', 'edukasi'), ('Transfer', 'transfer'),
]


def kolom(tabel):
    pola = r"^\s*`(\w+)` (\w+)(?:\((.*)\))?[ ,\r\n]"
    return [(m.group(1), m.group(2), m.group(3) or '') for m in re.finditer(pola, DDL[tabel] + '\n', re.M)]


def kunci(tabel):
    m = re.search(r'PRIMARY KEY \(([^)]*)\)', DDL[tabel])
    return m.group(1).replace('`', '').split(',') if m else []


def pemilik(tabel):
    pola = r'FOREIGN KEY \(`(\w+)`\) REFERENCES `(dokter|petugas|pegawai)`'
    return {m.group(1): m.group(2) for m in re.finditer(pola, DDL[tabel])}


def survei():
    berkas = REPO / 'docs/tracker/peta-fungsi.tsv'
    peta = csv.DictReader(io.StringIO(berkas.read_text(encoding='utf-8-sig')), delimiter='\t')
    hasil = []
    for r in peta:
        if r['Modul'] != 'rekammedis' or r['Status'] == 'tulis ada' or r['Tulis ada'].strip():
            continue
        tabel = sorted({a.split('|')[0] for a in re.findall(r'\S+\|\w+', r['Tulis belum'])})
        if len(tabel) != 1 or tabel[0] not in DDL:
            continue
        t = tabel[0]
        k, pk, own = kolom(t), kunci(t), pemilik(t)
        waktu = [c for c in k if c[1] in ('datetime', 'date', 'timestamp')]
        utama = [c for c in waktu if c[0] in ('tanggal', 'tgl_perawatan', 'tgl_asuhan', 'tgl_penilaian', 'tgl_skrining', 'waktu')]
        masalah = []
        if len(own) != 1:
            masalah.append('pemilik')
        if not pk or pk[0] != 'no_rawat' or len(pk) > 3:
            masalah.append('kunci')
        if any(not utama or c[0] != utama[0][0] for c in waktu):
            masalah.append('tanggal lain')
        hasil.append({'kelas': r['Jendela'], 'tabel': t, 'kolom': len(k), 'kunci': pk, 'pemilik': own,
                      'enum': sum(c[1] == 'enum' for c in k),
                      'angka': sum(c[1] in ('double', 'float', 'decimal', 'int', 'tinyint', 'smallint') and c[0] not in own for c in k),
                      'masalah': masalah, 'berkas': SUMBER / r['Sumber']})
    return hasil


def kelompok(nama):
    return next((g for kata, g in KELOMPOK if kata.lower() in nama.lower()), 'lain')


def daftar(hasil):
    print('satu tabel belum ada:', len(hasil), '| cocok pola mesin:', sum(not h['masalah'] for h in hasil))
    per = collections.defaultdict(list)
    for h in hasil:
        per[kelompok(h['kelas'])].append(h)
    for g, hs in sorted(per.items(), key=lambda x: -len(x[1])):
        cocok = [h for h in hs if not h['masalah']]
        print(f'\n## {g}: {len(hs)} (cocok {len(cocok)})')
        for h in cocok:
            print(f"  {h['kelas']}  kolom={h['kolom']} kunci={len(h['kunci'])} pemilik={list(h['pemilik'].values())[0]} "
                  f"angka={h['angka']} enum={h['enum']}")


def argumen(teks):
    """Pisahkan daftar argumen Java tingkat atas."""
    keluar, buf, dalam, kutip = [], '', 0, False
    for i, c in enumerate(teks):
        if kutip:
            buf += c
            kutip = not (c == '"' and teks[i - 1] != '\\')
            continue
        kutip = c == '"'
        dalam += (c in '([{') - (c in ')]}')
        if c == ',' and dalam == 0:
            keluar.append(buf.strip())
            buf = ''
        else:
            buf += c
    return keluar + ([buf.strip()] if buf.strip() else [])


def rinci(h):
    src = h['berkas'].read_text(encoding='utf-8', errors='replace')
    t, k = h['tabel'], kolom(h['tabel'])
    print(f"\n==================== {h['kelas']} | {t} | kunci={h['kunci']} | pemilik={h['pemilik']}")
    print('KOLOM:', '; '.join(f"{n}:{ty}{'(' + r + ')' if r else ''}" for n, ty, r in k))
    simpan = re.search(r'menyimpantf\d?\(\s*"%s"\s*,.*?new String\[\]\s*\{(.*?)\}\s*\)\s*(?:==|\)|;)' % t, src, re.S)
    if simpan:
        nilai = argumen(simpan.group(1))
        pasangan = []
        for (n, _, _), a in zip(k, nilai):
            komponen = re.match(r'(?:Valid\.SetTgl\()?\(?(\w+)', a)
            pasangan.append(f'{n}={komponen.group(1) if komponen else a[:30]}')
        print('SIMPAN:', ' '.join(pasangan), f'({len(nilai)}/{len(k)})')
    else:
        print('SIMPAN: pola tidak dikenali, baca method simpan di Java')
    kepala = re.search(r'new Object\[\]\s*\{(\s*"No\.Rawat".*?)\}', src, re.S)
    if kepala:
        print('HEADER:', ' | '.join(re.findall(r'"([^"]*)"', kepala.group(1))))
    handler = src.split('BtnSimpanActionPerformed(java.awt.event.ActionEvent evt) {', 1)[-1].split('KeyPressed', 1)[0]
    print('WAJIB:', re.findall(r'textKosong\(\s*(\w+)\s*,\s*"([^"]+)"', handler))
    label = {m.group(1): m.group(2) for m in re.finditer(r'(\w+)\.setText\("([^"]*)"\);', src)
             if m.group(1).startswith(('jLabel', 'label'))}
    letak = {m.group(1): tuple(map(int, m.groups()[1:]))
             for m in re.finditer(r'(\w+)\.setBounds\((-?\d+),\s*(-?\d+),\s*(-?\d+),\s*(-?\d+)\)', src)}
    print('LABEL(y,x):', ' | '.join(f'{label[c]}@{y},{x}' for y, x, c in
                                    sorted((b[1], b[0], c) for c, b in letak.items() if label.get(c))))
    print('KOMPONEN(y,x):', ' | '.join(f'{c}@{b[1]},{b[0]}' for c, b in sorted(letak.items(), key=lambda kv: (kv[1][1], kv[1][0]))
                                       if c not in label and not c.startswith(('scroll', 'Scroll', 'Btn', 'panel'))))
    print('MAKS:', {m.group(1): int(m.group(2)) for m in
                    re.finditer(r'(\w+)\.setDocument\(new batasInput\(\(?(?:byte|int)?\)?\s*(\d+)\)', src)})
    pilihan = re.findall(r'(\w+)\.setModel\(new javax\.swing\.DefaultComboBoxModel(?:<>)?\(new String\[\]\s*\{([^}]*)\}', src)
    print('PILIHAN:', [(c, re.findall(r'"([^"]*)"', v)) for c, v in pilihan if c not in ('Jam', 'Menit', 'Detik')])
    print('ATURAN:', sorted(set(re.findall(r'cekTanggal48jam|cekTanggalRegistrasi|TANGGALMUNDUR|akses\.getkode\(\)', src))))
    print('IZIN:', re.findall(r'BtnSimpan\.setEnabled\(akses\.get(\w+)\(\)\)', src))
    hitung = [l.strip() for l in src.split('\n')
              if re.search(r'(Double\.parseDouble|Valid\.SetAngka|Integer\.parseInt|getSelectedIndex\(\)\s*==)', l) and 'Jam' not in l]
    if hitung:
        print('HITUNG (12 baris pertama):', *hitung[:12], sep='\n  ')


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    semua = survei()
    if len(sys.argv) == 1:
        daftar(semua)
    else:
        per_nama = {h['kelas']: h for h in semua}
        for nama in sys.argv[1:]:
            if nama not in per_nama:
                sys.exit(f'{nama} tidak ada di daftar jendela satu tabel yang belum ditulis.')
            rinci(per_nama[nama])
