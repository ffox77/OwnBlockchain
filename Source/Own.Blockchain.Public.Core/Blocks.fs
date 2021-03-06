namespace Own.Blockchain.Public.Core

open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Common.Conversion
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos

module Blocks =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Assembling the block
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let createTxResultHash decodeHash createHash (TxHash txHash, txResult : TxResult) =
        let txResult = Mapping.txResultToDto txResult

        [
            decodeHash txHash
            [| txResult.Status |]
            txResult.ErrorCode |?? 0s |> int16ToBytes
            txResult.FailedActionNumber |?? 0s |> int16ToBytes
            txResult.BlockNumber |> int64ToBytes
        ]
        |> Array.concat
        |> createHash

    let createChxBalanceStateHash decodeHash createHash (BlockchainAddress address, state : ChxBalanceState) =
        let (ChxAmount amount) = state.Amount
        let (Nonce nonce) = state.Nonce

        [
            decodeHash address
            decimalToBytes amount
            int64ToBytes nonce
        ]
        |> Array.concat
        |> createHash

    let createHoldingStateHash
        decodeHash
        createHash
        (AccountHash accountHash, AssetHash assetHash, state : HoldingState)
        =

        let (AssetAmount amount) = state.Amount

        [
            decodeHash accountHash
            decodeHash assetHash
            decimalToBytes amount
        ]
        |> Array.concat
        |> createHash

    let createAccountStateHash
        decodeHash
        createHash
        (AccountHash accountHash, state : AccountState)
        =

        let addressBytes = state.ControllerAddress |> fun (BlockchainAddress a) -> decodeHash a

        [
            decodeHash accountHash
            addressBytes
        ]
        |> Array.concat
        |> createHash

    let createAssetStateHash
        decodeHash
        createHash
        (AssetHash assetHash, state : AssetState)
        =

        let addressBytes = state.ControllerAddress |> fun (BlockchainAddress a) -> decodeHash a
        let assetCodeBytes =
            match state.AssetCode with
            | Some (AssetCode code) -> code |> stringToBytes |> createHash |> decodeHash
            | None -> Array.empty

        [
            decodeHash assetHash
            assetCodeBytes
            addressBytes
        ]
        |> Array.concat
        |> createHash

    let createValidatorStateHash
        decodeHash
        createHash
        (BlockchainAddress validatorAddress, state : ValidatorState)
        =

        [
            decodeHash validatorAddress
            stringToBytes state.NetworkAddress
        ]
        |> Array.concat
        |> createHash

    let createValidatorSnapshotHash
        decodeHash
        createHash
        (validatorSnapshot : ValidatorSnapshot)
        =

        let (BlockchainAddress validatorAddress) = validatorSnapshot.ValidatorAddress
        let (ChxAmount totalStake) = validatorSnapshot.TotalStake

        [
            decodeHash validatorAddress
            stringToBytes validatorSnapshot.NetworkAddress
            decimalToBytes totalStake
        ]
        |> Array.concat
        |> createHash

    let createStakeStateHash
        decodeHash
        createHash
        (BlockchainAddress stakeholderAddress, BlockchainAddress validatorAddress, state : StakeState)
        =

        let (ChxAmount amount) = state.Amount

        [
            decodeHash stakeholderAddress
            decodeHash validatorAddress
            decimalToBytes amount
        ]
        |> Array.concat
        |> createHash

    let createBlockHash
        decodeHash
        createHash
        (BlockNumber blockNumber)
        (BlockHash previousBlockHash)
        (Timestamp timestamp)
        (BlockchainAddress validator)
        (MerkleTreeRoot txSetRoot)
        (MerkleTreeRoot txResultSetRoot)
        (MerkleTreeRoot stateRoot)
        (MerkleTreeRoot configurationRoot)
        =

        [
            blockNumber |> int64ToBytes
            previousBlockHash |> decodeHash
            timestamp |> int64ToBytes
            validator |> decodeHash
            txSetRoot |> decodeHash
            txResultSetRoot |> decodeHash
            stateRoot |> decodeHash
            configurationRoot |> decodeHash
        ]
        |> Array.concat
        |> createHash
        |> BlockHash

    let assembleBlock
        (decodeHash : string -> byte[])
        (createHash : byte[] -> string)
        (createMerkleTree : string list -> MerkleTreeRoot)
        (validator : BlockchainAddress)
        (blockNumber : BlockNumber)
        (timestamp : Timestamp)
        (previousBlockHash : BlockHash)
        (configurationBlockNumber : BlockNumber)
        (txSet : TxHash list)
        (output : ProcessingOutput)
        (blockchainConfiguration : BlockchainConfiguration option)
        : Block
        =

        if txSet.Length <> output.TxResults.Count then
            failwith "Number of elements in ProcessingOutput.TxResults and TxSet must be equal"

        let txSetRoot =
            txSet
            |> List.map (fun (TxHash hash) -> hash)
            |> createMerkleTree

        let txResultSetRoot =
            txSet
            |> List.map (fun txHash ->
                createTxResultHash
                    decodeHash
                    createHash
                    (txHash, output.TxResults.[txHash])
            )
            |> createMerkleTree

        let chxBalanceHashes =
            output.ChxBalances
            |> Map.toList
            |> List.sort // Ensure a predictable order
            |> List.map (createChxBalanceStateHash decodeHash createHash)

        let holdingHashes =
            output.Holdings
            |> Map.toList
            |> List.sort // Ensure a predictable order
            |> List.map (fun ((accountHash, assetHash), state) ->
                createHoldingStateHash decodeHash createHash (accountHash, assetHash, state)
            )

        let accountHashes =
            output.Accounts
            |> Map.toList
            |> List.sort // Ensure a predictable order
            |> List.map (createAccountStateHash decodeHash createHash)

        let assetHashes =
            output.Assets
            |> Map.toList
            |> List.sort // Ensure a predictable order
            |> List.map (createAssetStateHash decodeHash createHash)

        let validatorHashes =
            output.Validators
            |> Map.toList
            |> List.sort // Ensure a predictable order
            |> List.map (createValidatorStateHash decodeHash createHash)

        let stakeHashes =
            output.Stakes
            |> Map.toList
            |> List.sort // Ensure a predictable order
            |> List.map (fun ((stakeholderAddress, validatorAddress), state) ->
                createStakeStateHash decodeHash createHash (stakeholderAddress, validatorAddress, state)
            )

        let stateRoot =
            chxBalanceHashes
            @ holdingHashes
            @ accountHashes
            @ assetHashes
            @ validatorHashes
            @ stakeHashes
            |> createMerkleTree

        let configurationRoot =
            match blockchainConfiguration with
            | None -> []
            | Some c ->
                let validatorSnapshotHashes =
                    c.Validators
                    |> List.sortBy (fun v -> v.ValidatorAddress) // Ensure a predictable order
                    |> List.map (createValidatorSnapshotHash decodeHash createHash)
                validatorSnapshotHashes
            |> createMerkleTree

        let blockHash =
            createBlockHash
                decodeHash
                createHash
                blockNumber
                previousBlockHash
                timestamp
                validator
                txSetRoot
                txResultSetRoot
                stateRoot
                configurationRoot

        let blockHeader =
            {
                BlockHeader.Number = blockNumber
                Hash = blockHash
                PreviousHash = previousBlockHash
                ConfigurationBlockNumber = configurationBlockNumber
                Timestamp = timestamp
                Validator = validator
                TxSetRoot = txSetRoot
                TxResultSetRoot = txResultSetRoot
                StateRoot = stateRoot
                ConfigurationRoot = configurationRoot
            }

        {
            Header = blockHeader
            TxSet = txSet
            Configuration = blockchainConfiguration
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Genesis block
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let createGenesisState
        genesisChxSupply
        genesisAddress
        (genesisValidators : Map<BlockchainAddress, ValidatorState>)
        : ProcessingOutput
        =

        let genesisChxBalanceState =
            {
                ChxBalanceState.Amount = genesisChxSupply
                Nonce = Nonce 0L
            }

        let chxBalances =
            [
                genesisAddress, genesisChxBalanceState
            ]
            |> Map.ofList

        {
            TxResults = Map.empty
            ChxBalances = chxBalances
            Holdings = Map.empty
            Accounts = Map.empty
            Assets = Map.empty
            Validators = genesisValidators
            Stakes = Map.empty
        }

    let assembleGenesisBlock
        (decodeHash : string -> byte[])
        (createHash : byte[] -> string)
        (createMerkleTree : string list -> MerkleTreeRoot)
        zeroHash
        zeroAddress
        (output : ProcessingOutput)
        : Block
        =

        let blockNumber = BlockNumber 0L
        let timestamp = Timestamp 0L
        let previousBlockHash = zeroHash |> BlockHash
        let txSet = []

        let validatorSnapshots =
            output.Validators
            |> Map.toList
            |> List.map (fun (validatorAddress, state) ->
                {
                    ValidatorSnapshot.ValidatorAddress = validatorAddress
                    NetworkAddress = state.NetworkAddress
                    TotalStake = ChxAmount 0m
                }
            )

        let blockchainConfiguration =
            {
                BlockchainConfiguration.Validators = validatorSnapshots
            }
            |> Some

        assembleBlock
            decodeHash
            createHash
            createMerkleTree
            zeroAddress
            blockNumber
            timestamp
            previousBlockHash
            blockNumber
            txSet
            output
            blockchainConfiguration

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Configuration blocks
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let calculateConfigurationBlockNumberForNewBlock configurationBlockDelta (BlockNumber blockNumber) =
        let offset =
            match blockNumber % configurationBlockDelta with
            | 0L -> configurationBlockDelta
            | o -> o

        BlockNumber (blockNumber - offset)

    let isConfigurationBlock configurationBlockDelta (BlockNumber blockNumber) =
        blockNumber % configurationBlockDelta = 0L

    let createNewBlockchainConfiguration
        (getTopValidators : unit -> ValidatorSnapshot list)
        (getFallbackValidators : unit -> ValidatorSnapshot list)
        minValidatorCount
        =

        let validators =
            match getTopValidators () with
            | validators when validators.Length >= minValidatorCount -> validators
            | _ -> getFallbackValidators ()

        {
            BlockchainConfiguration.Validators = validators
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Helpers
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let extractBlockFromEnvelopeDto blockEnvelopeDto =
        blockEnvelopeDto
        |> Mapping.blockEnvelopeFromDto
        |> fun envelope -> Serialization.deserialize<BlockDto> envelope.RawBlock
        |> Result.map Mapping.blockFromDto

    /// Checks if the block is a valid potential successor of a previous block identified by previousBlockHash argument.
    let isValidSuccessorBlock
        decodeHash
        createHash
        createMerkleTree
        previousBlockHash
        (block : Block)
        : bool
        =

        let txSetRoot =
            block.TxSet
            |> List.map (fun (TxHash hash) -> hash)
            |> createMerkleTree

        let blockHash =
            createBlockHash
                decodeHash
                createHash
                block.Header.Number
                previousBlockHash
                block.Header.Timestamp
                block.Header.Validator
                txSetRoot
                block.Header.TxResultSetRoot
                block.Header.StateRoot
                block.Header.ConfigurationRoot

        block.Header.Hash = blockHash
