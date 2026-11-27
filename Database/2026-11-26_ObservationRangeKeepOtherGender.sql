/* =====================================================================
   Observation range save: keep the other gender's ranges  —  2026-11-26
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (CREATE OR ALTER)

   Owner: saving the Male (or Female) ranges of an observation must not remove
   the range female (or male) patients use.

   Before: D_InvastigationObservationRangeMaster with @Gender = 'M' / 'F' deleted
   the observation's 'B' (both genders) rows as well as the saved gender's rows.
   Result entry reads Gender IN ('B', patient gender), so after a Male save the
   female patients had no range left.

   Now (one procedure, called only by LabRepository.SubmitInvastigationObservationRangeMaster):
     @Gender = 'B'      : delete all rows of the observation (unchanged; 'B' replaces both)
     @Gender = 'M'/'F'  : 1. the 'B' rows stay for the other gender: they become rows of
                            the other gender, unless that gender already has its own rows
                            (then 'B' rows are removed, as before, so no patient gets two
                            ranges for one age)
                          2. delete the saved gender's rows (unchanged)
   @Result = number of rows deleted (not used by the caller).
   The API now runs this delete and the inserts in one transaction.

   Objects
     1. D_InvastigationObservationRangeMaster (CREATE OR ALTER)
   ===================================================================== */
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[D_InvastigationObservationRangeMaster]
(
    @ObservationId int,
    @Gender nvarchar(20),
    @Result int = NULL OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @deleted int = 0;
    SET @Gender = UPPER(LTRIM(RTRIM(@Gender)));

    IF (@Gender = 'B')
    BEGIN
        DELETE FROM InvastigationObservationRangeMaster WHERE ObservationId = @ObservationId;
        SET @deleted = @@ROWCOUNT;
    END
    ELSE IF (@Gender IN ('M', 'F'))
    BEGIN
        DECLARE @otherGender nvarchar(1) = CASE WHEN @Gender = 'M' THEN 'F' ELSE 'M' END;

        IF EXISTS (SELECT 1 FROM InvastigationObservationRangeMaster
                   WHERE ObservationId = @ObservationId AND Gender = @otherGender)
        BEGIN
            -- the other gender has its own ranges: the 'B' rows would give it a second range
            DELETE FROM InvastigationObservationRangeMaster
            WHERE ObservationId = @ObservationId AND Gender = 'B';
            SET @deleted = @@ROWCOUNT;
        END
        ELSE
        BEGIN
            -- keep the 'B' ranges for the other gender
            UPDATE InvastigationObservationRangeMaster
            SET Gender = @otherGender,
                LastModifiedDate = GETDATE()
            WHERE ObservationId = @ObservationId AND Gender = 'B';
        END

        DELETE FROM InvastigationObservationRangeMaster
        WHERE ObservationId = @ObservationId AND Gender = @Gender;
        SET @deleted += @@ROWCOUNT;
    END

    SET @Result = @deleted;
END
GO
