namespace Own.Blockchain.Public.Core.DomainTypes

open System

////////////////////////////////////////////////////////////////////////////////////////////////////
// Wallet
////////////////////////////////////////////////////////////////////////////////////////////////////

type PrivateKey = PrivateKey of string
type BlockchainAddress = BlockchainAddress of string

type WalletInfo = {
    PrivateKey : PrivateKey
    Address : BlockchainAddress
}

type Signature = Signature of string

////////////////////////////////////////////////////////////////////////////////////////////////////
// Accounts
////////////////////////////////////////////////////////////////////////////////////////////////////

type AccountHash = AccountHash of string
type AssetHash = AssetHash of string
type AssetCode = AssetCode of string

type Nonce = Nonce of int64
type ChxAmount = ChxAmount of decimal
type AssetAmount = AssetAmount of decimal

////////////////////////////////////////////////////////////////////////////////////////////////////
// Tx
////////////////////////////////////////////////////////////////////////////////////////////////////

type TxHash = TxHash of string

type TransferChxTxAction = {
    RecipientAddress : BlockchainAddress
    Amount : ChxAmount
}

type TransferAssetTxAction = {
    FromAccountHash : AccountHash
    ToAccountHash : AccountHash
    AssetHash : AssetHash
    Amount : AssetAmount
}

type CreateAssetEmissionTxAction = {
    EmissionAccountHash : AccountHash
    AssetHash : AssetHash
    Amount : AssetAmount
}

type SetAccountControllerTxAction = {
    AccountHash : AccountHash
    ControllerAddress : BlockchainAddress
}

type SetAssetControllerTxAction = {
    AssetHash : AssetHash
    ControllerAddress : BlockchainAddress
}

type SetAssetCodeTxAction = {
    AssetHash : AssetHash
    AssetCode : AssetCode
}

type SetValidatorNetworkAddressTxAction = {
    NetworkAddress : string
}

type DelegateStakeTxAction = {
    ValidatorAddress : BlockchainAddress
    Amount : ChxAmount
}

type TxAction =
    | TransferChx of TransferChxTxAction
    | TransferAsset of TransferAssetTxAction
    | CreateAssetEmission of CreateAssetEmissionTxAction
    | CreateAccount
    | CreateAsset
    | SetAccountController of SetAccountControllerTxAction
    | SetAssetController of SetAssetControllerTxAction
    | SetAssetCode of SetAssetCodeTxAction
    | SetValidatorNetworkAddress of SetValidatorNetworkAddressTxAction
    | DelegateStake of DelegateStakeTxAction

type Tx = {
    TxHash : TxHash
    Sender : BlockchainAddress
    Nonce : Nonce
    Fee : ChxAmount
    Actions : TxAction list
}

