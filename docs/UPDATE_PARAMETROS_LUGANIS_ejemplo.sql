/*
  Parametros LUGANIS — valores de ejemplo desde pantalla "Parametros generales".
  Ajusta USE, emp_codigo, username y password antes de ejecutar.
*/
USE [tu_base];  -- <-- cambiar
GO

-- Columnas (idempotente)
IF OBJECT_ID(N'dbo.Parametros', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Parametros', 'integracion_luganis') IS NULL
        ALTER TABLE dbo.Parametros ADD integracion_luganis BIT NOT NULL
            CONSTRAINT DF_Parametros_integracion_luganis DEFAULT (0);

    IF COL_LENGTH('dbo.Parametros', 'par_luganis_baseurl') IS NULL
        ALTER TABLE dbo.Parametros ADD par_luganis_baseurl VARCHAR(200) NULL;
    IF COL_LENGTH('dbo.Parametros', 'par_luganis_companycode') IS NULL
        ALTER TABLE dbo.Parametros ADD par_luganis_companycode VARCHAR(50) NULL;
    IF COL_LENGTH('dbo.Parametros', 'par_luganis_username') IS NULL
        ALTER TABLE dbo.Parametros ADD par_luganis_username VARCHAR(80) NULL;
    IF COL_LENGTH('dbo.Parametros', 'par_luganis_password') IS NULL
        ALTER TABLE dbo.Parametros ADD par_luganis_password VARCHAR(100) NULL;
    IF COL_LENGTH('dbo.Parametros', 'par_luganis_appversion') IS NULL
        ALTER TABLE dbo.Parametros ADD par_luganis_appversion VARCHAR(20) NULL;
    IF COL_LENGTH('dbo.Parametros', 'par_luganis_os') IS NULL
        ALTER TABLE dbo.Parametros ADD par_luganis_os VARCHAR(30) NULL;
    IF COL_LENGTH('dbo.Parametros', 'par_luganis_deviceid') IS NULL
        ALTER TABLE dbo.Parametros ADD par_luganis_deviceid VARCHAR(100) NULL;
    IF COL_LENGTH('dbo.Parametros', 'par_luganis_latitude') IS NULL
        ALTER TABLE dbo.Parametros ADD par_luganis_latitude VARCHAR(20) NULL;
    IF COL_LENGTH('dbo.Parametros', 'par_luganis_longitude') IS NULL
        ALTER TABLE dbo.Parametros ADD par_luganis_longitude VARCHAR(20) NULL;
    IF COL_LENGTH('dbo.Parametros', 'par_luganis_providerip') IS NULL
        ALTER TABLE dbo.Parametros ADD par_luganis_providerip VARCHAR(50) NULL;
END
GO

UPDATE dbo.Parametros
SET
    integracion_luganis       = 1,
    par_luganis_baseurl       = N'https://rd.stage-api.tech-luganis.net',
    par_luganis_companycode   = N'COMPANY',
    par_luganis_username      = N'TU_USUARIO',      -- <-- cambiar (ej. acuedEnvio)
    par_luganis_password      = N'TU_PASSWORD',     -- <-- cambiar
    par_luganis_appversion    = N'999-999-9999',
    par_luganis_os            = N'Chrome Generic',
    par_luganis_deviceid      = N'999-999-9999',
    par_luganis_latitude      = N'99.999999',
    par_luganis_longitude     = N'-99.99999',
    par_luganis_providerip    = N'99.99.99.99'
WHERE emp_codigo = 1;  -- <-- cambiar si aplica
GO

-- Verificar
SELECT
    emp_codigo,
    integracion_luganis,
    par_luganis_baseurl,
    par_luganis_companycode,
    par_luganis_username,
    par_luganis_appversion,
    par_luganis_os,
    par_luganis_deviceid,
    par_luganis_latitude,
    par_luganis_longitude,
    par_luganis_providerip
FROM dbo.Parametros
WHERE emp_codigo = 1;
GO
