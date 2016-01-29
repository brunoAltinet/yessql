﻿using Dapper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using YesSql.Core.Sql;
using YesSql.Core.Storage;

namespace YesSql.Storage.Sql
{
    public class SqlDocumentStorage : IDocumentStorage, IDisposable
    {
        private readonly SqlDocumentStorageFactory _factory;
        private readonly static JsonSerializerSettings _jsonSettings;
        private readonly DbConnection _dbConnection;
         
        static SqlDocumentStorage()
        {
            _jsonSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
        }

        public SqlDocumentStorage(SqlDocumentStorageFactory factory)
        {
            _factory = factory;
            _dbConnection = _factory.ConnectionFactory.CreateConnection();
        }

        public async Task CreateAsync(params IIdentityEntity[] documents)
        {
            await _dbConnection.OpenAsync();

            try
            {
                using (var tx = _dbConnection.BeginTransaction(_factory.IsolationLevel))
                {
                    foreach(var document in documents)
                    {
                        var content = JsonConvert.SerializeObject(document.Entity, _jsonSettings);

                        var dialect = SqlDialectFactory.For(_dbConnection);
                        var insertCmd = $"insert into [{_factory.TablePrefix}Content] ([Id], [Content]) values (@Id, @Content);";
                        await _dbConnection.ExecuteScalarAsync<int>(insertCmd, new { Id = document.Id, Content = content }, tx);
                    }

                    tx.Commit();
                }
            }
            finally
            {
                _dbConnection.Close();
            }
        }

        public async Task UpdateAsync(params IIdentityEntity[] documents)
        {
            await _dbConnection.OpenAsync();

            try
            {
                using (var tx = _dbConnection.BeginTransaction(_factory.IsolationLevel))
                {
                    foreach(var document in documents)
                    {
                        var content = JsonConvert.SerializeObject(document.Entity, _jsonSettings);

                        var dialect = SqlDialectFactory.For(_dbConnection);
                        var updateCmd = $"update [{_factory.TablePrefix}Content] set Content = @Content where Id = @Id;";
                        await _dbConnection.ExecuteScalarAsync<int>(updateCmd, new { Id = document.Id, Content = content }, tx);
                    }

                    tx.Commit();
                }
            }
            finally
            {
                _dbConnection.Close();
            }
        }
        public async Task DeleteAsync(params IIdentityEntity[] documents)
        {
            await _dbConnection.OpenAsync();

            try
            {
                using (var tx = _dbConnection.BeginTransaction(_factory.IsolationLevel))
                {
                    foreach (var documentsPage in documents.PagesOf(128))
                    {
                        var dialect = SqlDialectFactory.For(_dbConnection);
                        var deleteCmd = $"delete from [{_factory.TablePrefix}Content] where Id IN @Id;";
                        await _dbConnection.ExecuteScalarAsync<int>(deleteCmd, new { Id = documentsPage.Select(x => x.Id).ToArray() }, tx);
                    }

                    tx.Commit();
                }
            }
            finally
            {
                _dbConnection.Close();
            }
        }

        public async Task<IEnumerable<T>> GetAsync<T>(params int[] ids)
        {
            if (ids == null)
            {
                throw new ArgumentNullException("id");
            }

            var result = new List<T>();

            await _dbConnection.OpenAsync();

            try
            {
                using (var tx = _dbConnection.BeginTransaction(_factory.IsolationLevel))
                {
                    foreach (var idPages in ids.PagesOf(128))
                    {
                        var dialect = SqlDialectFactory.For(_dbConnection);
                        var selectCmd = $"select Content from [{_factory.TablePrefix}Content] where Id IN @Id;";
                        var entities = await _dbConnection.QueryAsync<string>(selectCmd, new { Id = idPages.ToArray() }, tx);

                        foreach (var entity in entities)
                        {
                            result.Add(JsonConvert.DeserializeObject<T>(entity, _jsonSettings));
                        }
                    }

                    tx.Commit();
                }
            }
            finally
            {
                _dbConnection.Close();
            }

            return result;
        }

        public Task<IEnumerable<object>> GetAsync(params IIdentityEntity[] documents)
        {
            return GetAsync<object>(documents.Select(x => x.Id).ToArray());
        }

        public void Dispose()
        {
            if(_factory.ConnectionFactory.Disposable)
            {
                _dbConnection.Dispose();
            }
        }
    }
}