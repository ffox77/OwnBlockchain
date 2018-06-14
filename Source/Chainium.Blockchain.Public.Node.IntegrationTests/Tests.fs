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
open System.Diagnostics
open Giraffe

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

let private baseDataFolder = testSetup.["Data"].Value<string>()

let private nodeAssembly = testSetup.["NodeAssemblyPath"].Value<string>()

type private NodeSetupConfig =
    {
        ConnectionString : string
        ListeningAddress : string
        InitialAmount : decimal
        NodeName : string
        Signer : WalletInfo
        DataPath : string
        Setup : JToken
    }

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

let private nodeSetup signer (setup : JToken) =
    let nodeName = setup.["ValidatorAddress"].Value<string>()
    let nodePath = Path.Combine(baseDataFolder,nodeName)

    {
        ConnectionString = connectionString setup nodePath
        ListeningAddress = setup.["ListeningAddresses"].Value<string>()
        NodeName = nodeName
        InitialAmount = setup.["InitialBalance"].Value<decimal>()
        Signer = signer
        DataPath = nodePath
        Setup = setup
    }

let private buildNodes (configurations : NodeSetupConfig list) =
    let dataDir newDir =
        if Directory.Exists newDir then
            Directory.Delete(newDir, true)

        Directory.CreateDirectory newDir
        |> ignore

    dataDir baseDataFolder

    let nodeProc (config : NodeSetupConfig) =
        dataDir config.DataPath

        let appSettings = Path.Combine(config.DataPath,"AppSettings.json")

        File.WriteAllText (appSettings, config.Setup.ToString())

        let startInfo = System.Diagnostics.ProcessStartInfo()
        startInfo.FileName <- "dotnet"
        startInfo.Arguments <- nodeAssembly
        startInfo.WorkingDirectory <- config.DataPath
        startInfo.UseShellExecute <- true

        let nodeProcess = new System.Diagnostics.Process()
        nodeProcess.StartInfo <- startInfo

        nodeProcess

    configurations
    |> List.map(nodeProc)

[<Fact>]
let ``Node - tests`` () =
    let signer = Signing.generateWallet()

    let configs =
        nodeSettings.Children()
        |> List.ofSeq
        |> List.map(fun a -> nodeSetup signer a)

    let nodes = buildNodes configs

    let startNode (nodeProcess : Process) =
        nodeProcess.Start() |> ignore

    try
        // start nodes
        nodes
        |> List.iter(startNode)

        // let nodes start
        Thread.Sleep(5000)


        // submit transaction
        let submitToNode config =
            let client = new HttpClient()
            client.BaseAddress <- Uri config.ListeningAddress

            let address = addressToString config.Signer.Address

            addBalanceAndAccount config.ConnectionString address 100M
            addBalanceAndAccount config.ConnectionString config.NodeName config.InitialAmount
            let tx =
                {
                    ActionType = "AccountControllerChange"
                    ActionData =
                    {
                        AccountControllerChangeTxActionDto.AccountHash = address
                        ControllerAddress = address
                    }
                }

            let fee = 1M
            let nonce = 1L
            let txDto = newTxDto fee nonce [ tx ]

            let expectedTx = transactionEnvelope config.Signer txDto

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
                sprintf "Node: %s failed to submit transaction." address
                |> Some
            else
                None

        // submit transaction to each node
        let messages =
            configs
            |> List.map submitToNode

        let messagesToPrint =
            messages
            |> List.filter(Option.isSome)

        if  (messagesToPrint |> Seq.length) > 0 then
            failwithf "%A" messagesToPrint
    finally
        // stop nodes
        nodes
        |> List.iter
            (
                fun nodeProcess -> nodeProcess.Kill()
            )