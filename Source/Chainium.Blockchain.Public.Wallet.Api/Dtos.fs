namespace Chainium.Blockchain.Public.Wallet.Api

module Dtos =
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

    [<CLIMutable>]
    type SignedDto =
        {
            V : string
            R : string
            S : string
            Tx : string
        }

    [<CLIMutable>]
    type PrivateKeyDto =
        {
            PrivateKey : string
        }

