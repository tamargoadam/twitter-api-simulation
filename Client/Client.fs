module Client

open User
open MessageTypes
open Requests

open System.Collections.Generic
open Akka.Actor
open Akka.FSharp
open MathNet.Numerics.Distributions

open Akka.Configuration
open Newtonsoft.Json

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
    let mutable userDict = new Dictionary<string, int>()
    let mutable totalTweets = 0

    let startSim (termAddr: IActorRef) =
        terminateAddress <- termAddr
        let zipf = Zipf(1.5, numUsers)

        printf "Press any key to register simulated users.\n"
        System.Console.ReadKey() |> ignore

        // register each user with server
        for i in 0..numUsers-1 do
            let username = "user"+i.ToString()
            let numSubscribers = zipf.Sample()
            let json = JsonConvert.SerializeObject(UsernameMsg(username))
            makeRequest "/register" "POST" json |> ignore
            userDict.Add(username, numSubscribers)

        // initialize subscribers for each user
        for user in userDict do
            let json = JsonConvert.SerializeObject(SimInitSubsMsg(user.Key, user.Value))
            makeRequest "/simulate/initSubs" "POST" json |> ignore
        
        printf "Press any key to spawn active user simulations.\n"
        System.Console.ReadKey() |> ignore

        // spawn actors to simulate user activity
        let mutable i = 0
        for user in userDict do
            let numTweets = (max 100 user.Value )/10
            totalTweets <- totalTweets + numTweets
            spawn mailbox ("worker"+i.ToString()) (twitterUser user.Key numUsers user.Value numTweets) |> ignore
            i <- i+1
        
        printf "Press any key to end the Twitter active user simulation.\n"
        System.Console.ReadKey() |> ignore
        
        terminateAddress <! "End simulation."
    

    let rec loop () = 
        actor {
            let! msg = mailbox.Receive()
            let sender = mailbox.Sender()
            match msg with
                | StartSimulation -> startSim sender 
            return! loop()
        }
    loop()