module ServerActor

open MessageTypes
open TwitterData

open Akka.FSharp
open Akka.Actor
open System.Data


let system = ActorSystem.Create "FSharp"

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
    let mutable timeProcessing = 0.0

    // Data access functions
    let makeUserOnline (userName: string, addr: IActorRef) = 
        let expression = "USERNAME = '" + userName + "'"
        let userRow = twitterData.Tables.["USERS"].Select(expression)
        userRow.[0].["CONNECTED"] <- true
        userRow.[0].["ADDRESS"] <- addr
        ReqSuccess


    let makeUserOffline (userName: string) = 
        let expression = "USERNAME = '" + userName + "'"
        let userRow = twitterData.Tables.["USERS"].Select(expression)
        userRow.[0].["CONNECTED"] <- false
        ReqSuccess


    let addSubsc(subscriber: string, user: string) = 
        let row = twitterData.Tables.["SUBSCRIBERS"].NewRow()
        row.["USER"] <- user
        row.["SUBSCRIBER"] <- subscriber

        twitterData.Tables.["SUBSCRIBERS"].Rows.Add(row)


    let addSubscReq(subscriber: string, user: string) = 
        addSubsc(subscriber, user)
        ReqSuccess


    let registerUser (userName: string) = 
        if userName.Length > 0 then
            let row = twitterData.Tables.["USERS"].NewRow()
            row.["USERNAME"] <- userName
            row.["CONNECTED"] <- false

            twitterData.Tables.["USERS"].Rows.Add(row)
            addSubsc(userName, userName)
            ReqSuccess
        else
            ReqFailure


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
            (row.["ADDRESS"] :?> IActorRef) <! ReceiveTweet (TweetData(id, tweet, user))


    let processTweet(rtId: int, t: string, user: string) =
        // clock time to process each tweet
        let stopWatch = System.Diagnostics.Stopwatch.StartNew()
        // get unique id
        let mutable tweetId = rand.Next(System.Int32.MaxValue)
        while twitterData.Tables.["TWEETS"].Select("ID = '" + tweetId.ToString() + "'").Length > 0 do
            tweetId <- rand.Next(System.Int32.MaxValue)
        // if rt, find origional
        let mutable tweet = t
        if rtId <> -1 then
            tweet <- twitterData.Tables.["TWEETS"].Select("ID = '" + rtId.ToString() + "'").[0].["TWEET"] :?> string
            if not (tweet.StartsWith("RT: ")) then
                tweet <- "RT: " + tweet

        let row = twitterData.Tables.["TWEETS"].NewRow()
        row.["ID"] <- tweetId
        row.["TWEET"] <- tweet
        row.["USER"] <- user
        row.["RT_ID"] <- rtId

        twitterData.Tables.["TWEETS"].Rows.Add(row)

        addHashtags(tweet, tweetId)
        addMentions(tweet, tweetId)

        sendToSubs(tweetId, tweet, user)

        stopWatch.Stop()
        timeProcessing <- timeProcessing + stopWatch.Elapsed.TotalMilliseconds

        numTweetsProcessed <- numTweetsProcessed + 1
        ReqSuccess


    let getTweetsWithHashtags(tag: string) = 
        let tagExpression = "TAG = '" + tag + "'"
        let tagRows = twitterData.Tables.["HASHTAGS"].Select(tagExpression)
        let mutable tweetExpression = ""
        for row in tagRows.[..10] do
            if tweetExpression.Length > 0 then
                tweetExpression <- tweetExpression + " OR "
            tweetExpression <- tweetExpression + "ID = '" + row.["TWEET_ID"].ToString() + "'"
        if tweetExpression = "" then 
            QueryTweets(TweetsMsg(Array.empty))
        else
            let tweetRows =  twitterData.Tables.["TWEETS"].Select(tweetExpression)
            let getTweetsFromRows = fun (x:DataRow) -> TweetData(x.["ID"] :?> int, x.["TWEET"] :?> string, x.["USER"] :?> string)
            QueryTweets(TweetsMsg(tweetRows |> Array.map getTweetsFromRows))
        

    let getSubscribedTo(subscriber: string) = 
        let subExpression = "SUBSCRIBER = '" + subscriber + "'"
        let subRows = twitterData.Tables.["SUBSCRIBERS"].Select(subExpression)
        let mutable tweetExpression = ""
        for row in subRows.[..10] do
            if tweetExpression.Length > 0 then
                tweetExpression <- tweetExpression + " OR "
            tweetExpression <- tweetExpression + "USER = '" + row.["USER"].ToString() + "'"
        if tweetExpression = "" then 
            QueryTweets(TweetsMsg(Array.empty))
        else
            let tweetRows =  twitterData.Tables.["TWEETS"].Select(tweetExpression)
            let getTweetsFromRows = fun (x:DataRow) -> TweetData(x.["ID"] :?> int, x.["TWEET"] :?> string, x.["USER"] :?> string)
            QueryTweets(TweetsMsg(tweetRows |> Array.map getTweetsFromRows))


    let getMentionedTweet(user: string) = 
        let mentionExpression = "MENTIONED_NAME = '" + user + "'"
        let mentionRows = twitterData.Tables.["MENTIONS"].Select(mentionExpression)
        let mutable tweetExpression = ""
        for row in mentionRows.[..10] do
            if tweetExpression.Length > 0 then
                tweetExpression <- tweetExpression + " OR "
            tweetExpression <- tweetExpression + "ID = '" + row.["TWEET_ID"].ToString() + "'"
        if tweetExpression = "" then 
            QueryTweets(TweetsMsg(Array.empty))
        else
            let tweetRows =  twitterData.Tables.["TWEETS"].Select(tweetExpression)
            let getTweetsFromRows = fun (x:DataRow) -> TweetData(x.["ID"] :?> int, x.["TWEET"] :?> string, x.["USER"] :?> string)
            QueryTweets(TweetsMsg(tweetRows |> Array.map getTweetsFromRows))


    let setInitialSubs (user: string) (numSubs: int) = 
        let mutable allUsers = twitterData.Tables.["USERS"].Select()
        shuffle allUsers
        for i in 0..numSubs-1 do
            let sub = allUsers.[i].["USERNAME"]
            if twitterData.Tables.["SUBSCRIBERS"].Select("USER = '" + user + "' AND SUBSCRIBER = '" + sub.ToString() + "'").Length = 0 then
                addSubsc(sub.ToString(), user)
        ReqSuccess


    // Actor loop
    let rec loop () = 
        actor {
            let! msg = mailbox.Receive()
            let sender = mailbox.Sender()

            try
                match msg with
                | Login (user, addr) -> sender <! makeUserOnline (user, addr)
                | Logout user -> sender <! makeUserOffline user
                | PostTweet (tweet, user) -> sender <! processTweet (-1, tweet, user)
                | SubscribeTo (subTo, user) -> sender <! addSubscReq (user, subTo)  
                | RegisterUser user -> sender <! registerUser user
                | ReTweet (origId, user) -> sender <! processTweet (origId, "", user)
                | GetTweetsSubscribedTo user -> sender <! getSubscribedTo user
                | GetTweetsByMention user -> sender <! getMentionedTweet user
                | GetTweetsByHashtag hashtag -> sender <! getTweetsWithHashtags hashtag
                | SimulateSetInitialSubs (numSubs, user) -> sender <! setInitialSubs numSubs user
                | GetStatistics -> sender <! RecieveStatistics (StatsMsg(numTweetsProcessed, timeProcessing))
            with
            |_-> sender <! ReqFailure

            return! loop()
        }
    loop()