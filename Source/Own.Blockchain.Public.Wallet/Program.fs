﻿open System
open System.Globalization
open System.Threading
open Own.Common
open Own.Blockchain.Common
open Own.Blockchain.Public.Wallet

[<EntryPoint>]
let main argv =
    try
        Thread.CurrentThread.CurrentCulture <- CultureInfo.InvariantCulture
        Thread.CurrentThread.CurrentUICulture <- CultureInfo.InvariantCulture

        argv |> Array.toList |> Cli.handleCommand
    with
    | ex -> Log.error ex.AllMessagesAndStackTraces

    0 // Exit code
