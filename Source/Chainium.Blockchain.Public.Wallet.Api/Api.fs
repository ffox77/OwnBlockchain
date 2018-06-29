namespace Chainium.Blockchain.Public.Wallet.Api

open System
open System.Text
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Chainium.Common
open Microsoft.AspNetCore.Hosting
open Chainium.Blockchain.Public.Core.DomainTypes
open Microsoft.AspNetCore.Cors.Infrastructure
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

            let rawTx =
                signingRequest.DataToSign
                |> Conversion.stringToBytes

            let signature =
                Signing.signMessage privateKey rawTx

            let result =
                {
                    V = signature.V
                    R = signature.R
                    S = signature.S
                    Tx = rawTx |> Convert.ToBase64String
                }
                |> json

            return! result next ctx
        }

    let getAddressHandler : HttpHandler = fun next ctx ->
        task {
            let! privateKey = ctx.BindJsonAsync<PrivateKeyDto>()

            let address =
                privateKey.PrivateKey
                |> PrivateKey
                |> Signing.addressFromPrivateKey
                |> (fun (ChainiumAddress addr) -> addr)
                |> json

            return! address next ctx

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
                            route "/address" >=> getAddressHandler
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
            .UseCors("Private")
            .UseGiraffe(api)

    let configureServices (services : IServiceCollection) =
        // TODO: make it configurable
        let corsPolicies (options : CorsOptions) =
            options.AddPolicy("Private",
                (
                    fun builder ->
                        builder
                            .AllowAnyOrigin()
                            .AllowAnyMethod()
                            .AllowAnyHeader()
                            .WithExposedHeaders("Access-Control-Allow-Origin")
                            |> ignore)
                    )


        services
            .AddCors(fun options -> corsPolicies options)
            // Add Giraffe dependencies
            .AddGiraffe() |> ignore

    let start () =
        WebHostBuilder()
            //.SuppressStatusMessages(true)
            .UseKestrel()
            .Configure(Action<IApplicationBuilder> configureApp)
            .ConfigureServices(configureServices)
            .UseUrls(Config.ListeningAddresses)
            .Build()
            .Run()
