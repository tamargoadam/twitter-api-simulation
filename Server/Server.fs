module Server

open ServerActor
open MessageTypes

open Akka.FSharp

open Suave
open Suave.Http
open Suave.Operators
open Suave.Filters
open Suave.Successful
open Suave.Files
open Suave.RequestErrors
open Suave.Logging
open Suave.Utils

open System.Collections.Generic
open System.Net

open Newtonsoft.Json

open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket
open Suave.ServerErrors


// JSON serializing and deserializing functions
let JSON v =
  let jsonSerializerSettings = JsonSerializerSettings()

  JsonConvert.SerializeObject(v, jsonSerializerSettings)
  |> OK
  >=> Writers.setMimeType "application/json; charset=utf-8"

let fromJson<'a> json =
  JsonConvert.DeserializeObject(json, typeof<'a>) :?> 'a

let getResourceFromReq<'a> (req : HttpRequest) =
  let getString rawForm =
    UTF8.toString rawForm
  req.rawForm |> getString |> fromJson<'a>

let stringToByteSeg (s: string) =
  s
  |> System.Text.Encoding.ASCII.GetBytes
  |> ByteSegment

// Spawn server actor
let serverActor = spawn system "server" serverActor
let socketIntexSet = new HashSet<int>()


// Web socket connection handler 
let ws (webSocket : WebSocket) =
  fun context ->
    let mutable index = rand.Next(System.Int32.MaxValue)
    while socketIntexSet.Contains(index) do
      index <- rand.Next(System.Int32.MaxValue)
    socketIntexSet.Add(index) |> ignore
    let inbox = spawn system ("ws"+index.ToString()) (fun (mailbox : Actor<UserMsg>) -> actor {
      let mutable close = false
      while not close do
        
        let! msg = mailbox.Receive()
        match msg with
        | ReceiveTweet (tweet: TweetData) ->
          let byteResponse = stringToByteSeg (JsonConvert.SerializeObject tweet)
          webSocket.send Text byteResponse true |> Async.RunSynchronously |> ignore
        
        | RequestLogin username ->
          let res = serverActor <? Login(username, mailbox.Context.Self) |> Async.RunSynchronously
          match res with
            | ReqSuccess -> 
              let response = sprintf "You are now logged in as %s." username
              let byteResponse = stringToByteSeg response
              webSocket.send Text byteResponse true |> Async.RunSynchronously |> ignore
            |_-> 
              let response = sprintf "You could not be logged in as %s." username
              let byteResponse = stringToByteSeg response
              webSocket.send Text byteResponse true |> Async.RunSynchronously |> ignore
              let emptyResponse = [||] |> ByteSegment
              webSocket.send Close emptyResponse true |> Async.RunSynchronously |> ignore
              close <- true

        |_-> ()
    })

    socket {
      let mutable loop = true

      while loop do

        let! msg = webSocket.read()

        match msg with
        | (Text, rawData, true) ->
          // Handle login request and connetion
          try
            let data = rawData |> UTF8.toString |> fromJson<UsernameMsg>
            inbox <! (RequestLogin data.username)
          with
          |_-> 
            let byteResponse = stringToByteSeg "An error occured. Login failed."
            do! webSocket.send Text byteResponse true
        
        | (Close, _, _) ->
          let emptyResponse = [||] |> ByteSegment
          do! webSocket.send Close emptyResponse true
          loop <- false

        | _ -> ()
      }

let wsWithErrorHandling (webSocket : WebSocket) (context: HttpContext) = 
  let websocketWorkflow = ws webSocket context
  async {
    let! successOrError = websocketWorkflow
    match successOrError with
    | Choice1Of2() -> ()
    | Choice2Of2(error) ->
        printfn "Error: [%A]" error        
    return successOrError
   }


// Application routing and HTTP req handling
let app : WebPart = 
  choose [
    path "/tweets/stream" >=> handShake wsWithErrorHandling
    GET >=> 
      choose [ 
        path "/" >=> file "index.html";
        path "/tweets/subscribedTo" >=> request (
          fun x ->
            match x.queryParam "username" with  
            | Choice1Of2 username -> 
              let res = serverActor <? GetTweetsSubscribedTo username |> Async.RunSynchronously
              match res with
              | QueryTweets (tweets: TweetsMsg) -> JSON tweets
              |_-> INTERNAL_ERROR "Failed to recieve query."
            | Choice2Of2 msg -> BAD_REQUEST msg
          );
        path "/tweets/byMentioned" >=> request (
          fun x ->
            match x.queryParam "username" with  
            | Choice1Of2 username -> 
              let res = serverActor <? GetTweetsByMention username |> Async.RunSynchronously
              match res with
              | QueryTweets (tweets: TweetsMsg) -> JSON tweets  
              |_-> INTERNAL_ERROR "Failed to recieve query."
            | Choice2Of2 msg -> BAD_REQUEST msg
          );
        path "/tweets/byHashtag" >=> request (
          fun x ->
            match x.queryParam "tag" with  
            | Choice1Of2 username -> 
              let res = serverActor <? GetTweetsByHashtag username |> Async.RunSynchronously
              match res with
              | QueryTweets (tweets: TweetsMsg) -> JSON tweets
              |_-> INTERNAL_ERROR "Failed to recieve query."
            | Choice2Of2 msg -> BAD_REQUEST msg
          );
        path "/statistics" >=> request (
          fun x ->
            let res = serverActor <? GetStatistics |> Async.RunSynchronously
            match res with
            | RecieveStatistics (stats: StatsMsg) -> JSON stats  
            |_-> INTERNAL_ERROR "Failed to receive server statistics."
          );
      ]
    POST >=> 
      choose [ 
        path "/register" >=> request (
          fun x ->
            let data = getResourceFromReq<UsernameMsg> x
            let res = serverActor <? RegisterUser data.username |> Async.RunSynchronously
            match res with
            | ReqSuccess -> OK "You have been registered."
            |_-> CONFLICT "Registration failed."
          );
        path "/postTweet" >=> request (
          fun x ->
            let data = getResourceFromReq<TweetMsg> x
            let res = serverActor <? PostTweet (data.tweet, data.user) |> Async.RunSynchronously
            match res with
            | ReqSuccess -> OK "Your tweet has been posted."
            |_-> CONFLICT "Posting failed."
          );
        path "/subscribeTo" >=> request (
          fun x ->
            let data = getResourceFromReq<SubscribeMsg> x
            let res = serverActor <? SubscribeTo (data.subTo, data.user) |> Async.RunSynchronously
            match res with
            | ReqSuccess -> OK ("You are now subscribed to "+data.subTo+".")
            |_-> CONFLICT ("Could not subscribe to user, "+data.subTo+".")
          );
        path "/reTweet" >=> request (
          fun x ->
            let data = getResourceFromReq<ReTweetMsg> x
            let res = serverActor <? ReTweet (data.origId, data.user) |> Async.RunSynchronously
            match res with
            | ReqSuccess -> OK "Your retweet has been posted."
            |_-> CONFLICT "Your retweet could not be posted."
          );
          path "/simulate/initSubs" >=> request (
          fun x ->
            let data = getResourceFromReq<SimInitSubsMsg> x
            let res = serverActor <? SimulateSetInitialSubs (data.username, data.numSubs) |> Async.RunSynchronously
            match res with
            | ReqSuccess -> OK "Subscriber simulation initialized."
            |_-> CONFLICT "Could not properly initialize subscriber simulation."
          );
      ]
    PUT >=>
      choose [
        path "/logout" >=> request (
          fun x ->
            let data = getResourceFromReq<UsernameMsg> x
            let res = serverActor <? Logout data.username |> Async.RunSynchronously
            match res with
            | ReqSuccess -> OK "You have been logged out."
            |_-> CONFLICT "You could not be logged out properly."
          );
      ]
    NOT_FOUND "Found no handlers." 
    ]


[<EntryPoint>]
let main _ =
  startWebServer { defaultConfig with logger = Targets.create Verbose [||] } app
  0