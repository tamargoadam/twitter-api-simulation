module User

open MessageTypes
open Requests

open System
open Akka.FSharp
open Akka.Actor
open Suave.WebSocket
open Suave.Sockets
open Suave.Sockets.Control
open Suave
open System.Net.WebSockets
open System.Threading
open System.Text

let rand = Random()

let twitterUser (username: string) (numUsers: int) (numSubscribers: int) (numTweets: int) (mailbox : Actor<UserMsg>) = 
    let ws = new ClientWebSocket()
    let wsUri = Uri(wsUrl+"tweets/stream")


    let openTweetSocket =
        async{
            do! Async.AwaitTask (ws.ConnectAsync(wsUri, CancellationToken.None))
        }


    let closeTweetSocket =
        async{
            do! Async.AwaitTask (ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "OK", CancellationToken.None))
        }


    let postTweets =
        for i in 0..numTweets-1 do
            let mutable tweetContent = "Tweet content here!"

            // add random hashtag to tweet 11% of the time
            if rand.NextDouble() <= 0.11 then
                tweetContent <- tweetContent + " #tag" + rand.Next(10).ToString()
            // add random mention to tweet 49% of the time
            if rand.NextDouble() <= 0.49 then
                tweetContent <- tweetContent + " @user" + rand.Next(numUsers).ToString()

            let json = """{"tweet":"""+tweetContent+""", "username":"""+username+"""}"""
            makeRequest "/postTweet" "POST" json |> ignore


    let viewTweet (id: int) (tweet: string) (user: string) =
        // retweet viewed tweet 13% of the time
        if rand.NextDouble() <= 0.13 then
            let json = """{"id":"""+id.ToString()+""" "tweet":"""+tweet+""", "username":"""+username+"""}"""
            makeRequest "/reTweet" "POST" json |> ignore
    

    let toggleDisconnection =
        // 25% chance to toggle period of disconnectivity
        async{
            if rand.NextDouble() <= 0.1 then
                let json = """{"username":"""+username+"""}"""
                makeRequest "/logout" "PUT" json |> ignore
                do! Async.Sleep (rand.Next(2500)) // disconnect for up to 2.5 seconds
                makeRequest "/login" "PUT" json |> ignore
            }


    // log user into server
    makeRequest "/login" "PUT" """{"username":"""+username+"""}""" |> ignore
    // do ws.ConnectAsync(wsUri, CancellationToken.None) |> Async.AwaitTask |> Async.RunSynchronously
    openTweetSocket |> Async.RunSynchronously

    postTweets


    let rec loop () = 
        Akka.Dispatch.ActorTaskScheduler.RunTask(fun() ->
            toggleDisconnection |> Async.StartAsTask :> Tasks.Task)
        actor {
            // let! msg = mailbox.Receive()
            // // run functions to record measurments that can be sent back to the client supervisor
            // match msg with
            //     | ReceiveTweet (id, tweet, user) -> viewTweet id tweet user
            let mutable contents = ""
            while ws.State = WebSocketState.Open do
                let buff = ArraySegment<byte>(Array.zeroCreate 1028)
                let result = ws.ReceiveAsync(buff, CancellationToken.None).Result
                contents <- Encoding.UTF8.GetString(buff.Array, 0, result.Count)
                Console.WriteLine("Socket Message: {0}",contents)     
            return! loop()
        }
    loop()