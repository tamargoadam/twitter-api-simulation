module Requests

open System.Text
open System.Net
open System
open Newtonsoft.Json

let httpUrl = "http://localhost:8080"

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
