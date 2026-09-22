/*
  dbo.SP_UPDATEDATOSDGIPOS

  Equivalente en lógica a dbo.SP_UPDATEDATOSDGI (Facturas), para dbo.Montos_Ticket (POS).

  Ajustes respecto a versiones anteriores:
  - Enviado_DGII = 1 cuando hubo respuesta DGII vía bitácora (mismo criterio que ya pasó los
    filtros SIN_ENVIO / NO_ENVIADO). Así queda alineado con Enviado_DGII en Facturas / RestBar.
  - eNCF: si viene en la llamada, se persiste (útil si la consulta de estado devolvió encf).

  Requiere columna Enviado_DGII en Montos_Ticket (si no existe, ejecutar ALTER al final).

  Ejecutar en la base de datos de la aplicación.
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[SP_UPDATEDATOSDGIPOS]
(
    @emp_codigo         INT,
    @suc_codigo         INT,
    @ticket             INT,
    @eNCF               VARCHAR(80),
    @usu_codigo         INT,
    @caja               INT,
    @codigoseguridad    VARCHAR(50),
    @fechafirma         DATETIME,
    @aceptado           BIT,
    @secuenciaUtilizada BIT
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE 
        @b_Estado       VARCHAR(100),
        @b_TrackingID   VARCHAR(200),
        @b_MensajeError NVARCHAR(MAX);

    SELECT TOP (1)
        @b_Estado       = b.Estado,
        @b_TrackingID   = b.trackingID,
        @b_MensajeError = b.MensajeError
    FROM dbo.BitacoraFacturacionElectronicaDGII b
    WHERE b.emp_codigo = @emp_codigo
      AND b.fac_numero = @ticket
      AND b.caja       = @caja
      AND b.usu_codigo = @usu_codigo
      AND (b.tipo = N'MONTOS_TICKET' OR b.tipo IS NULL)
    ORDER BY b.Fecha_Envio DESC;

    IF @b_Estado IS NULL
    BEGIN
        SELECT 0 AS filas_actualizadas, 
               'SIN_ENVIO' AS resultado, 
               'No existe registro en bitácora, no se actualiza factura' AS detalle;
        RETURN;
    END;

    IF UPPER(@b_Estado) IN ('NO ENVIADO','SIN CONEXION','NO ENVIO')
    BEGIN
        SELECT 0 AS filas_actualizadas, 
               'NO_ENVIADO' AS resultado, 
               'Detectado problema de conexión, no se marca rechazado' AS detalle;
        RETURN;
    END;

    UPDATE f
    SET 
        secuenciaUtilizadaDGI = @secuenciaUtilizada,

        codigoseguridad = CASE WHEN @aceptado = 1 THEN NULLIF(@codigoseguridad,'') ELSE NULL END,
        FechaFirma      = CASE WHEN @aceptado = 1 THEN @fechafirma ELSE NULL END,
        AceptadoDGII    = @aceptado,
        Error_DGII      = CASE 
                              WHEN @aceptado = 1 THEN 0
                              WHEN @aceptado = 0 AND @secuenciaUtilizada = 1 THEN 1
                              ELSE 0
                          END,

        /* Hubo ida/vuelta con DGII (respuesta reflejada en bitácora): marcar envío */
        Enviado_DGII    = 1,

        /* eNCF devuelto por consulta/envío (no pisar con vacío) */
        eNCF            = CASE
                              WHEN NULLIF(LTRIM(RTRIM(@eNCF)), '') IS NOT NULL
                              THEN LTRIM(RTRIM(@eNCF))
                              ELSE f.eNCF
                          END
    FROM dbo.Montos_Ticket f
    WHERE f.ticket     = @ticket
      AND f.emp_codigo = @emp_codigo
      AND f.suc_codigo = @suc_codigo
      AND f.usu_codigo = @usu_codigo
      AND f.caja       = @caja;

    SELECT @@ROWCOUNT AS filas_actualizadas, 
           'APLICADO' AS resultado,
           'Montos_Ticket actualizado. Estado bitácora: ' + ISNULL(@b_Estado,'') +
           ' | Aceptado: '        + CAST(@aceptado AS VARCHAR(50)) +
           ' | Secuencia usada: ' + CAST(@secuenciaUtilizada AS VARCHAR(50)) AS detalle;
END;
GO

/*
  Si Montos_Ticket no tiene aún Enviado_DGII (mismo criterio que Facturas / Factura_RestBar):

IF COL_LENGTH(N'dbo.Montos_Ticket', N'Enviado_DGII') IS NULL
BEGIN
    ALTER TABLE dbo.Montos_Ticket ADD
        [Enviado_DGII] BIT NOT NULL CONSTRAINT [DF_Montos_Ticket_Enviado_DGII] DEFAULT ((0));
END
GO
*/
