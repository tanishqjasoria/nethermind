//  Copyright (c) 2018 Demerzel Solutions Limited	
//  This file is part of the Nethermind library.	
// 	
//  The Nethermind library is free software: you can redistribute it and/or modify	
//  it under the terms of the GNU Lesser General Public License as published by	
//  the Free Software Foundation, either version 3 of the License, or	
//  (at your option) any later version.	
// 	
//  The Nethermind library is distributed in the hope that it will be useful,	
//  but WITHOUT ANY WARRANTY; without even the implied warranty of	
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the	
//  GNU Lesser General Public License for more details.	
// 	
//  You should have received a copy of the GNU Lesser General Public License	
//  along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.	

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethermind.Blockchain.Synchronization;
using Nethermind.DataMarketplace.Consumers.Infrastructure.Persistence.Rocks;
using Nethermind.DataMarketplace.Core.Configs;
using Nethermind.Db;
using Nethermind.Db.Rocks;
using Nethermind.Db.Rocks.Config;
using Nethermind.Logging;
using Nethermind.Synchronization.BeamSync;
using Nethermind.Synchronization.ParallelSync;
using NSubstitute;
using NUnit.Framework;

namespace Nethermind.DataMarketplace.Consumers.Test.Infrastructure.Persistence.Rocks
{
    [TestFixture]
    [Parallelizable(ParallelScope.Default)]
    public class ConsumerNdmDbInitializerTests
    {
        private string _folderWithDbs;

        [OneTimeSetUp]
        public void Initialize()
        {
            _folderWithDbs = Guid.NewGuid().ToString();
        }

        [Test]
        public async Task ProviderInitTests_MemDbProvider()
        {
            var dbProvider = new DbProvider(DbModeHint.Mem);
            var rocksDbFactory = new RocksDbFactory(new DbConfig(), LimboLogs.Instance, Path.Combine(_folderWithDbs, "mem"));
            var initializer = new ConsumerNdmDbInitializer(dbProvider, new NdmConfig(), rocksDbFactory, new MemDbFactory());
            await initializer.Init();
            Assert.AreEqual(4, dbProvider.RegisteredDbs.Count());
            Assert.IsTrue(dbProvider.GetDb<IDb>(ConsumerNdmDbConsts.ConsumerDepositApprovalsDbName) is MemDb);
            Assert.IsTrue(dbProvider.GetDb<IDb>(ConsumerNdmDbConsts.ConsumerReceiptsDbName) is MemDb);
            Assert.IsTrue(dbProvider.GetDb<IDb>(ConsumerNdmDbConsts.ConsumerSessionsDbName) is MemDb);
            Assert.IsTrue(dbProvider.GetDb<IDb>(ConsumerNdmDbConsts.DepositsDbName) is MemDb);
        }

        [Test]
        public async Task ProviderInitTests_RocksDbProvider()
        {
            var rocksDbFactory = new RocksDbFactory(new DbConfig(), LimboLogs.Instance, Path.Combine(_folderWithDbs, "rocks"));
            var dbProvider = new DbProvider(DbModeHint.Persisted);
            var initializer = new ConsumerNdmDbInitializer(dbProvider, new NdmConfig(), rocksDbFactory, new MemDbFactory());
            await initializer.Init();
            Assert.AreEqual(4, dbProvider.RegisteredDbs.Count());
            Assert.IsTrue(dbProvider.GetDb<IDb>(ConsumerNdmDbConsts.ConsumerDepositApprovalsDbName) is DbOnTheRocks);
            Assert.IsTrue(dbProvider.GetDb<IDb>(ConsumerNdmDbConsts.ConsumerReceiptsDbName) is DbOnTheRocks);
            Assert.IsTrue(dbProvider.GetDb<IDb>(ConsumerNdmDbConsts.ConsumerSessionsDbName) is DbOnTheRocks);
            Assert.IsTrue(dbProvider.GetDb<IDb>(ConsumerNdmDbConsts.DepositsDbName) is DbOnTheRocks);
        }

        [Test]
        public async Task ProviderInitTests_ReadonlyDbProvider()
        {
            var dbProvider = new DbProvider(DbModeHint.Persisted);
            var rocksDbFactory = new RocksDbFactory(new DbConfig(), LimboLogs.Instance, Path.Combine(_folderWithDbs, "readonly"));
            var readonlyDbProvider = new ReadOnlyDbProvider(dbProvider, true);
            var initializer = new ConsumerNdmDbInitializer(readonlyDbProvider, new NdmConfig(), rocksDbFactory, new MemDbFactory());
            await initializer.Init();
            Assert.AreEqual(4, readonlyDbProvider.RegisteredDbs.Count());
            Assert.IsTrue(readonlyDbProvider.GetDb<IDb>(ConsumerNdmDbConsts.ConsumerDepositApprovalsDbName) is ReadOnlyDb);
            Assert.IsTrue(readonlyDbProvider.GetDb<IDb>(ConsumerNdmDbConsts.ConsumerReceiptsDbName) is ReadOnlyDb);
            Assert.IsTrue(readonlyDbProvider.GetDb<IDb>(ConsumerNdmDbConsts.ConsumerSessionsDbName) is ReadOnlyDb);
            Assert.IsTrue(readonlyDbProvider.GetDb<IDb>(ConsumerNdmDbConsts.DepositsDbName) is ReadOnlyDb);
        }

        [Test]
        public async Task ProviderInitTests_BeamSyncDbProvider()
        {
            var syncModeSelector = Substitute.For<ISyncModeSelector>();
            var dbProvider = TestMemDbProvider.Init();
            var rocksDbFactory = new RocksDbFactory(new DbConfig(), LimboLogs.Instance, Path.Combine(_folderWithDbs, "beam"));
            IDbProvider beamSyncDbProvider = new BeamSyncDbProvider(syncModeSelector, dbProvider, new SyncConfig(), LimboLogs.Instance);
            var initializer = new ConsumerNdmDbInitializer(beamSyncDbProvider, new NdmConfig(), rocksDbFactory, new MemDbFactory());
            await initializer.Init();
            Assert.IsTrue(beamSyncDbProvider.GetDb<IDb>(ConsumerNdmDbConsts.ConsumerDepositApprovalsDbName) is MemDb);
            Assert.IsTrue(beamSyncDbProvider.GetDb<IDb>(ConsumerNdmDbConsts.ConsumerReceiptsDbName) is MemDb);
            Assert.IsTrue(beamSyncDbProvider.GetDb<IDb>(ConsumerNdmDbConsts.ConsumerSessionsDbName) is MemDb);
            Assert.IsTrue(beamSyncDbProvider.GetDb<IDb>(ConsumerNdmDbConsts.DepositsDbName) is MemDb);
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folderWithDbs))
                Directory.Delete(_folderWithDbs);
        }
    }
}
