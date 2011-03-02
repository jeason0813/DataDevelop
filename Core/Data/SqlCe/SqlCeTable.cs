using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlServerCe;
using System.Data;

namespace DataDevelop.Data.SqlCe
{
	internal sealed class SqlCeTable : Table
	{
		private string schema;
		private bool isView;
		private bool isReadOnly;

		public SqlCeTable(SqlCeDatabase database)
			: base(database)
		{
		}

		public string Schema
		{
			get { return this.schema; }
			internal set { this.schema = value; }
		}

		public override bool IsView
		{
			get { return this.isView; }
		}

		public override bool IsReadOnly
		{
			get { return this.isReadOnly; }
		}

		public new SqlCeDatabase Database
		{
			get { return (SqlCeDatabase)base.Database; }
		}

		private SqlCeConnection Connection
		{
			get { return ((SqlCeDatabase)this.Database).Connection; }
		}

		public void SetView(bool value)
		{
			this.isView = value;
		}

		public void SetReadOnly(bool value)
		{
			this.isReadOnly = value;
		}

		public override bool Rename(string newName)
		{
			throw new NotImplementedException();
		}

		public override bool Delete()
		{
			bool success = true;
			using (this.Database.CreateConnectionScope()) {
				using (SqlCeCommand command = this.Database.Connection.CreateCommand()) {
					command.CommandText = "DROP TABLE [" + this.Schema + "].[" + this.Name + "]";
					try {
						command.ExecuteNonQuery();
					} catch (SqlCeException) {
						success = false;
					}
				}
			}
			return success;
		}

		public override DataTable GetData(int startIndex, int count, TableFilter filter, TableSort sort)
		{
			DataTable data = new DataTable(this.Name);
			using (this.Database.CreateConnectionScope()) {
				SqlCeDataAdapter adapter = (SqlCeDataAdapter)this.Database.CreateAdapter(this, filter);
				adapter.SelectCommand.CommandText = this.GetSelectStatement(startIndex, count, filter, sort);
				adapter.Fill(data);
			}
			return data;
		}

		protected override void PopulateColumns(IList<Column> columnsCollection)
		{
			using (this.Database.CreateConnectionScope()) {
				DataTable columns = this.Connection.GetSchema("Columns", new string[] { null, null, this.Name, null });
				
				DataRow[] rows = new DataRow[columns.Rows.Count];
				foreach (DataRow row in columns.Rows) {
					int i = Convert.ToInt32(row["ORDINAL_POSITION"]) - 1;
					rows[i] = row;
				}
				
				string[] keys = this.GetPrimaryKeyColumns();
				foreach (DataRow row in rows) {
					Column column = new Column(this);
					column.Name = row["COLUMN_NAME"].ToString();
					
					if (InPrimaryKey(keys, column.Name)) {
						column.InPrimaryKey = true;
					}
					column.ProviderType = (string)row["DATA_TYPE"];
					string maxLength = row["CHARACTER_MAXIMUM_LENGTH"].ToString();
					if (!String.IsNullOrEmpty(maxLength)) {
						column.ProviderType = String.Format("{0}({1})", column.ProviderType, maxLength);
					} else if (column.ProviderType.ToLower() == "numeric") {
						column.ProviderType = String.Format("numeric({0}, {1})", row["NUMERIC_PRECISION"], row["NUMERIC_SCALE"]);
					}
					columnsCollection.Add(column);
				}
			}
		}

		protected override void PopulateForeignKeys(IList<ForeignKey> foreignKeysCollection)
		{
			using (this.Database.CreateConnectionScope()) {
				string[] restrictions = new string[] { null, null, this.Name, null };
				DataTable schema = this.Connection.GetSchema("ForeignKeys", restrictions);
				foreach (DataRow row in schema.Rows) {
					ForeignKey key = new ForeignKey();
					key.Name = (string)row["CONSTRAINT_NAME"];
					key.ChildTable = this.Name;
					////key.ChildTableColumns = (string)row["COLUMN_NAME"];
					foreignKeysCollection.Add(key);
				}
			}
		}

		protected override void PopulateTriggers(IList<Trigger> triggersCollection)
		{

		}

		private static bool InPrimaryKey(string[] primaryKeys, string columnName)
		{
			foreach (string key in primaryKeys) {
				if (String.Equals(key, columnName, StringComparison.InvariantCultureIgnoreCase)) {
					return true;
				}
			}
			return false;
		}

		private string[] GetPrimaryKeyColumns()
		{
			List<string> primaryKeys = new List<string>();
			using (this.Database.CreateConnectionScope()) {
				using (SqlCeCommand select = this.Connection.CreateCommand()) {
					select.CommandText = "SELECT u.COLUMN_NAME, c.CONSTRAINT_NAME, c.TABLE_NAME " +
						"FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS c INNER JOIN " +
						"INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS u ON c.CONSTRAINT_NAME = u.CONSTRAINT_NAME AND u.TABLE_NAME = c.TABLE_NAME " +
						"where c.CONSTRAINT_TYPE = 'PRIMARY KEY' ORDER BY c.CONSTRAINT_NAME";
					select.Parameters.AddWithValue("@TableName", this.Name);
					using (SqlCeDataReader reader = select.ExecuteReader()) {
						while (reader.Read()) {
							primaryKeys.Add(reader.GetString(0));
						}
					}
				}
				return primaryKeys.ToArray();
			}
		}

		private string GetSelectStatement(int startIndex, int count, TableFilter filter, TableSort sort)
		{
			StringBuilder text = new StringBuilder();
			text.Append("SELECT ");
			filter.WriteColumnsProjection(text);
			text.Append(" FROM ");
			text.Append(this.QuotedName);
			if (filter.IsRowFiltered) {
				text.Append(" WHERE ");
				filter.WriteWhereStatement(text);
			}
			text.Append(" ORDER BY ");
			if (sort != null && sort.IsSorted) {	
				sort.WriteOrderBy(text);
			} else {
				List<string> columns = new List<string>();
				foreach (Column c in this.Columns) {
					if (c.InPrimaryKey) {
						columns.Add(c.QuotedName);
					}
				}
				if (columns.Count == 0) {
					columns.Add(this.Columns[0].QuotedName);
				}
				text.Append(String.Join(", ", columns.ToArray()));
			}
			text.AppendFormat(" OFFSET {0} ROWS FETCH NEXT {1} ROWS ONLY", startIndex, count); 
			return text.ToString();
		}
	}
}