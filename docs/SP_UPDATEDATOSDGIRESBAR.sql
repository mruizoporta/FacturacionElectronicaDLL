/*
  dbo.SP_UPDATEDATOSDGIRESBAR

  Equivalente a dbo.SP_UPDATEDATOSDGI para dbo.Factura_RestBar.
  La DLL (FacturaElectronicaService) llama este SP en flujo RESBAR con la misma
  firma que SP_UPDATEDATOSDGIPOS:

    Aceptación factura/resumen POS:
      @emp_codigo, @suc_codigo, @ticket, @eNCF, @usu_codigo, @caja,
      @codigoseguridad, @fechafirma, @aceptado, @secuenciaUtilizada

    Rechazo resumen POS (rama Else):
      @emp_codigo, @suc_codigo, @fac_numero (mismo valor que ticket), @eNCF, ...

  Bitácora: misma tabla y criterio que SP_UPDATEDATOSDGI (emp_codigo + fac_numero
  del ticket, como string en INSERT desde Helper.InsertarBitacoraDGII).

  Ejecutar en [coral] tras ALTER_Factura_RestBar_DGII_columns.sql.
*/
USE [coral];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[SP_UPDATEDATOSDGIRESBAR]
(
    @emp_codigo         INT,
    @suc_codigo         INT,
    @ticket             VARCHAR(50) = NULL,
    @fac_numero         VARCHAR(50) = NULL,
    @eNCF               VARCHAR(80),
    @usu_codigo         VARCHAR(50),
    @caja               VARCHAR(50),
    @codigoseguridad    VARCHAR(50),
    @fechafirma         DATETIME,
    @aceptado           BIT,
    @secuenciaUtilizada BIT = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @claveDoc VARCHAR(50) = NULLIF(LTRIM(RTRIM(COALESCE(@ticket, @fac_numero))), '');
    DECLARE @CajeroId INT = CONVERT(INT, LTRIM(RTRIM(@usu_codigo)));
    DECLARE @CajaId   INT = CONVERT(INT, LTRIM(RTRIM(@caja)));
    DECLARE @FacturaId INT;
    /* Sin TRY_CONVERT (SQL Server < 2012): ticket numérico = FacturaID */
    SET @FacturaId = CASE
        WHEN @claveDoc IS NOT NULL
             AND LEN(@claveDoc) BETWEEN 1 AND 10
             AND @claveDoc NOT LIKE '%[^0-9]%'
        THEN CONVERT(INT, @claveDoc)
        ELSE NULL
    END;
    DECLARE @SeqUtilizada BIT = ISNULL(@secuenciaUtilizada, 0);

    IF @claveDoc IS NULL OR @FacturaId IS NULL
    BEGIN
        SELECT 0 AS filas_actualizadas,
               'SIN_CLAVE' AS resultado,
               'Falta @ticket o @fac_numero válido (FacturaID).' AS detalle;
        RETURN;
    END;

    /* 1) Última bitácora de este ticket (misma convención que SP_UPDATEDATOSDGI) */
    DECLARE
        @b_Estado       VARCHAR(100),
        @b_TrackingID   VARCHAR(200),
        @b_MensajeError NVARCHAR(MAX);

    SELECT TOP (1)
        @b_Estado       = b.Estado,
        @b_TrackingID   = b.trackingID,
        @b_MensajeError = b.MensajeError
    FROM dbo.BitacoraFacturacionElectronicaDGII AS b
    WHERE b.emp_codigo = @emp_codigo
      AND LTRIM(RTRIM(CONVERT(VARCHAR(50), b.fac_numero))) = @claveDoc
      AND (b.tipo = N'RESTBAR' OR b.tipo IS NULL)
    ORDER BY b.Fecha_Envio DESC;

    IF @b_Estado IS NULL
    BEGIN
        SELECT 0 AS filas_actualizadas,
               'SIN_ENVIO' AS resultado,
               'No existe registro en bitácora, no se actualiza Factura_RestBar' AS detalle;
        RETURN;
    END;

    IF UPPER(@b_Estado) IN ('NO ENVIADO', 'SIN CONEXION', 'NO ENVIO')
    BEGIN
        SELECT 0 AS filas_actualizadas,
               'NO_ENVIADO' AS resultado,
               'Detectado problema de conexión, no se marca rechazado' AS detalle;
        RETURN;
    END;

    /* 3) Actualizar Factura_RestBar
       Firma y código de seguridad: siempre que vengan en la llamada (aceptado o rechazado).
       Flags secuencia/error: misma lógica que SP_UPDATEDATOSDGI. */
    UPDATE rb
    SET
        secuenciaUtilizadaDGI = @SeqUtilizada,
        codigoseguridad = NULLIF(LTRIM(RTRIM(@codigoseguridad)), ''),
        fechafirma      = @fechafirma,
        AceptadoDGII    = @aceptado,
        Error_DGII      = CASE
                              WHEN @aceptado = 1 THEN 0
                              WHEN @aceptado = 0 AND @SeqUtilizada = 1 THEN 1
                              ELSE 0
                          END
    FROM dbo.Factura_RestBar AS rb
    WHERE rb.EMP_CODIGO = @emp_codigo
      AND rb.SUC_CODIGO = @suc_codigo
      AND rb.FacturaID  = @FacturaId
      AND rb.CajeroID   = @CajeroId
      AND rb.CajaID     = @CajaId;

    SELECT
        @@ROWCOUNT AS filas_actualizadas,
        'APLICADO' AS resultado,
        ('Factura_RestBar actualizada. Estado bitácora: ' + ISNULL(@b_Estado, '') +
         ' | Aceptado: ' + CAST(@aceptado AS VARCHAR(50)) +
         ' | Secuencia usada: ' + CAST(@SeqUtilizada AS VARCHAR(50))) AS detalle;
END;
GO
