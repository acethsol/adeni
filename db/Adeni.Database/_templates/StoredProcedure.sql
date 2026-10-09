/*
================================================================================
Procedure : [schema].[ProcedureName]
Purpose   : <one or two sentences — what this proc does and why>
Caller    : Adeni.Infrastructure.<Module>.<XxxExecutor>
            (via StoredProcedureExecutor — do not embed this SQL in C#)

Parameters
  @Param1   <description>
  @Param2   <description>
  @OutParam OUTPUT — <description>

Notes
  - CTEs are fine inside a single statement; use #temp if you need COUNT + page.
  - Keep heavy SQL here; C# stays a thin parameter + row-map wrapper.

Sample call
--------------------------------------------------------------------------------
DECLARE @OutParam INT;

EXEC [schema].[ProcedureName]
    @Param1   = ...,
    @Param2   = ...,
    @OutParam = @OutParam OUTPUT;

SELECT @OutParam AS OutParam;
-- Result set: <column list>
================================================================================
*/
CREATE PROCEDURE [schema].[ProcedureName]
    @Param1 INT,
    @OutParam INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- implementation
    SET @OutParam = 0;
END
GO
