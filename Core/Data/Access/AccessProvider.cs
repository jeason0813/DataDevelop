using System;
using System.Data.Common;
using System.Data.OleDb;

namespace DataDevelop.Data.Access
{
	internal sealed class AccessProvider : DbProvider
	{
		private static AccessProvider provider;
		
		private AccessProvider()
		{
		}

		public static AccessProvider Instance
		{
			get
			{
				if (provider == null) {
					provider = new AccessProvider();
				}
				return provider;
			}
		}

		public override string Name
		{
			get { return "Access"; }
		}

		public override Database CreateDatabase(string name, string connectionString)
		{
			return new AccessDatabase(name, connectionString);
		}

		public override DbConnectionStringBuilder CreateConnectionStringBuilder()
		{
			return new OleDbConnectionStringBuilder();
		}

		public override string CreateDatabaseFile(string fileName)
		{
			throw new NotImplementedException("Access database file cannot be created");
		}

		public override string ToString()
		{
			return "Microsoft Access";
		}
	}
}
