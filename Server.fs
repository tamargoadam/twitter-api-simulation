module Server

open MessageTypes
open TwitterData

open Akka.FSharp
open Akka.Actor
open System.Data


let twitterData = createTwitterDataSet

let rand = System.Random()

let swap (a: _[]) x y =
    let tmp = a.[x]
    a.[x] <- a.[y]
    a.[y] <- tmp

// shuffle an array (in-place)
let shuffle a =
    Array.iteri (fun i _ -> swap a i (rand.Next(i, Array.length a))) a


// Server actor to handle requests
let serverActor (mailbox : Actor<ServerMsg>)=
    let mutable numTweetsProcessed = 0
    let mutable totalTweets = System.Int32.MaxValue
    let mutable timeProcessing = 0.0
    let mutable clientAddr = mailbox.Context.Parent

    // Data access functions
    let makeUserOnline (userName: string, addr: IActorRef) = 
        let expression = "USERNAME = '" + userName + "'"
        let userRow = twitterData.Tables.["USERS"].Select(expression)
        if userRow.Length > 0 then
            userRow.[0].["CONNECTED"] <- true
            userRow.[0].["ADDRESS"] <- addr


    let makeUserOffline (userName: string) = 
        let expression = "USERNAME = '" + userName + "'"
        let userRow = twitterData.Tables.["USERS"].Select(expression)
        if userRow.Length > 0 then
            userRow.[0].["CONNECTED"] <- false


    let registerUser (userName: string) = 
        let row = twitterData.Tables.["USERS"].NewRow()
        row.["USERNAME"] <- userName
        row.["CONNECTED"] <- false

        twitterData.Tables.["USERS"].Rows.Add(row)


    let addHashtags(input: string, id: int) =
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


    let addMentions(input: string, id: int) = 
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

                let row = twitterData.Tables.["MENTIONS"].NewRow()
                row.["MENTIONED_NAME"] <- user
                row.["TWEET_ID"] <- id

                twitterData.Tables.["MENTIONS"].Rows.Add(row)

                i <- k-1
            i <- i+1


    let sendToSubs(id: int, tweet: string, user: string) =
        let subExpression = "USER = '" + user + "'"
        let subRows = twitterData.Tables.["SUBSCRIBERS"].Select(subExpression)
        let mutable userRows = [||]
        for row in subRows do
            let userExpression =  "USERNAME = '" + row.["SUBSCRIBER"].ToString() + "' AND CONNECTED"
            userRows <- Array.append userRows (twitterData.Tables.["USERS"].Select(userExpression))
        for row in userRows do
            (row.["ADDRESS"] :?> IActorRef) <! ReceiveTweet(id, tweet, user)


    let processTweet(rtId: int, tweet: string, user: string) =
        // clock time to process each tweet
        let stopWatch = System.Diagnostics.Stopwatch.StartNew()
        // get unique id
        let mutable tweetId = rand.Next(System.Int32.MaxValue)
        while twitterData.Tables.["TWEETS"].Select("ID = '" + tweetId.ToString() + "'").Length > 0 do
            tweetId <- rand.Next(System.Int32.MaxValue)

        let row = twitterData.Tables.["TWEETS"].NewRow()
        row.["ID"] <- tweetId
        row.["TWEET"] <- tweet
        row.["USER"] <- user
        row.["RT_ID"] <- rtId

        if rtId = -1 && not (tweet.StartsWith("RT: ")) then
            row.["TWEET"] <- "RT: " + tweet

        twitterData.Tables.["TWEETS"].Rows.Add(row)

        addHashtags(tweet, tweetId)
        addMentions(tweet, tweetId)

        sendToSubs(tweetId, tweet, user)

        stopWatch.Stop()
        timeProcessing <- timeProcessing + stopWatch.Elapsed.TotalMilliseconds

        numTweetsProcessed <- numTweetsProcessed + 1
        if numTweetsProcessed >= totalTweets then
            clientAddr <! RecieveStatistics (numTweetsProcessed, timeProcessing)


    let addSubsc(subscriber: string, user: string) = 
        let row = twitterData.Tables.["SUBSCRIBERS"].NewRow()
        row.["USER"] <- user
        row.["SUBSCRIBER"] <- subscriber

        twitterData.Tables.["SUBSCRIBERS"].Rows.Add(row)


    let getTweetsWithHashtags(tag: string, addr: IActorRef) = 
        let tagExpression = "TAG = '" + tag + "'"
        let tagRows = twitterData.Tables.["HASHTAGS"].Select(tagExpression)
        let mutable tweetExpression = ""
        for row in tagRows.[..10] do
            if tweetExpression.Length > 0 then
                tweetExpression <- tweetExpression + " OR "
            tweetExpression <- tweetExpression + "ID = '" + row.["TWEET_ID"].ToString() + "'"
        if tweetExpression = "" then 
            addr <! QueryTweets(Array.empty)
        else
            let tweetRows =  twitterData.Tables.["TWEETS"].Select(tweetExpression)
            let getTweetsFromRows = fun (x:DataRow) -> (x.["ID"] :?> int, x.["TWEET"] :?> string, x.["USER"] :?> string)
            addr <! QueryTweets(tweetRows |> Array.map getTweetsFromRows)
        

    let getSubscribedTo(subscriber: string, addr: IActorRef) = 
        let subExpression = "SUBSCRIBER = '" + subscriber + "'"
        let subRows = twitterData.Tables.["SUBSCRIBERS"].Select(subExpression)
        let mutable tweetExpression = ""
        for row in subRows.[..10] do
            if tweetExpression.Length > 0 then
                tweetExpression <- tweetExpression + " OR "
            tweetExpression <- tweetExpression + "USER = '" + row.["USER"].ToString() + "'"
        if tweetExpression = "" then 
            addr <! QueryTweets(Array.empty)
        else
            let tweetRows =  twitterData.Tables.["TWEETS"].Select(tweetExpression)
            let getTweetsFromRows = fun (x:DataRow) -> (x.["ID"] :?> int, x.["TWEET"] :?> string, x.["USER"] :?> string)
            addr <! QueryTweets(tweetRows |> Array.map getTweetsFromRows)


    let getMentionedTweet(user: string, addr: IActorRef) = 
        let mentionExpression = "MENTIONED_NAME = '" + user + "'"
        let mentionRows = twitterData.Tables.["MENTIONS"].Select(mentionExpression)
        let mutable tweetExpression = ""
        for row in mentionRows.[..10] do
            if tweetExpression.Length > 0 then
                tweetExpression <- tweetExpression + " OR "
            tweetExpression <- tweetExpression + "ID = '" + row.["TWEET_ID"].ToString() + "'"
        if tweetExpression = "" then 
            addr <! QueryTweets(Array.empty)
        else
            let tweetRows =  twitterData.Tables.["TWEETS"].Select(tweetExpression)
            let getTweetsFromRows = fun (x:DataRow) -> (x.["ID"] :?> int, x.["TWEET"] :?> string, x.["USER"] :?> string)
            addr <! QueryTweets(tweetRows |> Array.map getTweetsFromRows)


    let setInitialSubs (user: string) (numSubs: int) = 
        let mutable allUsers = twitterData.Tables.["USERS"].Select()
        shuffle allUsers
        for i in 0..numSubs-1 do
            let sub = allUsers.[i].["USERNAME"]
            if twitterData.Tables.["SUBSCRIBERS"].Select("USER = '" + user + "' AND SUBSCRIBER = '" + sub.ToString() + "'").Length = 0 then
                addSubsc(sub.ToString(), user)


    let setTotalTweets (num: int) (client: IActorRef) =
        totalTweets <- num
        clientAddr <- client
        if numTweetsProcessed >= totalTweets then
            client <! RecieveStatistics (numTweetsProcessed, timeProcessing)


    // Actor loop
    let rec loop () = 
        actor {
            let! msg = mailbox.Receive()
            let sender = mailbox.Sender()

            match msg with
            | Login user -> makeUserOnline (user, sender)
            | Logout user -> makeUserOffline user
            | PostTweet (tweet, user) -> processTweet (-1, tweet, user)
            | SubscribeTo (subTo, user) -> addSubsc (user, subTo)  
            | RegisterUser user -> registerUser user
            | ReTweet (origId, tweet, user) -> processTweet (origId, tweet, user)
            | GetTweetsSubscribedTo user -> getSubscribedTo (user, sender)
            | GetTweetsByMention user -> getMentionedTweet (user, sender)
            | GetTweetsByHashtag hashtag -> getTweetsWithHashtags (hashtag, sender)
            | SimulateSetInitialSubs (numSubs, user) -> setInitialSubs numSubs user
            | SimulateSetExpectedTweets numTweets -> setTotalTweets numTweets sender

            return! loop()
        }
    loop()