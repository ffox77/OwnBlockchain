﻿namespace Chainium.Blockchain.Public.Node

open Chainium.Common
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Crypto
open Chainium.Blockchain.Public.Data
open Chainium.Blockchain.Public.Net

module Composition =

    // Raw storage

    let saveTx = Raw.saveTx Config.DataDir

    let getTx = Raw.getTx Config.DataDir

    let saveTxResult = Raw.saveTxResult Config.DataDir

    let getTxResult = Raw.getTxResult Config.DataDir

    let saveBlock = Raw.saveBlock Config.DataDir

    let getBlock = Raw.getBlock Config.DataDir

    let saveBlockEnvelope = Raw.saveBlockEnvelope Config.DataDir

    let getBlockEnvelope = Raw.getBlockEnvelope Config.DataDir

    let blockExists = Raw.blockExists Config.DataDir

    // DB

    let initDb () = DbInit.init Config.DbEngineType Config.DbConnectionString

    let saveTxToDb = Db.saveTx Config.DbConnectionString

    let getTxInfo = Db.getTx Config.DbConnectionString

    let getPendingTxs = Db.getPendingTxs Config.DbConnectionString

    let getTotalFeeForPendingTxs = Db.getTotalFeeForPendingTxs Config.DbConnectionString

    let getLastBlockNumber () = Db.getLastBlockNumber Config.DbConnectionString

    let getLastBlockTimestamp () = Db.getLastBlockTimestamp Config.DbConnectionString

    let getChxBalanceState = Db.getChxBalanceState Config.DbConnectionString

    let getAddressAccounts = Db.getAddressAccounts Config.DbConnectionString

    let getAccountState = Db.getAccountState Config.DbConnectionString

    let getAccountHoldings = Db.getAccountHoldings Config.DbConnectionString

    let getHoldingState = Db.getHoldingState Config.DbConnectionString

    let getAssetState = Db.getAssetState Config.DbConnectionString

    let getValidatorState = Db.getValidatorState Config.DbConnectionString

    let getAllValidators () = Db.getAllValidators Config.DbConnectionString

    let getStakeState = Db.getStakeState Config.DbConnectionString

    let getTotalChxStaked = Db.getTotalChxStaked Config.DbConnectionString

    let getAllPeerNodes () = Db.getAllPeerNodes Config.DbConnectionString

    let savePeerNode = Db.savePeerNode Config.DbConnectionString

    let removePeerNode = Db.removePeerNode Config.DbConnectionString

    let applyNewState = Db.applyNewState Config.DbConnectionString

    // Workflows

    let getAvailableChxBalance =
        Workflows.getAvailableChxBalance
            getChxBalanceState
            getTotalChxStaked

    let submitTx =
        Workflows.submitTx
            Signing.verifySignature
            Hashing.isValidChainiumAddress
            Hashing.hash
            getAvailableChxBalance
            getTotalFeeForPendingTxs
            saveTx
            saveTxToDb
            (ChxAmount Config.MinTxActionFee)

    let addressFromPrivateKey = memoize Signing.addressFromPrivateKey

    let isMyTurnToProposeBlock () =
        Workflows.isMyTurnToProposeBlock
            getLastBlockNumber
            getAllValidators
            addressFromPrivateKey
            Config.ValidatorPrivateKey

    let persistTxResults =
        Workflows.persistTxResults
            saveTxResult

    let createBlock =
        Workflows.createBlock
            getTx
            Signing.verifySignature
            Hashing.isValidChainiumAddress
            getChxBalanceState
            getHoldingState
            getAccountState
            getAssetState
            getValidatorState
            getStakeState
            getTotalChxStaked
            getLastBlockNumber
            getBlock
            Hashing.decode
            Hashing.hash
            Hashing.merkleTree
            (ChxAmount Config.MinTxActionFee)

    let createNewBlock () =
        Workflows.createNewBlock
            createBlock
            getPendingTxs
            getChxBalanceState
            getAvailableChxBalance
            persistTxResults
            Signing.signMessage
            saveBlock
            saveBlockEnvelope
            applyNewState
            Config.MaxTxCountPerBlock
            addressFromPrivateKey
            Config.ValidatorPrivateKey

    let applyBlock =
        Workflows.applyBlock
            createBlock
            getAllValidators
            Signing.verifySignature
            persistTxResults
            saveBlock
            saveBlockEnvelope
            applyNewState

    let initBlockchainState () =
        Workflows.initBlockchainState
            getLastBlockNumber
            getBlock
            saveBlock
            applyNewState
            Hashing.decode
            Hashing.hash
            Hashing.merkleTree
            Hashing.zeroHash
            Hashing.zeroAddress
            (ChxAmount Config.GenesisChxSupply)
            (ChainiumAddress Config.GenesisAddress)
            Config.GenesisValidators

    let advanceToLastKnownBlock () =
        Workflows.advanceToLastKnownBlock
            createBlock
            Hashing.decode
            Hashing.hash
            Hashing.merkleTree
            applyNewState
            getLastBlockNumber
            blockExists
            getBlock

    let propagateTx = Workflows.propagateTx Peers.sendMessage Config.NetworkAddress getTx

    let propagateBlock =
        Workflows.propagateBlock
            Peers.sendMessage
            Config.NetworkAddress
            getBlockEnvelope

    // Network

    let processPeerMessage (peerMessage : PeerMessage) =
        Workflows.processPeerMessage
            getTx
            getBlockEnvelope
            submitTx
            applyBlock
            peerMessage

    let startGossip publishEvent =
        Peers.startGossip
            getAllPeerNodes
            savePeerNode
            removePeerNode
            Transport.sendGossipDiscoveryMessage
            Transport.sendGossipMessage
            Transport.sendMulticastMessage
            Transport.receiveMessage
            Transport.closeConnection
            Config.NetworkAddress
            Config.NetworkBootstrapNodes
            getAllValidators
            processPeerMessage
            publishEvent
    // API

    let getAddressApi = Workflows.getAddressApi getChxBalanceState

    let getAddressAccountsApi = Workflows.getAddressAccountsApi getAddressAccounts

    let getAccountApi = Workflows.getAccountApi getAccountState getAccountHoldings

    let getBlockApi = Workflows.getBlockApi getBlock

    let getTxApi = Workflows.getTxApi getTx Signing.verifySignature getTxResult
