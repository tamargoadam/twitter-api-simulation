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
      let close = ref false
      while not !close do
        
        let! msg = mailbox.Receive()
        match msg with
        | ReceiveTweet (tweet: TweetData) ->
          let byteResponse = stringToByteSeg (JsonConvert.SerializeObject tweet)
          webSocket.send Text byteResponse true |> Async.RunSynchronously |> ignore
          
        
        | RequestLogin username ->
          serverActor <! Login username
          let response = sprintf "You are now logged in as %s!" username
          let byteResponse = stringToByteSeg response
          webSocket.send Text byteResponse true |> Async.RunSynchronously |> ignore
    })

    socket {
      let mutable loop = true

      while loop do

        // // TEST
        // let test =
        //     "This is the test response"
        //     |> System.Text.Encoding.ASCII.GetBytes
        //     |> ByteSegment
        // do! webSocket.send Text test true
        // Console.WriteLine("msg sent!")
        // // TEST

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


// TODO: update action on requests to perform functionality between getResourceFromReq and JSON
// Application routing and HTTP req handling
let app : WebPart = 
  choose [
    path "/tweets/stream" >=> handShake wsWithErrorHandling
    GET >=> 
      choose [ 
        path "/" >=> file "index.html"; browseHome
        path "/tweets/subscribedTo" >=> request (
          fun x ->
            let data = getResourceFromReq<UsernameMsg> x
            let res = serverActor <? GetTweetsSubscribedTo data.username |> Async.RunSynchronously
            match res with
            | QueryTweets (tweets: TweetsMsg) -> JSON tweets
          );
        path "/tweets/byMentioned" >=> request (
          fun x ->
            let data = getResourceFromReq<UsernameMsg> x
            let res = serverActor <? GetTweetsByMention data.username |> Async.RunSynchronously
            match res with
            | QueryTweets (tweets: TweetsMsg) -> JSON tweets
          );
        path "/tweets/byHashtag" >=> request (
          fun x ->
            let data = getResourceFromReq<HashtagMsg> x
            let res = serverActor <? GetTweetsByHashtag data.tag |> Async.RunSynchronously
            match res with
            | QueryTweets (tweets: TweetsMsg) -> JSON tweets
          );
      ]
    POST >=> 
      choose [ 
        path "/register" >=> request (
          fun x ->
            let data = getResourceFromReq<UsernameMsg> x
            serverActor <! RegisterUser data.username
            OK "You have been registered."
          );
        path "/postTweet" >=> request (
          fun x ->
            let data = getResourceFromReq<TweetMsg> x
            serverActor <! PostTweet (data.tweet, data.user)
            OK "Your tweet has been posted."
          );
        path "/subscribeTo" >=> request (
          fun x ->
            let data = getResourceFromReq<SubscribeMsg> x
            serverActor <! SubscribeTo (data.subTo, data.user)
            OK ("You are now subscribed to "+data.subTo+".")
          );
        path "/reTweet" >=> request (
          fun x ->
            let data = getResourceFromReq<ReTweetMsg> x
            serverActor <! ReTweet (data.origId, data.tweet, data.user)
            OK ("Your retweet has been posted.")
          );
          path "/simulate/initSubs" >=> request (
          fun x ->
            let data = getResourceFromReq<SimInitSubsMsg> x
            serverActor <! SimulateSetInitialSubs (data.username, data.numSubs)
            OK ("Subscriber simulation initialized.")
          );
      ]
    PUT >=>
      choose [
        path "/login" >=> request (
          fun x ->
            let data = getResourceFromReq<UsernameMsg> x
            serverActor <! Login data.username
            OK "You have been logged in."
          );
        path "/logout" >=> request (
          fun x ->
            let data = getResourceFromReq<UsernameMsg> x
            serverActor <! Logout data.username
            OK "You have been logged out."
          );
      ]
    NOT_FOUND "Found no handlers." 
    ]


[<EntryPoint>]
let main _ =
  startWebServer { defaultConfig with logger = Targets.create Verbose [||] } app
  0