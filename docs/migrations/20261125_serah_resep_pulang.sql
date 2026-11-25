-- Apply once to the Khanza database before using Catat Serah Terima Obat.
CREATE TABLE IF NOT EXISTS gramedika_serah_resep_pulang (
    no_permintaan CHAR(14) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    no_rawat VARCHAR(17) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    waktu DATETIME NOT NULL,
    petugas VARCHAR(100) NOT NULL,
    penerima VARCHAR(100) NOT NULL,
    PRIMARY KEY (no_permintaan)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
