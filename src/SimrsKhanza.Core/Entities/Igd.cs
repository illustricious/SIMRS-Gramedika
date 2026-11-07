namespace SimrsKhanza.Core.Entities;

public enum CaraMasukIgd
{
    Jalan,
    Brankar,
    KursiRoda,
    Digendong,
}

public enum AlatTransportasiIgd
{
    Tidak, // legacy "-"
    Agd,
    Sendiri,
    Swasta,
}

public enum AlasanKedatanganIgd
{
    DatangSendiri,
    Polisi,
    Rujukan,
    Bidan,
    Puskesmas,
    RumahSakit,
    Poliklinik,
    FaskesLain,
    TidakDiketahui, // legacy "-"
}

/// <summary>Maps to legacy table `master_triase_macam_kasus` (PK: kode_kasus) — small lookup, read into a ComboBox.</summary>
public class MasterTriaseKasus
{
    public string KodeKasus { get; set; } = string.Empty;
    public string MacamKasus { get; set; } = string.Empty;
}

/// <summary>
/// Maps to legacy table `data_triase_igd` (PK: no_rawat) — the triage intake
/// record (how the patient arrived + initial vitals + case category). v1
/// deliberately stops here: the 5 `data_triase_igddetail_skala*` tables (the
/// actual ATS-style severity scoring criteria) are NOT implemented — see
/// docs/audit/HANDOVER.md. After triage, the doctor's exam itself reuses the
/// existing Rawat Jalan screen (its queue already includes IGD visits, since
/// it queries reg_periksa without filtering by poliklinik).
/// </summary>
public class TriaseIgd
{
    public string NoRawat { get; set; } = string.Empty;
    public RegPeriksa? RegPeriksa { get; set; }
    public DateTime TanggalKunjungan { get; set; }
    public CaraMasukIgd CaraMasuk { get; set; }
    public AlatTransportasiIgd AlatTransportasi { get; set; }
    public AlasanKedatanganIgd AlasanKedatangan { get; set; }
    public string KeteranganKedatangan { get; set; } = string.Empty;
    public string KodeKasus { get; set; } = string.Empty;
    public MasterTriaseKasus? Kasus { get; set; }
    public string TekananDarah { get; set; } = string.Empty;
    public string Nadi { get; set; } = string.Empty;
    public string Pernapasan { get; set; } = string.Empty;
    public string Suhu { get; set; } = string.Empty;
    public string SaturasiO2 { get; set; } = string.Empty;
    public string Nyeri { get; set; } = string.Empty;
}
