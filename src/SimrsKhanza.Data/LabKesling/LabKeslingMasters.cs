using SimrsKhanza.Config;
using SimrsKhanza.Data.Master;

namespace SimrsKhanza.Data.LabKesling;

/// <summary>
/// The four Lab Kesling master tables (legacy LabKeslingPelanggan, LabKeslingMasterSampelBakuMutu,
/// LabKeslingParameterPengujian, LabKeslingNilaiNormalBakuMutu) as data, edited by one generic screen.
/// </summary>
public static class LabKeslingMasters
{
    private static readonly string[] Jasa =
        ["jasa_sarana", "paket_bhp", "jasa_pj_lab", "jasa_pj_pengujian", "jasa_verifikator", "jasa_petugas", "kso", "jasa_menejemen"];

    public static readonly MasterSpec Pelanggan = new(
        "Pelanggan Lab Kesling", "labkesling_pelanggan",
        [
            new("kode_pelanggan", "Kode", MaxLen: 6, Key: true, AutoKey: true),
            new("nama_pelanggan", "Nama Pelanggan", MaxLen: 50),
            new("alamat", "Alamat", MaxLen: 50),
            new("kota", "Kota", MaxLen: 20),
            new("no_telp", "No.Telp", MaxLen: 13),
            new("kegiatan_usaha", "Kegiatan Usaha", MaxLen: 30),
            new("personal_dihubungi", "Personal Dihubungi", MaxLen: 30),
        ],
        "SELECT * FROM labkesling_pelanggan WHERE CONCAT_WS(' ',kode_pelanggan,nama_pelanggan,alamat,kota,no_telp,kegiatan_usaha,personal_dihubungi) LIKE @p0 ORDER BY kode_pelanggan",
        [("kode_pelanggan", "Kode"), ("nama_pelanggan", "Nama Pelanggan"), ("alamat", "Alamat"), ("kota", "Kota"),
         ("no_telp", "No.Telp"), ("kegiatan_usaha", "Kegiatan Usaha"), ("personal_dihubungi", "Personal Dihubungi")],
        "SELECT IFNULL(MAX(CONVERT(RIGHT(kode_pelanggan,6),SIGNED)),0) FROM labkesling_pelanggan", 6);

    public static readonly MasterSpec Sampel = new(
        "Master Sampel & Baku Mutu", "labkesling_master_sampel",
        [
            new("kode_sampel", "Kode Sampel", MaxLen: 4, Key: true),
            new("nama_sampel", "Nama Sampel", MaxLen: 40),
            new("baku_mutu", "Baku Mutu", MaxLen: 120),
        ],
        "SELECT * FROM labkesling_master_sampel WHERE CONCAT_WS(' ',kode_sampel,nama_sampel,baku_mutu) LIKE @p0 ORDER BY kode_sampel",
        [("kode_sampel", "Kode"), ("nama_sampel", "Nama Sampel"), ("baku_mutu", "Baku Mutu")]);

    public static readonly MasterSpec Parameter = new(
        "Parameter Pengujian", "labkesling_parameter_pengujian",
        [
            new("kode_parameter", "Kode", MaxLen: 5, Key: true, AutoKey: true),
            new("nama_parameter", "Nama Parameter", MaxLen: 70),
            new("metode_pengujian", "Metode", FieldKind.Choice, Choices:
                ["Spektrofotometri", "Elektrometri", "Gravimetri", "AAS", "Titrimetri", "Angka Lempeng Total",
                 "Membran Filter", "Tabung Fermentasi", "Biakan & Identifikasi", "Singlepath"]),
            new("satuan", "Satuan", MaxLen: 20),
            new("kategori", "Kategori", FieldKind.Choice, Choices: ["Fisika", "Kimia", "Mikrobiologi"]),
            new("jasa_sarana", "Jasa Sarana", FieldKind.Number),
            new("paket_bhp", "Paket BHP", FieldKind.Number),
            new("jasa_pj_lab", "Jasa PJ Lab", FieldKind.Number),
            new("jasa_pj_pengujian", "Jasa PJ Pengujian", FieldKind.Number),
            new("jasa_verifikator", "Jasa Verifikator", FieldKind.Number),
            new("jasa_petugas", "Jasa Petugas/Analis", FieldKind.Number),
            new("kso", "KSO", FieldKind.Number),
            new("jasa_menejemen", "Jasa Menejemen", FieldKind.Number),
        ],
        "SELECT * FROM labkesling_parameter_pengujian WHERE CONCAT_WS(' ',kode_parameter,nama_parameter,metode_pengujian,satuan,kategori) LIKE @p0 ORDER BY kode_parameter",
        [("kode_parameter", "Kode"), ("nama_parameter", "Nama Parameter"), ("metode_pengujian", "Metode"), ("satuan", "Satuan"),
         ("kategori", "Kategori"), ("jasa_sarana", "J.Sarana"), ("paket_bhp", "BHP"), ("jasa_pj_lab", "J.PJ Lab"),
         ("jasa_pj_pengujian", "J.PJ Uji"), ("jasa_verifikator", "J.Verif"), ("jasa_petugas", "J.Analis"), ("kso", "KSO"),
         ("jasa_menejemen", "J.Menej"), ("total", "Total")],
        "SELECT IFNULL(MAX(CONVERT(RIGHT(kode_parameter,3),SIGNED)),0) FROM labkesling_parameter_pengujian", 3,
        "total", Jasa);

    public static readonly MasterSpec NilaiNormal = new(
        "Nilai Normal Baku Mutu", "labkesling_nilai_normal_baku_mutu",
        [
            new("kode_sampel", "Sampel", FieldKind.Lookup, Key: true,
                LookupSql: "SELECT kode_sampel AS kode, nama_sampel AS nama FROM labkesling_master_sampel ORDER BY kode_sampel"),
            new("kode_parameter", "Parameter", FieldKind.Lookup, Key: true,
                LookupSql: "SELECT kode_parameter AS kode, CONCAT(nama_parameter,' (',IFNULL(satuan,''),')') AS nama FROM labkesling_parameter_pengujian ORDER BY kode_parameter"),
            new("nilai_normal", "Nilai Normal", MaxLen: 30),
        ],
        """
        SELECT n.kode_sampel, s.nama_sampel, n.kode_parameter, p.nama_parameter, p.satuan, n.nilai_normal
        FROM labkesling_nilai_normal_baku_mutu n
        JOIN labkesling_master_sampel s ON s.kode_sampel=n.kode_sampel
        JOIN labkesling_parameter_pengujian p ON p.kode_parameter=n.kode_parameter
        WHERE CONCAT_WS(' ',n.kode_sampel,s.nama_sampel,n.kode_parameter,p.nama_parameter,n.nilai_normal) LIKE @p0
        ORDER BY n.kode_sampel, n.kode_parameter
        """,
        [("kode_sampel", "Kd Sampel"), ("nama_sampel", "Sampel"), ("kode_parameter", "Kd Param"), ("nama_parameter", "Parameter"),
         ("satuan", "Satuan"), ("nilai_normal", "Nilai Normal")]);
}
