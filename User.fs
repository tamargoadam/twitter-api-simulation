module User

open MessageTypes

open System
open Akka.FSharp
open Akka.Actor


let twitterUser (username: string) (numUsers: int) (numSubscribers: int) (serverAddr: IActorRef) (mailbox : Actor<UserMsg>) = 
    let mutable isLoggedIn = false
    let rand = Random()
    // user posts proportional to subs-users ratio
    let subToUseRatio = (float numSubscribers)/(float numUsers)
    let postTimer = new Timers.Timer(250.0/subToUseRatio)
    let postEvent = Async.AwaitEvent (postTimer.Elapsed) |> Async.Ignore


    let postTweet (connected: bool) =
        if connected then
            let mutable tweetContent = "This is the content of the tweet"

            // add random hashtag to tweet 11% of the time
            if rand.NextDouble() <= 0.11 then
                tweetContent <- tweetContent + " #tag" + rand.Next(100).ToString()
            // add random mention to tweet 49% of the time
            if rand.NextDouble() <= 0.49 then
                tweetContent <- tweetContent + " @user" + rand.Next(numUsers).ToString()

            Console.WriteLine("user: {0}", tweetContent)
            serverAddr <! PostTweet (tweetContent, username)


    let viewTweet (id: int) (tweet: string) =
        // retweet viewed tweet 13% of the time
        if rand.NextDouble() <= 0.13 then
            serverAddr <! ReTweet (id, tweet, username)
    

    let toggleConnectivity (connected: bool) =
        // toggleConnectivity 50% of the time
        if rand.NextDouble() <= 0.5 then
            if connected then serverAddr <! Logout username
            else serverAddr <! Login username
            isLoggedIn <- not connected


    // register this user with the server
    serverAddr <! RegisterUser username
    serverAddr <! Login username
    isLoggedIn <- not isLoggedIn
        
    let rec loop () = 
        postTweet isLoggedIn
        actor {
            let! msg = mailbox.Receive()
            let sender = mailbox.Sender()
            // run functions to record measurments that can be sent back to the client supervisor
            match msg with
                | ReceiveTweet (id, tweet, user) -> viewTweet id tweet
                | ReceiveTweets tweets -> ignore
            return! loop()
        }
    loop()