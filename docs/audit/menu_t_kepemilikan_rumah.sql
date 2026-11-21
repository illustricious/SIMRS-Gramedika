-- Run in the database selected for SIMRS Khanza.
CREATE TABLE IF NOT EXISTS `zis_keterangan_kepemilikan_rumah_penerima_dankes` (
    `kode` CHAR(3) NOT NULL,
    `keterangan` VARCHAR(40) DEFAULT NULL,
    PRIMARY KEY (`kode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
