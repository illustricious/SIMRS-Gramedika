namespace SimrsGramedika.Data.Master;

public static class MenuTSpecs
{
    public static IReadOnlyDictionary<string, MasterSpec> Masters { get; } =
        new Dictionary<string, MasterSpec>(StringComparer.Ordinal)
        {
            ["btnZISPengeluaranPenerimaDankes"] = Reference("Ket Pengeluaran", "pengeluaran"),
            ["btnZISPenghasilanPenerimaDankes"] = Reference("Ket Penghasilan", "penghasilan"),
            ["btnZISUkuranRumahPenerimaDankes"] = Reference("Ukuran Rumah", "ukuran_rumah"),
            ["btnZISDindingRumahPenerimaDankes"] = Reference("Dinding Rumah", "dinding_rumah"),
            ["btnZISLantaiRumahPenerimaDankes"] = Reference("Lantai Rumah", "lantai_rumah"),
            ["btnZISAtapRumahPenerimaDankes"] = Reference("Atap Rumah", "atap_rumah"),
            ["btnZISKepemilikanRumahPenerimaDankes"] = Reference("Kepemilikan Rumah", "kepemilikan_rumah"),
            ["btnZISKamarMandiPenerimaDankes"] = Reference("Kamar Mandi", "kamar_mandi"),
            ["btnZISDapurRumahPenerimaDankes"] = Reference("Dapur Rumah", "dapur_rumah"),
            ["btnZISKursiRumahPenerimaDankes"] = Reference("Kursi Rumah", "kursi_rumah"),
            ["btnZISKategoriPHBSPenerimaDankes"] = Reference("Kategori PHBS", "kategori_phbs"),
            ["btnZISElektronikPenerimaDankes"] = Reference("Elektronik", "elektronik"),
            ["btnZISTernakPenerimaDankes"] = Reference("Ternak", "ternak"),
            ["btnZISJenisSimpananPenerimaDankes"] = Reference("Jenis Simpanan", "jenis_simpanan"),
            ["btnZISKategoriAsnafPenerimaDankes"] = Reference("Kategori Asnaf", "kategori_asnaf"),
            ["btnZISPatologisPenerimaDankes"] = Reference("Patologis", "patologis"),
        };

    private static MasterSpec Reference(string label, string subject)
    {
        var table = $"zis_keterangan_{subject}_penerima_dankes";
        return new(
            $"{label} Penerima Dankes", table,
            [
                new("kode", "Kode", MaxLen: 3, Key: true),
                new("keterangan", "Keterangan", MaxLen: 40, Required: true),
            ],
            $"SELECT kode, keterangan FROM `{table}` WHERE kode LIKE @p0 OR keterangan LIKE @p0 ORDER BY kode",
            [("kode", "Kode"), ("keterangan", "Keterangan")])
        {
            Hint = subject == "kepemilikan_rumah"
                ? "Referensi Kepemilikan Rumah. Siapkan tabel dengan skrip menu_t_kepemilikan_rumah.sql."
                : null,
        };
    }
}
