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
        Console.WriteLine("Starting Twitter Simulation...\n")
        let numUsers: int = int argv.[0]
        let client = spawn system ("client") (clientSupervisor numUsers)
        let server = spawn system ("server") serverActor
        let s = client <? ClientMsg.StartSimulation server |> Async.RunSynchronously
        
        let subscribedQuery = server <? ServerMsg.GetTweetsSubscribedTo "user0" |> Async.RunSynchronously
        Console.WriteLine("Result of query of tweets @user0 is subscribed to (first 5):")
        match subscribedQuery with
        | QueryTweets res -> Console.WriteLine(sprintf "%A\n" res.[..5])
        
        let hashtagQuery = server <? ServerMsg.GetTweetsByHashtag "tag5" |> Async.RunSynchronously
        Console.WriteLine("Result of query by hashtag #tag5 (first 5):")
        match hashtagQuery with
        | QueryTweets res -> Console.WriteLine(sprintf "%A\n" res.[..5])
        
        let mentionQuery = server <? ServerMsg.GetTweetsByMention "user0" |> Async.RunSynchronously
        Console.WriteLine("Result of query by metions of @user0 (first 5):")
        match mentionQuery with
        | QueryTweets res -> Console.WriteLine(sprintf "%A\n" res.[..5])

        0