namespace Own.Blockchain.Public.Core.Tests

open System
open Xunit
open Swensen.Unquote
open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos

module ValidatorTests =

    [<Theory>]
    [<InlineData(4, 3)>]
    [<InlineData(5, 4)>]
    [<InlineData(6, 5)>]
    [<InlineData(7, 5)>]
    [<InlineData(8, 6)>]
    [<InlineData(9, 7)>]
    [<InlineData(10, 7)>]
    [<InlineData(20, 14)>]
    [<InlineData(31, 21)>]
    [<InlineData(100, 67)>]
    let ``Validators.calculateQualifiedMajority`` (validatorCount, expectedQualifiedMajority) =
        // ACT
        let actualQualifiedMajority = Validators.calculateQualifiedMajority validatorCount

        // ASSERT
        test <@ actualQualifiedMajority = expectedQualifiedMajority @>

    [<Theory>]
    [<InlineData(4, 2)>]
    [<InlineData(5, 2)>]
    [<InlineData(6, 3)>]
    [<InlineData(7, 3)>]
    [<InlineData(8, 3)>]
    [<InlineData(9, 4)>]
    [<InlineData(10, 4)>]
    [<InlineData(20, 7)>]
    [<InlineData(31, 11)>]
    [<InlineData(100, 34)>]
    let ``Validators.calculateValidQuorum`` (validatorCount, expectedValidQuorum) =
        // ACT
        let actualValidQuorum = Validators.calculateValidQuorum validatorCount

        // ASSERT
        test <@ actualValidQuorum = expectedValidQuorum @>

    [<Fact>]
    let ``Validators.calculateQuorumSupply`` () =
        // ARRANGE
        let totalSupply = ChxAmount 1000m
        let quorumSupplyPercent = 33m
        let expectedQuorumSupply = ChxAmount 330m

        // ACT
        let actualQuorumSupply = Validators.calculateQuorumSupply quorumSupplyPercent totalSupply

        // ASSERT
        test <@ actualQuorumSupply = expectedQuorumSupply @>

    [<Fact>]
    let ``Validators.calculateQuorumSupply with rounding`` () =
        // ARRANGE
        let totalSupply = ChxAmount 1000m
        let quorumSupplyPercent = 99.99999999999999999955m
        let expectedQuorumSupply = ChxAmount 999.999999999999999996m

        // ACT
        let actualQuorumSupply = Validators.calculateQuorumSupply quorumSupplyPercent totalSupply

        // ASSERT
        test <@ actualQuorumSupply = expectedQuorumSupply @>

    [<Fact>]
    let ``Validators.calculateValidatorThreshold`` () =
        // ARRANGE
        let quorumSupply = ChxAmount 1000m
        let maxValidatorCount = 100
        let expectedValidatorThreshold = ChxAmount 10m

        // ACT
        let actualValidatorThreshold = Validators.calculateValidatorThreshold maxValidatorCount quorumSupply

        // ASSERT
        test <@ actualValidatorThreshold = expectedValidatorThreshold @>

    [<Fact>]
    let ``Validators.calculateValidatorThreshold with rounding`` () =
        // ARRANGE
        let quorumSupply = ChxAmount 1000m
        let maxValidatorCount = 11
        let expectedValidatorThreshold = ChxAmount 90.909090909090909091m

        // ACT
        let actualValidatorThreshold = Validators.calculateValidatorThreshold maxValidatorCount quorumSupply

        // ASSERT
        test <@ actualValidatorThreshold = expectedValidatorThreshold @>

    [<Fact>]
    let ``Validators.getProposer`` () =
        // ARRANGE
        let blockNumber = BlockNumber 1L
        let consensusRound = ConsensusRound 0
        let validators =
            [
                {ValidatorSnapshotDto.ValidatorAddress = "A"; NetworkAddress = "1"; TotalStake = 0m}
                {ValidatorSnapshotDto.ValidatorAddress = "B"; NetworkAddress = "2"; TotalStake = 0m}
                {ValidatorSnapshotDto.ValidatorAddress = "C"; NetworkAddress = "3"; TotalStake = 0m}
                {ValidatorSnapshotDto.ValidatorAddress = "D"; NetworkAddress = "4"; TotalStake = 0m}
            ]
            |> List.map Mapping.validatorSnapshotFromDto

        let expectedValidator = validators.[1]

        // ACT
        let actualValidator = Validators.getProposer blockNumber consensusRound validators

        // ASSERT
        test <@ actualValidator = expectedValidator @>
