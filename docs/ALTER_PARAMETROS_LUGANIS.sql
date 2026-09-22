/*
    Agrega columnas LUGANIS a la tabla Parametros.
    Cada empresa (emp_codigo) tendrá sus propios valores en estas columnas.
    Base de datos: steelltec (ajustar USE si aplica)
*/

USE [steelltec]
GO

-- Columnas LUGANIS (una por parámetro)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Parametros') AND name = 'par_luganis_baseurl')
    ALTER TABLE dbo.Parametros ADD par_luganis_baseurl VARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Parametros') AND name = 'par_luganis_companycode')
    ALTER TABLE dbo.Parametros ADD par_luganis_companycode VARCHAR(50) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Parametros') AND name = 'par_luganis_username')
    ALTER TABLE dbo.Parametros ADD par_luganis_username VARCHAR(80) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Parametros') AND name = 'par_luganis_password')
    ALTER TABLE dbo.Parametros ADD par_luganis_password VARCHAR(100) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Parametros') AND name = 'par_luganis_appversion')
    ALTER TABLE dbo.Parametros ADD par_luganis_appversion VARCHAR(20) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Parametros') AND name = 'par_luganis_os')
    ALTER TABLE dbo.Parametros ADD par_luganis_os VARCHAR(30) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Parametros') AND name = 'par_luganis_deviceid')
    ALTER TABLE dbo.Parametros ADD par_luganis_deviceid VARCHAR(100) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Parametros') AND name = 'par_luganis_latitude')
    ALTER TABLE dbo.Parametros ADD par_luganis_latitude VARCHAR(20) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Parametros') AND name = 'par_luganis_longitude')
    ALTER TABLE dbo.Parametros ADD par_luganis_longitude VARCHAR(20) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Parametros') AND name = 'par_luganis_providerip')
    ALTER TABLE dbo.Parametros ADD par_luganis_providerip VARCHAR(50) NULL;

PRINT 'Columnas LUGANIS agregadas a Parametros.';
GO
