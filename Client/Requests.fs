module Requests

open System.Text
open System.IO
open System.Net
open System.Net.WebSockets
open System
open System.Threading

let httpUrl = "http://localhost:8080/"
let wsUrl = "ws://localhost:8080/"

let makeRequest (url:string) (method:string) (content:string) = 
    // Create & configure HTTP web request
    let req = HttpWebRequest.Create(httpUrl+url) :?> HttpWebRequest 
    req.ProtocolVersion <- HttpVersion.Version10
    req.Method <- method

    // Encode body with POST data as array of bytes
    let postBytes = Encoding.ASCII.GetBytes(content)
    req.ContentType <- "application/json; charset=utf-8";
    req.ContentLength <- int64 postBytes.Length
    
    // Write data to the request
    let reqStream = req.GetRequestStream() 
    reqStream.Write(postBytes, 0, postBytes.Length);
    reqStream.Close()

    // Get response and open stream
    let response = req.GetResponse() 
    let receiveStream = response.GetResponseStream ();

    // Pipes the stream to a higher level stream reader with the required encoding format.
    let readStream = new StreamReader (receiveStream, Encoding.UTF8)
    let content = readStream.ReadToEnd()

    response.Close ();
    readStream.Close ();

    content

let openTweetSocket (ws: ClientWebSocket) = 
    async{
        Console.WriteLine("Here")
        let wsUri = Uri(wsUrl+"tweets/stream")
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