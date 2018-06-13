module Tests

open System
open Xunit
open System.Threading
open System.Net.Http

[<Fact>]
let ``Node - tests`` () =
    let nodeProcess = new System.Diagnostics.Process()
    let startInfo = new System.Diagnostics.ProcessStartInfo()
    startInfo.WindowStyle <- System.Diagnostics.ProcessWindowStyle.Normal
    startInfo.FileName <- "dotnet"
    startInfo.Arguments <- "D:\Git\Chainium\Source\Chainium.Blockchain.Public.Node\bin\Debug\netcoreapp2.1\Chainium.Blockchain.Public.Node.dll"
    startInfo.WorkingDirectory <- "D:\NodeSetup"
    nodeProcess.StartInfo <- startInfo
    nodeProcess.Start() |> ignore

    let client = new HttpClient()
    client.BaseAddress <- Uri "http://localhost:5001"

    client.PostAsJsonAsync("tx",

    Thread.Sleep(5000)
