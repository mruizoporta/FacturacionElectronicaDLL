/*
================================================================================
Despliegue idempotente LUGANIS + columnas Parametros (mesas_abiertas, etc.)
================================================================================
- Compatible SQL Server 2008 R2 en adelante:
  * No usa CREATE OR ALTER (2016+)
  * No usa WITH VALUES en ADD COLUMN (2012+); agrega BIT nullable, rellena, NOT NULL, default
- Cada columna en tablas documento solo se toca si la tabla existe.
- Tras este script, ejecute en orden (si aplica a su BD):
    docs/ALTER_vwFacturasDetalleXML_LuganisITBIS.sql  (o su vista optimizada)
    docs/SP_GetFacturaDetalleXML.sql                   (GetFacturaDetalleXML)
    docs/SP_UPDATEDATOSLUGANIS.sql                     (cuerpo del SP; ya es ALTER)
  Vistas vwDevolucionesDetalleXML / vwFormasPagoXML y fn_obtenerTotalGravadoDGINew
  manténgalas en scripts separados si son muy grandes; use el mismo patrón:
    IF OBJECT_ID('dbo.Nombre','V') IS NULL EXEC('CREATE VIEW dbo.Nombre AS SELECT 1 AS x');
    GO
    ALTER VIEW dbo.Nombre AS ...
================================================================================
*/

SET NOCOUNT ON;
GO

/* 0) Base de datos: descomente y ajuste */
-- USE [SuBaseDeDatos];
-- GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

/* =========================================================
   1) dbo.Parametros: mesas_abiertas (sin WITH VALUES)
   ========================================================= */
