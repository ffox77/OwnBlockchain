namespace Chainium.Blockchain.Public.Wallet.Api

open System
open System.IO
open System.Reflection
open Microsoft.Extensions.Configuration
open Chainium.Common

type Config () =

    static let appDir = Directory.GetCurrentDirectory()

    static let config =
        (
            ConfigurationBuilder()
                .SetBasePath(appDir)
                .AddJsonFile("AppSettings.json")
        ).Build()

    static member ListeningAddresses
        with get () =
            config.["ListeningAddresses"]
