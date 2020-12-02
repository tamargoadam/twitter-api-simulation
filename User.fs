module User

open MessageTypes

open System
open Akka.FSharp
open Akka.Actor

let rand = Random()

let twitterUser (username: string) (numUsers: int) (numSubscribers: int) (serverAddr: IActorRef) (mailbox : Actor<UserMsg>) = 
    let subToUseRatio = (float (min numSubscribers 1))/(float numUsers)
    let mutable numTweets = (max 100 numSubscribers )/10



    let postTweets =
        for i in 0..numTweets-1 do
            let mutable tweetContent = "This is the content of the tweet"

            // add random hashtag to tweet 11% of the time
            if rand.NextDouble() <= 0.11 then
                tweetContent <- tweetContent + " #tag" + rand.Next(100).ToString()
            // add random mention to tweet 49% of the time
            if rand.NextDouble() <= 0.49 then
                tweetContent <- tweetContent + " @user" + rand.Next(numUsers).ToString()

            serverAddr <! PostTweet (tweetContent, username)
            // Console.WriteLine("Remaining tweets for {0}: {1}", username, numTweets)


    let viewTweet (id: int) (tweet: string) (user: string) =
        // retweet viewed tweet 13% of the time
        if username = "user0" then
            System.Console.WriteLine("({0})  {1}: {2}        (viewed by: {3})", id, user, tweet, username)
        if rand.NextDouble() <= 0.13 then
            serverAddr <! ReTweet (id, tweet, username)
    

    let toggleDisconnection =
        // 25% chance to toggle period of disconnectivity
        async{
            if rand.NextDouble() <= 0.1 then
                serverAddr <! Logout username
                do! Async.Sleep (rand.Next(2500)) // disconnect for up to 2.5 seconds
                serverAddr <! Login username
            }


    // log user into server
    serverAddr <! Login username
    
    postTweets

    let rec loop () = 
        // Akka.Dispatch.ActorTaskScheduler.RunTask(fun() ->
        //     toggleDisconnection |> Async.StartAsTask :> Threading.Tasks.Task)
        actor {
            let! msg = mailbox.Receive()
            // run functions to record measurments that can be sent back to the client supervisor
            match msg with
                | ReceiveTweet (id, tweet, user) -> viewTweet id tweet user
                | ReceiveTweets tweets -> ignore
            return! loop()
        }
    loop()