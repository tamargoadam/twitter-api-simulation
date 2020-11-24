// Learn more about F# at https://urldefense.proofpoint.com/v2/url?u=http-3A__fsharp.org&d=DwIGaQ&c=sJ6xIWYx-zLMB3EPkvcnVg&r=VZpuJLCBOqkC7Oa1zQXRlw&m=5EPWVKUZG7KBEi6iaDrt8SE31xWwhQZr7R4_tGefnY0&s=kZPal_i3P1O9NqiMbtXf0O30hBRJoUvo-SLMoB5MJBI&e= 

module Main

open System

open Akka
// command to add Akka package:->  dotnet add package Akka.FSharp --version 1.4.10
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Collections.Generic

let system = ActorSystem.Create("FSharp")

let registerActor

let createRefArr (noOfUsers: int) (mailbox : Actor<'a>)=   
    [|
        for i in 0 .. noOfUsers-1 -> 
            (spawn mailbox ("worker"+i.ToString()) (registerActor (i)))
    |]

let gossipActor (neighbors: int[]) (mailbox : Actor<'a>) =    
    let mutable counter = 0
    let rec loop () = 
        actor {
            let! msg = mailbox.Receive()
            gossipSend neighbors msg
            counter <- counter + 1

            // terminate actor if rumor has been heard 10 times
            if counter < 50 then
                // let listener know this node has heard the rumor
                if counter = 1 then
                    mailbox.Context.Parent <! msg
                return! loop()
        }
    loop()


let startProtocol (algorithm: string) (refArr: IActorRef []) (numNodes: int) = 
    if algorithm = "gossip" then
        Console.WriteLine("Using Gossip Algorithm...")
        startGossip refArr "rumor"
           
    elif algorithm = "push-sum" then
        Console.WriteLine("Using Push Sum Algorithm...")
        startPushSum refArr


let supervisorActor (noOfUsers: int) (noOfTweets: int) (mailbox : Actor<'a>)= 
    let refArr = createRefArr noOfUsers mailbox
    startProtocol algorithm refArr numNodes
    let mutable numHeard = 0
    let mutable returnAddress = mailbox.Context.Parent
    let rec loop () = 
        actor {
            let! msg = mailbox.Receive()
            let sender = mailbox.Sender()
            if returnAddress = mailbox.Context.Parent then
                returnAddress <- sender
                return! loop()
            else
                numHeard <- numHeard + 1
                if numHeard < numNodes then 
                    // if algorithm = "gossip" then
                    //     Console.WriteLine("{0} heard the rumor, '{1}'!. ({2})", sender, msg, numHeard)
                    // elif algorithm = "push-sum" then
                    //     Console.WriteLine("{0} converged to the sum, {1}!. ({2})", sender, msg, numHeard)
                    return! loop()
                else
                    if algorithm = "gossip" then
                        Console.WriteLine("All {0} nodes heard the rumor!", numHeard)
                    elif algorithm = "push-sum" then
                        Console.WriteLine("All {0} nodes converged to the sum! (~{1})", numHeard, msg)
                    returnAddress <! "done!"
        }
    loop()

[<EntryPoint>]
let main argv =
    let noOfUsers:int = int argv.[0]
    let noOfTweets = argv.[1]

    let supervisor = spawn system "supervisorActor" (supervisorActor noOfUsers noOfTweets)
    let res = supervisor <? "done?" |> Async.RunSynchronously



    0 // return an integer exit code
