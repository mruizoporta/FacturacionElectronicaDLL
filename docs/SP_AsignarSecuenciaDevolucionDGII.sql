/* Ejecutar en la base del cliente.
   Asigna eNCF tipo 34 a dbo.Devolucion (misma lógica que facturas).
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.AsignarSecuenciaDevolucionDGII', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.AsignarSecuenciaDevolucionDGII AS RETURN 0;');
GO

ALTER PROCEDURE [dbo].[AsignarSecuenciaDevolucionDGII]
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
            FROM dbo.Devolucion d
            WHERE d.emp_codigo = @empresa
              AND d.suc_codigo = @sucursal
              AND d.DEV_NUMERO = @numero
              AND ISNULL(d.dev_status, '') <> 'ANU'
        )
        BEGIN
            SET @eNCF = NULL;
            COMMIT TRAN;
            RETURN;
        END;

        SELECT @eNCFActual = NULLIF(LTRIM(RTRIM(d.eNCF)), '')
        FROM dbo.Devolucion AS d WITH (UPDLOCK, HOLDLOCK)
        WHERE d.emp_codigo = @empresa
          AND d.suc_codigo = @sucursal
          AND d.DEV_NUMERO = @numero
          AND ISNULL(d.dev_status, '') <> 'ANU';

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

        UPDATE dbo.Devolucion
        SET eNCF = @nuevoENCF
        WHERE emp_codigo = @empresa
          AND suc_codigo = @sucursal
          AND DEV_NUMERO = @numero
          AND ISNULL(dev_status, '') <> 'ANU';

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
            FROM dbo.Devolucion
            WHERE emp_codigo = @empresa
              AND suc_codigo = @sucursal
              AND DEV_NUMERO = @numero
              AND ISNULL(dev_status, '') <> 'ANU';
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
