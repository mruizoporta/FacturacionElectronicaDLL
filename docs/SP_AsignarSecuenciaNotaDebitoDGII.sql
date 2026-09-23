/*
  dbo.AsignarSecuenciaNotaDebitoDGII — NotasDebito, e-CF 33.
  Si ya hay eNCF, se reutiliza (evita duplicados).
  Ejecutar en la base del cliente. Compatible SQL Server 2008 R2+ (stub + ALTER).
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.AsignarSecuenciaNotaDebitoDGII', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.AsignarSecuenciaNotaDebitoDGII AS SET NOCOUNT ON;');
GO

ALTER PROCEDURE [dbo].[AsignarSecuenciaNotaDebitoDGII]
    @empresa   INT,
    @sucursal  INT,
    @numero    INT,
    @eNCF      VARCHAR(50) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRAN;

        DECLARE @TipoeNCF INT = 33;
        DECLARE @eNCFActual VARCHAR(50);

        IF NOT EXISTS (
            SELECT 1
            FROM dbo.NotasDebito n
            WHERE n.emp_codigo = @empresa
              AND n.suc_codigo = @sucursal
              AND n.nde_numero = @numero
        )
        BEGIN
            SET @eNCF = NULL;
            COMMIT TRAN;
            RETURN;
        END;

        SELECT @eNCFActual = NULLIF(LTRIM(RTRIM(n.eNCF)), '')
        FROM dbo.NotasDebito AS n WITH (UPDLOCK, HOLDLOCK)
        WHERE n.emp_codigo = @empresa
          AND n.suc_codigo = @sucursal
          AND n.nde_numero = @numero;

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

        UPDATE dbo.NotasDebito
        SET eNCF = @nuevoENCF
        WHERE emp_codigo = @empresa
          AND suc_codigo = @sucursal
          AND nde_numero = @numero;

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
            FROM dbo.NotasDebito
            WHERE emp_codigo = @empresa
              AND suc_codigo = @sucursal
              AND nde_numero = @numero;
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
