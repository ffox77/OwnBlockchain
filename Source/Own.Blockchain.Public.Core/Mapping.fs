namespace Own.Blockchain.Public.Core

open System
open Own.Common
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos
open Own.Blockchain.Public.Core.Events

module Mapping =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Tx
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let txStatusNumberToString (txStatusNumber : byte) =
        match txStatusNumber with
        | 0uy -> "Pending"
        | 1uy -> "Success"
        | 2uy -> "Failure"
        | s -> failwithf "Unknown tx status: %i" s

    let txErrorCodeNumberToString (txErrorCodeNumber : Nullable<int16>) : string =
        if not txErrorCodeNumber.HasValue then
            None
        elif Enum.IsDefined(typeof<TxErrorCode>, txErrorCodeNumber.Value) then
            let txErrorCode : TxErrorCode = LanguagePrimitives.EnumOfValue txErrorCodeNumber.Value
            txErrorCode.ToString() |> Some
        else
            failwithf "Unknown tx error code: %s" (txErrorCodeNumber.ToString())
        |> Option.toObj

    let txEnvelopeFromDto (dto : TxEnvelopeDto) : TxEnvelope =
        {
            RawTx = dto.Tx |> Convert.FromBase64String
            Signature = Signature dto.Signature
        }

    let txActionFromDto (action : TxActionDto) =
        match action.ActionData with
        | :? TransferChxTxActionDto as a ->
            {
                TransferChxTxAction.RecipientAddress = BlockchainAddress a.RecipientAddress
                Amount = ChxAmount a.Amount
            }
            |> TransferChx
        | :? TransferAssetTxActionDto as a ->
            {
                FromAccountHash = AccountHash a.FromAccount
                ToAccountHash = AccountHash a.ToAccount
                AssetHash = AssetHash a.AssetHash
                Amount = AssetAmount a.Amount
            }
            |> TransferAsset
        | :? CreateAssetEmissionTxActionDto as a ->
            {
                CreateAssetEmissionTxAction.EmissionAccountHash = AccountHash a.EmissionAccountHash
                AssetHash = AssetHash a.AssetHash
                Amount = AssetAmount a.Amount
            }
            |> CreateAssetEmission
        | :? CreateAccountTxActionDto ->
            CreateAccount
        | :? CreateAssetTxActionDto ->
            CreateAsset
        | :? SetAccountControllerTxActionDto as a ->
            {
                SetAccountControllerTxAction.AccountHash = AccountHash a.AccountHash
                ControllerAddress = BlockchainAddress a.ControllerAddress
            }
            |> SetAccountController
        | :? SetAssetControllerTxActionDto as a ->
            {
                SetAssetControllerTxAction.AssetHash = AssetHash a.AssetHash
                ControllerAddress = BlockchainAddress a.ControllerAddress
            }
            |> SetAssetController
        | :? SetAssetCodeTxActionDto as a ->
            {
                SetAssetCodeTxAction.AssetHash = AssetHash a.AssetHash
                AssetCode = AssetCode a.AssetCode
            }
            |> SetAssetCode
        | :? SetValidatorNetworkAddressTxActionDto as a ->
            {
                SetValidatorNetworkAddressTxAction.NetworkAddress = a.NetworkAddress
            }
            |> SetValidatorNetworkAddress
        | :? DelegateStakeTxActionDto as a ->
            {
                DelegateStakeTxAction.ValidatorAddress = BlockchainAddress a.ValidatorAddress
                Amount = ChxAmount a.Amount
            }
            |> DelegateStake
        | _ ->
            failwith "Invalid action type to map."

    let txFromDto sender hash (dto : TxDto) : Tx =
        {
            TxHash = hash
            Sender = sender
            Nonce = Nonce dto.Nonce
            Fee = ChxAmount dto.Fee
            Actions = dto.Actions |> List.map txActionFromDto
        }

    let txToTxInfoDto (tx : Tx) : TxInfoDto =
        {
            TxHash = tx.TxHash |> (fun (TxHash h) -> h)
            SenderAddress = tx.Sender |> (fun (BlockchainAddress a) -> a)
            Nonce = tx.Nonce |> (fun (Nonce n) -> n)
            Fee = tx.Fee |> (fun (ChxAmount a) -> a)
            ActionCount = Convert.ToInt16 tx.Actions.Length
        }

    let pendingTxInfoFromDto (dto : PendingTxInfoDto) : PendingTxInfo =
        {
            TxHash = TxHash dto.TxHash
            Sender = BlockchainAddress dto.SenderAddress
            Nonce = Nonce dto.Nonce
            Fee = ChxAmount dto.Fee
            ActionCount = dto.ActionCount
            AppearanceOrder = dto.AppearanceOrder
        }

    let txResultToDto (txResult : TxResult) =
        let status, errorCode, failedActionNumber =
            match txResult.Status with
            | Success -> 1uy, Nullable (), Nullable ()
            | Failure txError ->
                let statusNumber = 2uy
                match txError with
                | TxError errorCode ->
                    let errorNumber = errorCode |> LanguagePrimitives.EnumToValue
                    statusNumber, Nullable errorNumber, Nullable ()
                | TxActionError (TxActionNumber actionNumber, errorCode) ->
                    let errorNumber = errorCode |> LanguagePrimitives.EnumToValue
                    statusNumber, Nullable errorNumber, Nullable actionNumber

        {
            Status = status
            ErrorCode = errorCode
            FailedActionNumber = failedActionNumber
            BlockNumber = txResult.BlockNumber |> (fun (BlockNumber b) -> b)
        }

    let txResultFromDto (dto : TxResultDto) : TxResult =
        {
            Status =
                match dto.Status with
                | 1uy -> Success
                | 2uy ->
                    match dto.ErrorCode.HasValue, dto.FailedActionNumber.HasValue with
                    | true, false ->
                        let (errorCode : TxErrorCode) = dto.ErrorCode.Value |> LanguagePrimitives.EnumOfValue
                        TxError errorCode
                    | true, true ->
                        let (errorCode : TxErrorCode) = dto.ErrorCode.Value |> LanguagePrimitives.EnumOfValue
                        TxActionError (TxActionNumber dto.FailedActionNumber.Value, errorCode)
                    | _, _ -> failwith "Invalid error code and action number state in TxResult."
                    |> Failure
                | c -> failwithf "Unknown TxStatus code %i" c
            BlockNumber = BlockNumber dto.BlockNumber
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Block
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let blockHeaderFromDto (dto : BlockHeaderDto) : BlockHeader =
        {
            BlockHeader.Number = BlockNumber dto.Number
            Hash = BlockHash dto.Hash
            PreviousHash = BlockHash dto.PreviousHash
            ConfigurationBlockNumber = BlockNumber dto.ConfigurationBlockNumber
            Timestamp = Timestamp dto.Timestamp
            Validator = BlockchainAddress dto.Validator
            TxSetRoot = MerkleTreeRoot dto.TxSetRoot
            TxResultSetRoot = MerkleTreeRoot dto.TxResultSetRoot
            StateRoot = MerkleTreeRoot dto.StateRoot
            ConfigurationRoot = MerkleTreeRoot dto.ConfigurationRoot
        }

    let blockHeaderToDto (block : BlockHeader) : BlockHeaderDto =
        {
            BlockHeaderDto.Number = block.Number |> fun (BlockNumber n) -> n
            Hash = block.Hash |> fun (BlockHash h) -> h
            PreviousHash = block.PreviousHash |> fun (BlockHash h) -> h
            ConfigurationBlockNumber = block.ConfigurationBlockNumber |> fun (BlockNumber n) -> n
            Timestamp = block.Timestamp |> fun (Timestamp t) -> t
            Validator = block.Validator |> fun (BlockchainAddress a) -> a
            TxSetRoot = block.TxSetRoot |> fun (MerkleTreeRoot r) -> r
            TxResultSetRoot = block.TxResultSetRoot |> fun (MerkleTreeRoot r) -> r
            StateRoot = block.StateRoot |> fun (MerkleTreeRoot r) -> r
            ConfigurationRoot = block.ConfigurationRoot |> fun (MerkleTreeRoot r) -> r
        }

    let validatorSnapshotFromDto (dto : ValidatorSnapshotDto) : ValidatorSnapshot =
        {
            ValidatorAddress = BlockchainAddress dto.ValidatorAddress
            NetworkAddress = dto.NetworkAddress
            TotalStake = ChxAmount dto.TotalStake
        }

    let validatorSnapshotToDto (snapshot : ValidatorSnapshot) : ValidatorSnapshotDto =
        {
            ValidatorAddress = snapshot.ValidatorAddress |> fun (BlockchainAddress a) -> a
            NetworkAddress = snapshot.NetworkAddress
            TotalStake = snapshot.TotalStake |> fun (ChxAmount a) -> a
        }

    let blockchainConfigurationFromDto (dto : BlockchainConfigurationDto) : BlockchainConfiguration =
        {
            BlockchainConfiguration.Validators = dto.Validators |> List.map validatorSnapshotFromDto
        }

    let blockchainConfigurationToDto (config : BlockchainConfiguration) : BlockchainConfigurationDto =
        {
            BlockchainConfigurationDto.Validators = config.Validators |> List.map validatorSnapshotToDto
        }

    let blockFromDto (dto : BlockDto) : Block =
        let config =
            if isNull (box dto.Configuration) then
                None
            else
                dto.Configuration |> blockchainConfigurationFromDto |> Some

        {
            Header = blockHeaderFromDto dto.Header
            TxSet = dto.TxSet |> List.map TxHash
            Configuration = config
        }

    let blockToDto (block : Block) : BlockDto =
        {
            Header = blockHeaderToDto block.Header
            TxSet = block.TxSet |> List.map (fun (TxHash h) -> h)
            Configuration =
                match block.Configuration with
                | None -> Unchecked.defaultof<_>
                | Some c -> blockchainConfigurationToDto c
        }

    let blockEnvelopeFromDto (dto : BlockEnvelopeDto) : BlockEnvelope =
        {
            RawBlock = dto.Block |> Convert.FromBase64String
            Signatures = dto.Signatures |> Array.map Signature |> Array.toList
        }

    let blockHeaderToBlockInfoDto (blockHeader : BlockHeader) : BlockInfoDto =
        {
            BlockNumber = blockHeader.Number |> fun (BlockNumber n) -> n
            BlockHash = blockHeader.Hash |> fun (BlockHash h) -> h
            BlockTimestamp = blockHeader.Timestamp |> fun (Timestamp t) -> t
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // State
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let chxBalanceStateFromDto (dto : ChxBalanceStateDto) : ChxBalanceState =
        {
            Amount = ChxAmount dto.Amount
            Nonce = Nonce dto.Nonce
        }

    let chxBalanceStateToDto (state : ChxBalanceState) : ChxBalanceStateDto =
        {
            Amount = state.Amount |> fun (ChxAmount a) -> a
            Nonce = state.Nonce |> fun (Nonce n) -> n
        }

    let holdingStateFromDto (dto : HoldingStateDto) : HoldingState =
        {
            Amount = AssetAmount dto.Amount
        }

    let holdingStateToDto (state : HoldingState) : HoldingStateDto =
        {
            Amount = state.Amount |> fun (AssetAmount a) -> a
        }

    let accountStateFromDto (dto : AccountStateDto) : AccountState =
        {
            ControllerAddress = BlockchainAddress dto.ControllerAddress
        }

    let accountStateToDto (state : AccountState) : AccountStateDto =
        {
            ControllerAddress = state.ControllerAddress |> fun (BlockchainAddress a) -> a
        }

    let assetStateFromDto (dto : AssetStateDto) : AssetState =
        {
            AssetCode =
                if dto.AssetCode.IsNullOrWhiteSpace() then
                    None
                else
                    dto.AssetCode |> AssetCode |> Some
            ControllerAddress = BlockchainAddress dto.ControllerAddress
        }

    let assetStateToDto (state : AssetState) : AssetStateDto =
        {
            AssetCode = state.AssetCode |> Option.map (fun (AssetCode c) -> c) |> Option.toObj
            ControllerAddress = state.ControllerAddress |> fun (BlockchainAddress a) -> a
        }

    let validatorStateFromDto (dto : ValidatorStateDto) : ValidatorState =
        {
            NetworkAddress = dto.NetworkAddress
        }

    let validatorStateToDto (state : ValidatorState) : ValidatorStateDto =
        {
            NetworkAddress = state.NetworkAddress
        }

    let stakeStateFromDto (dto : StakeStateDto) : StakeState =
        {
            Amount = ChxAmount dto.Amount
        }

    let stakeStateToDto (state : StakeState) : StakeStateDto =
        {
            Amount = state.Amount |> fun (ChxAmount a) -> a
        }

    let outputToDto (output : ProcessingOutput) : ProcessingOutputDto =
        let txResults =
            output.TxResults
            |> Map.toList
            |> List.map (fun (TxHash h, s : TxResult) -> h, s |> txResultToDto)
            |> Map.ofList

        let chxBalances =
            output.ChxBalances
            |> Map.toList
            |> List.map (fun (BlockchainAddress a, s : ChxBalanceState) -> a, chxBalanceStateToDto s)
            |> Map.ofList

        let holdings =
            output.Holdings
            |> Map.toList
            |> List.map (fun ((AccountHash ah, AssetHash ac), s : HoldingState) -> (ah, ac), holdingStateToDto s)
            |> Map.ofList

        let accounts =
            output.Accounts
            |> Map.toList
            |> List.map (fun (AccountHash ah, s : AccountState) -> ah, accountStateToDto s)
            |> Map.ofList

        let assets =
            output.Assets
            |> Map.toList
            |> List.map (fun (AssetHash ah, s : AssetState) -> ah, assetStateToDto s)
            |> Map.ofList

        let validators =
            output.Validators
            |> Map.toList
            |> List.map (fun (BlockchainAddress a, s : ValidatorState) -> a, validatorStateToDto s)
            |> Map.ofList

        let stakes =
            output.Stakes
            |> Map.toList
            |> List.map (fun ((BlockchainAddress sa, BlockchainAddress va), s : StakeState) ->
                (sa, va), stakeStateToDto s
            )
            |> Map.ofList

        {
            ProcessingOutputDto.TxResults = txResults
            ChxBalances = chxBalances
            Holdings = holdings
            Accounts = accounts
            Assets = assets
            Validators = validators
            Stakes = stakes
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Events
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let txSubmittedEventToSubmitTxResponseDto (event : TxReceivedEventData) =
        let (TxHash hash) = event.TxHash
        { SubmitTxResponseDto.TxHash = hash }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // API
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let chxBalanceStateDtoToGetAddressApiResponseDto
        (BlockchainAddress blockchainAddress)
        (chxBalanceState : ChxBalanceStateDto)
        =

        {
            GetAddressApiResponseDto.BlockchainAddress = blockchainAddress
            GetAddressApiResponseDto.Balance = chxBalanceState.Amount
            GetAddressApiResponseDto.Nonce = chxBalanceState.Nonce
        }

    let accountHoldingDtosToGetAccoungHoldingsResponseDto
        (AccountHash accountHash)
        (accountState : AccountStateDto)
        (holdings : AccountHoldingDto list)
        =

        let mapFn (holding : AccountHoldingDto) : GetAccountApiHoldingDto =
            {
                AssetHash = holding.AssetHash
                Balance = holding.Amount
            }

        {
            GetAccountApiResponseDto.AccountHash = accountHash
            GetAccountApiResponseDto.ControllerAddress = accountState.ControllerAddress
            GetAccountApiResponseDto.Holdings = List.map mapFn holdings
        }

    let blockDtosToGetBlockApiResponseDto
        (blockEnvelopeDto : BlockEnvelopeDto)
        (blockDto : BlockDto)
        =

        {
            GetBlockApiResponseDto.Number = blockDto.Header.Number
            GetBlockApiResponseDto.Hash = blockDto.Header.Hash
            GetBlockApiResponseDto.PreviousHash = blockDto.Header.PreviousHash
            GetBlockApiResponseDto.ConfigurationBlockNumber = blockDto.Header.ConfigurationBlockNumber
            GetBlockApiResponseDto.Timestamp = blockDto.Header.Timestamp
            GetBlockApiResponseDto.Validator = blockDto.Header.Validator
            GetBlockApiResponseDto.TxSetRoot = blockDto.Header.TxSetRoot
            GetBlockApiResponseDto.TxResultSetRoot = blockDto.Header.TxResultSetRoot
            GetBlockApiResponseDto.StateRoot = blockDto.Header.StateRoot
            GetBlockApiResponseDto.ConfigurationRoot = blockDto.Header.ConfigurationRoot
            GetBlockApiResponseDto.TxSet = blockDto.TxSet
            GetBlockApiResponseDto.Signatures = blockEnvelopeDto.Signatures |> Array.toList
            GetBlockApiResponseDto.Configuration = blockDto.Configuration
        }

    let txToGetTxApiResponseDto
        (TxHash txHash)
        (BlockchainAddress senderAddress)
        (txDto : TxDto)
        (txResult : TxResultDto option)
        =

        let txStatus, txErrorCode, failedActionNumber, blockNumber =
            match txResult with
            | Some r -> r.Status, r.ErrorCode, r.FailedActionNumber, Nullable r.BlockNumber
            | None -> 0uy, Nullable(), Nullable(), Nullable()

        {
            GetTxApiResponseDto.TxHash = txHash
            GetTxApiResponseDto.SenderAddress = senderAddress
            GetTxApiResponseDto.Nonce = txDto.Nonce
            GetTxApiResponseDto.Fee = txDto.Fee
            GetTxApiResponseDto.Actions = txDto.Actions
            GetTxApiResponseDto.Status = txStatus |> txStatusNumberToString
            GetTxApiResponseDto.ErrorCode = txErrorCode |> txErrorCodeNumberToString
            GetTxApiResponseDto.FailedActionNumber = failedActionNumber
            GetTxApiResponseDto.BlockNumber = blockNumber
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Consensus
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let consensusMessageFromDto (dto : ConsensusMessageDto) : ConsensusMessage =
        match dto.ConsensusMessageType with
        | "Propose" ->
            let block = dto.Block |> blockFromDto
            let validRound = dto.ValidRound.Value |> ConsensusRound
            Propose (block, validRound)
        | "Vote" ->
            if dto.BlockHash = (None |> unionCaseName) then
                None
            else
                dto.BlockHash |> BlockHash |> Some
            |> Vote
        | "Commit" ->
            if dto.BlockHash = (None |> unionCaseName) then
                None
            else
                dto.BlockHash |> BlockHash |> Some
            |> Commit
        | mt ->
            failwithf "Unknown consensus message type: %s" mt

    let consensusMessageToDto (consensusMessage : ConsensusMessage) : ConsensusMessageDto =
        match consensusMessage with
        | Propose (block, validRound) ->
            {
                ConsensusMessageType = "Propose"
                Block = block |> blockToDto
                ValidRound = validRound |> fun (ConsensusRound r) -> Nullable(r)
                BlockHash = Unchecked.defaultof<_>
            }
        | Vote blockHash ->
            let blockHash =
                match blockHash with
                | Some (BlockHash h) -> h
                | None -> None |> unionCaseName
            {
                ConsensusMessageType = "Vote"
                Block = Unchecked.defaultof<_>
                ValidRound = Unchecked.defaultof<_>
                BlockHash = blockHash
            }
        | Commit blockHash ->
            let blockHash =
                match blockHash with
                | Some (BlockHash h) -> h
                | None -> None |> unionCaseName
            {
                ConsensusMessageType = "Commit"
                Block = Unchecked.defaultof<_>
                ValidRound = Unchecked.defaultof<_>
                BlockHash = blockHash
            }

    let consensusMessageEnvelopeFromDto (dto : ConsensusMessageEnvelopeDto) : ConsensusMessageEnvelope =
        {
            BlockNumber = BlockNumber dto.BlockNumber
            Round = ConsensusRound dto.Round
            ConsensusMessage = consensusMessageFromDto dto.ConsensusMessage
            Signature = Signature dto.Signature
        }

    let consensusMessageEnvelopeToDto (envelope : ConsensusMessageEnvelope) : ConsensusMessageEnvelopeDto =
        {
            BlockNumber = envelope.BlockNumber |> fun (BlockNumber blockNr) -> blockNr
            Round = envelope.Round |> fun (ConsensusRound round) -> round
            ConsensusMessage = consensusMessageToDto envelope.ConsensusMessage
            Signature = envelope.Signature |> fun (Signature s) -> s
        }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Network
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let private networkMessageIdToIdTypeTuple networkMessageId =
        match networkMessageId with
        | Tx (TxHash txHash) -> "Tx", txHash
        | Block (BlockNumber blockNr) -> "Block", blockNr |> Convert.ToString
        | Consensus (ConsensusMessageId msgId) -> "Consensus", msgId

    let private messageTypeToNetworkMessageId (messageType : string) (messageId : string) =
        match messageType with
        | "Tx" -> messageId |> TxHash |> Tx
        | "Block" -> messageId |> Convert.ToInt64 |> BlockNumber |> Block
        | "Consensus" -> messageId |> ConsensusMessageId |> Consensus
        | _ -> failwithf "Invalid network message type %s" messageType

    let gossipMemberFromDto (dto: GossipMemberDto) : GossipMember =
        {
            NetworkAddress = NetworkAddress dto.NetworkAddress
            Heartbeat = dto.Heartbeat
        }

    let gossipMemberToDto (gossipMember : GossipMember) : GossipMemberDto =
        {
            NetworkAddress = gossipMember.NetworkAddress |> fun (NetworkAddress a) -> a
            Heartbeat = gossipMember.Heartbeat
        }

    let gossipDiscoveryMessageFromDto (dto : GossipDiscoveryMessageDto) : GossipDiscoveryMessage =
        {
            ActiveMembers = dto.ActiveMembers |> List.map gossipMemberFromDto
        }

    let gossipDiscoveryMessageToDto (gossipDiscoveryMessage: GossipDiscoveryMessage) =
        {
            ActiveMembers = gossipDiscoveryMessage.ActiveMembers |> List.map gossipMemberToDto
        }

    let gossipMessageFromDto (dto : GossipMessageDto) =
        let gossipMessageId = messageTypeToNetworkMessageId dto.MessageType dto.MessageId

        {
            MessageId = gossipMessageId
            SenderAddress = NetworkAddress dto.SenderAddress
            Data = dto.Data
        }

    let gossipMessageToDto (gossipMessage : GossipMessage) =
        let messageType, messageId = networkMessageIdToIdTypeTuple gossipMessage.MessageId

        {
            MessageId = messageId
            MessageType = messageType
            SenderAddress = gossipMessage.SenderAddress |> fun (NetworkAddress a) -> a
            Data = gossipMessage.Data
        }

    let multicastMessageFromDto (dto : MulticastMessageDto) : MulticastMessage =
        let multicastMessageId = messageTypeToNetworkMessageId dto.MessageType dto.MessageId

        {
            MessageId = multicastMessageId
            Data = dto.Data
        }

    let multicastMessageToDto (multicastMessage : MulticastMessage) =
        let messageType, messageId = networkMessageIdToIdTypeTuple multicastMessage.MessageId

        {
            MessageId = messageId
            MessageType = messageType
            Data = multicastMessage.Data
        }

    let requestDataMessageFromDto (dto : RequestDataMessageDto) : RequestDataMessage =
        let requestDataMessageId = messageTypeToNetworkMessageId dto.MessageType dto.MessageId

        {
            MessageId = requestDataMessageId
            SenderAddress = NetworkAddress dto.SenderAddress
        }

    let requestDataMessageToDto (requestDataMessage : RequestDataMessage) =
        let messageType, messageId = networkMessageIdToIdTypeTuple requestDataMessage.MessageId

        {
            MessageId = messageId
            MessageType = messageType
            SenderAddress = requestDataMessage.SenderAddress |> fun (NetworkAddress a) -> a
        }

    let responseDataMessageFromDto (dto : ResponseDataMessageDto) : ResponseDataMessage =
        let responseDataMessageId = messageTypeToNetworkMessageId dto.MessageType dto.MessageId

        {
            MessageId = responseDataMessageId
            Data = dto.Data
        }

    let responseDataMessageToDto (responseDataMessage : ResponseDataMessage) =
        let messageType, messageId = networkMessageIdToIdTypeTuple responseDataMessage.MessageId

        {
            MessageId = messageId
            MessageType = messageType
            Data = responseDataMessage.Data
        }

    let peerMessageFromDto (dto : PeerMessageDto) =
        match dto.MessageData with
        | :? GossipDiscoveryMessageDto as m ->
            gossipDiscoveryMessageFromDto m |> GossipDiscoveryMessage
        | :? GossipMessageDto as m ->
            gossipMessageFromDto m |> GossipMessage
        | :? MulticastMessageDto as m ->
            multicastMessageFromDto m |> MulticastMessage
        | :? RequestDataMessageDto as m ->
            requestDataMessageFromDto m |> RequestDataMessage
        | :? ResponseDataMessageDto as m ->
            responseDataMessageFromDto m |> ResponseDataMessage
        | _ -> failwith "Invalid message type to map."

    let peerMessageToDto (serialize : (obj -> string)) (peerMessage : PeerMessage) : PeerMessageDto =
        let messageType, data =
            match peerMessage with
            | GossipDiscoveryMessage m -> "GossipDiscoveryMessage", m |> gossipDiscoveryMessageToDto |> serialize
            | GossipMessage m -> "GossipMessage", m |> gossipMessageToDto |> serialize
            | MulticastMessage m -> "MulticastMessage", m |> multicastMessageToDto |> serialize
            | RequestDataMessage m -> "RequestDataMessage", m |> requestDataMessageToDto |> serialize
            | ResponseDataMessage m -> "ResponseDataMessage", m |> responseDataMessageToDto |> serialize

        {
            MessageType = messageType
            MessageData = data
        }
