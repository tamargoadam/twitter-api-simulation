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

open System
open System.Net

open Newtonsoft.Json

open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket

// Message type deffinitions
type UserMsg = {username:string} 
type HashtagMsg = {tag:string} 
type TweetMsg = {tweet:string; user:string}
type ReTweetMsg = {origId:int; tweet:string; user:string}
type SubscribeMsg = {subTo:string; user:string}


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


// Spawn server actor
let serverActor = spawn system ("server") serverActor


// Web socket connection handler 
let ws (webSocket : WebSocket) (context: HttpContext) =
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
      // the message has type (Opcode * byte [] * bool)
      //
      // - Opcode type:
      //    type Opcode = Continuation | Text | Binary | Reserved | Close | Ping | Pong
      // - byte [] contains the actual message
      // - the last element is the FIN byte

      match msg with
      | (Text, data, true) ->
        // the message can be converted to a string
        let str = UTF8.toString data
        let response = sprintf "response to %s" str

        // the response needs to be converted to a ByteSegment
        let byteResponse =
          response
          |> System.Text.Encoding.ASCII.GetBytes
          |> ByteSegment

        // the `send` function sends a message back to the client
        do! webSocket.send Text byteResponse true

      | (Close, _, _) ->
        let emptyResponse = [||] |> ByteSegment
        do! webSocket.send Close emptyResponse true

        // after sending a Close message, stop the loop
        loop <- false

      | _ -> ()
    }

let wsWithErrorHandling (webSocket : WebSocket) (context: HttpContext) = 
  let websocketWorkflow = ws webSocket context
   
  async {
    let! successOrError = websocketWorkflow
    match successOrError with
    // Success case
    | Choice1Of2() -> ()
    // Error case
    | Choice2Of2(error) ->
        // Example error handling logic here
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
        path "/tweets/subscribedTo" >=> request (getResourceFromReq<UserMsg> >> JSON);
        path "/tweets/byMentioned" >=> request (getResourceFromReq<UserMsg> >> JSON);
        path "/tweets/byHashtag" >=> request (getResourceFromReq<HashtagMsg> >> JSON);
      ]
    POST >=> 
      choose [ 
        path "/register" >=> request (
          fun x ->
            let data = getResourceFromReq<UserMsg> x
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
      ]
    PUT >=>
      choose [
        path "/login" >=> request (
          fun x ->
            let data = getResourceFromReq<UserMsg> x
            serverActor <! Login data.username
            OK "You have been logged in."
          );
        path "/logout" >=> request (
          fun x ->
            let data = getResourceFromReq<UserMsg> x
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