using System;
using System.Data;
using System.Data.Common;
using System.Data.OleDb;

namespace DataDevelop.Data.OleDb
{
	internal sealed class OleDbDatabase : Database, IDisposable
	{
		private string name;
		private OleDbConnection connection;

		public OleDbDatabase(string name, string connectionString)
		{
			this.name = name;
			this.connection = new OleDbConnection(connectionString);
		}

		public OleDbConnection Connection
		{
			get { return this.connection; }
		}

		public override string Name
		{
			get { return this.name; }
		}

		public override DbProvider Provider
		{
			get { return OleDbProvider.Instance; }
		}

		public override string ConnectionString
		{
			get { return this.connection.ConnectionString; }
		}

		public override DbDataAdapter CreateAdapter(Table table, TableFilter filter)
		{
			var oleDbTable = (OleDbTable)table;
			var adapter = new OleDbDataAdapter(table.GetBaseSelectCommandText(filter), this.connection);
			if (!table.IsReadOnly) {
				var builder = new OleDbCommandBuilder(adapter);
				builder.ConflictOption = ConflictOption.OverwriteChanges;
				try {
					adapter.UpdateCommand = builder.GetUpdateCommand();
					adapter.InsertCommand = builder.GetInsertCommand();
					adapter.DeleteCommand = builder.GetDeleteCommand();
				} catch (InvalidOperationException) {
					oleDbTable.SetReadOnly(true);
					return adapter;
				}
			}
			return adapter;
		}

		public override DbCommand CreateCommand()
		{
			return this.Connection.CreateCommand();
		}

		public override DbTransaction BeginTransaction()
		{
			return this.Connection.BeginTransaction();
		}

		public override int ExecuteNonQuery(string commandText)
		{
			using (var command = this.connection.CreateCommand()) {
				command.CommandText = commandText;
				return command.ExecuteNonQuery();
			}
		}

		public override DataTable ExecuteTable(string commandText)
		{
			using (var command = this.connection.CreateCommand()) {
				command.CommandText = commandText;
				var table = new DataTable();
				using (var reader = command.ExecuteReader()) {
					table.Load(reader);
				}
				return table;
			}
		}

		public override int ExecuteNonQuery(string commandText, DbTransaction transaction)
		{
			if ((object)transaction.Connection != (object)this.connection) {
				throw new InvalidOperationException();
			}
			using (var command = this.connection.CreateCommand()) {
				command.Transaction = (OleDbTransaction)transaction;
				command.CommandText = commandText;
				return command.ExecuteNonQuery();
			}
		}

		#region IDisposable Members

		public void Dispose()
		{
			if (this.connection != null) {
				this.connection.Dispose();
			}
			GC.SuppressFinalize(this);
		}

		#endregion

		public override void ChangeConnectionString(string newConnectionString)
		{
			if (this.IsConnected) {
				throw new InvalidOperationException("Database must be disconnected in order to change the ConnectionString");
			} else {
				this.connection.ConnectionString = newConnectionString;
			}
		}

		protected override void DoConnect()
		{
			this.connection.Open();
		}

		protected override void DoDisconnect()
		{
			this.connection.Close();
		}

		protected override void PopulateTables(DbObjectCollection<Table> tablesCollection)
		{
			var restrictions = new string[4];
			restrictions[3] = "Table";

			var tables = this.Connection.GetSchema("Tables", restrictions);
			foreach (DataRow row in tables.Rows) {
				var table = new OleDbTable(this);
				table.Schema = row["TABLE_SCHEMA"] as string;
				table.Name = row["TABLE_NAME"] as string;
				tablesCollection.Add(table);
			}

			restrictions[3] = "View";
			var views = this.Connection.GetSchema("Tables", restrictions);
			foreach (DataRow row in views.Rows) {
				var table = new OleDbTable(this);
				table.Name = row["TABLE_NAME"].ToString();
				table.SetView(true);
				tablesCollection.Add(table);
			}
		}

		protected override void PopulateStoredProcedures(DbObjectCollection<StoredProcedure> storedProceduresCollection)
		{
			throw new NotSupportedException();
		}
	}
}
