module Client

open User
open MessageTypes

open System
open Akka.Actor
open Akka.FSharp
open System.Collections.Generic
open MathNet.Numerics.Distributions

open Akka.Configuration

let config =
    ConfigurationFactory.ParseString(
        @"akka {
            actor.provider = ""Akka.Actor.LocalActorRefProvider""
            remote.helios.tcp {
                hostname = localhost
                port = 8090
            }
            
            debug : {
                    receive : on
                    autoreceive : on
                    lifecycle : on
                    event-stream : on
                    unhandled : on
                    
            }
            log-dead-letters = 0
            log-dead-letters-during-shutdown = off
        }")

let system = ActorSystem.Create("FSharp", config)

let clientSupervisor (numUsers: int) (mailbox : Actor<ClientMsg>)=
    let mutable terminateAddress = mailbox.Context.Parent
    let mutable userList = [] // username -> numSubs
    let mutable totalTweets = 0

    let startSim (termAddr: IActorRef) (serverAddr: IActorRef)=
        terminateAddress <- termAddr
        let zipf = Zipf(1.5, numUsers)
        // register each user with server
        for i in 0..numUsers-1 do
            let username = "user"+i.ToString()
            let numSubscribers = zipf.Sample()
            serverAddr <! RegisterUser username
            userList <- userList @ [(username, numSubscribers)]
        // spawn user actors
        for i in 0..userList.Length-1 do
            serverAddr <! SimulateSetInitialSubs ((fst userList.[i]), (snd userList.[i]))
            let numTweets = (max 100 (snd userList.[i]) )/10
            totalTweets <- totalTweets + numTweets
            serverAddr <! SimulateSetExpectedTweets totalTweets
            spawn mailbox ("worker"+i.ToString()) (twitterUser (fst userList.[i]) numUsers (snd userList.[i]) numTweets serverAddr) |> ignore



    let processStatistics (numTweets: int, timeProcessing: float)=
        // process stats here
        terminateAddress <! RecieveStatistics (numTweets, timeProcessing)
    

    let rec loop () = 
        actor {
            let! msg = mailbox.Receive()
            let sender = mailbox.Sender()
            match msg with
                | StartSimulation server -> startSim sender server
                | RecieveStatistics (numTweets, timeProcessing) -> processStatistics (numTweets, timeProcessing) 
            return! loop()
        }
    loop()