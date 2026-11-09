namespace SimrsKhanza.Data;

/// <summary>
/// Legacy splits the lab into three categories with separate screens and, for
/// permintaan, separate tables (permintaan_lab / permintaan_labpa / permintaan_labmb).
/// periksa_lab / jns_perawatan_lab carry it in their `kategori` enum column.
/// </summary>
public enum LabCategory { PK, PA, MB }
