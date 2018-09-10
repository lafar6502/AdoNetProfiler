using System;
#if !NETSTANDARD1_6
using System.ComponentModel;
#endif
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Configuration;
using System.Threading.Tasks;

namespace AdoNetProfiler
{
    /// <summary>
    /// The database connection wrapped <see cref="DbConnection"/>.
    /// </summary>
#if !NETSTANDARD1_6
    [DesignerCategory("")]
#endif
    public class AdoNetProfilerDbConnection : DbConnection
    {
        public int CreatedByThreadId = Thread.CurrentThread.ManagedThreadId;
        public DateTime CreatedDate = DateTime.Now;
        public int TotalQueries = 0;
        private bool _disposed = false;

        /// <inheritdoc cref="DbConnection.ConnectionString" />
        public override string ConnectionString
        {
            get
            {
                EnsureNotDisposed();
                return WrappedConnection.ConnectionString;
            }
            set
            {
                EnsureNotDisposed();
                WrappedConnection.ConnectionString = value;
            }
        }

        internal static bool ThrowOnErrors
        {
            get
            {
                var v = ConfigurationManager.AppSettings["AdoNetProfiler.ThrowOnError"];
                return "1".Equals(v) || "true".Equals(v, StringComparison.InvariantCultureIgnoreCase);
            }
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                Profiler.OnError("Connection already disposed");
                var v = ConfigurationManager.AppSettings["AdoNetProfiler.ThrowOnError"];
                if (ThrowOnErrors)
                {
                    throw new Exception("Connection already disposed");
                }
            }
        }

        /// <inheritdoc cref="DbConnection.ConnectionTimeout" />
        public override int ConnectionTimeout => WrappedConnection.ConnectionTimeout;

        /// <inheritdoc cref="DbConnection.Database" />
        public override string Database => WrappedConnection.Database;

        /// <inheritdoc cref="DbConnection.DataSource" />
        public override string DataSource => WrappedConnection.DataSource;

        /// <inheritdoc cref="DbConnection.ServerVersion" />
        public override string ServerVersion => WrappedConnection.ServerVersion;

        /// <inheritdoc cref="DbConnection.State" />
        public override ConnectionState State => WrappedConnection.State;

        /// <summary>
        /// The original <see cref="DbConnection"/>.
        /// </summary>
        public DbConnection WrappedConnection { get; private set; }
        
        /// <summary>
        /// The instance of <see cref="IAdoNetProfiler"/> used internally.
        /// </summary>
        public IAdoNetProfiler Profiler { get; private set; }

        public AdoNetProfilerDbConnection(string cs) 
        {
            var c0 = ConfigurationManager.ConnectionStrings[cs];
            var fact = c0 == null ? "System.Data.SqlClient" : c0.ProviderName ?? "System.Data.SqlClient";
            var dbf = DbProviderFactories.GetFactory(fact);
            var cn = dbf.CreateConnection();
            cn.ConnectionString = c0 == null ? cs : c0.ConnectionString;
            WrappedConnection = cn;
            Profiler = AdoNetProfilerFactory.GetProfiler();
            WrappedConnection.StateChange += StateChangeHandler;
        }

#if !COREFX        
        /// <summary>
        /// Create a new instance of <see cref="AdoNetProfilerDbConnection" /> with recieving the instance of original <see cref="DbConnection" />.
        /// </summary>
        /// <param name="connection">The instance of original <see cref="DbConnection" />.</param>
        public AdoNetProfilerDbConnection(DbConnection connection)
            : this(connection, AdoNetProfilerFactory.GetProfiler()) { }
#endif

        /// <summary>
        /// Create a new instance of <see cref="AdoNetProfilerDbConnection"/> with recieving the instance of original <see cref="DbConnection" /> and the instance of <see cref="IAdoNetProfiler"/>.
        /// </summary>
        /// <param name="connection">The instance of original <see cref="DbConnection" />.</param>
        /// <param name="profiler">The instance of original <see cref="IAdoNetProfiler" />.</param>
        public AdoNetProfilerDbConnection(DbConnection connection, IAdoNetProfiler profiler)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            WrappedConnection = connection;
            Profiler          = profiler;

