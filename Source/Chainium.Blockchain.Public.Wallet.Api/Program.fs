open System
open System.Threading
open System.Globalization
open Chainium.Common
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Wallet.Api

[<EntryPoint>]
let main argv =
    printfn "Chainium Public Wallet Api"

    try
        Thread.CurrentThread.CurrentCulture <- CultureInfo.InvariantCulture
        Thread.CurrentThread.CurrentUICulture <- CultureInfo.InvariantCulture

        Api.start()
    with
    | ex -> Log.error ex.AllMessagesAndStackTraces
    0
