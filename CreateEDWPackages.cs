
/***********************************************************************
code for the CreateEDWPackages biml file.
separated out here so can format correctly.

need to put something like this in te CreateEDWPackages.biml file:
<#@ code file="CreateEDWPackages.cs" #>
***********************************************************************/


using System.Windows.Forms;
using System.IO;

//using Varigence.Hadron;

/*
assembly name="C:\\Windows\\Microsoft.NET\\Framework\\v2.0.50727\\System.Windows.Forms.dll" 
import namespace="System.Windows.Forms" 
import namespace="System.IO" 
*/

Varigence.Utility.Collections.VulcanCollection<AstTableNode> GetTables()
{
	// return a collection of all the tables in the stage schema.

	Varigence.Utility.Collections.VulcanCollection<AstTableNode> retTables = new Varigence.Utility.Collections.VulcanCollection<AstTableNode>();
	foreach (var table in RootNode.Tables)
	{
		if (table.SchemaName.Equals("stg"))
		{
			var annotRequiresETL = table.Annotations.FirstOrDefault(a => a.Tag.Equals("RequiresETL"));
			if (annotRequiresETL.Text.Equals("True"))
			{
				retTables.Add(table);
			}
		}
	}

	return retTables;
}

bool IsDeltaLoad(AstTableNode table)
{
	// return if the table is Delta Load == True or not
	var annotLoadDelta = table.Annotations.FirstOrDefault(a => a.Tag.Equals("LoadDeltaOnly"));
	return annotLoadDelta.Text.Equals("True");
}

String BuildGetIndicatorSQL(AstTableNode table)
{
	// Build SQL string that will get the current indicator value
	// from the audit table, OR if there is no record there yet,
	// return a default value which should then load all history.

	var annotField = table.Annotations.FirstOrDefault(a => a.Tag.Equals("IndicatorField"));
	var annotDataType = table.Annotations.FirstOrDefault(a => a.Tag.Equals("IndicatorFieldDataType"));

	String strSQL = "SELECT IndicatorValue FROM audit.DeltaIndicators ";
	strSQL = strSQL + "WHERE TargetTableName = '" + table.Name + "' ";
	strSQL = strSQL + "AND IndicatorField = '" + annotField.Text + "' ";
	strSQL = strSQL + "AND IsCurrent = 1 ";
	strSQL = strSQL + "UNION ALL ";

	if (annotDataType.Text.Equals("DateTime"))
	{
		strSQL = strSQL + "SELECT '1-1-1900' AS IndicatorValue ";
	}
	else if (annotDataType.Text.Equals("DateTime2"))
	{
		strSQL = strSQL + "SELECT '1-1-0001' AS IndicatorValue ";
	}
	else
	{
		strSQL = strSQL + "SELECT '0' AS IndicatorValue ";
	}

	strSQL = strSQL + "WHERE NOT EXISTS ( ";
	strSQL = strSQL + "SELECT IndicatorValue FROM audit.DeltaIndicators ";
	strSQL = strSQL + "WHERE TargetTableName = '" + table.Name + "' ";
	strSQL = strSQL + "AND IndicatorField = '" + annotField.Text + "'); ";

	return strSQL;
}

String GetTableSourceSQL(AstTableNode table)
{
	// return concatenation of table's SourceSQL extended Properties
	var annotSourceSQL = table.Annotations.FirstOrDefault(a => a.Tag.Equals("SourceSQL"));
	var annotSourceSQL1 = table.Annotations.FirstOrDefault(a => a.Tag.Equals("SourceSQL1"));
	var annotSourceSQL2 = table.Annotations.FirstOrDefault(a => a.Tag.Equals("SourceSQL2"));
	var annotSourceSQL3 = table.Annotations.FirstOrDefault(a => a.Tag.Equals("SourceSQL3"));
	var annotSourceSQL4 = table.Annotations.FirstOrDefault(a => a.Tag.Equals("SourceSQL4"));
	var annotSourceSQL5 = table.Annotations.FirstOrDefault(a => a.Tag.Equals("SourceSQL5"));

	String retSQL = annotSourceSQL.Text;

	if (annotSourceSQL2 != null)
	{
		retSQL = retSQL + annotSourceSQL2.Text;
		if (annotSourceSQL3 != null)
		{
			retSQL = retSQL + annotSourceSQL3.Text;
			if (annotSourceSQL4 != null)
			{
				retSQL = retSQL + annotSourceSQL4.Text;
				if (annotSourceSQL5 != null)
				{
					retSQL = retSQL + annotSourceSQL5.Text;
				}
			}
		}
	}

	retSQL = retSQL.Replace("?", "'1/1/1'");    // *** TRG Change this! ***
	return retSQL;
}

