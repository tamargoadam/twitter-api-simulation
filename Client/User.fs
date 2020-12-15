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

open Newtonsoft.Json


let fromJson<'a> json =
  JsonConvert.DeserializeObject(json, typeof<'a>) :?> 'a

let rand = Random()

let twitterUser (username: string) (numUsers: int) (numSubscribers: int) (numTweets: int) (mailbox : Actor<UserMsg>) = 
    let ws = new ClientWebSocket()
    let wsUri = Uri("ws://localhost:8080/tweets/stream")


    let openTweetSocket =
        async{
            do! Async.AwaitTask (ws.ConnectAsync(wsUri, CancellationToken.None))
        }


    let postTweets =
        async{
            for i in 0..numTweets-1 do
                let mutable tweetContent = "Tweet content here!"

                // add random hashtag to tweet 11% of the time
                if rand.NextDouble() <= 0.11 then
                    tweetContent <- tweetContent + " #tag" + rand.Next(10).ToString()
                // add random mention to tweet 49% of the time
                if rand.NextDouble() <= 0.49 then
                    tweetContent <- tweetContent + " @user" + rand.Next(numUsers).ToString()

                let json = JsonConvert.SerializeObject(TweetMsg(tweetContent, username))
                makeRequest "/postTweet" "POST" json |> ignore
        }


    let viewTweet (id: int) (tweet: string) (user: string) =
        // retweet viewed tweet 13% of the time
        if rand.NextDouble() <= 0.13 then
            let json = JsonConvert.SerializeObject(ReTweetMsg(id, username))
            makeRequest "/reTweet" "POST" json |> ignore
    

    let login = 
        let json = JsonConvert.SerializeObject(UsernameMsg(username))
        // makeRequest "/login" "PUT" json |> ignore
        openTweetSocket |> Async.RunSynchronously
        let byteData =
            json
            |> System.Text.Encoding.ASCII.GetBytes
            |> ByteSegment
        ws.SendAsync (byteData, WebSocketMessageType.Text, true, CancellationToken.None)



    let toggleDisconnection =
        // 25% chance to toggle period of disconnectivity
        async{
            if rand.NextDouble() <= 0.1 then
                let json = JsonConvert.SerializeObject(UsernameMsg(username))
                makeRequest "/logout" "PUT" json |> ignore
                do! Async.Sleep (rand.Next(2500)) // disconnect for up to 2.5 seconds
                makeRequest "/login" "PUT" json |> ignore
            }


    login |> ignore

    postTweets |> Async.Start


    let rec loop () = 
        Akka.Dispatch.ActorTaskScheduler.RunTask(fun() ->
            toggleDisconnection |> Async.StartAsTask :> Tasks.Task)
        actor {
            let mutable contents = ""
            while ws.State = WebSocketState.Open do
                let buff = ArraySegment<byte>(Array.zeroCreate 1028)
                let result = ws.ReceiveAsync(buff, CancellationToken.None).Result
                contents <- Encoding.UTF8.GetString(buff.Array, 0, result.Count)
                try 
                    let data = contents |> fromJson<ReceiveTweetMsg>
                    viewTweet data.id data.tweet data.user
                with
                |_->()    
            return! loop()
        }
    loop()