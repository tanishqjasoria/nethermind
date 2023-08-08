// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Threading.Tasks;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Synchronization;
using Nethermind.Config;
using Nethermind.Consensus;
using Nethermind.Consensus.Comparers;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Consensus.Rewards;
using Nethermind.Consensus.Validators;
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Blockchain;
using Nethermind.Core.Timers;
using Nethermind.Db;
using Nethermind.Facade.Eth;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Merge.Plugin.BlockProduction;
using Nethermind.Merge.Plugin.Handlers;
using Nethermind.Merge.Plugin.Synchronization;
using Nethermind.Specs;
using Nethermind.Specs.Forks;
using Nethermind.State;

namespace Nethermind.Merge.Plugin.Test;

public partial class EngineModuleTests
{
    protected virtual MergeTestVerkleBlockchain CreateBaseVerkleBlockChain(IMergeConfig? mergeConfig = null,
        IPayloadPreparationService? mockedPayloadService = null, ILogManager? logManager = null) =>
        new(mergeConfig, mockedPayloadService, logManager);

    protected async Task<MergeTestVerkleBlockchain> CreateShanghaiVerkleBlockChain(IMergeConfig? mergeConfig = null,
        IPayloadPreparationService? mockedPayloadService = null)
        => await CreateVerkleBlockChain(mergeConfig, mockedPayloadService, Shanghai.Instance);


    protected async Task<MergeTestVerkleBlockchain> CreateVerkleBlockChain(IMergeConfig? mergeConfig = null,
        IPayloadPreparationService? mockedPayloadService = null, IReleaseSpec? releaseSpec = null)
        => await CreateBaseVerkleBlockChain(mergeConfig, mockedPayloadService)
            .Build(new TestSingleReleaseSpecProvider(releaseSpec ?? London.Instance));

    protected async Task<MergeTestVerkleBlockchain> CreateVerkleBlockChain(ISpecProvider specProvider,
        ILogManager? logManager = null)
        => await CreateBaseVerkleBlockChain(null, null, logManager).Build(specProvider);

    public class MergeTestVerkleBlockchain : TestVerkleBlockchain
    {
        public IMergeConfig MergeConfig { get; set; }

        public PostMergeBlockProducer? PostMergeBlockProducer { get; set; }

        public IPayloadPreparationService? PayloadPreparationService { get; set; }

        public ISealValidator? SealValidator { get; set; }

        public IBeaconPivot? BeaconPivot { get; set; }

        public BeaconSync? BeaconSync { get; set; }

        private int _blockProcessingThrottle = 0;

        public MergeTestVerkleBlockchain ThrottleBlockProcessor(int delayMs)
        {
            _blockProcessingThrottle = delayMs;
            if (BlockProcessor is TestBlockProcessorInterceptor testBlockProcessor)
            {
                testBlockProcessor.DelayMs = delayMs;
            }
            return this;
        }

        public MergeTestVerkleBlockchain(IMergeConfig? mergeConfig = null, IPayloadPreparationService? mockedPayloadPreparationService = null, ILogManager? logManager = null)
        {
            GenesisBlockBuilder = Core.Test.Builders.Build.A.Block.Genesis.Genesis.WithTimestamp(1UL);
            MergeConfig = mergeConfig ?? new MergeConfig() { TerminalTotalDifficulty = "0" };
            PayloadPreparationService = mockedPayloadPreparationService;
            LogManager = logManager ?? LogManager;
        }

        protected override Task AddBlocksOnStart() => Task.CompletedTask;

        public sealed override ILogManager LogManager { get; set; } = LimboLogs.Instance;

        public IEthSyncingInfo? EthSyncingInfo { get; protected set; }

