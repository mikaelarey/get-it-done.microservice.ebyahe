-- Executed by EF inside its migration transaction. No rows are deleted or reseeded.
SET XACT_ABORT ON;
IF @@TRANCOUNT = 0 THROW 51000, 'GUID key conversion requires a transaction.', 1;
DECLARE @originalAvailable bigint=(SELECT SUM(CONVERT(bigint,COALESCE(AvailableSeats,0))) FROM dbo.ScheduledTrips),
 @originalReserved bigint=(SELECT SUM(CONVERT(bigint,ReservedSeats)) FROM dbo.BusBookings);
SELECT TableName,Total INTO #OriginalCounts FROM (
 SELECT N'Partners' AS TableName,COUNT_BIG(*) AS Total FROM dbo.Partners UNION ALL SELECT N'Locations',COUNT_BIG(*) FROM dbo.Locations
 UNION ALL SELECT N'BusTerminals',COUNT_BIG(*) FROM dbo.BusTerminals UNION ALL SELECT N'Buses',COUNT_BIG(*) FROM dbo.Buses
 UNION ALL SELECT N'BusRoutes',COUNT_BIG(*) FROM dbo.BusRoutes UNION ALL SELECT N'ScheduledTrips',COUNT_BIG(*) FROM dbo.ScheduledTrips
 UNION ALL SELECT N'BusBookings',COUNT_BIG(*) FROM dbo.BusBookings UNION ALL SELECT N'RefUsers',COUNT_BIG(*) FROM dbo.RefUsers
) counts;

CREATE TABLE #Columns (TableName sysname NOT NULL, ColumnName sysname NOT NULL,
  TargetTable sysname NULL, IsPrimary bit NOT NULL, IsNullable bit NOT NULL,
  PRIMARY KEY (TableName, ColumnName));
INSERT #Columns VALUES
 ('Partners','Id',NULL,1,0),('Locations','Id',NULL,1,0),('BusTerminals','Id',NULL,1,0),
 ('Buses','Id',NULL,1,0),('BusRoutes','Id',NULL,1,0),('ScheduledTrips','Id',NULL,1,0),('RefUsers','Id',NULL,1,0),
 ('BusTerminals','PartnerId','Partners',0,0),('BusTerminals','LocationId','Locations',0,0),
 ('Buses','PartnerId','Partners',0,0),('BusRoutes','PartnerId','Partners',0,0),
 ('BusRoutes','OriginTerminalId','BusTerminals',0,0),('BusRoutes','DestinationTerminalId','BusTerminals',0,0),
 ('ScheduledTrips','PartnerId','Partners',0,0),('ScheduledTrips','BusId','Buses',0,0),
 ('ScheduledTrips','BusRouteId','BusRoutes',0,0),('BusBookings','ScheduledTripId','ScheduledTrips',0,1);

CREATE TABLE #Rebuild (Sequence int IDENTITY PRIMARY KEY, Phase int NOT NULL,
  DropSql nvarchar(max) NOT NULL, CreateSql nvarchar(max) NOT NULL);

-- Capture all affected foreign keys before altering any columns, including composite keys.
INSERT #Rebuild (Phase,DropSql,CreateSql)
SELECT 1,N'ALTER TABLE '+QUOTENAME(OBJECT_SCHEMA_NAME(f.parent_object_id))+N'.'+QUOTENAME(OBJECT_NAME(f.parent_object_id))+
 N' DROP CONSTRAINT '+QUOTENAME(f.name)+N';',
 N'ALTER TABLE '+QUOTENAME(OBJECT_SCHEMA_NAME(f.parent_object_id))+N'.'+QUOTENAME(OBJECT_NAME(f.parent_object_id))+
 N' WITH CHECK ADD CONSTRAINT '+QUOTENAME(f.name)+N' FOREIGN KEY ('+p.Columns+N') REFERENCES '+
 QUOTENAME(OBJECT_SCHEMA_NAME(f.referenced_object_id))+N'.'+QUOTENAME(OBJECT_NAME(f.referenced_object_id))+N' ('+r.Columns+
 N') ON DELETE '+REPLACE(f.delete_referential_action_desc,'_',' ')+N' ON UPDATE '+REPLACE(f.update_referential_action_desc,'_',' ')+N';'
FROM sys.foreign_keys f
CROSS APPLY (SELECT STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(COL_NAME(fc.parent_object_id,fc.parent_column_id))),N',')
 WITHIN GROUP (ORDER BY fc.constraint_column_id) AS Columns FROM sys.foreign_key_columns fc WHERE fc.constraint_object_id=f.object_id) p
CROSS APPLY (SELECT STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(COL_NAME(fc.referenced_object_id,fc.referenced_column_id))),N',')
 WITHIN GROUP (ORDER BY fc.constraint_column_id) AS Columns FROM sys.foreign_key_columns fc WHERE fc.constraint_object_id=f.object_id) r
