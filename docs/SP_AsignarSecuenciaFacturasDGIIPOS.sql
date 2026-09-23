/*
  dbo.AsignarSecuenciaFacturasDGIIPOS — tickets POS (Montos_Ticket).
  Si ya hay eNCF, se reutiliza (evita duplicados).
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.AsignarSecuenciaFacturasDGIIPOS', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.AsignarSecuenciaFacturasDGIIPOS AS SET NOCOUNT ON;');
GO

ALTER PROCEDURE [dbo].[AsignarSecuenciaFacturasDGIIPOS]
    @empresa    INT,
    @sucursal   INT,
    @usu_codigo INT,
    @caja       INT,
    @numero     INT,
    @eNCF       VARCHAR(50) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRAN;

        DECLARE @TipoeNCF        INT;
        DECLARE @eNCFActual      VARCHAR(50);
        DECLARE @TieneError      BIT;
        DECLARE @Aceptada        BIT;
        DECLARE @EnviadaDGII     BIT;
        DECLARE @SecuenciaUsada  BIT;

        SELECT
            @TipoeNCF        = t.cod_dgii,
            @TieneError      = f.Error_DGII,
            @Aceptada        = f.AceptadoDGII,
            @EnviadaDGII     = f.Enviado_DGII,
            @SecuenciaUsada  = ISNULL(f.secuenciaUtilizadaDGI, 0)
        FROM dbo.Montos_Ticket f
        INNER JOIN dbo.TipoNCF t
            ON t.tip_codigo = f.tip_codigo
           AND t.emp_codigo = f.emp_codigo
        WHERE f.emp_codigo = @empresa
          AND f.suc_codigo = @sucursal
          AND f.usu_codigo = @usu_codigo
          AND f.caja       = @caja
          AND f.ticket     = @numero;

        IF @TipoeNCF IS NULL
        BEGIN
            SET @eNCF = NULL;
            COMMIT TRAN;
            RETURN;
        END;

        SELECT @eNCFActual = NULLIF(LTRIM(RTRIM(f.eNCF)), '')
        FROM dbo.Montos_Ticket AS f WITH (UPDLOCK, HOLDLOCK)
        WHERE f.emp_codigo = @empresa
          AND f.suc_codigo = @sucursal
          AND f.usu_codigo = @usu_codigo
          AND f.caja       = @caja
          AND f.ticket     = @numero;

        IF @eNCFActual IS NOT NULL
        BEGIN
            SET @eNCF = @eNCFActual;
            COMMIT TRAN;
            RETURN;
        END;

        DECLARE @nuevoENCF VARCHAR(50);
        IF OBJECT_ID(N'dbo.fn_obtenerSecuenciaDGI', N'FN') IS NOT NULL
            SET @nuevoENCF = dbo.fn_obtenerSecuenciaDGI(@empresa, @TipoeNCF);
        IF (@nuevoENCF IS NULL OR LTRIM(RTRIM(@nuevoENCF)) = '')
           AND OBJECT_ID(N'dbo.fn_obtenerSecuenciaDGI_Facturas', N'FN') IS NOT NULL
            SET @nuevoENCF = dbo.fn_obtenerSecuenciaDGI_Facturas(@empresa, @TipoeNCF);

        UPDATE dbo.Montos_Ticket
        SET
            eNCF = @nuevoENCF,
            secuenciaUtilizadaDGI = 0
        WHERE emp_codigo = @empresa
          AND suc_codigo = @sucursal
          AND usu_codigo = @usu_codigo
          AND caja       = @caja
          AND ticket     = @numero;

        IF @@ROWCOUNT = 1
        BEGIN
            UPDATE dbo.SecuenciaDGII
            SET Ultima_secuencia_DGII = Ultima_secuencia_DGII + 1
            WHERE emp_codigo = @empresa
              AND Tipo       = @TipoeNCF;

            SET @eNCF = @nuevoENCF;
        END
        ELSE
        BEGIN
            SELECT @eNCF = eNCF
            FROM dbo.Montos_Ticket
            WHERE emp_codigo = @empresa
              AND suc_codigo = @sucursal
              AND usu_codigo = @usu_codigo
              AND caja       = @caja
              AND ticket     = @numero;
        END

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
