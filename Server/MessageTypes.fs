module MessageTypes
open Akka.Actor


// Message type deffinitions for serialization
type UsernameMsg = {username:string} 

type HashtagMsg = {tag:string} 

type TweetMsg = {tweet:string; user:string}

type ReTweetMsg = {origId:int; user:string}

type SubscribeMsg = {subTo:string; user:string}

type SimInitSubsMsg = {username: string; numSubs: int}

type TweetData(id: int, tweet: string, user: string) = 
  member this.id = id
  member this.tweet = tweet
  member this.user = user

type TweetsMsg(tweets:TweetData[]) =
  member this.tweets = tweets

type StatsMsg(numTweets: int, timeProcessing: float) = 
  member this.numTweets = numTweets
  member this.timeProcessing = timeProcessing


// server message types
type ServerMsg = 
    | Login of string * IActorRef
    | Logout of string
    | PostTweet of string * string
    | SubscribeTo of string * string
    | RegisterUser of string
    | ReTweet of int * string
    | GetTweetsSubscribedTo of string
    | GetTweetsByMention of string
    | GetTweetsByHashtag of string
    | SimulateSetInitialSubs of string * int
    | GetStatistics

// user message types
type UserMsg = 
    | ReceiveTweet of TweetData // id, tweet, user
    | RequestLogin of string
    | RecieveStatistics of StatsMsg
    | QueryTweets of TweetsMsg
    | ReqSuccess
    | ReqFailure