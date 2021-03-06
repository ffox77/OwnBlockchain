namespace Own.Blockchain.Public.Core

open System
open Own.Common
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos

module Validators =

    /// 2f + 1
    let calculateQualifiedMajority validatorCount =
        decimal validatorCount / 3m * 2m
        |> Math.Floor
        |> Convert.ToInt32
        |> (+) 1

    /// f + 1
    let calculateValidQuorum validatorCount =
        decimal validatorCount / 3m
        |> Math.Floor
        |> Convert.ToInt32
        |> (+) 1

    let calculateQuorumSupply quorumSupplyPercent (ChxAmount totalSupply) =
        Decimal.Round(totalSupply * quorumSupplyPercent / 100m, 18, MidpointRounding.AwayFromZero)
        |> ChxAmount

    let calculateValidatorThreshold maxValidatorCount (ChxAmount quorumSupply) =
        Decimal.Round(quorumSupply / (decimal maxValidatorCount), 18, MidpointRounding.AwayFromZero)
        |> ChxAmount

    let getTopValidators
        getTopValidatorsByStake
        totalSupply
        quorumSupplyPercent
        maxValidatorCount
        =

        totalSupply
        |> calculateQuorumSupply quorumSupplyPercent
        |> calculateValidatorThreshold maxValidatorCount
        |> getTopValidatorsByStake maxValidatorCount
        |> List.map Mapping.validatorSnapshotFromDto

    let getCurrentValidators getLastAppliedBlockNumber getBlock =
        let configBlock =
            match getLastAppliedBlockNumber () with
            | None -> failwith "Cannot get last applied block number."
            | Some blockNumber ->
                getBlock blockNumber
                >>= Blocks.extractBlockFromEnvelopeDto
                >>= (fun b ->
                    if b.Configuration.IsSome then
                        Ok b // Last block is the configuration block
                    else
                        getBlock b.Header.ConfigurationBlockNumber
                        >>= Blocks.extractBlockFromEnvelopeDto
                )

        match configBlock with
        | Error e -> failwith "Cannot get last applied configuration block."
        | Ok block ->
            match block.Configuration with
            | None -> failwith "Cannot find configuration in last applied configuration block."
            | Some config ->
                match config.Validators with
                | [] -> failwith "Cannot find validators in last applied configuration block."
                | validators -> validators

    let isValidator
        (getValidators : unit -> ValidatorSnapshot list)
        validatorAddress
        =

        getValidators ()
        |> List.exists (fun v -> v.ValidatorAddress = validatorAddress)

    let getProposer
        (BlockNumber blockNumber)
        (ConsensusRound consensusRound)
        (validators : ValidatorSnapshot list)
        =

        let validatorIndex = (blockNumber + int64 consensusRound) % (int64 validators.Length) |> Convert.ToInt32
        validators
        |> List.sortBy (fun v -> v.ValidatorAddress)
        |> List.item validatorIndex

    let getProposerAddress
        blockNumber
        consensusRound
        validators
        =

        (getProposer blockNumber consensusRound validators).ValidatorAddress