type TxEnvelope = {
    RawTx : byte[]
    Signature : Signature
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Blockchain Configuration
////////////////////////////////////////////////////////////////////////////////////////////////////

type ValidatorSnapshot = {
    ValidatorAddress : BlockchainAddress
    NetworkAddress : string
    TotalStake : ChxAmount
}

type BlockchainConfiguration = {
    Validators : ValidatorSnapshot list
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Block
////////////////////////////////////////////////////////////////////////////////////////////////////

type Timestamp = Timestamp of int64 // UNIX Timestamp
type BlockNumber = BlockNumber of int64
type BlockHash = BlockHash of string
type MerkleTreeRoot = MerkleTreeRoot of string

type BlockHeader = {
    Number : BlockNumber
    Hash : BlockHash
    PreviousHash : BlockHash
    ConfigurationBlockNumber : BlockNumber
    Timestamp : Timestamp
    Validator : BlockchainAddress // Fee beneficiary
    TxSetRoot : MerkleTreeRoot
    TxResultSetRoot : MerkleTreeRoot
    StateRoot : MerkleTreeRoot
    ConfigurationRoot : MerkleTreeRoot
}

type Block = {
    Header : BlockHeader
    TxSet : TxHash list
    Configuration : BlockchainConfiguration option
}

type BlockEnvelope = {
    RawBlock : byte[]
    Signatures : Signature list
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Processing
////////////////////////////////////////////////////////////////////////////////////////////////////

type TxActionNumber = TxActionNumber of int16

type TxErrorCode =
    // CHANGING THESE NUMBERS WILL INVALIDATE TX RESULTS MERKLE ROOT IN EXISTING BLOCKS!!!

    // Address
    | NonceTooLow = 100s
    | InsufficientChxBalance = 110s

    // Holding
    | InsufficientAssetHoldingBalance = 210s

    // Account
    | AccountNotFound = 310s
    | AccountAlreadyExists = 320s
    | SenderIsNotSourceAccountController = 330s
    | SourceAccountNotFound = 340s
    | DestinationAccountNotFound = 350s

    // Asset
    | AssetNotFound = 410s
    | AssetAlreadyExists = 420s
    | SenderIsNotAssetController = 430s

type TxError =
    | TxError of TxErrorCode
    | TxActionError of TxActionNumber * TxErrorCode

type TxStatus =
    | Success
    | Failure of TxError

type TxResult = {
    Status : TxStatus
    BlockNumber : BlockNumber
}

type PendingTxInfo = {
    TxHash : TxHash
    Sender : BlockchainAddress
    Nonce : Nonce
    Fee : ChxAmount
    ActionCount : int16
    AppearanceOrder : int64
}

type ChxBalanceState = {
    Amount : ChxAmount
    Nonce : Nonce
}

type HoldingState = {
    Amount : AssetAmount
}

type AccountState = {
    ControllerAddress : BlockchainAddress
}

type AssetState = {
    AssetCode : AssetCode option
    ControllerAddress : BlockchainAddress
}

type ValidatorState = {
    NetworkAddress : string
}

type StakeState = {
    Amount : ChxAmount
}

type ProcessingOutput = {
    TxResults : Map<TxHash, TxResult>
    ChxBalances : Map<BlockchainAddress, ChxBalanceState>
    Holdings : Map<AccountHash * AssetHash, HoldingState>
    Accounts : Map<AccountHash, AccountState>
    Assets : Map<AssetHash, AssetState>
    Validators : Map<BlockchainAddress, ValidatorState>
    Stakes : Map<BlockchainAddress * BlockchainAddress, StakeState>
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Consensus
////////////////////////////////////////////////////////////////////////////////////////////////////

type ConsensusRound = ConsensusRound of int

type ConsensusStep =
    | Propose
    | Vote
    | Commit

type ConsensusMessage =
    | Propose of Block * ConsensusRound
    | Vote of BlockHash option
    | Commit of BlockHash option

type ConsensusMessageEnvelope = {
    BlockNumber : BlockNumber
    Round : ConsensusRound
    ConsensusMessage : ConsensusMessage
    Signature : Signature
}

type ConsensusCommand =
    | Synchronize
    | Message of BlockchainAddress * ConsensusMessageEnvelope
    | Timeout of BlockNumber * ConsensusRound * ConsensusStep

type ConsensusMessageId = ConsensusMessageId of string // Just for the network layer

////////////////////////////////////////////////////////////////////////////////////////////////////
// Network
////////////////////////////////////////////////////////////////////////////////////////////////////

type NetworkAddress = NetworkAddress of string

type NetworkMessageId =
    | Tx of TxHash
    | Block of BlockNumber
    | Consensus of ConsensusMessageId

type NetworkNodeConfig = {
    NetworkAddress : NetworkAddress
    BootstrapNodes : NetworkAddress list
}

type GossipMember = {
    NetworkAddress : NetworkAddress
    Heartbeat : int64
}

type GossipDiscoveryMessage = {
    ActiveMembers : GossipMember list
}

type GossipMessage = {
    MessageId : NetworkMessageId
    SenderAddress : NetworkAddress
    Data : obj
}

type MulticastMessage = {
    MessageId : NetworkMessageId
    Data : obj
}

type RequestDataMessage = {
    MessageId : NetworkMessageId
    SenderAddress : NetworkAddress
}

type ResponseDataMessage = {
    MessageId : NetworkMessageId
    Data : obj
}

type PeerMessage =
    | GossipDiscoveryMessage of GossipDiscoveryMessage
    | GossipMessage of GossipMessage
    | MulticastMessage of MulticastMessage
    | RequestDataMessage of RequestDataMessage
    | ResponseDataMessage of ResponseDataMessage

////////////////////////////////////////////////////////////////////////////////////////////////////
// Domain Type Logic
////////////////////////////////////////////////////////////////////////////////////////////////////

type BlockNumber with
    static member Zero =
        BlockNumber 0L
    static member One =
        BlockNumber 1L
    static member (+) (BlockNumber n1, BlockNumber n2) =
        BlockNumber (n1 + n2)
    static member (+) (BlockNumber n1, n2) =
        BlockNumber (n1 + n2)
    static member (+) (BlockNumber n1, n2) =
        BlockNumber (n1 + int64 n2)
    static member (-) (BlockNumber n1, BlockNumber n2) =
        BlockNumber (n1 - n2)
    static member (-) (BlockNumber n1, n2) =
        BlockNumber (n1 - n2)
    static member (-) (BlockNumber n1, n2) =
        BlockNumber (n1 - int64 n2)

type ConsensusRound with
    static member Zero =
        ConsensusRound 0
    static member One =
        ConsensusRound 1
    static member (+) (ConsensusRound n1, ConsensusRound n2) =
        ConsensusRound (n1 + n2)
    static member (+) (ConsensusRound n1, n2) =
        ConsensusRound (n1 + n2)
    static member (-) (ConsensusRound n1, ConsensusRound n2) =
        ConsensusRound (n1 - n2)
    static member (-) (ConsensusRound n1, n2) =
        ConsensusRound (n1 - n2)

type Nonce with
    static member Zero =
        Nonce 0L
    static member One =
        Nonce 1L
    static member (+) (Nonce n1, Nonce n2) =
        Nonce (n1 + n2)
    static member (+) (Nonce n1, n2) =
        Nonce (n1 + n2)
    static member (+) (Nonce n1, n2) =
        Nonce (n1 + int64 n2)
    static member (-) (Nonce n1, Nonce n2) =
        Nonce (n1 - n2)
    static member (-) (Nonce n1, n2) =
        Nonce (n1 - n2)
    static member (-) (Nonce n1, n2) =
        Nonce (n1 - int64 n2)

type ChxAmount with
    static member Zero =
        ChxAmount 0m
    static member (+) (ChxAmount a1, ChxAmount a2) =
        ChxAmount (Decimal.Round(a1 + a2, 18))
    static member (+) (ChxAmount a1, a2) =
        ChxAmount (Decimal.Round(a1 + a2, 18))
    static member (-) (ChxAmount a1, ChxAmount a2) =
        ChxAmount (Decimal.Round(a1 - a2, 18))
    static member (-) (ChxAmount a1, a2) =
        ChxAmount (Decimal.Round(a1 - a2, 18))
    static member (*) (ChxAmount a1, ChxAmount a2) =
        ChxAmount (Decimal.Round(a1 * a2, 18))
    static member (*) (ChxAmount a1, a2) =
        ChxAmount (Decimal.Round(a1 * a2, 18))
    static member (/) (ChxAmount a1, ChxAmount a2) =
        ChxAmount (Decimal.Round(a1 / a2, 18))
    static member (/) (ChxAmount a1, a2) =
        ChxAmount (Decimal.Round(a1 / a2, 18))

type AssetAmount with
    static member Zero =
        AssetAmount 0m
    static member (+) (AssetAmount a1, AssetAmount a2) =
        AssetAmount (Decimal.Round(a1 + a2, 18))
    static member (+) (AssetAmount a1, a2) =
        AssetAmount (Decimal.Round(a1 + a2, 18))
    static member (-) (AssetAmount a1, AssetAmount a2) =
        AssetAmount (Decimal.Round(a1 - a2, 18))
    static member (-) (AssetAmount a1, a2) =
        AssetAmount (Decimal.Round(a1 - a2, 18))
    static member (*) (AssetAmount a1, AssetAmount a2) =
        AssetAmount (Decimal.Round(a1 * a2, 18))
    static member (*) (AssetAmount a1, a2) =
        AssetAmount (Decimal.Round(a1 * a2, 18))
    static member (/) (AssetAmount a1, AssetAmount a2) =
        AssetAmount (Decimal.Round(a1 / a2, 18))
    static member (/) (AssetAmount a1, a2) =
        AssetAmount (Decimal.Round(a1 / a2, 18))

type Tx with
    member __.TotalFee = __.Fee * decimal __.Actions.Length

type PendingTxInfo with
    member __.TotalFee = __.Fee * decimal __.ActionCount
