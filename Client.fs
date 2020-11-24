module Client

open User
open MessageTypes

open Akka.Actor
open Akka.FSharp
open System.Collections.Generic

let system = ActorSystem.Create("FSharp")

let clientSupervisor (numUsers: int) (mailbox : Actor<ClientMsg>)=
    let mutable userDict = new Dictionary<string, IActorRef>()

    for i in 0..numUsers-1 do
       userDict.Add("@user"+i.ToString(), spawn mailbox ("worker"+i.ToString()) (twitterUser i)) |> ignore
    
    let rec loop () = 
        actor {
            let! msg = mailbox.Receive()
            let sender = mailbox.Sender()
            // change data to support lookup by actor ref then use that instead of username for places with "sender"
            match msg with
                | StartSimulation -> ignore
                | RecieveStatistics (s1, s2, s3) -> ignore
            return! loop()
        }
    loop()