bool ReturnBooleanColumnAnnotationValue(AstTableColumnBaseNode column, string annotName)
{
	var annot = column.Annotations.FirstOrDefault(a => a.Tag.Equals(annotName));
	if (annot != null)
	{
		return annot.Text.Equals("True");
	}
	else
	{
		return false;
	}
}

bool ReturnBooleanTableAnnotationValue(AstTableNode table, string annotName)
{
	var annot = table.Annotations.FirstOrDefault(a => a.Tag.Equals(annotName));
	if (annot != null)
	{
		return annot.Text.Equals("True");
	}
	else
	{
		return false;
	}
}

bool TableContainsSurrogateKeys(AstTableNode table)
{
	// return if the passed table has columns with IsSurrogateKey == 'True'
	bool flag = false;
	foreach (var column in table.Columns)
	{
		var annotIsForeignKey = column.Annotations.FirstOrDefault(b => b.Tag.Equals("IsForeignKey"));
		if (annotIsForeignKey != null)
		{
			if (annotIsForeignKey.Text.Equals("True"))
			{
				flag = true;
				break;
			}
		}
	}
	return flag;
}

bool LookupTableIsType1(AstTableNode table, AstTableColumnBaseNode column)
{

	// get that table object from the RootNode.Tables collection. NOTE: assume the Schema is the 'dbo' schema.
	var annotForeignKeyTable = column.Annotations.FirstOrDefault(a => a.Tag.Equals("ForeignKeyTable"));
	var targetTable = RootNode.Tables.FirstOrDefault(a => a.Name.Equals(annotForeignKeyTable.Text) && a.Schema.Name.Equals("dbo"));

	var annotSCDType = targetTable.Annotations.FirstOrDefault(a => a.Tag.Equals("SCDType"));

	if (annotSCDType.Text.Equals("SCDType-1"))
	{
		return true;
	}
	else
	{
		return false;
	}
}

