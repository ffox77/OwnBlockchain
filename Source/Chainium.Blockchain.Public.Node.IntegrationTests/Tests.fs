module Tests

open System
open Xunit
open System.Threading
open System.Net.Http
open Newtonsoft.Json
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Node
open Chainium.Blockchain.Public.Core.Dtos
open Chainium.Blockchain.Public.Crypto
open Chainium.Blockchain.Public.Data
open Chainium.Blockchain.Public.Core
open Chainium.Blockchain.Public.Core.DomainTypes

let addressToString (ChainiumAddress a) = a

let runTask taskToRun =
    taskToRun
    |> Async.AwaitTask
    |> Async.RunSynchronously

let newTxDto fee nonce actions =
        {
            Nonce = nonce
            Fee = fee
            Actions = actions
        }

let private transactionEnvelope (sender : WalletInfo) txDto =
        let txBytes =
            txDto
            |> JsonConvert.SerializeObject
            |> Conversion.stringToBytes

        let signature = Signing.signMessage sender.PrivateKey txBytes

        {
            Tx = System.Convert.ToBase64String(txBytes)
            V = signature.V
            S = signature.S
            R = signature.R
        }


[<Fact>]
let ``Node - tests`` () =
    let nodeProcess = new System.Diagnostics.Process()
    let startInfo = new System.Diagnostics.ProcessStartInfo()
    startInfo.FileName <- "dotnet"
    startInfo.Arguments <- @"D:\Git\Chainium\Source\Chainium.Blockchain.Public.Node\bin\Debug\netcoreapp2.1\Chainium.Blockchain.Public.Node.dll"
    startInfo.WorkingDirectory <- @"D:\NodeSetup"
    startInfo.UseShellExecute <- true
    nodeProcess.StartInfo <- startInfo
    let isStarted = nodeProcess.Start()

    let client = new HttpClient()
    client.BaseAddress <- Uri "http://localhost:5001"

    let wallet = Signing.generateWallet()
    let tx =
            {
                ActionType = "AccountControllerChange"
                ActionData =
                    {
                        AccountControllerChangeTxActionDto.AccountHash = wallet.Address|> addressToString
                        ControllerAddress = wallet.Address |> addressToString
                    }
            }

    let fee = 1M
    let nonce = 1L
    let txDto = newTxDto fee nonce [ tx ]

    let expectedTx = transactionEnvelope wallet txDto

    let tx = JsonConvert.SerializeObject(expectedTx)
    let content = new StringContent(tx, System.Text.Encoding.UTF8, "application/json")

    let result = client.PostAsync("tx", content)
                 |> runTask

    let data = result.Content.ReadAsAsync<SubmitTxResponseDto>()
               |> runTask


    Thread.Sleep(5000)

    nodeProcess.Kill()
