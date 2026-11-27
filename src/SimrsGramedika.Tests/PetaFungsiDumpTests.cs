using System.Collections;
using System.Reflection;
using System.Text.Json;
using SimrsGramedika.Data.Master;
using SimrsGramedika.Data.RekamMedis;

namespace SimrsGramedika.Tests;

/// <summary>
/// Menulis daftar layar generik (spesifikasi dan formulir) ke berkas JSON untuk tools/peta-fungsi.mjs.
/// Dibaca dari objek yang dipakai aplikasi, sehingga nama tabel hasil rakitan kode ikut tercatat.
/// </summary>
public sealed class PetaFungsiDumpTests
{
    private sealed record Spek(string Katalog, string Tile, string Jenis, string Judul, string[] Tabel,
        bool Tambah, bool Ubah, bool Hapus, string[] Sql);

    public sealed class DumpFactAttribute : FactAttribute
    {
        public DumpFactAttribute()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SIMRS_PETA_DUMP")))
                Skip = "Hanya dijalankan oleh tools/peta-fungsi.mjs.";
        }
    }

    [DumpFact]
    public void TulisDaftarSpesifikasi()
    {
        var hasil = new List<Spek>();
        const BindingFlags semua = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        var assemblies = new[] { typeof(MasterSpec).Assembly, typeof(SimrsGramedika.UI.App).Assembly };
        foreach (var type in assemblies.SelectMany(a => a.GetTypes()).OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            if (type.ContainsGenericParameters) continue;
            var nilai = type.GetProperties(semua).Where(p => p.GetIndexParameters().Length == 0)
                .Select(p => (p.Name, Tipe: p.PropertyType, Ambil: (Func<object?>)(() => p.GetValue(null))))
                .Concat(type.GetFields(semua).Where(f => !f.Name.Contains('<'))
                    .Select(f => (f.Name, Tipe: f.FieldType, Ambil: (Func<object?>)(() => f.GetValue(null)))));
            foreach (var anggota in nilai)
            {
                if (anggota.Tipe == typeof(MasterSpec) || anggota.Tipe == typeof(ReportSpec))
                    Tambah(hasil, $"{type.Name}.{anggota.Name}", "", anggota.Ambil());
                else if (typeof(IEnumerable).IsAssignableFrom(anggota.Tipe)
                    && anggota.Tipe.IsGenericType
                    && anggota.Tipe.GetGenericArguments() is [var kunci, var isi] && kunci == typeof(string)
                    && (isi == typeof(MasterSpec) || isi == typeof(ReportSpec) || isi == typeof(MasterSpec[]))
                    && anggota.Ambil() is IEnumerable daftar)
                {
                    foreach (var pasangan in daftar)
                    {
                        var jenis = pasangan.GetType();
                        var tile = (string)jenis.GetProperty("Key")!.GetValue(pasangan)!;
                        var nilaiSpek = jenis.GetProperty("Value")!.GetValue(pasangan);
                        foreach (var spek in nilaiSpek is MasterSpec[] banyak ? banyak : new object?[] { nilaiSpek })
                            Tambah(hasil, $"{type.Name}.{anggota.Name}", tile, spek);
                    }
                }
            }
        }
        foreach (var form in FormKlinisKatalog.Semua)
            hasil.Add(new("FormKlinisKatalog", form.Tile, "formulir", form.Judul, [form.Tabel], true, true, true, []));

        File.WriteAllText(Environment.GetEnvironmentVariable("SIMRS_PETA_DUMP")!,
            JsonSerializer.Serialize(hasil, new JsonSerializerOptions { WriteIndented = true }));
        Assert.NotEmpty(hasil);
    }

    private static void Tambah(List<Spek> hasil, string katalog, string tile, object? spec)
    {
        switch (spec)
        {
            case MasterSpec m:
                hasil.Add(new(katalog, tile, "master", m.Title, [m.Table], m.CanInsert || m.Singleton || m.Upsert, m.CanEdit, m.CanDelete,
                    [m.ListSql, .. m.Actions.Select(a => a.Sql)]));
                break;
            case ReportSpec r:
                hasil.Add(new(katalog, tile, "laporan", r.Title, [], false, false, false,
                    r.VisitSql is null ? [r.Sql] : [r.Sql, r.VisitSql]));
                break;
        }
    }
}
