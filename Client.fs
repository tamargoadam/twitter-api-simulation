module Client

open User
open MessageTypes

open System
open Akka.Actor
open Akka.FSharp
open System.Collections.Generic
open MathNet.Numerics.Distributions

let system = ActorSystem.Create("FSharp")

let clientSupervisor (numUsers: int) (mailbox : Actor<ClientMsg>)=
    let mutable numDone = 0
    let mutable terminateAddress = mailbox.Context.Parent
    let mutable userDict = new Dictionary<string, IActorRef>() // username -> ref


    let startSim (termAddr: IActorRef) (serverAddr: IActorRef)=
        terminateAddress <- termAddr
        let zipf = Zipf(1.5, numUsers)
        for i in 0..numUsers-1 do
            let username = "user"+i.ToString()
            userDict.Add(username, spawn mailbox ("worker"+i.ToString()) (twitterUser username numUsers (zipf.Sample()) serverAddr)) |> ignore


    let processStatistics (stats: int*int*int)=
        // process stats here
        numDone <- numDone + 1
        if numDone >= numUsers then
            Console.WriteLine("Some info printed here...")
    

    let rec loop () = 
        actor {
            let! msg = mailbox.Receive()
            let sender = mailbox.Sender()
            match msg with
                | StartSimulation server -> startSim sender server
                | RecieveStatistics (s1, s2, s3) -> processStatistics (s1, s2, s3) // change to vars for decided upon stats
            return! loop()
        }
    loop()