String BuildInferredMemberSourceSQL(AstTableNode table, AstTableColumnBaseNode column)
{
	/****************************************************************
    Build SQL to insert inferred members, and return a set of created inferred members for logging.
    The SQL should look a bit like this:
    
		if the dimension is SCDType-2:
    INSERT INTO dimension (
      code, description, <... + other fields...>
			EffectiveFrom, EffectiveFromETLPackageRunID, 
			EffectiveTo, ETLEffectiveToETLPackageRunID, IsCurrent, 
			IsInferredMember, ETLIsInferredMemberETLPackageRunID)
    OUTPUT inserted.code AS NaturalKey, '<dimension>' AS Dimension, GETDATE() AS Created
    SELECT DISTINCT code, 'UNKNOWN', GETDATE(), ?, 
			'31-Dec-9999', -1, 1, 1, ?
    FROM stage
    WHERE stage.code NOT IN (
        SELECT code 
        FROM dimension
        WHERE EffectiveFrom <= stage.datetimestamp
          AND EffectiveTo >= stage.datetimestamp) 
		
		if the dimension is SCDType-1:
    INSERT INTO dimension (
      code, description, <... + other fields ...>
			Created, ETLCreatedETLPackageRunID, 
			Updated, ETLUpdatedETLPackageRunID, 
			IsInferredMember, ETLIsInferredMemberETLPackageRunID)
    OUTPUT inserted.code AS NaturalKey, '<dimension>' AS Dimension, GETDATE() AS Created, GETDATE() AS Updated
    SELECT DISTINCT code, 'UNKNOWN', GETDATE(), 1
    FROM stage
    WHERE stage.code NOT IN (
        SELECT code 
        FROM dimension)
    ****************************************************************/

	// find out the dimension table and its natural key we need to add inferred members to
	var annotForeignKeyTable = column.Annotations.FirstOrDefault(a => a.Tag.Equals("ForeignKeyTable"));
	var annotForeignKeyLookupColumn = column.Annotations.FirstOrDefault(a => a.Tag.Equals("ForeignKeyLookupColumn"));

	// get that table object from the RootNode.Tables collection. NOTE: assume the Schema is the 'dbo' schema.
	var targetTable = RootNode.Tables.FirstOrDefault(a => a.Name.Equals(annotForeignKeyTable.Text) && a.Schema.Name.Equals("dbo"));

	String strSQL = "INSERT INTO " + targetTable.Schema.Name + "." + targetTable.Name + " ( ";

	foreach (var targColumn in targetTable.Columns)
	{
		// add in column names, except for Surrogate Key of course
		var annotIsSurrogateKey = targColumn.Annotations.FirstOrDefault(a => a.Tag.Equals("IsSurrogateKey"));
		var annotIsAuditField = targColumn.Annotations.FirstOrDefault(a => a.Tag.Equals("IsAuditField"));
		if (annotIsSurrogateKey != null && annotIsAuditField != null)
		{
			if (annotIsSurrogateKey.Text.Equals("False") && annotIsAuditField.Text.Equals("False"))
			{
				strSQL = strSQL + targColumn.Name + ", ";
			}
		}
	}

	// find out if the dimension table is a SCDType-1 or SCDType-2
	var annotSCDType = targetTable.Annotations.FirstOrDefault(a => a.Tag.Equals("SCDType"));

	if (annotSCDType.Text.Equals("SCDType-2"))
	{
		// Build SCDType-2 query
		strSQL = strSQL + "EffectiveFrom, ETLEffectiveFromETLPackageRunID, EffectiveTo, ETLEffectiveToETLPackageRunID, ";
		strSQL = strSQL + "IsCurrent, IsInferredMember, ETLIsInferredMemberETLPackageRunID) ";
	}
	else
	{
		// Build SCDType-1 query
		strSQL = strSQL + "Created, ETLCreatedETLPackageRunID, Updated, ETLUpdatedETLPackageRunID, IsInferredMember, ETLIsInferredMemberETLPackageRunID) ";
	}

	// build the OUTPUT statement
	strSQL = strSQL + "OUTPUT inserted." + annotForeignKeyLookupColumn.Text + " AS NaturalKey, '" + targetTable.Name + "' AS Dimension, GETDATE() AS Created ";

	// build SELECT statement..
	strSQL = strSQL + "SELECT DISTINCT " + annotForeignKeyLookupColumn.Text + ", ";
	foreach (var targColumn in targetTable.Columns)
	{
		var annotIsSurrogateKey = targColumn.Annotations.FirstOrDefault(a => a.Tag.Equals("IsSurrogateKey"));
		var annotIsAuditField = targColumn.Annotations.FirstOrDefault(a => a.Tag.Equals("IsAuditField"));
		var annotInferredMemberValue = targColumn.Annotations.FirstOrDefault(a => a.Tag.Equals("InferredMemberValue"));
		if (annotIsSurrogateKey != null && annotIsAuditField != null)
		{
			if (annotIsSurrogateKey.Text.Equals("False") && annotIsAuditField.Text.Equals("False"))
			{
				if (!targColumn.Name.Equals(annotForeignKeyLookupColumn.Text))
				{
					// add in the correct inferred data depending on the data type of the column.
					strSQL = strSQL + annotInferredMemberValue.Text + ", ";
				}
			}
		}
	}

	if (annotSCDType.Text.Equals("SCDType-2"))
	{
		// Build SCDType-2 query
		strSQL = strSQL + "GETDATE(), ?, '31-12-9999', -1, 1, 1, ? ";
		//strSQL = strSQL + "GETDATE(), 1, '31-12-9999', -1, 1, 1, 1 ";
	}
	else
	{
		// Build SCDType-1 query
		strSQL = strSQL + "GETDATE(), ?, GETDATE(), ?, 1, ? ";
		//strSQL = strSQL + "GETDATE(), 1, GETDATE(), 1, 1, 1 ";
	}

	strSQL = strSQL + "FROM stg." + table.Name + " WHERE " + annotForeignKeyLookupColumn.Text + " NOT IN ( ";
	strSQL = strSQL + "SELECT " + annotForeignKeyLookupColumn.Text + " FROM " + targetTable.Schema.Name + "." + targetTable.Name + " ";

	if (annotSCDType.Text.Equals("SCDType-2"))
	{
		// Build SCDType-2 query

		// get the fact record effective / operative data field from the original table
		var annotHistEffField = table.Annotations.FirstOrDefault(a => a.Tag.Equals("HistoryEffectiveFromField"));

		strSQL = strSQL + "WHERE " + table.Name + "." + annotHistEffField.Text + " BETWEEN " + targetTable.Name + ".EffectiveFrom AND " + targetTable.Name + ".EffectiveTo";
	}

	strSQL = strSQL + ");";

	//MessageBox.Show(strSQL); 

	return strSQL;
}

