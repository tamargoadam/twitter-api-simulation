module User

open MessageTypes

open System
open Akka.FSharp
open Akka.Actor


let twitterUser (username: string) (numUsers: int) (numSubscribers: int) (serverAddr: IActorRef) (mailbox : Actor<UserMsg>) = 
    let mutable isLoggedIn = false
    let rand = Random()
    let subToUseRatio = (float (min numSubscribers 1))/(float numUsers)


    let postTweet (connected: bool) =
        if connected then
            let mutable tweetContent = "This is the content of the tweet"

            // add random hashtag to tweet 11% of the time
            if rand.NextDouble() <= 0.11 then
                tweetContent <- tweetContent + " #tag" + rand.Next(100).ToString()
            // add random mention to tweet 49% of the time
            if rand.NextDouble() <= 0.49 then
                tweetContent <- tweetContent + " @user" + rand.Next(numUsers).ToString()

            serverAddr <! PostTweet (tweetContent, username)


    let viewTweet (id: int) (tweet: string) (user: string)=
        // retweet viewed tweet 13% of the time
        System.Console.WriteLine("({0})  {1}: {2}", id, user, tweet)
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
    isLoggedIn <- true
    
    // set initial subscribers for simulation
    serverAddr <! SimulateSetInitialSubs (username, numSubscribers)
        
    let rec loop () = 
        Akka.Dispatch.ActorTaskScheduler.RunTask(fun() ->
            async {
                do! Async.Sleep (int (250.0/subToUseRatio)) // user tweets proportional to subs-users ratio
                postTweet isLoggedIn
            } |> Async.StartAsTask :> Threading.Tasks.Task)

        actor {

            let! msg = mailbox.Receive()
            let sender = mailbox.Sender()
            // run functions to record measurments that can be sent back to the client supervisor
            match msg with
                | ReceiveTweet (id, tweet, user) -> viewTweet id tweet user
                | ReceiveTweets tweets -> ignore
            return! loop()
        }
    loop()