namespace Chainium.Blockchain.Public.Wallet.Api

module Dtos =
    open Chainium.Blockchain.Public.Core.Dtos

    [<CLIMutable>]
    type WalletInfoDto =
        {
            PrivateKey : string
            Address : string
        }

    [<CLIMutable>]
    type SigningRequestDto =
        {
            PrivateKey : string
            DataToSign : string
        }

