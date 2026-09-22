/* Ejecutar en la base correcta, por ejemplo:
   USE [rincon];
   GO
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

/* Idempotente (SQL Server 2008 R2+): crear stub si no existe, luego reemplazar cuerpo. */
IF OBJECT_ID(N'dbo.SP_UPDATEDATOSLUGANIS', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.SP_UPDATEDATOSLUGANIS AS SET NOCOUNT ON;');
GO

/*
    Procedimiento para actualizar tablas con datos de LUGANIS.
    NO modifica los SP_UPDATEDATOSDGI* existentes.
    Según @tipo_documento actualiza: Facturas, ProvFacturas, Desembolsos, Devolucion, NotasCredito o NotasDebito.

    IMPORTANTE: Antes de ejecutar, agregar las columnas LUGANIS a cada tabla si no existen.
    Ver script de columnas al final de este archivo.
*/

ALTER PROCEDURE [dbo].[SP_UPDATEDATOSLUGANIS]
(
    @tipo_documento     VARCHAR(30),   -- 'FACTURA','COMPRA','DESEMBOLSO','DEVOLUCION','NOTACREDITO','NOTADEBITO'
    @emp_codigo         INT,
    @suc_codigo         INT,
    @fac_numero         VARCHAR(30),   -- número del documento (fac_numero, dev_numero, ncr_numero, nde_numero, des_numero)
    @eNCF               VARCHAR(80),
    @trackId            VARCHAR(200),
    @aceptado           BIT,           -- ok del JSON de LUGANIS
    @filename           VARCHAR(150),
    @qrCode             VARCHAR(500) = NULL,
    @fac_forma          VARCHAR(80)   = NULL,  -- solo para Facturas
    @tfa_codigo         VARCHAR(80)   = NULL,  -- solo para Facturas
    @sup_codigo         VARCHAR(80)   = NULL,  -- solo para Compras
    @codigoseguridad    VARCHAR(100)  = NULL,
    @fechafirma         DATETIME      = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @filas INT = 0;
    DECLARE @resultado VARCHAR(50);
    DECLARE @detalle NVARCHAR(500);

    IF UPPER(RTRIM(LTRIM(ISNULL(@tipo_documento,'')))) = 'FACTURA'
    BEGIN
        UPDATE f
        SET 
            eNCF                 = CASE WHEN NULLIF(@eNCF,'') IS NOT NULL THEN @eNCF ELSE f.eNCF END,
            trackIdLuganis       = NULLIF(@trackId,''),
            filenameLuganis      = NULLIF(@filename,''),
            qrCodeLuganis        = NULLIF(@qrCode,''),
            AceptadoLuganis      = @aceptado,
            Error_Luganis        = CASE WHEN @aceptado = 1 THEN 0 ELSE 1 END,
            codigoseguridad      = ISNULL(@codigoseguridad, f.codigoseguridad),
            fechafirma           = ISNULL(@fechafirma,     f.fechafirma),
            FechaEnvioLuganis    = GETDATE()
        FROM dbo.Facturas f
        WHERE f.fac_numero = CAST(@fac_numero AS INT)
          AND f.emp_codigo = @emp_codigo
          AND f.suc_codigo = @suc_codigo
          AND f.fac_forma  = ISNULL(@fac_forma, f.fac_forma)
          AND f.tfa_codigo = ISNULL(@tfa_codigo, f.tfa_codigo);

        SET @filas = @@ROWCOUNT;
        SET @resultado = 'APLICADO';
        SET @detalle = 'Factura actualizada con datos LUGANIS';
    END
    ELSE IF UPPER(RTRIM(LTRIM(@tipo_documento))) = 'COMPRA'
    BEGIN
        UPDATE pf
        SET 
            eNCF                 = CASE WHEN NULLIF(@eNCF,'') IS NOT NULL THEN @eNCF ELSE pf.eNCF END,
            trackIdLuganis       = NULLIF(@trackId,''),
            filenameLuganis      = NULLIF(@filename,''),
            qrCodeLuganis        = NULLIF(@qrCode,''),
            AceptadoLuganis      = @aceptado,
            Error_Luganis        = CASE WHEN @aceptado = 1 THEN 0 ELSE 1 END,
            codigoseguridad      = ISNULL(@codigoseguridad, pf.codigoseguridad),
            fechafirma           = ISNULL(@fechafirma,      pf.fechafirma),
            FechaEnvioLuganis    = GETDATE()
        FROM dbo.ProvFacturas pf
        WHERE pf.fac_numero = @fac_numero
          AND pf.emp_codigo = @emp_codigo
          AND pf.suc_codigo = @suc_codigo
          AND pf.sup_codigo = ISNULL(@sup_codigo, pf.sup_codigo);

        SET @filas = @@ROWCOUNT;
        SET @resultado = 'APLICADO';
        SET @detalle = 'Compra actualizada con datos LUGANIS';
    END
    ELSE IF UPPER(RTRIM(LTRIM(@tipo_documento))) = 'DESEMBOLSO'
    BEGIN
        UPDATE d
        SET 
            eNCF                 = CASE WHEN NULLIF(@eNCF,'') IS NOT NULL THEN @eNCF ELSE d.eNCF END,
            trackIdLuganis       = NULLIF(@trackId,''),
            filenameLuganis      = NULLIF(@filename,''),
            qrCodeLuganis        = NULLIF(@qrCode,''),
            AceptadoLuganis      = @aceptado,
            Error_Luganis        = CASE WHEN @aceptado = 1 THEN 0 ELSE 1 END,
            codigoseguridad      = ISNULL(@codigoseguridad, d.codigoseguridad),
            fechafirma           = ISNULL(@fechafirma,      d.fechafirma),
            FechaEnvioLuganis    = GETDATE()
        FROM dbo.Desembolsos d
        WHERE d.des_numero = CAST(@fac_numero AS INT)
          AND d.emp_codigo = @emp_codigo
          AND d.suc_codigo = @suc_codigo;

        SET @filas = @@ROWCOUNT;
        SET @resultado = 'APLICADO';
        SET @detalle = 'Desembolso actualizado con datos LUGANIS';
    END
    ELSE IF UPPER(RTRIM(LTRIM(@tipo_documento))) IN ('DEVOLUCION','DEV')
    BEGIN
        UPDATE d
        SET 
            eNCF                 = CASE WHEN NULLIF(@eNCF,'') IS NOT NULL THEN @eNCF ELSE d.eNCF END,
            trackIdLuganis       = NULLIF(@trackId,''),
            filenameLuganis      = NULLIF(@filename,''),
            qrCodeLuganis        = NULLIF(@qrCode,''),
            AceptadoLuganis      = @aceptado,
            Error_Luganis        = CASE WHEN @aceptado = 1 THEN 0 ELSE 1 END,
            codigoseguridad      = ISNULL(@codigoseguridad, d.codigoseguridad),
            fechafirma           = ISNULL(@fechafirma,      d.fechafirma),
            FechaEnvioLuganis    = GETDATE()
        FROM dbo.Devolucion d
        WHERE d.dev_numero = CAST(@fac_numero AS INT)
          AND d.emp_codigo = @emp_codigo
          AND d.suc_codigo = @suc_codigo;

        SET @filas = @@ROWCOUNT;
        SET @resultado = 'APLICADO';
        SET @detalle = 'Devolución actualizada con datos LUGANIS';
    END
    ELSE IF UPPER(RTRIM(LTRIM(@tipo_documento))) IN ('NOTACREDITO','NC')
    BEGIN
        UPDATE d
        SET 
            eNCF                 = CASE WHEN NULLIF(@eNCF,'') IS NOT NULL THEN @eNCF ELSE d.eNCF END,
            trackIdLuganis       = NULLIF(@trackId,''),
            filenameLuganis      = NULLIF(@filename,''),
            qrCodeLuganis        = NULLIF(@qrCode,''),
            AceptadoLuganis      = @aceptado,
            Error_Luganis        = CASE WHEN @aceptado = 1 THEN 0 ELSE 1 END,
            codigoseguridad      = ISNULL(@codigoseguridad, d.codigoseguridad),
            fechafirma           = ISNULL(@fechafirma,      d.fechafirma),
            FechaEnvioLuganis    = GETDATE()
        FROM dbo.NotasCredito d
        WHERE d.ncr_numero = CAST(@fac_numero AS INT)
          AND d.emp_codigo = @emp_codigo
          AND d.suc_codigo = @suc_codigo;

        SET @filas = @@ROWCOUNT;
        SET @resultado = 'APLICADO';
        SET @detalle = 'Nota de crédito actualizada con datos LUGANIS';
    END
    ELSE IF UPPER(RTRIM(LTRIM(@tipo_documento))) IN ('NOTADEBITO','ND')
    BEGIN
        UPDATE d
        SET 
            eNCF                 = CASE WHEN NULLIF(@eNCF,'') IS NOT NULL THEN @eNCF ELSE d.eNCF END,
            trackIdLuganis       = NULLIF(@trackId,''),
            filenameLuganis      = NULLIF(@filename,''),
            qrCodeLuganis        = NULLIF(@qrCode,''),
            AceptadoLuganis      = @aceptado,
            Error_Luganis        = CASE WHEN @aceptado = 1 THEN 0 ELSE 1 END,
            codigoseguridad      = ISNULL(@codigoseguridad, d.codigoseguridad),
            fechafirma           = ISNULL(@fechafirma,      d.fechafirma),
            FechaEnvioLuganis    = GETDATE()
        FROM dbo.NotasDebito d
        WHERE d.nde_numero = CAST(@fac_numero AS INT)
          AND d.emp_codigo = @emp_codigo
          AND d.suc_codigo = @suc_codigo;

        SET @filas = @@ROWCOUNT;
        SET @resultado = 'APLICADO';
        SET @detalle = 'Nota de débito actualizada con datos LUGANIS';
    END
    ELSE
    BEGIN
        SET @filas = 0;
        SET @resultado = 'TIPO_INVALIDO';
        SET @detalle = 'Tipo de documento no reconocido: ' + ISNULL(@tipo_documento,'NULL') + 
            '. Valores válidos: FACTURA, COMPRA, DESEMBOLSO, DEVOLUCION, NOTACREDITO, NOTADEBITO';
    END

    SELECT @filas AS filas_actualizadas, @resultado AS resultado, @detalle AS detalle;
END
GO

/*
================================================================================
SCRIPT PARA AGREGAR COLUMNAS LUGANIS A LAS TABLAS (ejecutar solo si no existen)
================================================================================
*/

/*
-- Facturas
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
*/
