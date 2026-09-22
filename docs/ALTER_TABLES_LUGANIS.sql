/*
    Script para agregar columnas LUGANIS a las tablas.
    Ejecutar ANTES de crear SP_UPDATEDATOSLUGANIS si las columnas no existen.
    Idempotente: solo ALTER si la tabla existe (versiones viejas / BDs parciales).
    Preferible ejecutar docs/Script_Luganis_despliegue_idempotente.sql (incluye Parametros).
*/

-- USE [SuBaseDeDatos];
-- GO

-- Facturas
IF OBJECT_ID(N'dbo.Facturas', N'U') IS NOT NULL
BEGIN
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Facturas') AND name = 'trackIdLuganis')
    ALTER TABLE dbo.Facturas ADD trackIdLuganis VARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Facturas') AND name = 'filenameLuganis')
    ALTER TABLE dbo.Facturas ADD filenameLuganis VARCHAR(150) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Facturas') AND name = 'qrCodeLuganis')
    ALTER TABLE dbo.Facturas ADD qrCodeLuganis VARCHAR(500) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Facturas') AND name = 'AceptadoLuganis')
    ALTER TABLE dbo.Facturas ADD AceptadoLuganis BIT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Facturas') AND name = 'Error_Luganis')
    ALTER TABLE dbo.Facturas ADD Error_Luganis BIT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Facturas') AND name = 'FechaEnvioLuganis')
    ALTER TABLE dbo.Facturas ADD FechaEnvioLuganis DATETIME NULL;

-- ProvFacturas
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProvFacturas') AND name = 'trackIdLuganis')
    ALTER TABLE dbo.ProvFacturas ADD trackIdLuganis VARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProvFacturas') AND name = 'filenameLuganis')
    ALTER TABLE dbo.ProvFacturas ADD filenameLuganis VARCHAR(150) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProvFacturas') AND name = 'qrCodeLuganis')
    ALTER TABLE dbo.ProvFacturas ADD qrCodeLuganis VARCHAR(500) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProvFacturas') AND name = 'AceptadoLuganis')
    ALTER TABLE dbo.ProvFacturas ADD AceptadoLuganis BIT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProvFacturas') AND name = 'Error_Luganis')
    ALTER TABLE dbo.ProvFacturas ADD Error_Luganis BIT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProvFacturas') AND name = 'FechaEnvioLuganis')
    ALTER TABLE dbo.ProvFacturas ADD FechaEnvioLuganis DATETIME NULL;

-- Desembolsos
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Desembolsos') AND name = 'trackIdLuganis')
    ALTER TABLE dbo.Desembolsos ADD trackIdLuganis VARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Desembolsos') AND name = 'filenameLuganis')
    ALTER TABLE dbo.Desembolsos ADD filenameLuganis VARCHAR(150) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Desembolsos') AND name = 'qrCodeLuganis')
    ALTER TABLE dbo.Desembolsos ADD qrCodeLuganis VARCHAR(500) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Desembolsos') AND name = 'AceptadoLuganis')
    ALTER TABLE dbo.Desembolsos ADD AceptadoLuganis BIT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Desembolsos') AND name = 'Error_Luganis')
    ALTER TABLE dbo.Desembolsos ADD Error_Luganis BIT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Desembolsos') AND name = 'FechaEnvioLuganis')
    ALTER TABLE dbo.Desembolsos ADD FechaEnvioLuganis DATETIME NULL;

-- Devolucion
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Devolucion') AND name = 'trackIdLuganis')
    ALTER TABLE dbo.Devolucion ADD trackIdLuganis VARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Devolucion') AND name = 'filenameLuganis')
    ALTER TABLE dbo.Devolucion ADD filenameLuganis VARCHAR(150) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Devolucion') AND name = 'qrCodeLuganis')
    ALTER TABLE dbo.Devolucion ADD qrCodeLuganis VARCHAR(500) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Devolucion') AND name = 'AceptadoLuganis')
    ALTER TABLE dbo.Devolucion ADD AceptadoLuganis BIT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Devolucion') AND name = 'Error_Luganis')
    ALTER TABLE dbo.Devolucion ADD Error_Luganis BIT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Devolucion') AND name = 'FechaEnvioLuganis')
    ALTER TABLE dbo.Devolucion ADD FechaEnvioLuganis DATETIME NULL;

-- NotasCredito
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.NotasCredito') AND name = 'trackIdLuganis')
    ALTER TABLE dbo.NotasCredito ADD trackIdLuganis VARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.NotasCredito') AND name = 'filenameLuganis')
    ALTER TABLE dbo.NotasCredito ADD filenameLuganis VARCHAR(150) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.NotasCredito') AND name = 'qrCodeLuganis')
    ALTER TABLE dbo.NotasCredito ADD qrCodeLuganis VARCHAR(500) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.NotasCredito') AND name = 'AceptadoLuganis')
    ALTER TABLE dbo.NotasCredito ADD AceptadoLuganis BIT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.NotasCredito') AND name = 'Error_Luganis')
    ALTER TABLE dbo.NotasCredito ADD Error_Luganis BIT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.NotasCredito') AND name = 'FechaEnvioLuganis')
    ALTER TABLE dbo.NotasCredito ADD FechaEnvioLuganis DATETIME NULL;

-- NotasDebito
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.NotasDebito') AND name = 'trackIdLuganis')
    ALTER TABLE dbo.NotasDebito ADD trackIdLuganis VARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.NotasDebito') AND name = 'filenameLuganis')
    ALTER TABLE dbo.NotasDebito ADD filenameLuganis VARCHAR(150) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.NotasDebito') AND name = 'qrCodeLuganis')
    ALTER TABLE dbo.NotasDebito ADD qrCodeLuganis VARCHAR(500) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.NotasDebito') AND name = 'AceptadoLuganis')
    ALTER TABLE dbo.NotasDebito ADD AceptadoLuganis BIT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.NotasDebito') AND name = 'Error_Luganis')
    ALTER TABLE dbo.NotasDebito ADD Error_Luganis BIT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.NotasDebito') AND name = 'FechaEnvioLuganis')
    ALTER TABLE dbo.NotasDebito ADD FechaEnvioLuganis DATETIME NULL;

PRINT 'Columnas LUGANIS agregadas/verificadas correctamente.';
GO
