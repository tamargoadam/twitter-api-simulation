module Server

open MessageTypes

open System
open Akka.Actor
open Akka.FSharp
open System.Collections.Generic

let subscribers = new Dictionary<string, string []>()
let subscribedTo = new Dictionary<string, string []>()
let tweets = new Dictionary<string, string []>()
let hashtags = new Dictionary<string, string []>()
let isConnected = new Dictionary<string, Boolean>()


// Register Account
let makeUserOnline(user) = 
    isConnected.Remove(user) |> ignore
    isConnected.Add(user, true)

let makeUserOffline(user) = 
    isConnected.Remove(user) |> ignore
    isConnected.Add(user, false)

let returnStatus(userName) = 
    isConnected.Item(userName)


let registerUser(userName) = 
    if subscribers.Item(userName) <> Array.empty then
        subscribers.Add(userName, Array.empty)
    
    subscribedTo.Add(userName, Array.empty)
    tweets.Add(userName, Array.empty)
    makeUserOnline(userName)


// Send Tweet
let addHashtags input =
    let mutable i = 0
    let l = String.length input
        
    while i < l do 
        if input.[i] = '#' then
            let mutable k = i+1
            while k < l && input.[k] <> ' ' do
                k <- k+1

            let hash = input.[i+1..k-1]

            // Add (hash, tweet) in hashtags dictionary
            let mutable pastTweets = hashtags.Item(hash) |> Array.toList
            pastTweets <- input :: pastTweets
            hashtags.Remove(hash) |> ignore
            hashtags.Add(hash, pastTweets |> List.toArray)

            i <- k-1
        i <- i+1


let addMentions input = 
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

            // Add (hash, tweet) in hashtags dictionary
            let mutable pastTweets = tweets.Item(user) |> Array.toList
            pastTweets <- input :: pastTweets
            tweets.Remove(user) |> ignore
            tweets.Add(user, pastTweets |> List.toArray)

            i <- k-1
        i <- i+1


let processTweet(userName, tweet) =
    // Add a tweet to the user's database
    let mutable pastTweets = tweets.Item(userName) |> Array.toList
    pastTweets <- tweet :: pastTweets
    tweets.Remove(userName) |> ignore
    tweets.Add(userName, pastTweets |> List.toArray)

    // Add (hashtag, tweet) in hashtags dictionary
    addHashtags tweet

    // Add (mention, tweet) in tweets dictionary
    addMentions tweet


// Subscribe to user's tweets
let addSubsc(user1, user2) = 
    let mutable pastUsers = subscribers.Item(user1) |> Array.toList
    pastUsers <- user2 :: pastUsers
    tweets.Remove(user1) |> ignore
    tweets.Add(user1, pastUsers |> List.toArray)

let addSubscTo(user2, user1) = 
    let mutable pastUsers = tweets.Item(user2) |> Array.toList
    pastUsers <- user1 :: pastUsers
    tweets.Remove(user2) |> ignore
    tweets.Add(user2, pastUsers |> List.toArray)

let addSubscriber(userName1, userName2) = 
    // user2 subscribes to user1
    addSubsc(userName1, userName2)
    addSubscTo(userName2, userName1)


// Re-tweets (so that your subscribers get an interesting tweet you got by other means)
let reTweet(user, tweet) = 
    processTweet(user, tweet)

// Allow querying tweets subscribed to, tweets with specific hashtags, tweets in which the user is mentioned (my mentions)
let getTweetsWithHashtags(hash) = 
    hashtags.Item(hash)

let getSubscribers(userName) = 
    subscribers.Item(userName)

let getSubscribedTo(userName) = 
    subscribedTo.Item(userName)


let getTweets(userName) = 
    tweets.Item(userName)
 
// If the user is connected, deliver the above types of tweets live (without querying)
// let getUserFeed(user) = 
    // if isConnected.Item(user) then
    //     let mutable feedTweets : string list = []
    //     let length = tweets.Item(user).Length

    //     for i in 0..length-1 do
    //         feedTweets <- Array.get (tweets.Item(user)) i :: feedTweets


let serverActor (mailbox : Actor<ServerMsg>)=
    let rec loop () = 
        actor {
            let! msg = mailbox.Receive()
            let sender = mailbox.Sender()
            // change data to support lookup by actor ref then use that instead of username for places with "sender"
            match msg with
            | Login -> makeUserOnline "sender"
            | Logout -> makeUserOffline "sender"
            | PostTweet tweet -> processTweet ("sender", tweet)
            | SubscribeTo user -> addSubsc (user, "sender")
            | RegisterUser user -> registerUser user // add sender to register by IActorRef as well
            | ReTweet tweet -> reTweet ("sender", tweet)
            | GetTweetsSubscribedTo user -> getSubscribedTo user // modify this function to send a msg back to client with corresponding tweets
            // | GetTweetsByMention user -> FUNCTION TO GET TWEETS BY MENTION HERE
            | GetTweetsByHashtag hashtag -> getTweetsWithHashtags hashtag // modify this function to send a msg back to client with corresponding tweets

            return! loop()
        }
    loop()