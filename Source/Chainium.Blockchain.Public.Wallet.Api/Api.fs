namespace Chainium.Blockchain.Public.Wallet.Api

open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Chainium.Common
open Microsoft.AspNetCore.Hosting
open Chainium.Blockchain.Public.Core.DomainTypes
open Chainium.Blockchain.Public.Core.Dtos
open Chainium.Blockchain.Common
open Chainium.Blockchain.Public.Crypto
open Dtos

module Api =

    let generateWalletHandler  : HttpHandler = fun next ctx ->
        task {
            let toDto (walletInfo : WalletInfo) =
                let keyToStr (PrivateKey key) = key
                let addrToStr (ChainiumAddress addr) = addr

                {
                    WalletInfoDto.PrivateKey = keyToStr walletInfo.PrivateKey
                    Address = addrToStr walletInfo.Address
                }

            let result =
                Signing.generateWallet()
                |> toDto
                |> json

            return! result next ctx
        }

    let signMessageHandler : HttpHandler = fun next ctx ->
        task {
            let! signingRequest = ctx.BindJsonAsync<SigningRequestDto>()

            let privateKey =
                signingRequest.PrivateKey
                |> PrivateKey

            let itemToSign =
                signingRequest.DataToSign
                |> Conversion.stringToBytes

            let result =
                Signing.signMessage privateKey itemToSign
                |> json

            return! result next ctx
        }

    let api =
        choose [
                GET >=>
                    choose
                        [
                            route "/wallet" >=> generateWalletHandler
                        ]
                POST >=>
                    choose
                        [
                            route "/sign" >=> signMessageHandler
                        ]
              ]

    let errorHandler (ex : Exception) _ =
        Log.errorf "API request failed: %s" ex.AllMessagesAndStackTraces

        clearResponse
        >=> ServerErrors.INTERNAL_ERROR ex.AllMessages


    let configureApp (app : IApplicationBuilder) =
        // Add Giraffe to the ASP.NET Core pipeline
        app.UseGiraffeErrorHandler(errorHandler)
            .UseGiraffe(api)

    let configureServices (services : IServiceCollection) =
        // Add Giraffe dependencies
        services.AddGiraffe() |> ignore

    let start () =
        WebHostBuilder()
            //.SuppressStatusMessages(true)
            .UseKestrel()
            .Configure(Action<IApplicationBuilder> configureApp)
            .ConfigureServices(configureServices)
            .UseUrls(Config.ListeningAddresses)
            .Build()
            .Run()