String BuildSKLookupSQL(AstTableNode table, AstTableColumnBaseNode column)
{
	/****************************************************************
    Build SQL to lookup surrogate keys for the natural key values.
    The SQL should look a bit like this:
    
    UPDATE stage SET
      ForeignKey = SurrogateKey
    FROM dimension
    WHERE dimension.NaturalKey = stage.NaturalKey
		(if the dimension is SCDType-2:)
      AND dimension.EffectiveFrom <= the as at date/time
      AND dimension.EffectiveTo >= the as at date/time
    ****************************************************************/

	var annotForeignKeyTable = column.Annotations.FirstOrDefault(a => a.Tag.Equals("ForeignKeyTable"));
	var annotForeignKeyLookupColumn = column.Annotations.FirstOrDefault(a => a.Tag.Equals("ForeignKeyLookupColumn"));
	// get that table object from the RootNode.Tables collection. NOTE: assume the Schema is the 'dbo' schema.
	var targetTable = RootNode.Tables.FirstOrDefault(a => a.Name.Equals(annotForeignKeyTable.Text) && a.Schema.Name.Equals("dbo"));

	String strSQL = "UPDATE " + table.Schema.Name + "." + table.Name + " SET ";
	strSQL = strSQL + column.Name + " = " + column.Name + " ";
	strSQL = strSQL + "FROM " + annotForeignKeyTable.Text + " ";
	strSQL = strSQL + "WHERE " + annotForeignKeyLookupColumn.Text + " = " + annotForeignKeyLookupColumn.Text + " ";

	// find out if the dimension table is a SCDType-1 or SCDType-2
	var annotSCDType = targetTable.Annotations.FirstOrDefault(a => a.Tag.Equals("SCDType"));
	if (annotSCDType.Text.Equals("SCDType-2"))
	{
		var annotHistEffField = targetTable.Annotations.FirstOrDefault(a => a.Tag.Equals("HistoryEffectiveFromField"));

		strSQL = strSQL + "AND " + table.Schema.Name + "." + table.Name + "." + annotHistEffField.Text + " BETWEEN ";
		strSQL = strSQL + annotForeignKeyTable.Text + ".EffectiveFrom AND " + annotForeignKeyTable.Text + ".EffectiveTo";
	}
	strSQL = strSQL + ";";

	return strSQL;
}


String CreateStageTableHistoryIndex(AstTableNode table)
{
	/**********************************************
	Build SQL to create an index on the Natural Keys + History Effective From field on the Stage table.
	Something like: 
		CREATE CLUSTERED INDEX PK_<table name> ON stg.<table name>
		(
			<natural key(s)> ASC,
			<history effective from field> ASC
		)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

	**********************************************/
	String strSQL = "CREATE CLUSTERED INDEX PK_" + table.Name + " ON stg." + table.Name + " ( ";

	// get Natural Key(s)
	var annotNaturalKeys = table.Annotations.FirstOrDefault(a => a.Tag.Equals("NaturalKey"));
	string[] separator = new string[] { "|" };
	string[] keys = annotNaturalKeys.Text.Split(separator, StringSplitOptions.RemoveEmptyEntries);

	for (int i = 0; i < keys.Length; i++)
	{
		strSQL = strSQL + keys[i] + " ASC, ";
	}

	// get HistoryEffectiveFromField
	var annotHistoryEffectiveFromField = table.Annotations.FirstOrDefault(a => a.Tag.Equals("HistoryEffectiveFromField"));
	strSQL = strSQL + annotHistoryEffectiveFromField.Text + " ASC ) ";

	strSQL = strSQL + "WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]";

	return strSQL;
}