WHERE EXISTS (SELECT 1 FROM sys.foreign_key_columns fc JOIN #Columns c
 ON (fc.parent_object_id=OBJECT_ID(N'dbo.'+c.TableName) AND COL_NAME(fc.parent_object_id,fc.parent_column_id)=c.ColumnName)
 OR (fc.referenced_object_id=OBJECT_ID(N'dbo.'+c.TableName) AND COL_NAME(fc.referenced_object_id,fc.referenced_column_id)=c.ColumnName)
 WHERE fc.constraint_object_id=f.object_id);

-- Preserve names, uniqueness, order, included columns and filter expressions on indexes.
INSERT #Rebuild (Phase,DropSql,CreateSql)
SELECT CASE WHEN k.object_id IS NULL THEN 2 ELSE 3 END,
 CASE WHEN k.object_id IS NULL THEN N'DROP INDEX '+QUOTENAME(i.name)+N' ON ' ELSE N'ALTER TABLE ' END+
 QUOTENAME(OBJECT_SCHEMA_NAME(i.object_id))+N'.'+QUOTENAME(OBJECT_NAME(i.object_id))+
 CASE WHEN k.object_id IS NULL THEN N';' ELSE N' DROP CONSTRAINT '+QUOTENAME(k.name)+N';' END,
 CASE WHEN k.object_id IS NULL THEN N'CREATE '+CASE WHEN i.is_unique=1 THEN N'UNIQUE ' ELSE N'' END+i.type_desc+N' INDEX '+QUOTENAME(i.name)+N' ON '
 ELSE N'ALTER TABLE ' END+
 QUOTENAME(OBJECT_SCHEMA_NAME(i.object_id))+N'.'+QUOTENAME(OBJECT_NAME(i.object_id))+
 CASE WHEN k.object_id IS NULL THEN N'' ELSE N' ADD CONSTRAINT '+QUOTENAME(k.name)+N' '+
 CASE WHEN k.type='PK' THEN N'PRIMARY KEY ' ELSE N'UNIQUE ' END+i.type_desc END+
 N' ('+keys.Columns+N')'+CASE WHEN k.object_id IS NULL AND inc.Columns IS NOT NULL THEN N' INCLUDE ('+inc.Columns+N')' ELSE N'' END+
 CASE WHEN i.has_filter=1 THEN N' WHERE '+i.filter_definition ELSE N'' END+N';'
FROM sys.indexes i LEFT JOIN sys.key_constraints k ON k.parent_object_id=i.object_id AND k.unique_index_id=i.index_id
CROSS APPLY (SELECT STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(COL_NAME(ic.object_id,ic.column_id))+
 CASE WHEN ic.is_descending_key=1 THEN N' DESC' ELSE N' ASC' END),N',') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Columns
 FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) keys
OUTER APPLY (SELECT STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(COL_NAME(ic.object_id,ic.column_id))),N',')
 WITHIN GROUP (ORDER BY ic.index_column_id) AS Columns FROM sys.index_columns ic
 WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.is_included_column=1) inc
WHERE i.type IN (1,2) AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN #Columns c
 ON ic.object_id=OBJECT_ID(N'dbo.'+c.TableName) AND COL_NAME(ic.object_id,ic.column_id)=c.ColumnName
 WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id);

INSERT #Rebuild (Phase,DropSql,CreateSql)
SELECT 4,N'ALTER TABLE '+QUOTENAME(OBJECT_SCHEMA_NAME(c.parent_object_id))+N'.'+QUOTENAME(OBJECT_NAME(c.parent_object_id))+
 N' DROP CONSTRAINT '+QUOTENAME(c.name)+N';',
 N'ALTER TABLE '+QUOTENAME(OBJECT_SCHEMA_NAME(c.parent_object_id))+N'.'+QUOTENAME(OBJECT_NAME(c.parent_object_id))+
 N' WITH CHECK ADD CONSTRAINT '+QUOTENAME(c.name)+N' CHECK '+c.definition+N';'
