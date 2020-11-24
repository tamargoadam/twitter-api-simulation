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
    let mutable userDict = new Dictionary<string, IActorRef>()


    let startSim (termAdd: IActorRef)=
        terminateAddress <- termAdd
        let zipf = Zipf(1.5, numUsers)
        for i in 0..numUsers-1 do
            userDict.Add("@user"+i.ToString(), spawn mailbox ("worker"+i.ToString()) (twitterUser (zipf.Sample()))) |> ignore


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
                | StartSimulation -> startSim sender
                | RecieveStatistics (s1, s2, s3) -> processStatistics (s1, s2, s3) // change to vars for decided upon stats
            return! loop()
        }
    loop()