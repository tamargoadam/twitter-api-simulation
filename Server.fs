module Server

open MessageTypes
open TwitterData

open Akka.FSharp
open Akka.Actor
open System.Data


let twitterData = createTwitterDataSet


// Server actor to handle requests
let serverActor (mailbox : Actor<ServerMsg>)=
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

                let row = twitterData.Tables.["HASHTAGS"].NewRow()
                row.["MENTIONED_NAME"] <- user
                row.["TWEET_ID"] <- id

                twitterData.Tables.["HASHTAGS"].Rows.Add(row)

                i <- k-1
            i <- i+1


    let sendToSubs(id: int, tweet: string, user: string) =
        let subExpression = "USER = '" + user + "'"
        let subRows = twitterData.Tables.["SUBSCRIBERS"].Select(subExpression)
        let mutable userExpression = ""
        for row in subRows do
            if userExpression.Length > 0 then
                userExpression <- " OR "
            userExpression <- "USERNAME = '" + row.["SUBSCRIBER"].ToString() + "'"
        let userRows =  twitterData.Tables.["TWEETS"].Select(userExpression)
        for row in userRows do
            (row.["ADDRESS"] :?> IActorRef) <! ReceiveTweet(id, tweet, user)

    let mutable tweetId = 1

    let processTweet(rtId: int, tweet: string, user: string) =
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


    let addSubsc(subscriber: string, user: string) = 
        let row = twitterData.Tables.["SUBSCRIBERS"].NewRow()
        row.["USER"] <- user
        row.["SUBSCRIBER"] <- subscriber

        twitterData.Tables.["SUBSCRIBERS"].Rows.Add(row)


    let getTweetsWithHashtags(tag: string, addr: IActorRef) = 
        let tagExpression = "TAG = '" + tag + "'"
        let tagRows = twitterData.Tables.["HASHTAGS"].Select(tagExpression)
        let mutable tweetExpression = ""
        for row in tagRows do
            if tweetExpression.Length > 0 then
                tweetExpression <- " OR "
            tweetExpression <- "ID = '" + row.["TWEET_ID"].ToString() + "'"
        let tweetRows =  twitterData.Tables.["TWEETS"].Select(tweetExpression)
        let getTweetsFromRows = fun (x:DataRow) -> (x.["ID"] :?> int, x.["TWEET"] :?> string, x.["USER"] :?> string)
        addr <! ReceiveTweets(tweetRows |> Array.map getTweetsFromRows)
        


    let getSubscribedTo(subscriber: string, addr: IActorRef) = 
        let subExpression = "SUBSCRIBER = '" + subscriber + "'"
        let subRows = twitterData.Tables.["SUBSCRIBERS"].Select(subExpression)
        let mutable tweetExpression = ""
        for row in subRows do
            if tweetExpression.Length > 0 then
                tweetExpression <- " OR "
            tweetExpression <- "USER = '" + row.["USER"].ToString() + "'"
        let tweetRows =  twitterData.Tables.["TWEETS"].Select(tweetExpression)
        let getTweetsFromRows = fun (x:DataRow) -> (x.["ID"] :?> int, x.["TWEET"] :?> string, x.["USER"] :?> string)
        addr <! ReceiveTweets(tweetRows |> Array.map getTweetsFromRows)

    let getMentionedTweet(user: string, addr: IActorRef) = 
        let mentionExpression = "MENTIONED_NAME = '" + user + "'"
        let mentionRows = twitterData.Tables.["MENTIONS"].Select(mentionExpression)
        let mutable tweetExpression = ""
        for row in mentionRows do
            if tweetExpression.Length > 0 then
                tweetExpression <- " OR "
            tweetExpression <- "ID = '" + row.["TWEET_ID"].ToString() + "'"
        let tweetRows =  twitterData.Tables.["TWEETS"].Select(tweetExpression)
        let getTweetsFromRows = fun (x:DataRow) -> (x.["ID"] :?> int, x.["TWEET"] :?> string, x.["USER"] :?> string)
        addr <! ReceiveTweets(tweetRows |> Array.map getTweetsFromRows)
    
    // Actor loop
    let rec loop () = 
        actor {
            let! msg = mailbox.Receive()
            let sender = mailbox.Sender()
            // change data to support lookup by actor ref then use that instead of username for places with "sender"
            match msg with
            | Login user -> makeUserOnline(user)
            | Logout user -> makeUserOffline(user)
            | PostTweet (user, tweet) -> processTweet (-1, tweet, user)
            | SubscribeTo (user, subTo) -> addSubsc (user, subTo)  
            | RegisterUser user -> registerUser(user, sender)
            | ReTweet (user, tweet, origId) -> processTweet (origId, tweet, user)
            | GetTweetsSubscribedTo user -> getSubscribedTo (user, sender)
            | GetTweetsByMention user -> getMentionedTweet (user, sender)
            | GetTweetsByHashtag hashtag -> getTweetsWithHashtags (hashtag, sender)

            return! loop()
        }
    loop()