String UpdateRankNoSQL(AstTableNode table)
{
	/************************************************
	Create SQL that will update the RankNo field on the Stage Table, so we can MERGE the history data in the correct order.

	SQL will be like this:
	UPDATE stg.TableName
	SET RankNo = thing.Rnk
	FROM 
		(SELECT NaturalKeyField1, NaturalKeyField2, HistoryEffectiveFromField,
			ROW_NUMBER() OVER (PARTITION BY NaturalKeyField1, NaturalKeyField2 ORDER BY HistoryEffectiveFromField DESC) AS Rnk
		FROM stg.TableName) thing
	WHERE stg.TableName.NaturalKeyField1 = thing.NaturalKeyField1
		AND stg.TableName.NaturalKeyField2 = thing.NaturalKeyField2
		AND stg.TableName.HistoryEffectiveFromField = thing.HistoryEffectiveFromField;

	************************************************/

	String strSQL = "UPDATE stg." + table.Name + " SET RankNo = thing.Rnk FROM ( SELECT ";

	// get Natural Key(s)
	var annotNaturalKeys = table.Annotations.FirstOrDefault(a => a.Tag.Equals("NaturalKey"));
	string[] separator = new string[] { "|" };
	string[] keys = annotNaturalKeys.Text.Split(separator, StringSplitOptions.RemoveEmptyEntries);

	for (int i = 0; i < keys.Length; i++)
	{
		strSQL = strSQL + keys[i] + ", ";
	}

	// get HistoryEffectiveFromField
	var annotHistoryEffectiveFromField = table.Annotations.FirstOrDefault(a => a.Tag.Equals("HistoryEffectiveFromField"));
	strSQL = strSQL + annotHistoryEffectiveFromField.Text + " ROW_NUMBER() OVER (PARTITION BY ";

	for (int i = 0; i < keys.Length; i++)
	{
		strSQL = strSQL + keys[i] + ", ";
	}
	strSQL = strSQL.Remove(strSQL.Length - 2) + " ORDER BY ";

	strSQL = strSQL + annotHistoryEffectiveFromField.Text + " DESC) AS Rnk FROM stg." + table.Name + ") thing WHERE ";

	for (int i = 0; i < keys.Length; i++)
	{
		if (i > 0)
		{
			strSQL = strSQL + "AND ";
		}
		strSQL = strSQL + "stg." + keys[i] + " = thing." + keys[i] + " ";
	}
	strSQL = strSQL + " AND stg." + annotHistoryEffectiveFromField.Text + " = thing." + annotHistoryEffectiveFromField + ";";

	return strSQL;
}




/*********

Merge bits

Differences:
	Type-1 only:
		Build a MERGE for Type-1 dims / facts.
		May also include history?

	Type-2 (may also have Type-1 fields):
		Build a MERGE for Type-2 dims / facts.
		May also include History.

*********/