            WrappedConnection.StateChange += StateChangeHandler;
        }

        /// <inheritdoc cref="DbConnection.ChangeDatabase(string)" />
        public override void ChangeDatabase(string databaseName)
        {
            EnsureNotDisposed();
            WrappedConnection.ChangeDatabase(databaseName);
        }


        public override void EnlistTransaction(System.Transactions.Transaction t)
        {
            EnsureNotDisposed();
            WrappedConnection.EnlistTransaction(t);
        }

        

        /// <inheritdoc cref="DbConnection.Close()" />
        public override void Close()
        {
            EnsureNotDisposed();
            if (Profiler == null || !Profiler.IsEnabled)
            {
                WrappedConnection.Close();

                return;
            }

            Profiler.OnClosing(this);

            WrappedConnection.Close();

            Profiler.OnClosed(this);
        }

#if !NETSTANDARD1_6
        /// <inheritdoc cref="DbConnection.GetSchema()" />
        public override DataTable GetSchema()
        {
            EnsureNotDisposed();
            return WrappedConnection.GetSchema();
        }
        
        /// <inheritdoc cref="DbConnection.GetSchema(string)" />
        public override DataTable GetSchema(string collectionName)
        {
            EnsureNotDisposed();
            return WrappedConnection.GetSchema(collectionName);
        }
        
        /// <inheritdoc cref="DbConnection.GetSchema(string, string[])" />
        public override DataTable GetSchema(string collectionName, string[] restrictionValues)
        {
            EnsureNotDisposed();
            return WrappedConnection.GetSchema(collectionName, restrictionValues);
        }
#endif

        /// <inheritdoc cref="DbConnection.Open()" />
        public override void Open()
        {
            EnsureNotDisposed();
            if (Profiler == null || !Profiler.IsEnabled)
            {
                WrappedConnection.Open();

                return;
            }

            Profiler.OnOpening(this);

            WrappedConnection.Open();

            Profiler.OnOpened(this);
        }

        /// <inheritdoc cref="DbConnection.BeginDbTransaction(IsolationLevel)" />
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            EnsureNotDisposed();
            if (Profiler == null || !Profiler.IsEnabled)
            {
                return WrappedConnection.BeginTransaction(isolationLevel);
            }

            Profiler.OnStartingTransaction(this);

            var transaction = WrappedConnection.BeginTransaction(isolationLevel);

            Profiler.OnStartedTransaction(transaction);

            return new AdoNetProfilerDbTransaction(transaction, WrappedConnection, Profiler);
        }

        /// <inheritdoc cref="DbConnection.CreateDbCommand()" />
        protected override DbCommand CreateDbCommand()
        {
            EnsureNotDisposed();
            return new AdoNetProfilerDbCommand(WrappedConnection.CreateCommand(), this, Profiler);
        }

        /// <summary>
        /// Free, release, or reset managed or unmanaged resources.
        /// </summary>
        /// <param name="disposing">Wether to free, release, or resetting unmanaged resources or not.</param>
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                Profiler.OnError("Connection already disposed!");
            }

            if (disposing && WrappedConnection != null)
            {
                if (State != ConnectionState.Closed)
                {
                    Close();
                }

                WrappedConnection.StateChange -= StateChangeHandler;
                WrappedConnection.Dispose();
            }
            _disposed = true;
            WrappedConnection = null;
            Profiler          = null;

            // corefx calls Close() in Dispose() without checking ConnectionState.
#if !NETSTANDARD1_6
            base.Dispose(disposing);
#endif
        }

        private void StateChangeHandler(object sender, StateChangeEventArgs a)
        {
            
            OnStateChange(a);
        }
    }
}