FROM sys.check_constraints c WHERE EXISTS (SELECT 1 FROM #Columns x WHERE c.parent_object_id=OBJECT_ID(N'dbo.'+x.TableName));

DECLARE @table sysname,@column sysname,@target sysname,@nullable bit,@sql nvarchar(max);
DECLARE primary_columns CURSOR LOCAL FAST_FORWARD FOR SELECT TableName,ColumnName FROM #Columns WHERE IsPrimary=1;
OPEN primary_columns; FETCH NEXT FROM primary_columns INTO @table,@column;
WHILE @@FETCH_STATUS=0
BEGIN
 SET @sql=N'ALTER TABLE dbo.'+QUOTENAME(@table)+N' ADD '+QUOTENAME(N'__Guid'+@column)+N' uniqueidentifier NULL;'; EXEC sp_executesql @sql;
 SET @sql=N'UPDATE dbo.'+QUOTENAME(@table)+N' SET '+QUOTENAME(N'__Guid'+@column)+N'=NEWID();'; EXEC sp_executesql @sql;
 FETCH NEXT FROM primary_columns INTO @table,@column;
END;
CLOSE primary_columns; DEALLOCATE primary_columns;

DECLARE foreign_columns CURSOR LOCAL FAST_FORWARD FOR SELECT TableName,ColumnName,TargetTable FROM #Columns WHERE IsPrimary=0;
OPEN foreign_columns; FETCH NEXT FROM foreign_columns INTO @table,@column,@target;
WHILE @@FETCH_STATUS=0
BEGIN
 SET @sql=N'ALTER TABLE dbo.'+QUOTENAME(@table)+N' ADD '+QUOTENAME(N'__Guid'+@column)+N' uniqueidentifier NULL;'; EXEC sp_executesql @sql;
 SET @sql=N'UPDATE child SET '+QUOTENAME(N'__Guid'+@column)+N'=parent.__GuidId FROM dbo.'+QUOTENAME(@table)+
 N' child JOIN dbo.'+QUOTENAME(@target)+N' parent ON child.'+QUOTENAME(@column)+N'=parent.Id; '+
 N'IF EXISTS(SELECT 1 FROM dbo.'+QUOTENAME(@table)+N' WHERE '+QUOTENAME(@column)+N' IS NOT NULL AND '+QUOTENAME(N'__Guid'+@column)+
 N' IS NULL) THROW 51001,''A foreign key could not be mapped to its GUID.'',1;'; EXEC sp_executesql @sql;
 FETCH NEXT FROM foreign_columns INTO @table,@column,@target;
END;
CLOSE foreign_columns; DEALLOCATE foreign_columns;

-- Patch snapshot IDs before dropping the numeric mapping columns. A pre-catalog
-- snapshot with no FK stays historical/unlinked. Match its exact departure where
-- possible; otherwise give that historical snapshot its own opaque GUID identity.
EXEC sp_executesql N'
IF EXISTS(SELECT 1 FROM dbo.BusBookings WHERE JSON_QUERY(DetailsJson,''$.trip'') IS NULL)
 THROW 51002,''A booking snapshot is missing its trip object.'',1;
UPDATE booking SET DetailsJson=JSON_MODIFY(booking.DetailsJson,''$.trip.tripId'',
 CONVERT(nvarchar(36),COALESCE(booking.__GuidScheduledTripId,
 TRY_CONVERT(uniqueidentifier,JSON_VALUE(booking.DetailsJson,''$.trip.tripId'')),
 CASE WHEN matched.Matches=1 THEN TRY_CONVERT(uniqueidentifier,matched.GuidId) END,NEWID())))
FROM dbo.BusBookings booking
OUTER APPLY (
 SELECT COUNT(*) AS Matches,MIN(CONVERT(nvarchar(36),s.__GuidId)) AS GuidId FROM dbo.ScheduledTrips s
 JOIN dbo.BusRoutes route ON route.Id=s.BusRouteId JOIN dbo.Buses bus ON bus.Id=s.BusId
 JOIN dbo.Partners partner ON partner.Id=s.PartnerId
 JOIN dbo.BusTerminals origin ON origin.Id=route.OriginTerminalId JOIN dbo.Locations originCity ON originCity.Id=origin.LocationId
 JOIN dbo.BusTerminals destination ON destination.Id=route.DestinationTerminalId JOIN dbo.Locations destinationCity ON destinationCity.Id=destination.LocationId
 WHERE originCity.Name=JSON_VALUE(booking.DetailsJson,''$.trip.origin'')
 AND destinationCity.Name=JSON_VALUE(booking.DetailsJson,''$.trip.destination'')
 AND partner.DisplayName=JSON_VALUE(booking.DetailsJson,''$.trip.operator'')
 AND bus.BusClass=JSON_VALUE(booking.DetailsJson,''$.trip.busClass'')
 AND CONVERT(char(10),SWITCHOFFSET(s.DepartureAtUtc,''+08:00''),23)=JSON_VALUE(booking.DetailsJson,''$.trip.departureDate'')
 AND CONVERT(char(5),CONVERT(time,SWITCHOFFSET(s.DepartureAtUtc,''+08:00'')),108)=JSON_VALUE(booking.DetailsJson,''$.trip.departureTime'')
) matched;
IF EXISTS(SELECT 1 FROM dbo.BusBookings WHERE TRY_CONVERT(uniqueidentifier,JSON_VALUE(DetailsJson,''$.trip.tripId'')) IS NULL)
 THROW 51003,''A booking trip ID could not be converted to a GUID.'',1;';

DECLARE drop_objects CURSOR LOCAL FAST_FORWARD FOR SELECT DropSql FROM #Rebuild ORDER BY Phase,Sequence;
OPEN drop_objects; FETCH NEXT FROM drop_objects INTO @sql;
WHILE @@FETCH_STATUS=0 BEGIN EXEC sp_executesql @sql; FETCH NEXT FROM drop_objects INTO @sql; END;
CLOSE drop_objects; DEALLOCATE drop_objects;

DECLARE replace_columns CURSOR LOCAL FAST_FORWARD FOR SELECT TableName,ColumnName,IsNullable FROM #Columns ORDER BY IsPrimary,TableName,ColumnName;
OPEN replace_columns; FETCH NEXT FROM replace_columns INTO @table,@column,@nullable;
WHILE @@FETCH_STATUS=0
BEGIN
 IF @table=N'RefUsers' AND @column=N'Id'
  EXEC sp_rename N'dbo.RefUsers.Id',N'ExternalUserId',N'COLUMN';
 ELSE
 BEGIN
  SET @sql=N'ALTER TABLE dbo.'+QUOTENAME(@table)+N' DROP COLUMN '+QUOTENAME(@column)+N';'; EXEC sp_executesql @sql;
 END;
 SET @sql=N'ALTER TABLE dbo.'+QUOTENAME(@table)+N' ALTER COLUMN '+QUOTENAME(N'__Guid'+@column)+N' uniqueidentifier '+CASE WHEN @nullable=1 THEN N'NULL' ELSE N'NOT NULL' END+N';';
 EXEC sp_executesql @sql;
 DECLARE @qualified nvarchar(776)=N'dbo.'+QUOTENAME(@table)+N'.'+QUOTENAME(N'__Guid'+@column);
 EXEC sp_rename @qualified,@column,N'COLUMN';
 FETCH NEXT FROM replace_columns INTO @table,@column,@nullable;
END;
CLOSE replace_columns; DEALLOCATE replace_columns;

DECLARE create_objects CURSOR LOCAL FAST_FORWARD FOR SELECT CreateSql FROM #Rebuild ORDER BY Phase DESC,Sequence;
OPEN create_objects; FETCH NEXT FROM create_objects INTO @sql;
WHILE @@FETCH_STATUS=0 BEGIN EXEC sp_executesql @sql; FETCH NEXT FROM create_objects INTO @sql; END;
CLOSE create_objects; DEALLOCATE create_objects;
CREATE UNIQUE INDEX UX_RefUsers_ExternalUserId ON dbo.RefUsers(ExternalUserId);

IF EXISTS(SELECT 1 FROM sys.tables t JOIN sys.indexes i ON i.object_id=t.object_id AND i.is_primary_key=1
 JOIN sys.index_columns ic ON ic.object_id=t.object_id AND ic.index_id=i.index_id
 JOIN sys.columns c ON c.object_id=t.object_id AND c.column_id=ic.column_id
 JOIN sys.types ty ON ty.user_type_id=c.user_type_id
 WHERE t.name<>N'__EFMigrationsHistory' AND ty.name<>N'uniqueidentifier')
 THROW 51004,'An application primary key is still not a GUID.',1;

IF EXISTS(SELECT TableName,Total FROM #OriginalCounts EXCEPT
 SELECT TableName,Total FROM (
 SELECT N'Partners' AS TableName,COUNT_BIG(*) AS Total FROM dbo.Partners UNION ALL SELECT N'Locations',COUNT_BIG(*) FROM dbo.Locations
 UNION ALL SELECT N'BusTerminals',COUNT_BIG(*) FROM dbo.BusTerminals UNION ALL SELECT N'Buses',COUNT_BIG(*) FROM dbo.Buses
 UNION ALL SELECT N'BusRoutes',COUNT_BIG(*) FROM dbo.BusRoutes UNION ALL SELECT N'ScheduledTrips',COUNT_BIG(*) FROM dbo.ScheduledTrips
 UNION ALL SELECT N'BusBookings',COUNT_BIG(*) FROM dbo.BusBookings UNION ALL SELECT N'RefUsers',COUNT_BIG(*) FROM dbo.RefUsers) counts)
 THROW 51005,'Row counts changed during GUID conversion.',1;
IF COALESCE(@originalAvailable,0)<>COALESCE((SELECT SUM(CONVERT(bigint,AvailableSeats)) FROM dbo.ScheduledTrips),0)
 OR COALESCE(@originalReserved,0)<>COALESCE((SELECT SUM(CONVERT(bigint,ReservedSeats)) FROM dbo.BusBookings),0)
 THROW 51006,'Seat inventory changed during GUID conversion.',1;

DROP TABLE #Rebuild;
DROP TABLE #Columns;
DROP TABLE #OriginalCounts;
