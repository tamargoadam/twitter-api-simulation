module Main

open Client
open Server
open MessageTypes

open System
open Akka.FSharp
open Server

[<EntryPoint>]
let main argv =
    if argv.Length <> 1 then
        Console.WriteLine("Invalid Input Provided")
        Console.WriteLine("Required Format: dotnet run <num_users>")
        -1
    else
        Console.WriteLine("Starting Twitter Simulation...")
        let numUsers: int = int argv.[0]
        let client = spawn system ("client") (clientSupervisor numUsers)
        let server = spawn system ("server") serverActor
        let res = client <? ClientMsg.StartSimulation server |> Async.RunSynchronously
        0