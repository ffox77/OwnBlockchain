namespace Own.Blockchain.Public.Wallet

open System
open System.Text
open System.Reflection
open Own.Common
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Crypto

module Cli =

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Handlers
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let handleShowVersionCommand () =
        let assembly = System.Reflection.Assembly.GetExecutingAssembly()
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion
        |> printfn "%s"

    let handleGenerateWalletCommand () =
        let wallet = Signing.generateWallet ()
        let (PrivateKey pk) = wallet.PrivateKey
        let (BlockchainAddress address) = wallet.Address
        printfn "Private Key: %s\nAddress: %s" pk address

    let handleDeriveAddressCommand privateKey =
        privateKey // TODO: Use key file path, to prevent keys being logged in terminal history.
        |> PrivateKey
        |> Signing.addressFromPrivateKey
        |> fun (BlockchainAddress a) -> printfn "Address: %s" a

    let handleSignMessageCommand privateKey message =
        let privateKey = PrivateKey privateKey

        message
        |> Convert.FromBase64String // TODO: Provide input as a file path, so the raw data can be read.
        |> Signing.signMessage privateKey // TODO: Use key file path, to prevent keys being logged in terminal history.
        |> fun (Signature signature) -> printfn "Signature: %s" signature

    let handleHelpCommand args =
        printfn "TODO: Print short command reference"

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Configuration
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let handleCommand args =
        match args with
        | ["-v"] -> handleShowVersionCommand ()
        | ["-g"] -> handleGenerateWalletCommand ()
        | ["-a"; privateKey] -> handleDeriveAddressCommand privateKey
        | ["-s"; privateKey; message] -> handleSignMessageCommand privateKey message
        | ["--help"] | _ -> handleHelpCommand args
