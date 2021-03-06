using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using MySql.Data.MySqlClient;

namespace DataDevelop.Data.MySql
{
	internal class MySqlTable : Table
	{
		private MySqlDatabase database;
		private bool isView;

		public MySqlTable(MySqlDatabase database)
			: base(database)
		{
			this.database = database;
		}

		public MySqlConnection Connection
		{
			get { return this.database.Connection; }
		}

		public override bool IsView
		{
			get { return this.isView; }
		}

		public void SetView(bool value)
		{
			this.isView = value;
		}

		public override bool Rename(string newName)
		{
			using (var alter = this.Connection.CreateCommand()) {
				alter.CommandText = "RENAME TABLE `" + Name + "` TO `" + newName + "`";
				try {
					alter.ExecuteNonQuery();
					return true;
				} catch (MySqlException) {
					return false;
				}
			}
		}

		public override bool Delete()
		{
			using (var drop = this.Connection.CreateCommand()) {
				drop.CommandText = "DROP TABLE " + this.QuotedName;
				try {
					drop.ExecuteNonQuery();
					return true;
				} catch (MySqlException) {
					return false;
				}
			}
		}

		public override DataTable GetData(int startIndex, int count, TableFilter filter, TableSort sort)
		{
			var data = new DataTable(this.Name);

			var text = new StringBuilder();
			text.Append("SELECT ");
			filter.WriteColumnsProjection(text);
			text.Append(" FROM ");
			text.Append(this.QuotedName);

			if (filter.IsRowFiltered) {
				text.Append(" WHERE " );
				filter.WriteWhereStatement(text);
			}
			if (sort != null && sort.IsSorted) {
				text.Append(" ORDER BY ");
				sort.WriteOrderBy(text);
			}
			text.AppendFormat(" LIMIT {0}, {1}", startIndex, count);

			using (var select = this.Connection.CreateCommand()) {
				select.CommandText = text.ToString();
				using (this.Database.CreateConnectionScope()) {
					using (var adapter = (MySqlDataAdapter)this.Database.CreateAdapter(this, filter)) {
						adapter.SelectCommand = select;
						adapter.Fill(data);
					}
				}
			}
			return data;
		}

		protected override void PopulateColumns(IList<Column> columnsCollection)
		{
			using (this.Database.CreateConnectionScope()) {
				var columns = this.Connection.GetSchema("Columns", new string[] { null, this.Connection.Database, this.Name, null });
				foreach (DataRow row in columns.Rows) {
					var column = new Column(this);
					column.Name = row["COLUMN_NAME"].ToString();
					if (!IsReadOnly) {
						column.InPrimaryKey = row["COLUMN_KEY"].ToString() == "PRI";
					}
					column.ProviderType = row["COLUMN_TYPE"].ToString();
					columnsCollection.Add(column);
				}
				this.SetColumnTypes(columnsCollection);
			}
		}

		protected override void PopulateTriggers(IList<Trigger> triggersCollection)
		{
			using (this.Database.CreateConnectionScope()) {
				var triggers = this.Connection.GetSchema("Triggers", new string[] { null, this.Connection.Database, this.Name, null });
				foreach (DataRow row in triggers.Rows) {
					var trigger = new MySqlTrigger(this, row);
					triggersCollection.Add(trigger);
				}
			}
		}

		protected override void PopulateForeignKeys(IList<ForeignKey> foreignKeysCollection)
		{
			using (this.Database.CreateConnectionScope()) {
				var keys = this.Connection.GetSchema("Foreign Keys", new string[] { null, this.Connection.Database, this.Name, null });
				foreach (DataRow row in keys.Rows) {
					var name = row["CONSTRAINT_NAME"] as string;
					var key = new ForeignKey(name, this);
					key.PrimaryTable = row["REFERENCED_TABLE_NAME"] as string;
					key.ChildTable = row["TABLE_NAME"] as string;
					foreignKeysCollection.Add(key);
				}
			}
		}

		public override string GenerateCreateStatement()
		{
			using (this.Database.CreateConnectionScope()) {
				var data = Database.ExecuteTable("SHOW CREATE TABLE " + this.QuotedName);
				if (data.Rows.Count > 0 && data.Columns.Count >= 2) {
					return data.Rows[0][1] as string;
				}
				return "Error: Query returned 0 rows.";
			}
		}
	}
}
