module Tests

open System
open System.IO
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
open Newtonsoft.Json.Linq
open Microsoft.Data.Sqlite
open Chainium.Common

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

let addBalanceAndAccount connectionString (address : string) (amount : decimal) =
    let insertStatement =
        """
        INSERT INTO chx_balance (chainium_address, amount, nonce) VALUES (@chainium_address, @amount, 0);
        INSERT INTO account (account_hash, controller_address) VALUES (@chainium_address, @chainium_address);
        """
    [
        "@amount", amount |> box
        "@chainium_address", address |> box
    ]
    |> DbTools.execute connectionString insertStatement
    |> ignore

let testSetup =
    File.ReadAllText("TestSetup.json")
    |> JsonConvert.DeserializeObject :?> JObject

let private nodeSettings =
        testSetup.["Nodes"]

let private listeningAddress (setup : JToken) =
    setup.["ListeningAddresses"].Value<string>()

let private nodeAddress (setup : JToken) =
    setup.["ValidatorAddress"].Value<string>()

let private connectionString (setup : JToken) nodePath =
        let connString = setup.["DbConnectionString"].Value<string>()
        let dbType = setup.["DbEngineType"].Value<string>()
        if dbType = "SQLite" then
            let dbConn = new SqliteConnection(connString)

            if dbConn.DataSource |> Path.IsPathRooted |> not then
                Path.Combine(nodePath, dbConn.DataSource)
                |> sprintf "Data Source=%s"
            else
                connString
        else
            connString

let buildNodes () =

    let dataDir newDir =
        if Directory.Exists newDir then
            Directory.Delete(newDir, true)

        Directory.CreateDirectory newDir
        |> ignore


    let dataFolder = testSetup.["Data"].Value<string>()
    dataDir dataFolder

    let nodeAssembly = testSetup.["NodeAssemblyPath"].Value<string>()

    let prepareNodeInstance (setup: JToken) =
        let nodeName = nodeAddress setup
        let nodeDataDir = Path.Combine(dataFolder,nodeName)

        nodeDataDir
        |> dataDir

        let appSettings = Path.Combine(nodeDataDir,"AppSettings.json")

        File.WriteAllText (appSettings, setup.ToString())




        let balance = setup.["InitialBalance"].Value<decimal>()

        let startInfo = System.Diagnostics.ProcessStartInfo()
        startInfo.FileName <- "dotnet"
        startInfo.Arguments <- nodeAssembly
        startInfo.WorkingDirectory <- nodeDataDir
        startInfo.UseShellExecute <- true

        let nodeProcess = new System.Diagnostics.Process()
        nodeProcess.StartInfo <- startInfo


        (
            listeningAddress setup,
            connectionString setup nodeDataDir,
            nodeName,
            balance,
            nodeProcess
        )

    if nodeSettings.HasValues then
        nodeSettings.Children()
        |> List.ofSeq
        |> List.map
            (
                fun setup -> prepareNodeInstance setup
            )
    else
        failwith "There is no test setup."

[<Fact>]
let ``Node - tests`` () =
    let nodes = buildNodes()

    try
        // start nodes
        nodes
        |> List.iter
            (
                fun (_,_,_,_,nodeProcess) ->
                     nodeProcess.Start() |> ignore
            )

        // let nodes start
        Thread.Sleep(5000)

        // submit transaction to each node
        let messages =
            nodes
            |> List.map
                (
                    fun (address, connString, nodeAddress, nodeBalance, _) ->
                        let client = new HttpClient()
                        client.BaseAddress <- Uri address

                        let wallet = Signing.generateWallet()
                        let walletAddress = wallet.Address |> addressToString
                        addBalanceAndAccount connString walletAddress 100M
                        addBalanceAndAccount connString nodeAddress nodeBalance
                        let tx =
                            {
                                ActionType = "AccountControllerChange"
                                ActionData =
                                {
                                    AccountControllerChangeTxActionDto.AccountHash = walletAddress
                                    ControllerAddress = walletAddress
                                }
                            }

                        let fee = 1M
                        let nonce = 1L
                        let txDto = newTxDto fee nonce [ tx ]

                        let expectedTx = transactionEnvelope wallet txDto

                        let tx = JsonConvert.SerializeObject(expectedTx)
                        let content = new StringContent(tx, System.Text.Encoding.UTF8, "application/json")

                        let result =
                            client.PostAsync("tx", content)
                            |> runTask

                        let data =
                            result.Content.ReadAsStringAsync()
                            |> runTask
                            |> JsonConvert.DeserializeObject<SubmitTxResponseDto>

                        if data.TxHash.IsNullOrWhiteSpace() then
                            (
                                true,
                                sprintf "Node: %s failed to submit transaction." address
                            )
                        else
                            (
                                false,
                                ""
                            )
                   )

        let messagesToPrint =
            messages
            |> List.filter(fun (k,v) -> k = true)
            |> List.map(fun (k,v) -> v)
        if  (messagesToPrint |> Seq.length) > 0 then
            failwithf "%A" messagesToPrint
    finally
        // stop nodes
        nodes
        |> List.iter
            (
                fun (_,_,_,_,nodeProcess) ->
                     nodeProcess.Kill()
            )