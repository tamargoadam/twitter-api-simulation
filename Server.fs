module Server

open MessageTypes
open TwitterData

open Akka.FSharp
open Akka.Actor
open System.Data

let twitterData = createTwitterDataSet


// Data access functions

let makeUserOnline (userName: string) = 
    let expression = "USERNAME = '" + userName + "'"
    let userRow = twitterData.Tables.["USERS"].Select(expression)
    if userRow.Length > 0 then
        userRow.[0].["CONNECTED"] <- true



let makeUserOffline (userName: string) = 
    let expression = "USERNAME = '" + userName + "'"
    let userRow = twitterData.Tables.["USERS"].Select(expression)
    if userRow.Length > 0 then
        userRow.[0].["CONNECTED"] <- false


let registerUser(userName: string, userAddress: IActorRef) = 
    let row = twitterData.Tables.["USERS"].NewRow()
    row.["USERNAME"] <- userName
    row.["ADDRESS"] <- userAddress
    row.["CONNECTED"] <- false

    twitterData.Tables.["USERS"].Rows.Add(row)


let addHashtags(input, id) =
    let mutable i = 0
    let l = String.length input
        
    while i < l do 
        if input.[i] = '#' then
            let mutable k = i+1
            while k < l && input.[k] <> ' ' do
                k <- k+1

            let hash = input.[i+1..k-1]

            let row = createHashtagTable.NewRow()
            row.["TAG"] <- hash
            row.["TWEET_ID"] <- id

            createHashtagTable.Rows.Add(row)

            i <- k-1
        i <- i+1


let addMentions(input, id) = 
    let mutable i = 0
    let l = String.length input
        
    let mutable words : string list = []
    while i < l do 
        if input.[i] = '@' then
            let mutable k = i+1
            while k < l && input.[k] <> ' ' do
                k <- k+1
            words <- input.[i+1..k-1] :: words

            let user = input.[i+1..k-1]

            let row = twitterData.Tables.["HASHTAGS"].NewRow()
            row.["MENTIONED_NAME"] <- user
            row.["TWEET_ID"] <- id

            twitterData.Tables.["HASHTAGS"].Rows.Add(row)

            i <- k-1
        i <- i+1


let mutable tweetId = 1

let processTweet(user, tweet, rtId) =
    let row = twitterData.Tables.["TWEETS"].NewRow()
    row.["ID"] <- tweetId
    row.["TWEET"] <- tweet
    row.["USER"] <- user
    row.["RT_ID"] <- rtId

    twitterData.Tables.["TWEETS"].Rows.Add(row)

    // NEED TO WORK ON SENDING TWEETS TO ALL SUBSCRIBERS.
    addHashtags(tweet, tweetId)
    addMentions(tweet, tweetId)
    tweetId <- tweetId+1


// subscriber subs to user
let addSubsc(subscriber, user) = 
    let row = twitterData.Tables.["SUBSCRIBERS"].NewRow()
    row.["USER"] <- user
    row.["SUBSCRIBER"] <- subscriber

    twitterData.Tables.["SUBSCRIBERS"].Rows.Add(row)


let getTweetsWithHashtags(tag) = 
    let tagExpression = "TAG = '" + tag + "'"
    let tagRows = twitterData.Tables.["HASHTAGS"].Select(tagExpression)
    let mutable tweetExpression = ""
    for row in tagRows do
        if tweetExpression.Length > 0 then
            tweetExpression <- " OR "
        tweetExpression <- "ID = '" + row.["TWEET_ID"].ToString() + "'"
    let tweetRows =  twitterData.Tables.["TWEETS"].Select(tweetExpression)
    let getTweetsFromRows = fun (x:DataRow) -> x.["TWEET"]
    tweetRows |> Array.map getTweetsFromRows
    


let getSubscribedTo(user) = 
    let mutable tweetList : obj list = []
    let dv = new DataView(createTweetTable)

    for drv in dv do
        if drv.["USER"] = user then
            tweetList <- drv.["TWEET"] :: tweetList

    let tweetArray = tweetList |> List.toArray
    tweetArray

let getMentionedTweet(user) = 
    let mentionExpression = "MENTIONED_NAME = '" + user + "'"
    let mentionRows = twitterData.Tables.["MENTIONS"].Select(mentionExpression)
    let mutable tweetExpression = ""
    for row in mentionRows do
        if tweetExpression.Length > 0 then
            tweetExpression <- " OR "
        tweetExpression <- "ID = '" + row.["TWEET_ID"].ToString() + "'"
    let tweetRows =  twitterData.Tables.["TWEETS"].Select(tweetExpression)
    let getTweetsFromRows = fun (x:DataRow) -> x.["TWEET"]
    tweetRows |> Array.map getTweetsFromRows


// Server actor to delegate functionality

let serverActor (mailbox : Actor<ServerMsg>)=
    let rec loop () = 
        actor {
            let! msg = mailbox.Receive()
            let sender = mailbox.Sender()
            // change data to support lookup by actor ref then use that instead of username for places with "sender"
            match msg with
            | Login user -> makeUserOnline(user)
            | Logout user -> makeUserOffline(user)
            | PostTweet (user, tweet) -> processTweet (user, tweet, -1)
            | SubscribeTo (user, subTo) -> addSubsc (user, subTo)  
            | RegisterUser user -> registerUser(user, sender)
            | ReTweet (user, tweet, origId) -> processTweet (user, tweet, origId)
            | GetTweetsSubscribedTo user -> getSubscribedTo user // modify this function to send a msg back to client with corresponding tweets //Done
            | GetTweetsByMention user -> getMentionedTweet user   //FUNCTION TO GET TWEETS BY MENTION HERE  //Done
            | GetTweetsByHashtag hashtag -> getTweetsWithHashtags hashtag // modify this function to send a msg back to client with corresponding tweets //Done

            return! loop()
        }
    loop()