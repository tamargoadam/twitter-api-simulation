module Main

open Client
open MessageTypes

open System
open Akka.FSharp


[<EntryPoint>]
let main argv =
    if argv.Length <> 1 then
        Console.WriteLine("Invalid input provided")
        Console.WriteLine("Required format: dotnet run <num_users>")
        -1
    else
        Console.WriteLine("Starting Twitter active user simulation...\n")
        let numUsers: int = int argv.[0]
        let client = spawn system ("client") (clientSupervisor numUsers)

        let endMsg = client <? StartSimulation |> Async.RunSynchronously

        Console.WriteLine("Twitter active user simulation ended.\nGoodbye.")

        0
