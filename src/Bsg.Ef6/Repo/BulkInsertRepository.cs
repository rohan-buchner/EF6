﻿namespace Bsg.Ef6.Repo
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Text;
    using Context;
    using Domain;
    using Mapping;
    using Timeout;
    using Utils;

    public abstract class BulkInsertRepository<TEntity, TContext> : BulkEnabledRepository<TEntity, TContext>, IBulkInsertRepository<TEntity, TContext>
        where TEntity : class, IEntity<TContext>, new()
        where TContext : IDbContext
    {
        private readonly IBulkInserterFactory bulkInserterFactory;

        private readonly IDbContextSession<TContext> session;

        private readonly ITimeoutCacheService timeoutCacheService;

        #region Constructor

        protected BulkInsertRepository(
            IDbContextSession<TContext> session,
            ITimeoutCacheService timeoutCacheService,
            IBulkInserterFactory bulkInserterFactory,
            ITableMappingsCacheService tableMappingsCacheService)
            : base(session, timeoutCacheService, tableMappingsCacheService)
        {
            this.timeoutCacheService = timeoutCacheService;
            this.bulkInserterFactory = bulkInserterFactory;
            this.session = session;
        }

        #endregion

        #region Interface Methods

        public int BulkAdd(TEntity item)
        {
            return this.BulkAdd(new[] { item });
        }

        public int BulkAdd(IEnumerable<TEntity> items)
        {
            return this.BulkAdd(items, 5000);
        }

        public int BulkAdd(IEnumerable<TEntity> items, int bufferSize)
        {
            return this.ExecuteAdd(items, bufferSize, null);
        }

        public int BulkAdd(TEntity item, IContextTransaction contextTransaction)
        {
            return this.BulkAdd(new[] { item }, contextTransaction);
        }

        public int BulkAdd(IEnumerable<TEntity> items, IContextTransaction contextTransaction)
        {
            return this.BulkAdd(items, 5000, contextTransaction);
        }

        public int BulkAdd(IEnumerable<TEntity> items, int bufferSize, IContextTransaction contextTransaction)
        {
            return this.ExecuteAdd(items, bufferSize, contextTransaction);
        }

        public IBulkInserter<TEntity> BuildInserter(int bufferSize, IContextTransaction contextTransaction)
        {
            return this.bulkInserterFactory.BuildInserter(
                this.GetMapping(),
                contextTransaction,
                bufferSize,
                this.timeoutCacheService.BulkInsertTimeout<TContext>(),
                true);
        }

        public void BulkSelectAndAdd(IQueryable query)
        {
            this.ExecuteSelectAndAdd(query, null);
        }

        public void BulkSelectAndAdd(IQueryable query, IContextTransaction contextTransaction)
        {
            this.ExecuteSelectAndAdd(query, contextTransaction);
        }

        private void ExecuteSelectAndAdd(IQueryable query, IContextTransaction contextTransaction)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            var mappings = this.GetMapping();

            var selectBindings = this.ExtractSelectBindings(query);
            var selectQuery = this.BuildSelect(query);
            var selectParameters = selectQuery.Item2;
            var tableName = mappings.FullyQualifiedTableName;
            var orderedColumns = this.MapPropertiesToColumns(selectBindings, mappings);
            var requiredSelectLines = this.FixAdditionalSelectedColumn(selectQuery.Item1, selectBindings.Count);
            var actualSelectQuery = string.Join(Environment.NewLine, requiredSelectLines);

            var insertQuery = $"INSERT INTO {tableName} ({orderedColumns}) {actualSelectQuery}";

            this.Execute(insertQuery, selectParameters, contextTransaction);
        }

        private int ExecuteAdd(IEnumerable<TEntity> items, int bufferSize, IContextTransaction contextTransaction)
        {
            return (contextTransaction == null)
                ? this.ExecuteAddWithLocalTransaction(items, bufferSize)
                : this.ExceuteAddWithExternalTransaction(items, bufferSize, contextTransaction);
        }

        private int ExecuteAddWithLocalTransaction(IEnumerable<TEntity> items, int bufferSize)
        {
            using (var contextTransaction = this.session.StartNewTransaction())
            {
                try
                {
                    var result = this.ExceuteAddWithExternalTransaction(items, bufferSize, contextTransaction);
                    contextTransaction.Commit();
                    return result;
                }
                catch (Exception)
                {
                    contextTransaction?.Rollback();
                    throw;
                }
            }
        }

        private int ExceuteAddWithExternalTransaction(IEnumerable<TEntity> items, int bufferSize, IContextTransaction contextTransaction)
        {
            using (
                var inserter = this.bulkInserterFactory.BuildInserter(
                    this.GetMapping(),
                    contextTransaction,
                    bufferSize,
                    this.timeoutCacheService.BulkInsertTimeout<TContext>(),
                    true))
            {
                inserter.Insert(items);
                return inserter.InsertedCount();
            }
        }

        private string MapPropertiesToColumns(
            IReadOnlyList<MemberBinding> selectBindings,
            TableMapping<TEntity, TContext> mappings)
        {
            var columnNames = new StringBuilder();

            for (var i = 0; i < selectBindings.Count; i++)
            {
                var binding = selectBindings[i];
                var propertyName = binding.Member.Name;
                var columnName = mappings.ColumnMappings[propertyName];

                columnNames.Append($"[{columnName}]");

                if (i != selectBindings.Count - 1)
                {
                    columnNames.AppendLine(", ");
                }
            }

            return columnNames.ToString();
        }

        #endregion
    }
}
