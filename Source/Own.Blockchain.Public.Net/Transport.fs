﻿namespace Own.Blockchain.Public.Net

open System
open System.Collections.Concurrent
open System.Collections.Generic
open Own.Common
open Own.Blockchain.Public.Core
open Own.Blockchain.Public.Core.Dtos
open NetMQ
open NetMQ.Sockets
open Newtonsoft.Json

module Transport =

    let private poller = new NetMQPoller()
    let mutable private receiverSocket : PullSocket option = None
    let private connectionPool = new ConcurrentDictionary<string, DealerSocket * NetMQQueue<string>>()

    let private packMessage message =
        message |> Serialization.serializePeerMessage

    let private unpackMessage message =
        message |> Serialization.deserializePeerMessage

    let private send (msg : string) targetAddress =
        match connectionPool.TryGetValue targetAddress with
        | true, (_, queue) ->
            queue.Enqueue msg
        | _ ->
            let senderSocket = new DealerSocket(">tcp://" + targetAddress)
            let messageQueue = new NetMQQueue<string>()
            messageQueue.ReceiveReady |> Observable.subscribe (fun eventArgs ->
                let msgList = new HashSet<string>()
                let msg = ref ""
                while eventArgs.Queue.TryDequeue(msg, TimeSpan.FromMilliseconds(100.)) do
                    msgList.Add !msg |> ignore

                for message in msgList do
                    senderSocket.TrySendFrame message |> ignore
            )
            |> ignore

            connectionPool.AddOrUpdate (
                targetAddress,
                (senderSocket, messageQueue),
                fun _ _ -> (senderSocket, messageQueue))
            |> ignore

            poller.Add messageQueue
            messageQueue.Enqueue msg

    let sendGossipDiscoveryMessage gossipDiscoveryMessage targetAddress =
        let msg = packMessage gossipDiscoveryMessage
        send msg targetAddress

    let sendGossipMessage gossipMessage (targetMember: GossipMemberDto) =
        let msg = packMessage gossipMessage
        send msg targetMember.NetworkAddress

    let sendUnicastMessage unicastMessage targetAddress =
        let msg = packMessage unicastMessage
        send msg targetAddress

    let sendMulticastMessage senderAddress multicastMessage multicastAddresses =
        let multicastAddresses =
            multicastAddresses
            |> List.filter (fun a -> a <> senderAddress)

        match multicastAddresses with
        | [] -> ()
        | _ ->
            multicastAddresses
            |> Seq.shuffle
            |> Seq.toList
            |> List.iter (fun networkAddress ->
                let msg = packMessage multicastMessage
                send msg networkAddress
            )

    let receiveMessage networkAddress receiveCallback =
        match receiverSocket with
        | Some _ -> ()
        | None -> receiverSocket <- new PullSocket("@tcp://" + networkAddress) |> Some

        receiverSocket |> Option.iter(fun socket ->
            poller.Add socket
            socket.ReceiveReady
            |> Observable.subscribe (fun eventArgs ->
                let received, message = eventArgs.Socket.TryReceiveFrameString()
                if received then
                    let peerMessage = unpackMessage message
                    receiveCallback peerMessage
            )
            |> ignore
        )
        poller.RunAsync()

    let closeConnection networkAddress =
        match connectionPool.TryGetValue networkAddress with
        | true, (socket, _) ->
            connectionPool.TryRemove networkAddress |> ignore
            socket.Dispose()
        | _ -> ()

    let closeAllConnections () =
        poller.Dispose()
        connectionPool
        |> Map.ofDict
        |> Seq.iter (fun x ->
            (fst x.Value).Dispose()
            (snd x.Value).Dispose()
        )
