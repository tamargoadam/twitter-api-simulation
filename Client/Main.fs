module Main

open Client
open MessageTypes
open Requests

open System
open Akka.FSharp
open System.IO
open System.Text
open System.Net.WebSockets
open System.Threading

[<EntryPoint>]
let main argv =
    if argv.Length <> 1 then
        Console.WriteLine("Invalid Input Provided")
        Console.WriteLine("Required Format: dotnet run <num_users>")
        -1
    else
        Console.WriteLine("Starting Twitter Simulation...\n")
        let numUsers: int = int argv.[0]
        let client = spawn system ("client") (clientSupervisor numUsers)
        
        let stats = client <? ClientMsg.StartSimulation "server" |> Async.RunSynchronously
        match stats with
        | RecieveStatistics (numTweets, timeProcessing)  -> 
            Console.WriteLine("Total number of tweets processed: {0}", numTweets)
            Console.WriteLine("Average time to process a tweet: {0} ms\n", timeProcessing/(float numTweets))
        |_-> Console.WriteLine("An issue occured retreaving server measurments.\n")
    
        0