String BuildType2MergeSQL(AstTableNode table, bool mergeDelta)
{
	/****************************************************************
    Build SQL MERGE statement to process the EDW table from it's staging table.
		
		Note that we may be merging History data - ie there could be multiple records in our Staging table with the same
			natural key, but differentiated by their effective dates.
		
		Note also that 
    We're merging ALL records from the Source that have been Staged, with the records in our EDS table. 
		
    The SQL should look a bit like this:
    
    DECLARE @Counter smallint
    SET @Counter = (SELECT MAX(RankNo) FROM stage)

    WHILE (@Counter >= 1)
    BEGIN

		insert new type-2 records as taken from the OUTPUT clause of the MERGE statement
    INSERT dimension (... dimension column names,
	    EffectiveFrom, EffectiveTo, IsCurrent, IsInferredMember)
		SELECT ... column names,
	    Source.ChangeDate
    FROM (

    MERGE dimension AS Target
	    USING (
		    SELECT ...
		    FROM stage
		    WHERE RankNo = @Counter) AS Source
	    ON Target.NaturalKey = Source.NaturalKey

    WHEN MATCHED
	    AND Target.IsCurrent = 1
	    AND EXISTS (
		     this tests for changes between Source and Target, handling NULLS gracefully.
		     basically, if everything matches here, it doesn't register as a change
		     Type-1 fields need to be left out of here.
		    SELECT Source. ... (include Type-2 change columns here.)
		    EXCEPT
		    SELECT Target. ...)
	    THEN
		     update the target row to close off for Type-2 change. 
		    UPDATE SET
			    Target.EffectiveTo = DATEADD(d, -1, CONVERT(datetime, CONVERT(char(8), (Source.ChangeDate)))),
			    Target.IsCurrent = 0,
			    Target.IsInferredMember = 0

    WHEN NOT MATCHED BY TARGET 
	    THEN
		     new surrogate key, so add new record 
		    INSERT (...target column names,
			    EffectiveFrom, EffectiveTo, IsCurrent, IsInferredMember)
		    VALUES (Source. ...,
			    Source.ChangeDate, '31-Dec-9999', 1, 0)

    OUTPUT $ACTION ActionOut, Source. ...
	    ) AS MergeOut
	    WHERE MergeOut.ActionOut = 'UPDATE';

    SET @Counter = @Counter - 1;

    END  end of loop  

     Type-1 changes done here.
		 *** TRG: note: we prob want to do a MERGE here as well, just updating those records that need it. ***
		 
     take the value for the type-1 fields for the _last_ record (RankNo = 1)
     and update all the records in the dimension for the same natural key
    UPDATE dimension SET
	    ...column name = ...column name
    FROM stage s
    WHERE s.RankNo = 1
	    AND s.naturalkey = dimension.naturalkey;
    
    ****************************************************************/


	// get a collection of columns we want in our MERGE statement
	Varigence.Utility.Collections.VulcanCollection<AstTableColumnBaseNode> cols = new Varigence.Utility.Collections.VulcanCollection<AstTableColumnBaseNode>();
	foreach (var column in table.Columns)
	{
		var annotIsSurrogateKey = column.Annotations.FirstOrDefault(a => a.Tag.Equals("IsSurrogateKey"));
		var annotIsAuditField = column.Annotations.FirstOrDefault(a => a.Tag.Equals("IsAuditField"));
		if (annotIsSurrogateKey != null && annotIsAuditField != null)
		{
			if (!annotIsSurrogateKey.Text.Equals("True") && !annotIsAuditField.Text.Equals("True"))
			{
				cols.Add(column);
			}
		}
	}

	// first, create variable to hold counter.
	String strSQL = "DECLARE @Counter smallint; ";
	strSQL = strSQL + "SET @Counter = (SELECT MAX(RankNo) FROM stg." + table.Name + ");";

	strSQL = strSQL + "WHILE (@Counter >= 1) BEGIN ";

	// create the INSERT statement for the new Type-2 records we create
	strSQL = strSQL + "INSERT INTO dbo." + table.Name + " ( ";
	foreach (var col in cols)
	{
		strSQL = strSQL + col.Name + ", ";
	}
	strSQL = strSQL + "EffectiveFrom, ETLEffectiveFromETLPackageRunID, EffectiveTo, ETLEffectiveToETLPackageRunID, IsCurrent, IsInferredMember ) ";
	strSQL = strSQL + "SELECT ";
	foreach (var col in cols)
	{
		strSQL = strSQL + col.Name + ", ";
	}
	strSQL = strSQL + "'1-Jan-0001', ?, '31-Dec-9999', -1, 1, 0 FROM ( ";

	// start MERGE statement
	strSQL = strSQL + "MERGE dbo." + table.Name + " AS Target ";
	strSQL = strSQL + "USING (SELECT ";
	foreach (var col in cols)
	{
		strSQL = strSQL + col.Name + ", ";
	}
	strSQL = strSQL.Substring(0, strSQL.Length - 2) + " FROM stg." + table.Name + " WHERE RankNo = @Counter) AS Source ";

	// get Natural Keys to complete join ON statement
	var annotNaturalKeys = table.Annotations.FirstOrDefault(a => a.Tag.Equals("NaturalKey"));
	string[] separator = new string[] { "|" };
	string[] keys = annotNaturalKeys.Text.Split(separator, StringSplitOptions.RemoveEmptyEntries);

	strSQL = strSQL + "ON ";
	for (int i = 0; i < keys.Length; i++)
	{
		if (i > 0) strSQL = strSQL + "AND ";
		strSQL = strSQL + "Target." + keys[i] + " = Source." + keys[i] + " ";
	}

	// When there has been a change in a row we've already got in the EDS
	strSQL = strSQL + "WHEN MATCHED AND Target.IsCurrent = 1 AND Target.IsInferredMember = 0 AND EXISTS (SELECT ";

	// only include Type-2 columns (and natual keys???)
	var annotSCDType = cols[0].Annotations.FirstOrDefault(a => a.Tag.Equals("SCDType"));
	foreach (var col in cols)
	{
		annotSCDType = col.Annotations.FirstOrDefault(a => a.Tag.Equals("SCDType"));
		if (annotSCDType.Text.Equals("SCDType-2"))
		{
			strSQL = strSQL + "Source." + col.Name + ", ";
		}
	}
	strSQL = strSQL.Substring(0, strSQL.Length - 2) + " EXCEPT SELECT ";
	foreach (var col in cols)
	{
		annotSCDType = col.Annotations.FirstOrDefault(a => a.Tag.Equals("SCDType"));
		if (annotSCDType.Text.Equals("SCDType-2"))
		{
			strSQL = strSQL + "Target." + col.Name + ", ";
		}
	}
	strSQL = strSQL.Substring(0, strSQL.Length - 2) + ") THEN UPDATE SET ";
	strSQL = strSQL + " Target.EffectiveTo = GETDATE(), ";
	strSQL = strSQL + " Target.ETLEffectiveToETLPackageRunID = ?, ";
	strSQL = strSQL + " Target.IsCurrent = 0 ";

	// when the row isnt in the table yet, insert it
	strSQL = strSQL + "WHEN NOT MATCHED BY TARGET THEN INSERT (";
	foreach (var col in cols)
	{
		strSQL = strSQL + col.Name + ", ";
	}
	strSQL = strSQL + "EffectiveFrom, ETLEffectiveFromETLPackageRunID, EffectiveTo, ETLEffectiveToETLPackageRunID, IsCurrent, IsInferredMember) VALUES (";
	foreach (var col in cols)
	{
		strSQL = strSQL + "Source." + col.Name + ", ";
	}
	strSQL = strSQL + "GETDATE(), ?, '31-Dec-9999', -1, 1, 0) ";

	// set up output so we can insert new type-2 rows...
	strSQL = strSQL + "OUTPUT $ACTION ActionOut, ";
	foreach (var col in cols)
	{
		strSQL = strSQL + "Source." + col.Name + ", ";
	}
	strSQL = strSQL.Remove(strSQL.Length - 2);  // trim off that last comma

	strSQL = strSQL + " ) AS MergeOut WHERE MergeOut.ActionOut = 'UPDATE'; ";


	// now handle inferred members with another MERGE
	strSQL = strSQL + "MERGE dbo." + table.Name + " AS Target ";
	strSQL = strSQL + "USING (SELECT ";
	foreach (var col in cols)
	{
		strSQL = strSQL + col.Name + ", ";
	}
	strSQL = strSQL.Substring(0, strSQL.Length - 2) + " FROM stg." + table.Name + " WHERE RankNo = @Counter) AS Source ";

	// Put in Natural keys...
	strSQL = strSQL + "ON ";
	for (int i = 0; i < keys.Length; i++)
	{
		if (i > 0) strSQL = strSQL + "AND ";
		strSQL = strSQL + "Target." + keys[i] + " = Source." + keys[i] + " ";
	}

	// When Matched, update all columns
	strSQL = strSQL + "WHEN MATCHED AND Target.IsCurrent = 1 AND Target.IsInferredMember = 1 THEN ";
	strSQL = strSQL + "UPDATE SET ";
	foreach (var col in cols)
	{
		strSQL = strSQL + "Target." + col.Name + " = Source." + col.Name + ", ";
	}
	strSQL = strSQL + "Target.IsInferredMember = 0; ";

	strSQL = strSQL + "SET @Counter = @Counter - 1; END ";


	// Now do Type-1 Changes with another MERGE
	strSQL = strSQL + "MERGE dbo." + table.Name + " AS Target ";
	strSQL = strSQL + "USING (SELECT ";
	foreach (var col in cols)
	{
		strSQL = strSQL + col.Name + ", ";
	}
	strSQL = strSQL.Substring(0, strSQL.Length - 2) + " FROM stg." + table.Name + " WHERE RankNo = 1) AS Source ";

	// Put in Natural keys...
	strSQL = strSQL + "ON ";
	for (int i = 0; i < keys.Length; i++)
	{
		if (i > 0) strSQL = strSQL + "AND ";
		strSQL = strSQL + "Target." + keys[i] + " = Source." + keys[i] + " ";
	}

	// do the Type-1 Changes when Matched...
	strSQL = strSQL + "WHEN MATCHED AND EXISTS ( SELECT ";

	// Only Type-1 columns here...
	foreach (var col in cols)
	{
		annotSCDType = col.Annotations.FirstOrDefault(a => a.Tag.Equals("SCDType"));
		if (annotSCDType.Text.Equals("SCDType-1"))
		{
			strSQL = strSQL + "Source." + col.Name + ", ";
		}
	}
	strSQL = strSQL.Substring(0, strSQL.Length - 2) + " EXCEPT SELECT ";
	foreach (var col in cols)
	{
		annotSCDType = col.Annotations.FirstOrDefault(a => a.Tag.Equals("SCDType"));
		if (annotSCDType.Text.Equals("SCDType-1"))
		{
			strSQL = strSQL + "Target." + col.Name + ", ";
		}
	}
	strSQL = strSQL.Substring(0, strSQL.Length - 2) + ") THEN UPDATE SET ";
	foreach (var col in cols)
	{
		strSQL = strSQL + "Target." + col.Name + " = Source." + col.Name + ", ";
	}

	strSQL = strSQL.Substring(0, strSQL.Length - 2) + ";";


	MessageBox.Show(strSQL);
	return strSQL;
}






