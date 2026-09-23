/*
  dbo.AsignarSecuenciaFacturasDGIIPOSRESBAR

  Misma lógica que dbo.AsignarSecuenciaFacturasDGII, pero sobre dbo.Factura_RestBar
  y las llaves del POS RestBar (DLL: FacturaElectronicaService.LlamarAsignarSecuenciaDGIIPOSResBar).

  Parámetros (alineados al VB):
    @empresa, @sucursal, @usu_codigo (CajeroID), @caja (CajaID), @numero (FacturaID), @eNCF OUTPUT

  Requisitos:
    - Columnas DGII en Factura_RestBar (docs/ALTER_Factura_RestBar_DGII_columns.sql)
    - dbo.TipoNCF por TipoNCF + EMP_CODIGO (cod_dgii para fn_obtenerSecuenciaDGI)
    - dbo.fn_obtenerSecuenciaDGI, dbo.SecuenciaDGII (igual que facturación estándar)

  Ejecutar en [coral] (o ajustar USE).
*/
USE [coral];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[AsignarSecuenciaFacturasDGIIPOSRESBAR]
    @empresa    INT,
    @sucursal   INT,
    @usu_codigo VARCHAR(50),
    @caja       VARCHAR(50),
    @numero     INT,
    @eNCF       VARCHAR(50) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRAN;

        DECLARE @CajeroId INT = CONVERT(INT, LTRIM(RTRIM(@usu_codigo)));
        DECLARE @CajaId   INT = CONVERT(INT, LTRIM(RTRIM(@caja)));

        DECLARE @TipoeNCF INT;
        DECLARE @eNCFActual VARCHAR(100);
        DECLARE @TieneError BIT;
        DECLARE @Aceptada BIT;
        DECLARE @EnviadaDGII BIT;
        DECLARE @SecuenciaUsada BIT;

        SELECT
            @TipoeNCF       = t.cod_dgii,
            @TieneError     = rb.Error_DGII,
            @Aceptada       = rb.AceptadoDGII,
            @EnviadaDGII    = rb.Enviado_DGII,
            @SecuenciaUsada = ISNULL(rb.secuenciaUtilizadaDGI, 0)
        FROM dbo.Factura_RestBar AS rb
        INNER JOIN dbo.TipoNCF AS t
            ON t.tip_codigo = rb.TipoNCF
           AND t.emp_codigo = rb.EMP_CODIGO
        WHERE rb.EMP_CODIGO = @empresa
          AND rb.SUC_CODIGO = @sucursal
          AND rb.FacturaID  = @numero
          AND rb.CajeroID   = @CajeroId
          AND rb.CajaID     = @CajaId;

        IF @TipoeNCF IS NULL
        BEGIN
            SET @eNCF = NULL;
            COMMIT TRAN;
            RETURN;
        END;

        SELECT @eNCFActual = NULLIF(LTRIM(RTRIM(CAST(rb.eNCF AS VARCHAR(100)))), '')
        FROM dbo.Factura_RestBar AS rb WITH (UPDLOCK, HOLDLOCK)
        WHERE rb.EMP_CODIGO = @empresa
          AND rb.SUC_CODIGO = @sucursal
          AND rb.FacturaID  = @numero
          AND rb.CajeroID   = @CajeroId
          AND rb.CajaID     = @CajaId;

        IF @eNCFActual IS NOT NULL
        BEGIN
            /* Ya hay eNCF: reutilizar SIEMPRE (evita duplicados si el
               primer envío fue aceptado y acá se marcó error/pendiente). */
            SET @eNCF = LEFT(@eNCFActual, 50);
            COMMIT TRAN;
            RETURN;
        END;

        DECLARE @nuevoENCF VARCHAR(100);
        SET @nuevoENCF = dbo.fn_obtenerSecuenciaDGI(@empresa, @TipoeNCF);

        UPDATE dbo.Factura_RestBar
        SET
            eNCF = @nuevoENCF,
            secuenciaUtilizadaDGI = 0
        WHERE EMP_CODIGO = @empresa
          AND SUC_CODIGO = @sucursal
          AND FacturaID  = @numero
          AND CajeroID   = @CajeroId
          AND CajaID     = @CajaId;

        IF @@ROWCOUNT = 1
        BEGIN
            UPDATE dbo.SecuenciaDGII
            SET Ultima_secuencia_DGII = Ultima_secuencia_DGII + 1
            WHERE emp_codigo = @empresa
              AND Tipo = @TipoeNCF;

            SET @eNCF = LEFT(@nuevoENCF, 50);
        END
        ELSE
        BEGIN
            SELECT @eNCF = LEFT(NULLIF(LTRIM(RTRIM(CAST(rb.eNCF AS VARCHAR(100)))), ''), 50)
            FROM dbo.Factura_RestBar AS rb
            WHERE rb.EMP_CODIGO = @empresa
              AND rb.SUC_CODIGO = @sucursal
              AND rb.FacturaID  = @numero
              AND rb.CajeroID   = @CajeroId
              AND rb.CajaID     = @CajaId;
        END;

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRAN;
        DECLARE @ErrMsg NVARCHAR(4000), @ErrSev INT, @ErrState INT;
        SELECT @ErrMsg = ERROR_MESSAGE(), @ErrSev = ERROR_SEVERITY(), @ErrState = ERROR_STATE();
        RAISERROR(@ErrMsg, @ErrSev, @ErrState);
    END CATCH
END;
GO
