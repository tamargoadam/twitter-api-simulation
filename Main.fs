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
        
        let stats = client <? ClientMsg.StartSimulation server |> Async.RunSynchronously
        match stats with
        | RecieveStatistics (numTweets, timeProcessing)  -> 
            Console.WriteLine("Total number of tweets processed: {0}", numTweets)
            Console.WriteLine("Average time to process a tweet: {0} ms\n", timeProcessing/(float numTweets))
        |_-> Console.WriteLine("An issue occured retreaving server measurments.\n")

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