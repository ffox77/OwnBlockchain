namespace Own.Blockchain.Public.Core.Tests

open System
open Xunit
open Swensen.Unquote
open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Crypto

module BlockTests =

    [<Fact>]
    let ``Blocks.createTxResultHash for Success`` () =
        let txHash = TxHash "ABC"
        let txStatus =
            {
                TxResult.Status = Success // Code 1 = A
                BlockNumber = BlockNumber 4L
            }

        // ACT
        let txResultHash = Blocks.createTxResultHash DummyHash.decode DummyHash.create (txHash, txStatus)

        // ASSERT
        test <@ txResultHash = "ABCA...........D" @>

    [<Fact>]
    let ``Blocks.createTxResultHash for TxError`` () =
        let txHash = TxHash "ABC"
        let txStatus =
            {
                TxResult.Status = TxErrorCode.NonceTooLow |> TxError |> Failure
                BlockNumber = BlockNumber 4L
            }

        // ACT
        let txResultHash = Blocks.createTxResultHash DummyHash.decode DummyHash.create (txHash, txStatus)

        // ASSERT
        test <@ txResultHash = "ABCB...........D" @>

    [<Fact>]
    let ``Blocks.createTxResultHash for TxActionError`` () =
        let txHash = TxHash "ABC"
        let txStatus =
            {
                TxResult.Status = (TxActionNumber 3s, TxErrorCode.NonceTooLow) |> TxActionError |> Failure
                BlockNumber = BlockNumber 4L
            }

        // ACT
        let txResultHash = Blocks.createTxResultHash DummyHash.decode DummyHash.create (txHash, txStatus)

        // ASSERT
        test <@ txResultHash = "ABCB...C.......D" @>

    [<Fact>]
    let ``Blocks.createChxBalanceStateHash`` () =
        let address = BlockchainAddress "ABC"
        let state = {ChxBalanceState.Amount = ChxAmount 1m; Nonce = Nonce 2L}

        // ACT
        let stateHash = Blocks.createChxBalanceStateHash DummyHash.decode DummyHash.create (address, state)

        // ASSERT
        test <@ stateHash = "ABC...A...................B" @>

    [<Fact>]
    let ``Blocks.createHoldingStateHash`` () =
        let account = AccountHash "HHH"
        let assetHash = AssetHash "II"
        let state = {HoldingState.Amount = AssetAmount 7m}

        // ACT
        let stateHash = Blocks.createHoldingStateHash DummyHash.decode DummyHash.create (account, assetHash, state)

        // ASSERT
        test <@ stateHash = "HHHII...G............" @>

    [<Fact>]
    let ``Blocks.createAccountStateHash`` () =
        let account = AccountHash "AAA"
        let controllerAddress = BlockchainAddress "CC"
        let state = {AccountState.ControllerAddress = controllerAddress}

        // ACT
        let stateHash = Blocks.createAccountStateHash DummyHash.decode DummyHash.create (account, state)

        // ASSERT
        test <@ stateHash = "AAACC" @>

    [<Fact>]
    let ``Blocks.createAssetStateHash`` () =
        let asset = AssetHash "AAA"
        let assetCode = AssetCode "XXX" |> Some // X = 88 = 8 = H
        let controllerAddress = BlockchainAddress "CC"
        let state = {AssetState.AssetCode = assetCode; ControllerAddress = controllerAddress}

        // ACT
        let stateHash = Blocks.createAssetStateHash DummyHash.decode DummyHash.create (asset, state)

        // ASSERT
        test <@ stateHash = "AAAHHHCC" @>

    [<Fact>]
    let ``Blocks.createValidatorStateHash`` () =
        let validator = BlockchainAddress "AAA"
        let networkAddress = "XXX" // X = 88 = 8 = H
        let state = {ValidatorState.NetworkAddress = networkAddress}

        // ACT
        let stateHash = Blocks.createValidatorStateHash DummyHash.decode DummyHash.create (validator, state)

        // ASSERT
        test <@ stateHash = "AAAHHH" @>

    [<Fact>]
    let ``Blocks.createValidatorSnapshotHash`` () =
        let validatorSnapshot =
            {
                ValidatorSnapshot.ValidatorAddress = BlockchainAddress "AAA"
                NetworkAddress = "XXX" // X = 88 = 8 = H
                TotalStake = ChxAmount 5m
            }

        // ACT
        let snapshotHash = Blocks.createValidatorSnapshotHash DummyHash.decode DummyHash.create validatorSnapshot

        // ASSERT
        test <@ snapshotHash = "AAAHHH...E............" @>

    [<Fact>]
    let ``Blocks.createStakeStateHash`` () =
        let stakeholder = BlockchainAddress "AAA"
        let validator = BlockchainAddress "BBB"
        let state = {StakeState.Amount = ChxAmount 5m}

        // ACT
        let stateHash = Blocks.createStakeStateHash DummyHash.decode DummyHash.create (stakeholder, validator, state)

        // ASSERT
        test <@ stateHash = "AAABBB...E............" @>

    [<Fact>]
    let ``Blocks.createBlockHash`` () =
        let blockNumber = BlockNumber 1L
        let previousBlockHash = BlockHash "B"
        let timestamp = Timestamp 3L
        let validator = BlockchainAddress "D"
        let txSetRoot = MerkleTreeRoot "E"
        let txResultSetRoot = MerkleTreeRoot "F"
        let stateRoot = MerkleTreeRoot "G"
        let configurationRoot = MerkleTreeRoot "H"

        // ACT
        let (BlockHash blockHash) =
            Blocks.createBlockHash
                DummyHash.decode
                DummyHash.create
                blockNumber
                previousBlockHash
                timestamp
                validator
                txSetRoot
                txResultSetRoot
                stateRoot
                configurationRoot

        // ASSERT
        test <@ blockHash = ".......AB.......CDEFGH" @>

    [<Fact>]
    let ``Blocks.assembleBlock`` () =
        let blockNumber = BlockNumber 1L
        let previousBlockHash = BlockHash "B"
        let configurationBlockNumber = BlockNumber 0L
        let timestamp = Timestamp 3L
        let validator = BlockchainAddress "D"

        let txSet =
            ["AAA"; "BBB"; "CCC"]
            |> List.map TxHash

        let txResult1 : TxResult = {
            Status = Success
            BlockNumber = BlockNumber 5L
        }

        let txResult2 : TxResult = {
            Status = (TxActionNumber 7s, TxErrorCode.InsufficientChxBalance) |> TxActionError |> Failure
            BlockNumber = BlockNumber 5L
        }

        let txResult3 : TxResult = {
            Status = Success
            BlockNumber = BlockNumber 5L
        }

        let txResults =
            [txResult1; txResult2; txResult3]
            |> List.zip txSet
            |> Map.ofList

        let chxBalances =
            [
                BlockchainAddress "HH", {ChxBalanceState.Amount = ChxAmount 5m; Nonce = Nonce 7L}
                BlockchainAddress "II", {ChxBalanceState.Amount = ChxAmount 6m; Nonce = Nonce 8L}
            ]
            |> Map.ofList

        let holdings =
            [
                (AccountHash "DDD", AssetHash "EEE"), {HoldingState.Amount = AssetAmount 1m}
                (AccountHash "FFF", AssetHash "GGG"), {HoldingState.Amount = AssetAmount 2m}
            ]
            |> Map.ofList

        let accounts =
            [
                AccountHash "AAAA", {AccountState.ControllerAddress = BlockchainAddress "BBBB"}
                AccountHash "CCCC", {AccountState.ControllerAddress = BlockchainAddress "DDDD"}
            ]
            |> Map.ofList

        let assets =
            [
                AssetHash "EEEE", {AssetState.AssetCode = None; ControllerAddress = BlockchainAddress "FFFF"}
                AssetHash "GGGG", {AssetState.AssetCode = None; ControllerAddress = BlockchainAddress "HHHH"}
            ]
            |> Map.ofList

        let validators =
            [
                BlockchainAddress "AAAAA", {ValidatorState.NetworkAddress = "WWW"} // W = 87 = 7 = G
                BlockchainAddress "BBBBB", {ValidatorState.NetworkAddress = "XXX"} // X = 88 = 8 = H
                BlockchainAddress "CCCCC", {ValidatorState.NetworkAddress = "YYY"} // Y = 89 = 9 = I
            ]
            |> Map.ofList

        let stakes =
            [
                (BlockchainAddress "HH", BlockchainAddress "AAAAA"), {StakeState.Amount = ChxAmount 1m}
                (BlockchainAddress "II", BlockchainAddress "BBBBB"), {StakeState.Amount = ChxAmount 2m}
            ]
            |> Map.ofList

        let processingOutput =
            {
                ProcessingOutput.TxResults = txResults
                ChxBalances = chxBalances
                Holdings = holdings
                Accounts = accounts
                Assets = assets
                Validators = validators
                Stakes = stakes
            }

        let txSetRoot = "AAABBBCCC"

        let txResultSetRoot =
            [
                "AAAA...........E" // Tx 1
                "BBBB...G.......E" // Tx 2
                "CCCA...........E" // Tx 3
            ]
            |> String.Concat

        let stateRoot =
            [
                "HH...E...................G" // CHX balance 1
                "II...F...................H" // CHX balance 2
                "DDDEEE...A............" // Holding balance 1
                "FFFGGG...B............" // Holding balance 2
                "AAAABBBB" // Account controller 1
                "CCCCDDDD" // Account controller 2
                "EEEEFFFF" // Asset controller 1
                "GGGGHHHH" // Asset controller 2
                "AAAAAGGG" // Validator 1
                "BBBBBHHH" // Validator 2
                "CCCCCIII" // Validator 3
                "HHAAAAA...A............" // Stake 1
                "IIBBBBB...B............" // Stake 2
            ]
            |> String.Concat

        let blockHash =
            [
                ".......A" // blockNumber
                "B" // previousBlockHash
                ".......C" // timestamp
                "D" // validator
                txSetRoot
                txResultSetRoot
                stateRoot
            ]
            |> String.Concat

        // ACT
        let block =
            Blocks.assembleBlock
                DummyHash.decode
                DummyHash.create
                DummyHash.merkleTree
                validator
                blockNumber
                timestamp
                previousBlockHash
                configurationBlockNumber
                txSet
                processingOutput
                None

        // ASSERT
        test <@ block.Header.Number = blockNumber @>
        test <@ block.Header.PreviousHash = previousBlockHash @>
        test <@ block.Header.Timestamp = timestamp @>
        test <@ block.Header.Validator = validator @>
        test <@ block.Header.TxSetRoot = MerkleTreeRoot txSetRoot @>
        test <@ block.Header.TxResultSetRoot = MerkleTreeRoot txResultSetRoot @>
        test <@ block.Header.StateRoot = MerkleTreeRoot stateRoot @>
        test <@ block.Header.Hash = BlockHash blockHash @>
        test <@ block.TxSet = [TxHash "AAA"; TxHash "BBB"; TxHash "CCC"] @>

    [<Fact>]
    let ``Blocks.assembleBlock and verify merkle proofs`` () =
        let wallet1 = Signing.generateWallet ()
        let wallet2 = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let blockNumber = BlockNumber 1L
        let configurationBlockNumber = BlockNumber 0L
        let timestamp = Utils.getUnixTimestamp () |> Timestamp

        let previousBlockHash =
            Signing.generateRandomBytes 64
            |> Hashing.hash
            |> BlockHash

        let txSet =
            ["Tx1"; "Tx2"; "Tx3"]
            |> List.map (Conversion.stringToBytes >> Hashing.hash >> TxHash)

        let txResult1 : TxResult = {
            Status = Success
            BlockNumber = BlockNumber 5L
        }

        let txResult2 : TxResult = {
            Status = (TxActionNumber 0s, TxErrorCode.InsufficientChxBalance) |> TxActionError |> Failure
            BlockNumber = BlockNumber 5L
        }

        let txResult3 : TxResult = {
            Status = Success
            BlockNumber = BlockNumber 5L
        }

        let txResults =
            [txResult1; txResult2; txResult3]
            |> List.zip txSet
            |> Map.ofList

        let chxBalances =
            [
                wallet1.Address, {ChxBalanceState.Amount = ChxAmount 10m; Nonce = Nonce 1L}
                wallet2.Address, {ChxBalanceState.Amount = ChxAmount 20m; Nonce = Nonce 2L}
            ]
            |> Map.ofList

        let holdings =
            [
                (AccountHash "Acc1", AssetHash "Eq1"), {HoldingState.Amount = AssetAmount 100m}
                (AccountHash "Acc2", AssetHash "Eq2"), {HoldingState.Amount = AssetAmount 200m}
            ]
            |> Map.ofList

        let accounts =
            [
                AccountHash "AAAA", {AccountState.ControllerAddress = BlockchainAddress "BBBB"}
                AccountHash "CCCC", {AccountState.ControllerAddress = BlockchainAddress "DDDD"}
            ]
            |> Map.ofList

        let assets =
            [
                AssetHash "EEEE", {AssetState.AssetCode = None; ControllerAddress = BlockchainAddress "FFFF"}
                AssetHash "GGGG", {AssetState.AssetCode = None; ControllerAddress = BlockchainAddress "HHHH"}
            ]
            |> Map.ofList

        let validators =
            [
                BlockchainAddress "AAAAA", {ValidatorState.NetworkAddress = "WWW"} // W = 87 = 7 = G
                BlockchainAddress "BBBBB", {ValidatorState.NetworkAddress = "XXX"} // X = 88 = 8 = H
                BlockchainAddress "CCCCC", {ValidatorState.NetworkAddress = "YYY"} // Y = 89 = 9 = I
            ]
            |> Map.ofList

        let stakes =
            [
                (BlockchainAddress "CC", BlockchainAddress "AAAAA"), {StakeState.Amount = ChxAmount 1m}
                (BlockchainAddress "DD", BlockchainAddress "BBBBB"), {StakeState.Amount = ChxAmount 2m}
            ]
            |> Map.ofList

        let processingOutput =
            {
                ProcessingOutput.TxResults = txResults
                ChxBalances = chxBalances
                Holdings = holdings
                Accounts = accounts
                Assets = assets
                Validators = validators
                Stakes = stakes
            }

        // ACT
        let block =
            Blocks.assembleBlock
                Hashing.decode
                Hashing.hash
                Hashing.merkleTree
                validatorWallet.Address
                blockNumber
                timestamp
                previousBlockHash
                configurationBlockNumber
                txSet
                processingOutput
                None

        // ASSERT
        test <@ block.Header.Number = blockNumber @>
        test <@ block.Header.PreviousHash = previousBlockHash @>
        test <@ block.Header.Timestamp = timestamp @>
        test <@ block.Header.Validator = validatorWallet.Address @>
        test <@ block.TxSet = txSet @>

        let txSetMerkleProofs =
            txSet
            |> List.map (fun (TxHash h) -> h)
            |> Helpers.verifyMerkleProofs block.Header.TxSetRoot

        test <@ txSetMerkleProofs = List.replicate 3 true @>

        let txResultSetMerkleProofs =
            txSet
            |> List.map (fun h -> h, txResults.[h])
            |> List.map (Blocks.createTxResultHash Hashing.decode Hashing.hash)
            |> Helpers.verifyMerkleProofs block.Header.TxResultSetRoot

        test <@ txResultSetMerkleProofs = List.replicate 3 true @>

        let stateMerkleProofs =
            [
                chxBalances
                |> Map.toList
                |> List.map (Blocks.createChxBalanceStateHash Hashing.decode Hashing.hash)

                holdings
                |> Map.toList
                |> List.map (fun ((accountHash, assetHash), state) ->
                    Blocks.createHoldingStateHash Hashing.decode Hashing.hash (accountHash, assetHash, state)
                )

                accounts
                |> Map.toList
                |> List.map (Blocks.createAccountStateHash Hashing.decode Hashing.hash)

                assets
                |> Map.toList
                |> List.map (Blocks.createAssetStateHash Hashing.decode Hashing.hash)

                validators
                |> Map.toList
                |> List.map (Blocks.createValidatorStateHash Hashing.decode Hashing.hash)

                stakes
                |> Map.toList
                |> List.map (fun ((stakeholderAddress, validatorAddress), state) ->
                    (stakeholderAddress, validatorAddress, state)
                    |> Blocks.createStakeStateHash Hashing.decode Hashing.hash
                )
            ]
            |> List.concat
            |> Helpers.verifyMerkleProofs block.Header.StateRoot

        test <@ stateMerkleProofs = List.replicate 13 true @>

    [<Theory>]
    [<InlineData ("RIGHT_PREVIOUS_BLOCK_HASH", true)>]
    [<InlineData ("WRONG_PREVIOUS_BLOCK_HASH", false)>]
    let ``Blocks.isValidSuccessorBlock`` (previousBlockHashInTestedBlock, expectedSuccess) =
        let wallet1 = Signing.generateWallet ()
        let wallet2 = Signing.generateWallet ()
        let validatorWallet = Signing.generateWallet ()
        let blockNumber = BlockNumber 1L
        let configurationBlockNumber = BlockNumber 0L
        let timestamp = Utils.getUnixTimestamp () |> Timestamp

        let previousBlockHash =
            "RIGHT_PREVIOUS_BLOCK_HASH"
            |> Conversion.stringToBytes
            |> Hashing.hash
            |> BlockHash

        let txSet =
            ["Tx1"; "Tx2"; "Tx3"]
            |> List.map (Conversion.stringToBytes >> Hashing.hash >> TxHash)

        let txResult1 : TxResult = {
            Status = Success
            BlockNumber = BlockNumber 5L
        }

        let txResult2 : TxResult = {
            Status = (TxActionNumber 0s, TxErrorCode.InsufficientChxBalance) |> TxActionError |> Failure
            BlockNumber = BlockNumber 5L
        }

        let txResult3 : TxResult = {
            Status = Success
            BlockNumber = BlockNumber 5L
        }

        let txResults =
            [txResult1; txResult2; txResult3]
            |> List.zip txSet
            |> Map.ofList

        let chxBalances =
            [
                wallet1.Address, {ChxBalanceState.Amount = ChxAmount 10m; Nonce = Nonce 1L}
                wallet2.Address, {ChxBalanceState.Amount = ChxAmount 20m; Nonce = Nonce 2L}
            ]
            |> Map.ofList

        let holdings =
            [
                (AccountHash "Acc1", AssetHash "Eq1"), {HoldingState.Amount = AssetAmount 100m}
                (AccountHash "Acc2", AssetHash "Eq2"), {HoldingState.Amount = AssetAmount 200m}
            ]
            |> Map.ofList

        let accounts =
            [
                AccountHash "AAAA", {AccountState.ControllerAddress = BlockchainAddress "BBBB"}
                AccountHash "CCCC", {AccountState.ControllerAddress = BlockchainAddress "DDDD"}
            ]
            |> Map.ofList

        let assets =
            [
                AssetHash "EEEE", {AssetState.AssetCode = None; ControllerAddress = BlockchainAddress "FFFF"}
                AssetHash "GGGG", {AssetState.AssetCode = None; ControllerAddress = BlockchainAddress "HHHH"}
            ]
            |> Map.ofList

        let validators =
            [
                BlockchainAddress "AAAAA", {ValidatorState.NetworkAddress = "WWW"} // W = 87 = 7 = G
                BlockchainAddress "BBBBB", {ValidatorState.NetworkAddress = "XXX"} // X = 88 = 8 = H
                BlockchainAddress "CCCCC", {ValidatorState.NetworkAddress = "YYY"} // Y = 89 = 9 = I
            ]
            |> Map.ofList

        let validatorSnapshots =
            [
                {
                    ValidatorSnapshot.ValidatorAddress = BlockchainAddress "AAAAA"
                    NetworkAddress = "WWW" // W = 87 = 7 = G
                    TotalStake = ChxAmount 1m
                }
                {
                    ValidatorSnapshot.ValidatorAddress = BlockchainAddress "BBBBB"
                    NetworkAddress = "XXX" // X = 88 = 8 = H
                    TotalStake = ChxAmount 2m
                }
            ]

        let stakes =
            [
                (BlockchainAddress "CC", BlockchainAddress "AAAAA"), {StakeState.Amount = ChxAmount 1m}
                (BlockchainAddress "DD", BlockchainAddress "BBBBB"), {StakeState.Amount = ChxAmount 2m}
            ]
            |> Map.ofList

        let processingOutput =
            {
                ProcessingOutput.TxResults = txResults
                ChxBalances = chxBalances
                Holdings = holdings
                Accounts = accounts
                Assets = assets
                Validators = validators
                Stakes = stakes
            }

        let assembledBlock =
            Blocks.assembleBlock
                Hashing.decode
                Hashing.hash
                Hashing.merkleTree
                validatorWallet.Address
                blockNumber
                timestamp
                previousBlockHash
                configurationBlockNumber
                txSet
                processingOutput
                None

        let testedBlock =
            Blocks.assembleBlock
                Hashing.decode
                Hashing.hash
                Hashing.merkleTree
                validatorWallet.Address
                blockNumber
                timestamp
                (previousBlockHashInTestedBlock |> Conversion.stringToBytes |> Hashing.hash |> BlockHash)
                configurationBlockNumber
                txSet
                processingOutput
                None

        // ACT
        let isValid =
            Blocks.isValidSuccessorBlock
                Hashing.decode
                Hashing.hash
                Hashing.merkleTree
                previousBlockHash
                testedBlock

        // ASSERT
        test <@ isValid = expectedSuccess @>