IF OBJECT_ID(N'dbo.Parametros', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Parametros', 'mesas_abiertas') IS NULL
    BEGIN
        ALTER TABLE dbo.Parametros ADD mesas_abiertas BIT NULL;
        UPDATE dbo.Parametros SET mesas_abiertas = 0 WHERE mesas_abiertas IS NULL;
        ALTER TABLE dbo.Parametros ALTER COLUMN mesas_abiertas BIT NOT NULL;
        IF NOT EXISTS (
            SELECT 1
            FROM sys.default_constraints dc
            INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
            WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Parametros') AND c.name = N'mesas_abiertas'
        )
            ALTER TABLE dbo.Parametros ADD CONSTRAINT DF_Parametros_mesas_abiertas DEFAULT ((0)) FOR mesas_abiertas;
    END
    ELSE
    BEGIN
        IF EXISTS (
            SELECT 1 FROM sys.columns c
            INNER JOIN sys.objects o ON o.object_id = c.object_id
            WHERE o.name = N'Parametros' AND SCHEMA_NAME(o.schema_id) = N'dbo'
              AND c.name = N'mesas_abiertas' AND c.is_nullable = 1
        )
        BEGIN
            UPDATE dbo.Parametros SET mesas_abiertas = ISNULL(mesas_abiertas, 0) WHERE mesas_abiertas IS NULL;
            ALTER TABLE dbo.Parametros ALTER COLUMN mesas_abiertas BIT NOT NULL;
        END
        IF NOT EXISTS (
            SELECT 1
            FROM sys.default_constraints dc
            INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
            WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Parametros') AND c.name = N'mesas_abiertas'
        )
            ALTER TABLE dbo.Parametros ADD CONSTRAINT DF_Parametros_mesas_abiertas DEFAULT ((0)) FOR mesas_abiertas;
    END
END
ELSE
    PRINT N'Aviso: dbo.Parametros no existe; se omitió mesas_abiertas.';
GO

/* =========================================================
   1b) dbo.Parametros: integracion_luganis (mismo criterio)
   ========================================================= */
IF OBJECT_ID(N'dbo.Parametros', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Parametros', 'integracion_luganis') IS NULL
    BEGIN
        ALTER TABLE dbo.Parametros ADD integracion_luganis BIT NULL;
        UPDATE dbo.Parametros SET integracion_luganis = 0 WHERE integracion_luganis IS NULL;
        ALTER TABLE dbo.Parametros ALTER COLUMN integracion_luganis BIT NOT NULL;
        IF NOT EXISTS (
            SELECT 1
            FROM sys.default_constraints dc
            INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
            WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Parametros') AND c.name = N'integracion_luganis'
        )
            ALTER TABLE dbo.Parametros ADD CONSTRAINT DF_Parametros_integracion_luganis DEFAULT ((0)) FOR integracion_luganis;
    END
    ELSE
    BEGIN
        IF EXISTS (
            SELECT 1 FROM sys.columns c
            INNER JOIN sys.objects o ON o.object_id = c.object_id
            WHERE o.name = N'Parametros' AND SCHEMA_NAME(o.schema_id) = N'dbo'
              AND c.name = N'integracion_luganis' AND c.is_nullable = 1
        )
        BEGIN
            UPDATE dbo.Parametros SET integracion_luganis = ISNULL(integracion_luganis, 0) WHERE integracion_luganis IS NULL;
            ALTER TABLE dbo.Parametros ALTER COLUMN integracion_luganis BIT NOT NULL;
        END
        IF NOT EXISTS (
            SELECT 1
            FROM sys.default_constraints dc
            INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
            WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Parametros') AND c.name = N'integracion_luganis'
        )
            ALTER TABLE dbo.Parametros ADD CONSTRAINT DF_Parametros_integracion_luganis DEFAULT ((0)) FOR integracion_luganis;
    END
END
GO

/* =========================================================
   2) Columnas LUGANIS en tablas de documentos
   ========================================================= */
IF OBJECT_ID(N'dbo.Facturas', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Facturas', 'trackIdLuganis') IS NULL       ALTER TABLE dbo.Facturas ADD trackIdLuganis VARCHAR(200) NULL;
    IF COL_LENGTH('dbo.Facturas', 'filenameLuganis') IS NULL      ALTER TABLE dbo.Facturas ADD filenameLuganis VARCHAR(150) NULL;
    IF COL_LENGTH('dbo.Facturas', 'qrCodeLuganis') IS NULL       ALTER TABLE dbo.Facturas ADD qrCodeLuganis VARCHAR(500) NULL;
    IF COL_LENGTH('dbo.Facturas', 'AceptadoLuganis') IS NULL      ALTER TABLE dbo.Facturas ADD AceptadoLuganis BIT NULL;
    IF COL_LENGTH('dbo.Facturas', 'Error_Luganis') IS NULL       ALTER TABLE dbo.Facturas ADD Error_Luganis BIT NULL;
    IF COL_LENGTH('dbo.Facturas', 'FechaEnvioLuganis') IS NULL    ALTER TABLE dbo.Facturas ADD FechaEnvioLuganis DATETIME NULL;
END

IF OBJECT_ID(N'dbo.ProvFacturas', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.ProvFacturas', 'trackIdLuganis') IS NULL       ALTER TABLE dbo.ProvFacturas ADD trackIdLuganis VARCHAR(200) NULL;
    IF COL_LENGTH('dbo.ProvFacturas', 'filenameLuganis') IS NULL      ALTER TABLE dbo.ProvFacturas ADD filenameLuganis VARCHAR(150) NULL;
    IF COL_LENGTH('dbo.ProvFacturas', 'qrCodeLuganis') IS NULL       ALTER TABLE dbo.ProvFacturas ADD qrCodeLuganis VARCHAR(500) NULL;
    IF COL_LENGTH('dbo.ProvFacturas', 'AceptadoLuganis') IS NULL      ALTER TABLE dbo.ProvFacturas ADD AceptadoLuganis BIT NULL;
    IF COL_LENGTH('dbo.ProvFacturas', 'Error_Luganis') IS NULL       ALTER TABLE dbo.ProvFacturas ADD Error_Luganis BIT NULL;
    IF COL_LENGTH('dbo.ProvFacturas', 'FechaEnvioLuganis') IS NULL    ALTER TABLE dbo.ProvFacturas ADD FechaEnvioLuganis DATETIME NULL;
END

IF OBJECT_ID(N'dbo.Desembolsos', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Desembolsos', 'trackIdLuganis') IS NULL       ALTER TABLE dbo.Desembolsos ADD trackIdLuganis VARCHAR(200) NULL;
    IF COL_LENGTH('dbo.Desembolsos', 'filenameLuganis') IS NULL      ALTER TABLE dbo.Desembolsos ADD filenameLuganis VARCHAR(150) NULL;
    IF COL_LENGTH('dbo.Desembolsos', 'qrCodeLuganis') IS NULL       ALTER TABLE dbo.Desembolsos ADD qrCodeLuganis VARCHAR(500) NULL;
    IF COL_LENGTH('dbo.Desembolsos', 'AceptadoLuganis') IS NULL      ALTER TABLE dbo.Desembolsos ADD AceptadoLuganis BIT NULL;
    IF COL_LENGTH('dbo.Desembolsos', 'Error_Luganis') IS NULL       ALTER TABLE dbo.Desembolsos ADD Error_Luganis BIT NULL;
    IF COL_LENGTH('dbo.Desembolsos', 'FechaEnvioLuganis') IS NULL    ALTER TABLE dbo.Desembolsos ADD FechaEnvioLuganis DATETIME NULL;
END

IF OBJECT_ID(N'dbo.Devolucion', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Devolucion', 'trackIdLuganis') IS NULL       ALTER TABLE dbo.Devolucion ADD trackIdLuganis VARCHAR(200) NULL;
    IF COL_LENGTH('dbo.Devolucion', 'filenameLuganis') IS NULL      ALTER TABLE dbo.Devolucion ADD filenameLuganis VARCHAR(150) NULL;
    IF COL_LENGTH('dbo.Devolucion', 'qrCodeLuganis') IS NULL       ALTER TABLE dbo.Devolucion ADD qrCodeLuganis VARCHAR(500) NULL;
    IF COL_LENGTH('dbo.Devolucion', 'AceptadoLuganis') IS NULL      ALTER TABLE dbo.Devolucion ADD AceptadoLuganis BIT NULL;
    IF COL_LENGTH('dbo.Devolucion', 'Error_Luganis') IS NULL       ALTER TABLE dbo.Devolucion ADD Error_Luganis BIT NULL;
    IF COL_LENGTH('dbo.Devolucion', 'FechaEnvioLuganis') IS NULL    ALTER TABLE dbo.Devolucion ADD FechaEnvioLuganis DATETIME NULL;
END

IF OBJECT_ID(N'dbo.NotasCredito', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.NotasCredito', 'trackIdLuganis') IS NULL       ALTER TABLE dbo.NotasCredito ADD trackIdLuganis VARCHAR(200) NULL;
    IF COL_LENGTH('dbo.NotasCredito', 'filenameLuganis') IS NULL      ALTER TABLE dbo.NotasCredito ADD filenameLuganis VARCHAR(150) NULL;
    IF COL_LENGTH('dbo.NotasCredito', 'qrCodeLuganis') IS NULL       ALTER TABLE dbo.NotasCredito ADD qrCodeLuganis VARCHAR(500) NULL;
    IF COL_LENGTH('dbo.NotasCredito', 'AceptadoLuganis') IS NULL      ALTER TABLE dbo.NotasCredito ADD AceptadoLuganis BIT NULL;
    IF COL_LENGTH('dbo.NotasCredito', 'Error_Luganis') IS NULL       ALTER TABLE dbo.NotasCredito ADD Error_Luganis BIT NULL;
    IF COL_LENGTH('dbo.NotasCredito', 'FechaEnvioLuganis') IS NULL    ALTER TABLE dbo.NotasCredito ADD FechaEnvioLuganis DATETIME NULL;
END

IF OBJECT_ID(N'dbo.NotasDebito', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.NotasDebito', 'trackIdLuganis') IS NULL       ALTER TABLE dbo.NotasDebito ADD trackIdLuganis VARCHAR(200) NULL;
    IF COL_LENGTH('dbo.NotasDebito', 'filenameLuganis') IS NULL      ALTER TABLE dbo.NotasDebito ADD filenameLuganis VARCHAR(150) NULL;
    IF COL_LENGTH('dbo.NotasDebito', 'qrCodeLuganis') IS NULL       ALTER TABLE dbo.NotasDebito ADD qrCodeLuganis VARCHAR(500) NULL;
    IF COL_LENGTH('dbo.NotasDebito', 'AceptadoLuganis') IS NULL      ALTER TABLE dbo.NotasDebito ADD AceptadoLuganis BIT NULL;
    IF COL_LENGTH('dbo.NotasDebito', 'Error_Luganis') IS NULL       ALTER TABLE dbo.NotasDebito ADD Error_Luganis BIT NULL;
    IF COL_LENGTH('dbo.NotasDebito', 'FechaEnvioLuganis') IS NULL    ALTER TABLE dbo.NotasDebito ADD FechaEnvioLuganis DATETIME NULL;
END
GO

PRINT N'Columnas LUGANIS en tablas de documentos: verificadas.';
GO

/* =========================================================
   3) Columnas par_luganis* en Parametros
   ========================================================= */
IF OBJECT_ID(N'dbo.Parametros', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Parametros', 'par_luganis_baseurl') IS NULL     ALTER TABLE dbo.Parametros ADD par_luganis_baseurl VARCHAR(200) NULL;
    IF COL_LENGTH('dbo.Parametros', 'par_luganis_companycode') IS NULL  ALTER TABLE dbo.Parametros ADD par_luganis_companycode VARCHAR(50) NULL;
    IF COL_LENGTH('dbo.Parametros', 'par_luganis_username') IS NULL     ALTER TABLE dbo.Parametros ADD par_luganis_username VARCHAR(80) NULL;
    IF COL_LENGTH('dbo.Parametros', 'par_luganis_password') IS NULL     ALTER TABLE dbo.Parametros ADD par_luganis_password VARCHAR(100) NULL;
    IF COL_LENGTH('dbo.Parametros', 'par_luganis_appversion') IS NULL   ALTER TABLE dbo.Parametros ADD par_luganis_appversion VARCHAR(20) NULL;
    IF COL_LENGTH('dbo.Parametros', 'par_luganis_os') IS NULL           ALTER TABLE dbo.Parametros ADD par_luganis_os VARCHAR(30) NULL;
    IF COL_LENGTH('dbo.Parametros', 'par_luganis_deviceid') IS NULL    ALTER TABLE dbo.Parametros ADD par_luganis_deviceid VARCHAR(100) NULL;
    IF COL_LENGTH('dbo.Parametros', 'par_luganis_latitude') IS NULL    ALTER TABLE dbo.Parametros ADD par_luganis_latitude VARCHAR(20) NULL;
    IF COL_LENGTH('dbo.Parametros', 'par_luganis_longitude') IS NULL   ALTER TABLE dbo.Parametros ADD par_luganis_longitude VARCHAR(20) NULL;
    IF COL_LENGTH('dbo.Parametros', 'par_luganis_providerip') IS NULL  ALTER TABLE dbo.Parametros ADD par_luganis_providerip VARCHAR(50) NULL;
END
GO

PRINT N'Parametros LUGANIS: columnas verificadas. Ejecute docs/SP_UPDATEDATOSLUGANIS.sql y docs/SP_GetFacturaDetalleXML.sql si aplica.';
GO
