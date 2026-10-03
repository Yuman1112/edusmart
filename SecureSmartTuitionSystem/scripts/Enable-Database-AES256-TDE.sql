/* OPTIONAL DEPLOYMENT SCRIPT: not for LocalDB / Express.
   Run in SSMS as a database administrator after restoring a tested database backup
   to a SQL Server edition that supports TDE. The application never runs this script.
   TDE encrypts database/log/backup files, including complete payment and billing records.
   It does not replace authorization or hide data from an authorized database reader.
   https://learn.microsoft.com/sql/relational-databases/security/encryption/transparent-data-encryption
*/
USE [master];
GO
SET NOCOUNT ON;
DECLARE @DatabaseName sysname = N'SecureSmartTuitionSystemDb';
DECLARE @MasterKeyPassword nvarchar(128) = N'REPLACE_WITH_A_STRONG_MASTER_KEY_PASSWORD';
DECLARE @BackupPassword nvarchar(128) = N'REPLACE_WITH_A_DIFFERENT_CERTIFICATE_BACKUP_PASSWORD';
DECLARE @CertificateFile nvarchar(128) = N'REPLACE_WITH_CERTIFICATE_BACKUP_PATH';
DECLARE @PrivateKeyFile nvarchar(128) = N'REPLACE_WITH_PRIVATE_KEY_BACKUP_PATH';

IF CONVERT(int, SERVERPROPERTY('IsLocalDB')) = 1 OR
   CONVERT(int, SERVERPROPERTY('EngineEdition')) NOT IN (2, 3)
    THROW 51010, 'Use a SQL Server edition supporting TDE; LocalDB/Express is not supported by this script.', 1;
IF CONVERT(int, SERVERPROPERTY('ProductMajorVersion')) < 15
    THROW 51011, 'This deployment script requires SQL Server 2019 or later.', 1;
IF DB_ID(@DatabaseName) IS NULL
    THROW 51012, 'Restore the application database before enabling TDE.', 1;
IF @MasterKeyPassword LIKE N'REPLACE%' OR @BackupPassword LIKE N'REPLACE%' OR
   @CertificateFile LIKE N'REPLACE%' OR @PrivateKeyFile LIKE N'REPLACE%'
    THROW 51013, 'Set passwords and secure backup paths before running the script.', 1;
IF EXISTS (SELECT 1 FROM sys.dm_database_encryption_keys WHERE database_id = DB_ID(@DatabaseName) AND encryption_state = 3)
BEGIN
    PRINT 'Database encryption is already enabled. Verify key and certificate backups separately.';
    RETURN;
END;

IF EXISTS (SELECT 1 FROM sys.dm_database_encryption_keys WHERE database_id = DB_ID(@DatabaseName) AND (key_algorithm <> 'AES' OR key_length <> 256))
    THROW 51014, 'An existing encryption key is not AES-256. Review its migration before proceeding.', 1;

DECLARE @Sql nvarchar(max);
IF NOT EXISTS (SELECT 1 FROM sys.symmetric_keys WHERE name = '##MS_DatabaseMasterKey##')
BEGIN
    SET @Sql = N'CREATE MASTER KEY ENCRYPTION BY PASSWORD = ' + QUOTENAME(@MasterKeyPassword, '''') + N';';
    EXEC sp_executesql @Sql;
END;
IF NOT EXISTS (SELECT 1 FROM sys.certificates WHERE name = 'EduSmartTDECertificate')
    CREATE CERTIFICATE [EduSmartTDECertificate] WITH SUBJECT = 'EduSmart database encryption';

-- Backup first. If backup fails, encryption is not enabled.
-- SQL Server's service account must be able to write these paths; move the backups offline afterward.
SET @Sql = N'BACKUP CERTIFICATE [EduSmartTDECertificate] TO FILE = ' + QUOTENAME(@CertificateFile, '''') +
    N' WITH PRIVATE KEY (FILE = ' + QUOTENAME(@PrivateKeyFile, '''') +
    N', ENCRYPTION BY PASSWORD = ' + QUOTENAME(@BackupPassword, '''') + N');';
EXEC sp_executesql @Sql;

SET @Sql = N'USE ' + QUOTENAME(@DatabaseName) + N';
IF NOT EXISTS (SELECT 1 FROM sys.dm_database_encryption_keys WHERE database_id = DB_ID())
    CREATE DATABASE ENCRYPTION KEY WITH ALGORITHM = AES_256 ENCRYPTION BY SERVER CERTIFICATE [EduSmartTDECertificate];
ALTER DATABASE ' + QUOTENAME(@DatabaseName) + N' SET ENCRYPTION ON;';
EXEC sp_executesql @Sql;

SELECT DB_NAME(database_id) AS DatabaseName, encryption_state, key_algorithm, key_length, percent_complete
FROM sys.dm_database_encryption_keys WHERE database_id = DB_ID(@DatabaseName);
-- Verify encryption_state=3, key_algorithm=AES, key_length=256 after the background scan completes.
-- Never lose the certificate, private-key backup or their password: encrypted backups require them for restore.
GO