String BuildGetDeltaIndicatorSQL(AstTableNode table)
{
	// build SQL to get the latest Delta Indicator for the passed table
	var annotField = table.Annotations.FirstOrDefault(a => a.Tag.Equals("IndicatorField"));
	var annotDataType = table.Annotations.FirstOrDefault(a => a.Tag.Equals("IndicatorFieldDataType"));

	String strSQL = "SELECT ";

	if (annotDataType.Text.Equals("DateTime"))
	{
		strSQL = strSQL + "ISNULL(MAX(" + annotField.Text + "), '1-1-1900') ";
	}
	else if (annotDataType.Text.Equals("DateTime2"))
	{
		strSQL = strSQL + "ISNULL(MAX(" + annotField.Text + "), '1-1-0001') ";
	}
	else
	{
		strSQL = strSQL + "ISNULL(MAX(" + annotField.Text + "), '0') ";
	}

	strSQL = strSQL + "AS DeltaString FROM dbo." + table.Name + ";";

	return strSQL;
}

String BuildSaveDeltaIndicatorSQL(AstTableNode table)
{
	// build SQL to save latest delta indicator to audit table and mark it as current.
	var annotField = table.Annotations.FirstOrDefault(a => a.Tag.Equals("IndicatorField"));

	String strSQL = "UPDATE audit.DeltaIndicators SET IsCurrent = 0 ";
	strSQL = strSQL + "WHERE TargetTableName = '" + table.Name + "' ";
	strSQL = strSQL + "AND IndicatorField = '" + annotField.Text + "'; ";

	strSQL = strSQL + "INSERT INTO audit.DeltaIndicators ( ";
	strSQL = strSQL + "ETLPackageRunID, PackageName, TargetTableName, IndicatorField, ";
	strSQL = strSQL + "IndicatorValue, Created, Iscurrent ) VALUES ( ";
	strSQL = strSQL + "?, ?, '" + table.Name + "', '" + annotField.Text + "', ";
	strSQL = strSQL + "?, GETDATE(), 1 );";

	return strSQL;
}


