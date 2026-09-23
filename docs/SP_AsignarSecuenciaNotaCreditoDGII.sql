/*
  dbo.AsignarSecuenciaNotaCreditoDGII — NotasCredito, e-CF 34.
  Si ya hay eNCF, se reutiliza (evita duplicados).
  Ejecutar en la base del cliente. Compatible SQL Server 2008 R2+ (stub + ALTER).
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.AsignarSecuenciaNotaCreditoDGII', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.AsignarSecuenciaNotaCreditoDGII AS SET NOCOUNT ON;');
GO

ALTER PROCEDURE [dbo].[AsignarSecuenciaNotaCreditoDGII]
    @empresa   INT,
    @sucursal  INT,
    @numero    INT,
    @eNCF      VARCHAR(50) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRAN;

        DECLARE @TipoeNCF INT = 34;
        DECLARE @eNCFActual VARCHAR(50);

        IF NOT EXISTS (
            SELECT 1
            FROM dbo.NotasCredito n
            WHERE n.emp_codigo = @empresa
              AND n.suc_codigo = @sucursal
              AND n.ncr_numero = @numero
        )
        BEGIN
            SET @eNCF = NULL;
            COMMIT TRAN;
            RETURN;
        END;

        SELECT @eNCFActual = NULLIF(LTRIM(RTRIM(n.eNCF)), '')
        FROM dbo.NotasCredito AS n WITH (UPDLOCK, HOLDLOCK)
        WHERE n.emp_codigo = @empresa
          AND n.suc_codigo = @sucursal
          AND n.ncr_numero = @numero;

        IF @eNCFActual IS NOT NULL
        BEGIN
            SET @eNCF = @eNCFActual;
            COMMIT TRAN;
            RETURN;
        END;

        DECLARE @nuevoENCF VARCHAR(50);
        SET @nuevoENCF = dbo.fn_obtenerSecuenciaDGI(@empresa, @TipoeNCF);

        IF @nuevoENCF IS NULL OR LTRIM(RTRIM(@nuevoENCF)) = ''
        BEGIN
            SET @eNCF = NULL;
            COMMIT TRAN;
            RETURN;
        END;

        UPDATE dbo.NotasCredito
        SET eNCF = @nuevoENCF
        WHERE emp_codigo = @empresa
          AND suc_codigo = @sucursal
          AND ncr_numero = @numero;

        IF @@ROWCOUNT = 1
        BEGIN
            UPDATE dbo.SecuenciaDGII
            SET Ultima_secuencia_DGII = Ultima_secuencia_DGII + 1
            WHERE emp_codigo = @empresa
              AND Tipo = @TipoeNCF;

            SET @eNCF = @nuevoENCF;
        END
        ELSE
        BEGIN
            SELECT @eNCF = NULLIF(LTRIM(RTRIM(eNCF)), '')
            FROM dbo.NotasCredito
            WHERE emp_codigo = @empresa
              AND suc_codigo = @sucursal
              AND ncr_numero = @numero;
        END

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRAN;
        DECLARE @ErrMsg NVARCHAR(4000), @ErrSev INT, @ErrState INT;
        SELECT @ErrMsg = ERROR_MESSAGE(), @ErrSev = ERROR_SEVERITY(), @ErrState = ERROR_STATE();
        RAISERROR(@ErrMsg, @ErrSev, @ErrState);
    END CATCH
END
GO