        protected override IBlockProducer CreateTestBlockProducer(TxPoolTxSource txPoolTxSource, ISealer sealer, ITransactionComparerProvider transactionComparerProvider)
        {
            SealEngine = new MergeSealEngine(SealEngine, PoSSwitcher, SealValidator!, LogManager);
            IBlockProducer preMergeBlockProducer =
                base.CreateTestBlockProducer(txPoolTxSource, sealer, transactionComparerProvider);
            BlocksConfig blocksConfig = new() { MinGasPrice = 0 };
            TargetAdjustedGasLimitCalculator targetAdjustedGasLimitCalculator = new(SpecProvider, blocksConfig);
            ISyncConfig syncConfig = new SyncConfig();
            EthSyncingInfo = new EthSyncingInfo(BlockTree, ReceiptStorage, syncConfig, LogManager);
            PostMergeBlockProducerFactory? blockProducerFactory = new(
                SpecProvider,
                SealEngine,
                Timestamper,
                blocksConfig,
                LogManager,
                targetAdjustedGasLimitCalculator);

            BlockProducerEnvFactory blockProducerEnvFactory = new(
                DbProvider,
                BlockTree,
                ReadOnlyTrieStore,
                SpecProvider,
                BlockValidator,
                NoBlockRewards.Instance,
                ReceiptStorage,
                BlockPreprocessorStep,
                TxPool,
                transactionComparerProvider,
                blocksConfig,
                LogManager);


            BlockProducerEnv blockProducerEnv = blockProducerEnvFactory.Create();
            PostMergeBlockProducer? postMergeBlockProducer = blockProducerFactory.Create(
                blockProducerEnv, BlockProductionTrigger);
            PostMergeBlockProducer = postMergeBlockProducer;
            PayloadPreparationService ??= new PayloadPreparationService(
                postMergeBlockProducer,
                new BlockImprovementContextFactory(BlockProductionTrigger, TimeSpan.FromSeconds(MergeConfig.SecondsPerSlot)),
                TimerFactory.Default,
                LogManager,
                TimeSpan.FromSeconds(MergeConfig.SecondsPerSlot),
                50000); // by default we want to avoid cleanup payload effects in testing
            return new MergeBlockProducer(preMergeBlockProducer, postMergeBlockProducer, PoSSwitcher);
        }

        protected override IBlockProcessor CreateBlockProcessor()
        {
            BlockValidator = CreateBlockValidator();
            IBlockProcessor processor = new BlockProcessor(
                SpecProvider,
                BlockValidator,
                NoBlockRewards.Instance,
                new BlockProcessor.BlockValidationTransactionsExecutor(TxProcessor, State),
                State,
                ReceiptStorage,
                NullWitnessCollector.Instance,
                LogManager);

            return new TestBlockProcessorInterceptor(processor, _blockProcessingThrottle);
        }

        private IBlockValidator CreateBlockValidator()
        {
            IBlockCacheService blockCacheService = new BlockCacheService();
            PoSSwitcher = new PoSSwitcher(MergeConfig, SyncConfig.Default, new MemDb(), BlockTree, SpecProvider, LogManager);
            SealValidator = new MergeSealValidator(PoSSwitcher, Always.Valid);
            HeaderValidator preMergeHeaderValidator = new HeaderValidator(BlockTree, SealValidator, SpecProvider, LogManager);
            HeaderValidator = new MergeHeaderValidator(PoSSwitcher, preMergeHeaderValidator, BlockTree, SpecProvider, SealValidator, LogManager);

            return new BlockValidator(
                new TxValidator(SpecProvider.ChainId),
                HeaderValidator,
                Always.Valid,
                SpecProvider,
                LogManager);
        }

        public IManualBlockFinalizationManager BlockFinalizationManager { get; } = new ManualBlockFinalizationManager();

        public override async Task<TestVerkleBlockchain> Build(ISpecProvider? specProvider = null, UInt256? initialValues = null)
        {
            TestVerkleBlockchain chain = await base.Build(specProvider, initialValues);
            return chain;
        }

        public async Task<MergeTestVerkleBlockchain> Build(ISpecProvider? specProvider = null) =>
            (MergeTestVerkleBlockchain)await Build(specProvider, null);
    }
}
