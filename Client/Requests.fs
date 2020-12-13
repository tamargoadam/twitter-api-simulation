module Requests

open System.Text
open System.IO
open System.Net
open System.Net.WebSockets
open System
open System.Threading
open Newtonsoft.Json

let httpUrl = "http://localhost:8080"
let wsUrl = "ws://localhost:8080"


let getString rawForm =
    UTF8.toString rawForm

let getResourceFromResponse<'a> (rawForm: byte[]) =
  let fromJson json =
    JsonConvert.DeserializeObject(json, typeof<'a>) :?> 'a
  getString rawForm |> fromJson


let makeRequest (url:string) (method:string) (content:string) = 
    let client = new WebClient()
    let httpUri = Uri(httpUrl+url)
    let (postBytes: byte[]) = Encoding.ASCII.GetBytes(content)
    client.Headers.[HttpRequestHeader.ContentType] <- "application/json; charset=utf-8"

    let response = client.UploadData(httpUri, method, postBytes)
    if method = "GET" then
        getResourceFromResponse response
    else
        getString response

let openTweetSocket (ws: ClientWebSocket) = 
    async{
        Console.WriteLine("Here")
        let wsUri = Uri(wsUrl+"/tweets/stream")
        do! Async.AwaitTask (ws.ConnectAsync(wsUri, CancellationToken.None))
        let mutable contents = ""
        while ws.State = WebSocketState.Open do
            let buff = ArraySegment<byte>(Array.zeroCreate 1028)
            let result = ws.ReceiveAsync(buff, CancellationToken.None).Result
            contents <- Encoding.UTF8.GetString(buff.Array, 0, result.Count)
            Console.WriteLine("Socket Message: {0}",contents)            
    }

let closeTweetSocket (ws: ClientWebSocket) =
    async{
        Console.WriteLine(ws)
        do! Async.AwaitTask (ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "OK", CancellationToken.None))
    }